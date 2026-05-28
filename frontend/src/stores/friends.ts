import { defineStore } from 'pinia'
import { ref } from 'vue'
import {
  friendService,
  type FriendDto,
  type FriendRequestDto,
  type FriendSuggestionDto,
} from '@/services/friendService'

export const useFriendsStore = defineStore('friends', () => {
  // ── State ─────────────────────────────────────────────────────────────────

  const friends = ref<FriendDto[]>([])
  const pendingRequests = ref<FriendRequestDto[]>([])
  const sentRequests = ref<FriendRequestDto[]>([])
  const suggestions = ref<FriendSuggestionDto[]>([])
  const pendingCount = ref(0)

  const friendsPage = ref(1)
  const friendsHasMore = ref(true)
  const loadingFriends = ref(false)
  const loadingRequests = ref(false)
  const loadingSuggestions = ref(false)

  // ── Friends list ──────────────────────────────────────────────────────────

  async function loadFriends(reset = false) {
    if (reset) {
      friends.value = []
      friendsPage.value = 1
      friendsHasMore.value = true
    }
    if (!friendsHasMore.value || loadingFriends.value) return
    loadingFriends.value = true
    try {
      const result = await friendService.getFriends(friendsPage.value)
      friends.value.push(...result.items)
      friendsHasMore.value = result.hasMore
      friendsPage.value++
    } finally {
      loadingFriends.value = false
    }
  }

  // ── Requests ──────────────────────────────────────────────────────────────

  async function loadRequests() {
    loadingRequests.value = true
    try {
      const [incoming, outgoing] = await Promise.all([
        friendService.getPendingRequests(),
        friendService.getSentRequests(),
      ])
      pendingRequests.value = incoming
      sentRequests.value = outgoing
      pendingCount.value = incoming.length
    } finally {
      loadingRequests.value = false
    }
  }

  async function refreshPendingCount() {
    try {
      pendingCount.value = await friendService.getPendingCount()
    } catch {
      // silently fail
    }
  }

  // ── Suggestions ───────────────────────────────────────────────────────────

  async function loadSuggestions() {
    loadingSuggestions.value = true
    try {
      suggestions.value = await friendService.getSuggestions()
    } finally {
      loadingSuggestions.value = false
    }
  }

  // ── Actions ───────────────────────────────────────────────────────────────

  async function sendRequest(userId: string) {
    await friendService.sendRequest(userId)
  }

  async function acceptRequest(userId: string) {
    await friendService.acceptRequest(userId)
    pendingRequests.value = pendingRequests.value.filter(r => r.from.id !== userId)
    pendingCount.value = Math.max(0, pendingCount.value - 1)
    // Reload friends to include the newly accepted one
    await loadFriends(true)
  }

  async function declineRequest(userId: string) {
    await friendService.declineRequest(userId)
    pendingRequests.value = pendingRequests.value.filter(r => r.from.id !== userId)
    pendingCount.value = Math.max(0, pendingCount.value - 1)
  }

  async function cancelRequest(userId: string) {
    await friendService.cancelRequest(userId)
    sentRequests.value = sentRequests.value.filter(r => r.from.id !== userId)
  }

  async function removeFriend(userId: string) {
    await friendService.removeFriend(userId)
    friends.value = friends.value.filter(f => f.user.id !== userId)
  }

  // ── SignalR: incoming real-time events ────────────────────────────────────

  function onFriendRequestReceived(from: { id: string; displayName: string }) {
    pendingCount.value++
    // Append to pendingRequests if the requests panel is open
    if (!pendingRequests.value.some(r => r.from.id === from.id)) {
      pendingRequests.value.unshift({
        friendshipId: '',
        from: { id: from.id, displayName: from.displayName },
        sentAt: new Date().toISOString(),
      })
    }
  }

  function onFriendRequestAccepted(by: { id: string; displayName: string }) {
    sentRequests.value = sentRequests.value.filter(r => r.from.id !== by.id)
    // Reload friend list
    loadFriends(true)
  }

  function reset() {
    friends.value = []
    pendingRequests.value = []
    sentRequests.value = []
    suggestions.value = []
    pendingCount.value = 0
    friendsPage.value = 1
    friendsHasMore.value = true
  }

  return {
    friends,
    pendingRequests,
    sentRequests,
    suggestions,
    pendingCount,
    friendsPage,
    friendsHasMore,
    loadingFriends,
    loadingRequests,
    loadingSuggestions,
    loadFriends,
    loadRequests,
    refreshPendingCount,
    loadSuggestions,
    sendRequest,
    acceptRequest,
    declineRequest,
    cancelRequest,
    removeFriend,
    onFriendRequestReceived,
    onFriendRequestAccepted,
    reset,
  }
})
