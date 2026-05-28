<script setup lang="ts">
import { ref, onMounted, onUnmounted } from 'vue'
import { RouterLink, useRouter } from 'vue-router'
import { useAuthStore } from '@/stores/auth'
import { useFriendsStore } from '@/stores/friends'
import { useChatStore } from '@/stores/chat'
import * as signalR from '@microsoft/signalr'

defineProps<{ sidebarOpen: boolean }>()
const emit = defineEmits<{ 'toggle-sidebar': [] }>()

const auth = useAuthStore()
const friendsStore = useFriendsStore()
const chatStore = useChatStore()
const router = useRouter()

const profileOpen = ref(false)
let hubConnection: signalR.HubConnection | null = null

onMounted(async () => {
  if (auth.isAuthenticated) {
    await friendsStore.refreshPendingCount()
    connectSignalR()
  }
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

  hubConnection.on('FriendRequestReceived', () => {
    friendsStore.pendingCount++
  })

  hubConnection.start().catch(() => { /* silent fail during dev */ })
}

async function logout() {
  profileOpen.value = false
  hubConnection?.stop()
  await auth.logout()
  router.push('/')
}
</script>

<template>
  <!-- Transparent click-catcher to close profile dropdown -->
  <div
    v-if="profileOpen"
    class="fixed inset-0 z-40"
    @click="profileOpen = false"
  />

  <header class="fixed top-0 inset-x-0 z-50 h-16 bg-white border-b border-gray-200 flex items-center px-3 gap-2 lg:gap-4">
    <!-- Hamburger (mobile only) -->
    <button
      class="lg:hidden p-2 rounded-lg text-gray-600 hover:bg-gray-100 transition-colors"
      :aria-label="sidebarOpen ? 'Close menu' : 'Open menu'"
      @click="emit('toggle-sidebar')"
    >
      <svg v-if="!sidebarOpen" class="h-5 w-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 6h16M4 12h16M4 18h16"/>
      </svg>
      <svg v-else class="h-5 w-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12"/>
      </svg>
    </button>

    <!-- Logo -->
    <RouterLink to="/wall" class="shrink-0 flex items-center gap-1.5">
      <div class="h-8 w-8 rounded-lg bg-indigo-600 flex items-center justify-center">
        <span class="text-white font-bold text-sm">OJ</span>
      </div>
      <span class="hidden sm:block text-lg font-bold text-indigo-600 tracking-tight">Omnijoy</span>
    </RouterLink>

    <!-- Search (grows to fill available space) -->
    <div class="flex-1 max-w-sm mx-auto">
      <div class="relative">
        <svg class="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-gray-400 pointer-events-none" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z"/>
        </svg>
        <input
          type="search"
          placeholder="Search Omnijoy…"
          class="w-full bg-gray-100 rounded-full pl-9 pr-4 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500 focus:bg-white transition-colors"
        />
      </div>
    </div>

    <!-- Right icons -->
    <div class="flex items-center gap-1 shrink-0">
      <!-- Friend requests button with badge -->
      <RouterLink
        to="/friends"
        class="relative p-2 rounded-full text-gray-600 hover:bg-gray-100 transition-colors"
        aria-label="Friend requests"
      >
        <svg class="h-6 w-6" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
            d="M17 20h5v-2a3 3 0 00-5.356-1.857M17 20H7m10 0v-2c0-.656-.126-1.283-.356-1.857M7 20H2v-2a3 3 0 015.356-1.857M7 20v-2c0-.656.126-1.283.356-1.857m0 0a5.002 5.002 0 019.288 0M15 7a3 3 0 11-6 0 3 3 0 016 0zm6 3a2 2 0 11-4 0 2 2 0 014 0zM7 10a2 2 0 11-4 0 2 2 0 014 0z"/>
        </svg>
        <span
          v-if="friendsStore.pendingCount > 0"
          class="absolute top-0.5 right-0.5 min-w-[1.1rem] h-[1.1rem] rounded-full bg-red-500 ring-2 ring-white flex items-center justify-center text-white text-[10px] font-bold px-0.5"
        >
          {{ friendsStore.pendingCount > 9 ? '9+' : friendsStore.pendingCount }}
        </span>
      </RouterLink>

      <!-- Notifications bell -->
      <button
        class="relative p-2 rounded-full text-gray-600 hover:bg-gray-100 transition-colors"
        aria-label="Notifications"
      >
        <svg class="h-6 w-6" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 17h5l-1.405-1.405A2.032 2.032 0 0118 14.158V11a6.002 6.002 0 00-4-5.659V5a2 2 0 10-4 0v.341C7.67 6.165 6 8.388 6 11v3.159c0 .538-.214 1.055-.595 1.436L4 17h5m6 0v1a3 3 0 11-6 0v-1m6 0H9"/>
        </svg>
      </button>

      <!-- Messenger / chat -->
      <button
        class="relative p-2 rounded-full text-gray-600 hover:bg-gray-100 transition-colors"
        aria-label="Messages"
        @click="chatStore.toggleList()"
      >
        <svg class="h-6 w-6" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M8 12h.01M12 12h.01M16 12h.01M21 8a9 9 0 11-18 0 9 9 0 0118 0z"/>
        </svg>
        <!-- Unread badge -->
        <span
          v-if="chatStore.totalUnread > 0"
          class="absolute top-0.5 right-0.5 min-w-[1.1rem] h-[1.1rem] rounded-full bg-red-500 ring-2 ring-white flex items-center justify-center text-white text-[10px] font-bold px-0.5"
        >
          {{ chatStore.totalUnread > 9 ? '9+' : chatStore.totalUnread }}
        </span>
      </button>

      <!-- Profile dropdown trigger -->
      <div class="relative z-50">
        <button
          class="flex items-center p-1 rounded-full hover:bg-gray-100 transition-colors"
          aria-label="Your account"
          @click.stop="profileOpen = !profileOpen"
        >
          <div
            v-if="auth.user?.avatarUrl"
            class="h-8 w-8 rounded-full overflow-hidden ring-2 ring-transparent hover:ring-indigo-300 transition-all"
          >
            <img :src="auth.user.avatarUrl" :alt="auth.user.displayName" class="h-full w-full object-cover" />
          </div>
          <div
            v-else
            class="h-8 w-8 rounded-full bg-indigo-100 text-indigo-600 flex items-center justify-center text-sm font-bold ring-2 ring-transparent hover:ring-indigo-300 transition-all"
          >
            {{ auth.user?.displayName?.[0]?.toUpperCase() ?? '?' }}
          </div>
        </button>

        <!-- Dropdown panel -->
        <Transition
          enter-active-class="transition ease-out duration-100 origin-top-right"
          enter-from-class="opacity-0 scale-95"
          enter-to-class="opacity-100 scale-100"
          leave-active-class="transition ease-in duration-75 origin-top-right"
          leave-from-class="opacity-100 scale-100"
          leave-to-class="opacity-0 scale-95"
        >
          <div
            v-if="profileOpen"
            class="absolute right-0 top-full mt-1 w-56 bg-white rounded-xl shadow-lg border border-gray-200 py-1"
          >
            <!-- User info -->
            <div class="px-4 py-3 border-b border-gray-100">
              <p class="text-sm font-semibold text-gray-900 truncate">{{ auth.user?.displayName ?? 'You' }}</p>
              <p class="text-xs text-gray-500 truncate mt-0.5">{{ auth.user?.email }}</p>
            </div>

            <!-- Links -->
            <RouterLink
              :to="auth.user ? `/profile/${auth.user.id}` : '/'"
              class="flex items-center gap-3 px-4 py-2.5 text-sm text-gray-700 hover:bg-gray-50 transition-colors"
              @click="profileOpen = false"
            >
              <svg class="h-4 w-4 text-gray-400 shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M16 7a4 4 0 11-8 0 4 4 0 018 0zM12 14a7 7 0 00-7 7h14a7 7 0 00-7-7z"/>
              </svg>
              My Profile
            </RouterLink>

            <RouterLink
              to="/settings"
              class="flex items-center gap-3 px-4 py-2.5 text-sm text-gray-700 hover:bg-gray-50 transition-colors"
              @click="profileOpen = false"
            >
              <svg class="h-4 w-4 text-gray-400 shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M10.325 4.317c.426-1.756 2.924-1.756 3.35 0a1.724 1.724 0 002.573 1.066c1.543-.94 3.31.826 2.37 2.37a1.724 1.724 0 001.065 2.572c1.756.426 1.756 2.924 0 3.35a1.724 1.724 0 00-1.066 2.573c.94 1.543-.826 3.31-2.37 2.37a1.724 1.724 0 00-2.572 1.065c-.426 1.756-2.924 1.756-3.35 0a1.724 1.724 0 00-2.573-1.066c-1.543.94-3.31-.826-2.37-2.37a1.724 1.724 0 00-1.065-2.572c-1.756-.426-1.756-2.924 0-3.35a1.724 1.724 0 001.066-2.573c-.94-1.543.826-3.31 2.37-2.37.996.608 2.296.07 2.572-1.065z"/>
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 12a3 3 0 11-6 0 3 3 0 016 0z"/>
              </svg>
              Settings
            </RouterLink>

            <div class="border-t border-gray-100 mt-1 pt-1">
              <button
                class="w-full flex items-center gap-3 px-4 py-2.5 text-sm text-red-600 hover:bg-red-50 transition-colors"
                @click="logout"
              >
                <svg class="h-4 w-4 shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M17 16l4-4m0 0l-4-4m4 4H7m6 4v1a3 3 0 01-3 3H6a3 3 0 01-3-3V7a3 3 0 013-3h4a3 3 0 013 3v1"/>
                </svg>
                Log out
              </button>
            </div>
          </div>
        </Transition>
      </div>
    </div>
  </header>
</template>
