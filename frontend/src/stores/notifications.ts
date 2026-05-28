/**
 * Pinia store for the notification bell + dropdown.
 *
 * - Maintains a list of recent notifications (loaded on demand).
 * - Tracks the unread count for the bell badge.
 * - Owns the SignalR connection to /hubs/notifications, which:
 *     • adds the user to the "user:{id}" group automatically (server-side),
 *     • receives NotificationReceived for every new notification,
 *     • receives UnreadCountChanged after server-side mark-as-read,
 *     • receives UserOnline / UserOffline for presence — these are forwarded
 *       to the presence store.
 *
 * connect() is idempotent; call it once on login. disconnect() on logout.
 */
import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import * as signalR from '@microsoft/signalr'
import type { Notification } from '@/types'
import { notificationService } from '@/services/notificationService'
import { useAuthStore } from '@/stores/auth'
import { usePresenceStore } from '@/stores/presence'
import { useFriendsStore } from '@/stores/friends'

const MAX_KEPT = 100

export const useNotificationsStore = defineStore('notifications', () => {
  const auth = useAuthStore()

  // ── State ───────────────────────────────────────────────────────────────────

  const notifications = ref<Notification[]>([])
  const unreadCount   = ref(0)
  const dropdownOpen  = ref(false)
  const loading       = ref(false)
  const hasMore       = ref(true)
  const page          = ref(1)

  let hubConnection: signalR.HubConnection | null = null

  // ── Computed ────────────────────────────────────────────────────────────────

  const hasUnread = computed(() => unreadCount.value > 0)

  // ── Inbox actions ───────────────────────────────────────────────────────────

  async function loadFirstPage() {
    loading.value = true
    try {
      const result = await notificationService.list(1, 20)
      notifications.value = result.items
      unreadCount.value   = result.unreadCount
      hasMore.value       = result.hasMore
      page.value          = 1
    } finally {
      loading.value = false
    }
  }

  async function loadMore() {
    if (!hasMore.value || loading.value) return
    loading.value = true
    try {
      const next   = page.value + 1
      const result = await notificationService.list(next, 20)
      // Deduplicate (server may push the same notif via SignalR between fetches).
      const knownIds = new Set(notifications.value.map((n) => n.id))
      const fresh    = result.items.filter((n) => !knownIds.has(n.id))
      notifications.value.push(...fresh)
      hasMore.value = result.hasMore
      page.value    = next
    } finally {
      loading.value = false
    }
  }

  async function refreshUnreadCount() {
    try {
      unreadCount.value = await notificationService.getUnreadCount()
    } catch {
      // silent
    }
  }

  async function markRead(notificationId: string) {
    const n = notifications.value.find((x) => x.id === notificationId)
    if (!n || n.isRead) return
    n.isRead = true
    unreadCount.value = Math.max(0, unreadCount.value - 1)
    try {
      await notificationService.markRead(notificationId)
    } catch {
      // revert on failure
      n.isRead = false
      unreadCount.value++
    }
  }

  async function markAllRead() {
    const prev = notifications.value.map((n) => ({ ...n }))
    notifications.value.forEach((n) => (n.isRead = true))
    unreadCount.value = 0
    try {
      await notificationService.markAllRead()
    } catch {
      // revert
      notifications.value = prev
      await refreshUnreadCount()
    }
  }

  // ── Dropdown ────────────────────────────────────────────────────────────────

  function toggleDropdown() {
    dropdownOpen.value = !dropdownOpen.value
    if (dropdownOpen.value) {
      // (Re)load on open so it's always fresh.
      loadFirstPage().catch(() => {})
    }
  }

  function closeDropdown() {
    dropdownOpen.value = false
  }

  // ── SignalR ─────────────────────────────────────────────────────────────────

  function connect() {
    if (hubConnection || !auth.accessToken) return

    hubConnection = new signalR.HubConnectionBuilder()
      .withUrl('/hubs/notifications', {
        accessTokenFactory: () => auth.accessToken ?? '',
      })
      .withAutomaticReconnect()
      .configureLogging(signalR.LogLevel.Warning)
      .build()

    // Persisted notification — surfaces in the bell dropdown
    hubConnection.on('NotificationReceived', (n: Notification) => {
      prependNotification(n)
      if (!n.isRead) unreadCount.value++
      // Side-channel updates for related stores
      forwardToRelatedStores(n)
    })

    // Server-side mark-as-read (e.g. from another tab/device)
    hubConnection.on('UnreadCountChanged', (payload: { unread: number }) => {
      unreadCount.value = payload.unread ?? 0
    })

    // Presence broadcast from the user's friends
    hubConnection.on('UserOnline', (payload: { userId: string; at: string }) => {
      const presence = usePresenceStore()
      presence.setOnline(payload.userId)
    })
    hubConnection.on('UserOffline', (payload: { userId: string; at: string }) => {
      const presence = usePresenceStore()
      presence.setOffline(payload.userId, payload.at)
    })

    // ── Legacy events kept for backwards compatibility ───────────────────────
    hubConnection.on('FriendRequestReceived', (payload: { from: { id: string; displayName: string } }) => {
      const friends = useFriendsStore()
      friends.onFriendRequestReceived(payload.from)
    })

    hubConnection.on('FriendRequestAccepted', (payload: { by: { id: string; displayName: string } }) => {
      const friends = useFriendsStore()
      friends.onFriendRequestAccepted(payload.by)
    })

    hubConnection.on('MessageReceived', (_payload: { conversationId: string; messageId: string; senderId: string }) => {
      // The chat store already bumps unreadCount via ReceiveMessage on its own
      // hub; this transient event only matters when the chat hub is not yet
      // connected (e.g. first page load before MessengerPopup mounts).
      // No-op for now — kept as a hook for future toast/sound integration.
    })

    hubConnection.start().catch(() => {
      /* dev-mode hot-reload friendly: swallow */
    })
  }

  function disconnect() {
    hubConnection?.stop()
    hubConnection = null
  }

  // ── Helpers ─────────────────────────────────────────────────────────────────

  function prependNotification(n: Notification) {
    // Drop duplicates (server may resend on reconnect)
    if (notifications.value.some((x) => x.id === n.id)) return
    notifications.value.unshift(n)
    if (notifications.value.length > MAX_KEPT) {
      notifications.value.length = MAX_KEPT
    }
  }

  /**
   * Route specific notification types to their dedicated stores so badges
   * and lists stay consistent without an extra round-trip.
   */
  function forwardToRelatedStores(n: Notification) {
    switch (n.type) {
      case 'FriendRequest': {
        const friends = useFriendsStore()
        if (n.actor) {
          friends.onFriendRequestReceived({ id: n.actor.id, displayName: n.actor.displayName })
        } else {
          friends.refreshPendingCount().catch(() => {})
        }
        break
      }
      case 'FriendRequestAccepted': {
        const friends = useFriendsStore()
        if (n.actor) {
          friends.onFriendRequestAccepted({ id: n.actor.id, displayName: n.actor.displayName })
        }
        break
      }
      // NewPostFromFriend is delivered to the feed store via the dedicated
      // FeedHub "NewPost" event; we don't need to forward it here.
      default:
        break
    }
  }

  return {
    notifications,
    unreadCount,
    dropdownOpen,
    loading,
    hasMore,
    hasUnread,
    loadFirstPage,
    loadMore,
    refreshUnreadCount,
    markRead,
    markAllRead,
    toggleDropdown,
    closeDropdown,
    connect,
    disconnect,
  }
})
