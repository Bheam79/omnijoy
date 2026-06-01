<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted } from 'vue'
import { RouterLink, useRouter } from 'vue-router'
import { useAuthStore } from '@/stores/auth'
import { useFriendsStore } from '@/stores/friends'
import { useChatStore } from '@/stores/chat'
import { useLiveStore } from '@/stores/live'
import { useNotificationsStore } from '@/stores/notifications'
import { usePresenceStore } from '@/stores/presence'
import { useCompanyModeStore } from '@/stores/companyMode'
import NotificationBell from './NotificationBell.vue'
import SearchSuggest from './SearchSuggest.vue'

defineProps<{ sidebarOpen: boolean }>()
const emit = defineEmits<{ 'toggle-sidebar': [] }>()

const auth          = useAuthStore()
const friendsStore  = useFriendsStore()
const chatStore     = useChatStore()
const liveStore     = useLiveStore()
const notifications = useNotificationsStore()
const presence      = usePresenceStore()
const companyMode   = useCompanyModeStore()
const router        = useRouter()

const profileOpen = ref(false)
const hasLiveStreams = computed(() => liveStore.streams.length > 0)

onMounted(async () => {
  if (auth.isAuthenticated) {
    // Connect to the centralised NotificationHub (also delivers presence +
    // friend-request events to their respective stores).
    notifications.connect()
    await Promise.all([
      friendsStore.refreshPendingCount(),
      notifications.refreshUnreadCount(),
    ])
  }
})

onUnmounted(() => {
  notifications.disconnect()
})

async function logout() {
  profileOpen.value = false
  notifications.disconnect()
  presence.reset()
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

  <!-- Company mode banner (shown below the main nav bar) -->
  <Transition name="slide-down">
    <div
      v-if="companyMode.isActive"
      class="fixed top-16 inset-x-0 z-40 flex items-center justify-center gap-3 px-4 py-1.5 bg-indigo-900/90 border-b border-indigo-700 backdrop-blur-sm text-sm"
    >
      <span class="text-indigo-200">
        🏢 Acting as <strong class="text-white">{{ companyMode.activeCompany?.name }}</strong>
      </span>
      <button
        class="text-indigo-400 hover:text-white text-xs font-medium border border-indigo-700 rounded-full px-2 py-0.5 hover:border-indigo-400 transition"
        @click="companyMode.deactivate()"
      >
        Exit
      </button>
    </div>
  </Transition>

  <header class="fixed top-0 inset-x-0 z-50 h-16 bg-slate-900 border-b border-slate-700 flex items-center px-3 gap-2 lg:gap-4">
    <!-- Hamburger (mobile only) -->
    <button
      class="lg:hidden p-2 rounded-lg text-slate-400 hover:bg-slate-700 transition-colors"
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

    <!-- Logo — navigates to company home when acting as a company -->
    <RouterLink
      :to="companyMode.isActive ? `/company/${companyMode.activeCompany!.id}` : '/wall'"
      class="shrink-0 flex items-center gap-1.5"
    >
      <img src="/logo.png" alt="Omnijoy" class="h-8 w-8 rounded-lg" />
      <span class="hidden sm:block text-lg font-bold text-slate-100 tracking-tight">Omnijoy</span>
    </RouterLink>

    <!-- Search (grows to fill available space) -->
    <div class="flex-1 max-w-sm mx-auto">
      <SearchSuggest />
    </div>

    <!-- Right icons -->
    <div class="flex items-center gap-1 shrink-0">
      <!-- Go Live button -->
      <RouterLink
        to="/live"
        class="relative hidden sm:flex items-center gap-1.5 px-3 py-1.5 rounded-full text-sm font-semibold transition"
        :class="hasLiveStreams
          ? 'bg-red-100 text-red-400 hover:bg-red-200'
          : 'text-slate-400 hover:bg-slate-700'"
        aria-label="Live streams"
      >
        <span
          class="w-2 h-2 rounded-full"
          :class="hasLiveStreams ? 'bg-red-500 animate-pulse' : 'bg-gray-400'"
        />
        Live
      </RouterLink>
      <!-- Friend requests button with badge -->
      <RouterLink
        to="/friends"
        class="relative p-2 rounded-full text-slate-400 hover:bg-slate-700 transition-colors"
        aria-label="Friend requests"
      >
        <svg class="h-6 w-6" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
            d="M17 20h5v-2a3 3 0 00-5.356-1.857M17 20H7m10 0v-2c0-.656-.126-1.283-.356-1.857M7 20H2v-2a3 3 0 015.356-1.857M7 20v-2c0-.656.126-1.283.356-1.857m0 0a5.002 5.002 0 019.288 0M15 7a3 3 0 11-6 0 3 3 0 016 0zm6 3a2 2 0 11-4 0 2 2 0 014 0zM7 10a2 2 0 11-4 0 2 2 0 014 0z"/>
        </svg>
        <span
          v-if="friendsStore.pendingCount > 0"
          class="absolute top-0.5 right-0.5 min-w-[1.1rem] h-[1.1rem] rounded-full bg-red-500 ring-2 ring-slate-900 flex items-center justify-center text-white text-[10px] font-bold px-0.5"
        >
          {{ friendsStore.pendingCount > 9 ? '9+' : friendsStore.pendingCount }}
        </span>
      </RouterLink>

      <!-- Notifications bell + dropdown -->
      <NotificationBell />

      <!-- Messenger / chat -->
      <button
        class="relative p-2 rounded-full text-slate-400 hover:bg-slate-700 transition-colors"
        aria-label="Messages"
        @click="chatStore.toggleList()"
      >
        <svg class="h-6 w-6" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M8 12h.01M12 12h.01M16 12h.01M21 12c0 4.418-4.03 8-9 8a9.863 9.863 0 01-4.255-.949L3 20l1.395-3.72C3.512 15.042 3 13.574 3 12c0-4.418 4.03-8 9-8s9 3.582 9 8z"/>
        </svg>
        <!-- Unread badge -->
        <span
          v-if="chatStore.totalUnread > 0"
          class="absolute top-0.5 right-0.5 min-w-[1.1rem] h-[1.1rem] rounded-full bg-red-500 ring-2 ring-slate-900 flex items-center justify-center text-white text-[10px] font-bold px-0.5"
        >
          {{ chatStore.totalUnread > 9 ? '9+' : chatStore.totalUnread }}
        </span>
      </button>

      <!-- Profile dropdown trigger -->
      <div class="relative z-50">
        <button
          data-testid="user-menu-button"
          class="flex items-center p-1 rounded-full hover:bg-slate-700 transition-colors"
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
            class="h-8 w-8 rounded-full bg-indigo-900 text-indigo-400 flex items-center justify-center text-sm font-bold ring-2 ring-transparent hover:ring-indigo-300 transition-all"
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
            class="absolute right-0 top-full mt-1 w-56 bg-slate-800 rounded-xl shadow-lg border border-slate-700 py-1"
          >
            <!-- User info -->
            <div class="px-4 py-3 border-b border-slate-700">
              <p class="text-sm font-semibold text-slate-100 truncate">{{ auth.user?.displayName ?? 'You' }}</p>
              <p class="text-xs text-gray-500 truncate mt-0.5">{{ auth.user?.email }}</p>
            </div>

            <!-- Links -->
            <RouterLink
              :to="auth.user ? `/profile/${auth.user.id}` : '/'"
              class="flex items-center gap-3 px-4 py-2.5 text-sm text-slate-300 hover:bg-slate-700 transition-colors"
              @click="profileOpen = false"
            >
              <svg class="h-4 w-4 text-slate-500 shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M16 7a4 4 0 11-8 0 4 4 0 018 0zM12 14a7 7 0 00-7 7h14a7 7 0 00-7-7z"/>
              </svg>
              My Profile
            </RouterLink>

            <RouterLink
              to="/settings"
              data-testid="nav-settings"
              class="flex items-center gap-3 px-4 py-2.5 text-sm text-slate-300 hover:bg-slate-700 transition-colors"
              @click="profileOpen = false"
            >
              <svg class="h-4 w-4 text-slate-500 shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M10.325 4.317c.426-1.756 2.924-1.756 3.35 0a1.724 1.724 0 002.573 1.066c1.543-.94 3.31.826 2.37 2.37a1.724 1.724 0 001.065 2.572c1.756.426 1.756 2.924 0 3.35a1.724 1.724 0 00-1.066 2.573c.94 1.543-.826 3.31-2.37 2.37a1.724 1.724 0 00-2.572 1.065c-.426 1.756-2.924 1.756-3.35 0a1.724 1.724 0 00-2.573-1.066c-1.543.94-3.31-.826-2.37-2.37a1.724 1.724 0 00-1.065-2.572c-1.756-.426-1.756-2.924 0-3.35a1.724 1.724 0 001.066-2.573c-.94-1.543.826-3.31 2.37-2.37.996.608 2.296.07 2.572-1.065z"/>
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 12a3 3 0 11-6 0 3 3 0 016 0z"/>
              </svg>
              Settings
            </RouterLink>

            <div class="border-t border-slate-700 mt-1 pt-1">
              <button
                data-testid="logout-button"
                class="w-full flex items-center gap-3 px-4 py-2.5 text-sm text-red-400 hover:bg-red-950 transition-colors"
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

<style scoped>
.slide-down-enter-active,
.slide-down-leave-active {
  transition: transform 0.2s ease, opacity 0.2s ease;
}
.slide-down-enter-from,
.slide-down-leave-to {
  transform: translateY(-100%);
  opacity: 0;
}
</style>
