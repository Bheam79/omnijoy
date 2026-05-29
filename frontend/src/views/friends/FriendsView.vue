<script setup lang="ts">
import { ref, onMounted, onUnmounted } from 'vue'
import * as signalR from '@microsoft/signalr'
import { useFriendsStore } from '@/stores/friends'
import { useAuthStore } from '@/stores/auth'
import FriendButton from '@/components/friends/FriendButton.vue'

const store = useFriendsStore()
const auth = useAuthStore()

type Tab = 'friends' | 'requests' | 'sent' | 'suggestions'
const activeTab = ref<Tab>('friends')

let hubConnection: signalR.HubConnection | null = null

onMounted(async () => {
  await store.loadFriends(true)
  await store.loadRequests()
  await store.loadSuggestions()
  connectSignalR()
})

onUnmounted(() => {
  hubConnection?.stop()
})

function connectSignalR() {
  if (!auth.accessToken) return

  hubConnection = new signalR.HubConnectionBuilder()
    .withUrl('/hubs/notifications', { accessTokenFactory: () => auth.accessToken ?? '' })
    .withAutomaticReconnect()
    .configureLogging(signalR.LogLevel.Warning)
    .build()

  hubConnection.on('FriendRequestReceived', (data: { from: { id: string; displayName: string } }) => {
    store.onFriendRequestReceived(data.from)
  })

  hubConnection.on('FriendRequestAccepted', (data: { by: { id: string; displayName: string } }) => {
    store.onFriendRequestAccepted(data.by)
  })

  hubConnection.start().catch(console.error)
}

function formatDate(iso: string) {
  return new Date(iso).toLocaleDateString(undefined, { year: 'numeric', month: 'short', day: 'numeric' })
}
</script>

<template>
  <div class="space-y-4">
    <h1 class="text-2xl font-bold text-slate-100">Friends</h1>

    <!-- Tabs -->
    <div class="flex gap-1 bg-slate-900 p-1 rounded-xl text-sm">
      <button
        v-for="(label, tab) in ({
          friends: `Friends (${store.friends.length})`,
          requests: store.pendingCount > 0 ? `Requests (${store.pendingCount})` : 'Requests',
          sent: 'Sent',
          suggestions: 'Suggestions',
        } as Record<Tab, string>)"
        :key="tab"
        :data-testid="`${tab}-tab`"
        :class="[
          'flex-1 py-1.5 px-2 rounded-lg font-medium transition',
          activeTab === tab
            ? 'bg-slate-800 shadow text-indigo-400'
            : 'text-gray-500 hover:text-slate-300',
        ]"
        @click="activeTab = tab"
      >
        {{ label }}
      </button>
    </div>

    <!-- ── Friends list ──────────────────────────────────────────────────── -->
    <template v-if="activeTab === 'friends'">
      <div v-if="store.loadingFriends && store.friends.length === 0" class="grid grid-cols-1 sm:grid-cols-2 gap-3">
        <div v-for="i in 4" :key="i" class="bg-slate-800 rounded-xl p-4 shadow-sm border border-slate-700 animate-pulse flex items-center gap-3">
          <div class="w-12 h-12 rounded-full bg-slate-700 shrink-0" />
          <div class="flex-1 space-y-2"><div class="h-4 bg-slate-700 rounded w-32" /><div class="h-3 bg-slate-700 rounded w-20" /></div>
        </div>
      </div>

      <div v-else-if="store.friends.length === 0" class="text-center py-16">
        <p class="text-4xl mb-3">👥</p>
        <p class="font-semibold text-slate-300">No friends yet</p>
        <p class="text-sm text-gray-500 mt-1">Check out suggestions to find people you may know.</p>
        <button class="mt-4 text-sm font-medium text-indigo-400 hover:underline" @click="activeTab = 'suggestions'">
          Browse suggestions →
        </button>
      </div>

      <div v-else class="grid grid-cols-1 sm:grid-cols-2 gap-3">
        <div
          v-for="friend in store.friends"
          :key="friend.friendshipId"
          data-testid="friend-card"
          class="bg-slate-800 rounded-xl p-4 shadow-sm border border-slate-700 flex items-center gap-3"
        >
          <RouterLink :to="`/profile/${friend.user.id}`">
            <img
              v-if="friend.user.avatarUrl"
              :src="friend.user.avatarUrl"
              :alt="friend.user.displayName"
              class="w-12 h-12 rounded-full object-cover"
            />
            <div v-else class="w-12 h-12 rounded-full bg-indigo-900 text-indigo-400 flex items-center justify-center font-bold">
              {{ friend.user.displayName.charAt(0).toUpperCase() }}
            </div>
          </RouterLink>

          <div class="flex-1 min-w-0">
            <RouterLink :to="`/profile/${friend.user.id}`" class="font-semibold text-slate-100 hover:underline text-sm truncate block">
              {{ friend.user.displayName }}
            </RouterLink>
            <p class="text-xs text-gray-500">
              {{ friend.mutualFriendCount > 0 ? `${friend.mutualFriendCount} mutual` : 'Friends' }}
              <template v-if="friend.familyRelation">
                · <span class="text-indigo-400">{{ friend.familyRelation.subtype }}</span>
              </template>
            </p>
          </div>

          <button
            class="text-xs text-red-500 hover:text-red-400 font-medium shrink-0"
            @click="store.removeFriend(friend.user.id)"
          >Unfriend</button>
        </div>

        <button
          v-if="store.friendsHasMore"
          class="col-span-full text-sm text-indigo-400 hover:underline py-3 text-center"
          :disabled="store.loadingFriends"
          @click="store.loadFriends()"
        >
          {{ store.loadingFriends ? 'Loading…' : 'Show more' }}
        </button>
      </div>
    </template>

    <!-- ── Pending requests ───────────────────────────────────────────────── -->
    <template v-else-if="activeTab === 'requests'">
      <div v-if="store.loadingRequests" class="space-y-3">
        <div v-for="i in 3" :key="i" class="bg-slate-800 rounded-xl p-4 shadow-sm border border-slate-700 animate-pulse flex items-center gap-3">
          <div class="w-12 h-12 rounded-full bg-slate-700 shrink-0" />
          <div class="flex-1 space-y-2"><div class="h-4 bg-slate-700 rounded w-32" /><div class="h-3 bg-slate-700 rounded w-24" /></div>
        </div>
      </div>

      <div v-else-if="store.pendingRequests.length === 0" class="text-center py-16">
        <p class="text-3xl mb-2">📬</p>
        <p class="text-gray-500 text-sm">No pending friend requests</p>
      </div>

      <div v-else data-testid="pending-requests" class="space-y-3">
        <div
          v-for="req in store.pendingRequests"
          :key="req.friendshipId || req.from.id"
          class="bg-slate-800 rounded-xl p-4 shadow-sm border border-slate-700 flex items-center gap-3"
        >
          <RouterLink :to="`/profile/${req.from.id}`">
            <img v-if="req.from.avatarUrl" :src="req.from.avatarUrl" :alt="req.from.displayName" class="w-12 h-12 rounded-full object-cover" />
            <div v-else class="w-12 h-12 rounded-full bg-indigo-900 text-indigo-400 flex items-center justify-center font-bold">
              {{ req.from.displayName.charAt(0).toUpperCase() }}
            </div>
          </RouterLink>

          <div class="flex-1 min-w-0">
            <RouterLink :to="`/profile/${req.from.id}`" class="font-semibold text-slate-100 hover:underline text-sm">
              {{ req.from.displayName }}
            </RouterLink>
            <p class="text-xs text-gray-500">Sent {{ formatDate(req.sentAt) }}</p>
          </div>

          <div class="flex items-center gap-2 shrink-0">
            <button data-testid="accept-button" class="text-sm font-medium bg-indigo-600 hover:bg-indigo-700 text-white px-3 py-1.5 rounded-lg transition" @click="store.acceptRequest(req.from.id)">Accept</button>
            <button data-testid="decline-button" class="text-sm font-medium bg-slate-700 hover:bg-slate-700 text-slate-300 px-3 py-1.5 rounded-lg transition" @click="store.declineRequest(req.from.id)">Decline</button>
          </div>
        </div>
      </div>
    </template>

    <!-- ── Sent requests ──────────────────────────────────────────────────── -->
    <template v-else-if="activeTab === 'sent'">
      <div v-if="store.sentRequests.length === 0" class="text-center py-16">
        <p class="text-3xl mb-2">📤</p>
        <p class="text-gray-500 text-sm">No outgoing friend requests</p>
      </div>

      <div v-else class="space-y-3">
        <div
          v-for="req in store.sentRequests"
          :key="req.friendshipId || req.from.id"
          class="bg-slate-800 rounded-xl p-4 shadow-sm border border-slate-700 flex items-center gap-3"
        >
          <RouterLink :to="`/profile/${req.from.id}`">
            <div class="w-12 h-12 rounded-full bg-indigo-900 text-indigo-400 flex items-center justify-center font-bold">
              {{ req.from.displayName.charAt(0).toUpperCase() }}
            </div>
          </RouterLink>
          <div class="flex-1 min-w-0">
            <RouterLink :to="`/profile/${req.from.id}`" class="font-semibold text-slate-100 hover:underline text-sm">
              {{ req.from.displayName }}
            </RouterLink>
            <p class="text-xs text-gray-500">Sent {{ formatDate(req.sentAt) }}</p>
          </div>
          <button class="text-sm font-medium bg-slate-700 hover:bg-slate-700 text-slate-300 px-3 py-1.5 rounded-lg transition" @click="store.cancelRequest(req.from.id)">Cancel</button>
        </div>
      </div>
    </template>

    <!-- ── Suggestions ────────────────────────────────────────────────────── -->
    <template v-else-if="activeTab === 'suggestions'">
      <div v-if="store.loadingSuggestions" class="grid grid-cols-1 sm:grid-cols-2 gap-3">
        <div v-for="i in 4" :key="i" class="bg-slate-800 rounded-xl p-4 shadow-sm border border-slate-700 animate-pulse">
          <div class="flex items-center gap-3 mb-3"><div class="w-12 h-12 rounded-full bg-slate-700" /><div class="h-4 bg-slate-700 rounded w-28" /></div>
          <div class="h-8 bg-slate-700 rounded-lg" />
        </div>
      </div>

      <div v-else-if="store.suggestions.length === 0" class="text-center py-16">
        <p class="text-3xl mb-2">🤔</p>
        <p class="text-gray-500 text-sm">No suggestions right now — add more friends to expand your network.</p>
      </div>

      <div v-else class="grid grid-cols-1 sm:grid-cols-2 gap-3">
        <div
          v-for="s in store.suggestions"
          :key="s.user.id"
          class="bg-slate-800 rounded-xl p-4 shadow-sm border border-slate-700"
        >
          <div class="flex items-center gap-3 mb-3">
            <RouterLink :to="`/profile/${s.user.id}`">
              <div class="w-12 h-12 rounded-full bg-indigo-900 text-indigo-400 flex items-center justify-center font-bold">
                {{ s.user.displayName.charAt(0).toUpperCase() }}
              </div>
            </RouterLink>
            <div class="flex-1 min-w-0">
              <RouterLink :to="`/profile/${s.user.id}`" class="font-semibold text-slate-100 hover:underline text-sm block truncate">
                {{ s.user.displayName }}
              </RouterLink>
              <p class="text-xs text-gray-500">{{ s.mutualFriendCount }} mutual friend{{ s.mutualFriendCount !== 1 ? 's' : '' }}</p>
            </div>
          </div>
          <FriendButton :user-id="s.user.id" />
        </div>
      </div>
    </template>
  </div>
</template>
