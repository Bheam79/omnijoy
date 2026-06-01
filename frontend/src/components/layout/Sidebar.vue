<script setup lang="ts">
import { computed } from 'vue'
import { RouterLink, useRoute } from 'vue-router'
import { useLiveStore } from '@/stores/live'
import { useAuthStore } from '@/stores/auth'
import { useCompanyModeStore } from '@/stores/companyMode'

defineProps<{ open: boolean }>()
const emit = defineEmits<{ close: [] }>()

const liveStore = useLiveStore()
const auth      = useAuthStore()
const companyMode = useCompanyModeStore()
const hasLiveStreams = computed(() => liveStore.streams.length > 0)

const route = useRoute()

interface NavItem {
  label: string
  to: string
  icon: 'home' | 'users' | 'calendar' | 'building' | 'broadcast' | 'shield'
  /** If true, also mark active for sub-routes (e.g. /events/123) */
  matchPrefix?: boolean
  testId: string
}

const navItems = computed<NavItem[]>(() => {
  const items: NavItem[] = [
    { label: auth.user?.displayName ?? 'My Wall', to: '/wall', icon: 'home', testId: 'nav-wall' },
    { label: 'Friends',       to: '/friends', icon: 'users',     testId: 'nav-friends' },
    { label: 'Events',        to: '/events',  icon: 'calendar',  matchPrefix: true, testId: 'nav-events' },
    { label: 'Company Pages', to: '/company', icon: 'building',  matchPrefix: true, testId: 'nav-company' },
    { label: 'Live',          to: '/live',    icon: 'broadcast', matchPrefix: true, testId: 'nav-live' },
  ]
  const role = auth.user?.role
  if (role === 'Admin' || role === 'Moderator') {
    items.push({ label: 'Admin', to: '/admin', icon: 'shield', matchPrefix: true, testId: 'nav-admin' })
  }
  return items
})

function isActive(item: NavItem): boolean {
  if (item.matchPrefix) {
    return route.path === item.to || route.path.startsWith(item.to + '/')
  }
  return route.path === item.to
}

// ── Event management section ────────────────────────────────────────────────
// Shown only in personal mode (not company mode) when the user can manage
// the active event. CompanySidebar covers the company-mode equivalent.
const showEventSection = computed(() =>
  route.path.startsWith('/events/') &&
  !!companyMode.activeEvent &&
  companyMode.canManageActiveEvent
)

const eventId    = computed(() => companyMode.activeEvent?.id ?? '')
const eventTitle = computed(() => companyMode.activeEvent?.title ?? 'Event')

function isEventActive(suffix: string): boolean {
  if (!eventId.value) return false
  return route.path === `/events/${eventId.value}${suffix}`
}
</script>

<template>
  <!-- Mobile backdrop -->
  <Transition
    enter-active-class="transition-opacity ease-out duration-200"
    enter-from-class="opacity-0"
    enter-to-class="opacity-100"
    leave-active-class="transition-opacity ease-in duration-150"
    leave-from-class="opacity-100"
    leave-to-class="opacity-0"
  >
    <div
      v-if="open"
      class="fixed inset-0 z-30 bg-black/40 lg:hidden"
      aria-hidden="true"
      @click="emit('close')"
    />
  </Transition>

  <!-- Sidebar panel — always in DOM; CSS drives show/hide for smooth transitions -->
  <aside
    class="fixed top-16 left-0 bottom-0 z-40 w-64 bg-slate-900 border-r border-slate-700 overflow-y-auto transition-transform duration-200"
    :class="open ? 'translate-x-0' : '-translate-x-full lg:translate-x-0'"
  >
    <nav class="p-3 space-y-0.5">
      <RouterLink
        v-for="item in navItems"
        :key="item.to"
        :to="item.to"
        :data-testid="item.testId"
        class="flex items-center gap-3 px-3 py-2.5 rounded-xl text-sm font-medium transition-colors"
        :class="isActive(item)
          ? 'bg-indigo-900/50 text-indigo-300'
          : 'text-slate-300 hover:bg-slate-700'"
        @click="emit('close')"
      >
        <!-- My Wall — user avatar or initial -->
        <template v-if="item.icon === 'home'">
          <img
            v-if="auth.user?.avatarUrl"
            :src="auth.user.avatarUrl"
            :alt="auth.user.displayName"
            class="h-5 w-5 shrink-0 rounded-full object-cover"
          />
          <div
            v-else
            class="h-5 w-5 shrink-0 rounded-full bg-indigo-900 text-indigo-400 flex items-center justify-center text-xs font-bold"
          >
            {{ auth.user?.displayName?.[0]?.toUpperCase() ?? '?' }}
          </div>
        </template>

        <!-- Friends / Users -->
        <svg v-else-if="item.icon === 'users'" class="h-5 w-5 shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M17 20h5v-2a3 3 0 00-5.356-1.857M17 20H7m10 0v-2c0-.656-.126-1.283-.356-1.857M7 20H2v-2a3 3 0 015.356-1.857M7 20v-2c0-.656.126-1.283.356-1.857m0 0a5.002 5.002 0 019.288 0M15 7a3 3 0 11-6 0 3 3 0 016 0zm6 3a2 2 0 11-4 0 2 2 0 014 0zM7 10a2 2 0 11-4 0 2 2 0 014 0z"/>
        </svg>

        <!-- Calendar / Events -->
        <svg v-else-if="item.icon === 'calendar'" class="h-5 w-5 shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M8 7V3m8 4V3m-9 8h10M5 21h14a2 2 0 002-2V7a2 2 0 00-2-2H5a2 2 0 00-2 2v12a2 2 0 002 2z"/>
        </svg>

        <!-- Building / Company -->
        <svg v-else-if="item.icon === 'building'" class="h-5 w-5 shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 21V5a2 2 0 00-2-2H7a2 2 0 00-2 2v16m14 0h2m-2 0h-5m-9 0H3m2 0h5M9 7h1m-1 4h1m4-4h1m-1 4h1m-5 10v-5a1 1 0 011-1h2a1 1 0 011 1v5m-4 0h4"/>
        </svg>

        <!-- Broadcast / Live -->
        <svg v-else-if="item.icon === 'broadcast'" class="h-5 w-5 shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 10l4.553-2.069A1 1 0 0121 8.876V15.124a1 1 0 01-1.447.894L15 14M3 8a2 2 0 012-2h8a2 2 0 012 2v8a2 2 0 01-2 2H5a2 2 0 01-2-2V8z"/>
        </svg>

        <!-- Shield / Admin -->
        <svg v-else-if="item.icon === 'shield'" class="h-5 w-5 shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 12l2 2 4-4M12 3l8 4v5c0 5-3.5 9-8 10-4.5-1-8-5-8-10V7l8-4z"/>
        </svg>

        <span>{{ item.label }}</span>

        <!-- Live indicator dot — shown only when there are active streams -->
        <span
          v-if="item.icon === 'broadcast' && hasLiveStreams"
          class="ml-auto h-2 w-2 rounded-full bg-red-500 animate-pulse"
        />
      </RouterLink>
      <!-- ── Event management section (personal mode, owner only) ────────── -->
      <template v-if="showEventSection">
        <!-- Separator -->
        <div class="mx-3 my-2 border-t border-slate-700" />

        <p class="px-3 pb-1 text-[10px] font-semibold text-slate-500 uppercase tracking-widest truncate">
          Event
        </p>

        <!-- Event home (event name) -->
        <RouterLink
          :to="`/events/${eventId}`"
          data-testid="event-nav-home"
          class="flex items-center gap-3 px-3 py-2.5 rounded-xl text-sm font-medium transition-colors"
          :class="isEventActive('')
            ? 'bg-indigo-900/50 text-indigo-300'
            : 'text-slate-300 hover:bg-slate-700'"
          @click="emit('close')"
        >
          <svg class="h-5 w-5 shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
              d="M8 7V3m8 4V3m-9 8h10M5 21h14a2 2 0 002-2V7a2 2 0 00-2-2H5a2 2 0 00-2 2v12a2 2 0 002 2z"/>
          </svg>
          <span class="truncate">{{ eventTitle }}</span>
        </RouterLink>

        <!-- Edit Event -->
        <RouterLink
          :to="`/events/${eventId}/edit`"
          data-testid="event-nav-edit"
          class="flex items-center gap-3 px-3 py-2.5 rounded-xl text-sm font-medium transition-colors"
          :class="isEventActive('/edit')
            ? 'bg-indigo-900/50 text-indigo-300'
            : 'text-slate-300 hover:bg-slate-700'"
          @click="emit('close')"
        >
          <svg class="h-5 w-5 shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
              d="M11 5H6a2 2 0 00-2 2v11a2 2 0 002 2h11a2 2 0 002-2v-5m-1.414-9.414a2 2 0 112.828 2.828L11.828 15H9v-2.828l8.586-8.586z"/>
          </svg>
          <span>Edit Event</span>
        </RouterLink>

        <!-- Participants -->
        <RouterLink
          :to="`/events/${eventId}/participants`"
          data-testid="event-nav-participants"
          class="flex items-center gap-3 px-3 py-2.5 rounded-xl text-sm font-medium transition-colors"
          :class="isEventActive('/participants')
            ? 'bg-indigo-900/50 text-indigo-300'
            : 'text-slate-300 hover:bg-slate-700'"
          @click="emit('close')"
        >
          <svg class="h-5 w-5 shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
              d="M17 20h5v-2a3 3 0 00-5.356-1.857M17 20H7m10 0v-2c0-.656-.126-1.283-.356-1.857M7 20H2v-2a3 3 0 015.356-1.857M7 20v-2c0-.656.126-1.283.356-1.857m0 0a5.002 5.002 0 019.288 0M15 7a3 3 0 11-6 0 3 3 0 016 0zm6 3a2 2 0 11-4 0 2 2 0 014 0zM7 10a2 2 0 11-4 0 2 2 0 014 0z"/>
          </svg>
          <span>Participants</span>
        </RouterLink>

        <!-- Event Settings -->
        <RouterLink
          :to="`/events/${eventId}/settings`"
          data-testid="event-nav-settings"
          class="flex items-center gap-3 px-3 py-2.5 rounded-xl text-sm font-medium transition-colors"
          :class="isEventActive('/settings')
            ? 'bg-indigo-900/50 text-indigo-300'
            : 'text-slate-300 hover:bg-slate-700'"
          @click="emit('close')"
        >
          <svg class="h-5 w-5 shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
              d="M10.325 4.317c.426-1.756 2.924-1.756 3.35 0a1.724 1.724 0 002.573 1.066c1.543-.94 3.31.826 2.37 2.37a1.724 1.724 0 001.065 2.572c1.756.426 1.756 2.924 0 3.35a1.724 1.724 0 00-1.066 2.573c.94 1.543-.826 3.31-2.37 2.37a1.724 1.724 0 00-2.572 1.065c-.426 1.756-2.924 1.756-3.35 0a1.724 1.724 0 00-2.573-1.066c-1.543.94-3.31-.826-2.37-2.37a1.724 1.724 0 00-1.065-2.572c-1.756-.426-1.756-2.924 0-3.35a1.724 1.724 0 001.066-2.573c-.94-1.543.826-3.31 2.37-2.37.996.608 2.296.07 2.572-1.065z"/>
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 12a3 3 0 11-6 0 3 3 0 016 0z"/>
          </svg>
          <span>Event Settings</span>
        </RouterLink>
      </template>
    </nav>

    <!-- Footer -->
    <div class="absolute bottom-0 inset-x-0 p-4 border-t border-slate-700">
      <p class="text-xs text-slate-500 text-center">© 2025 Omnijoy · No ads, ever.</p>
    </div>
  </aside>
</template>
