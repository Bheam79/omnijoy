<script setup lang="ts">
import { RouterLink, useRoute } from 'vue-router'

defineProps<{ open: boolean }>()
const emit = defineEmits<{ close: [] }>()

const route = useRoute()

interface NavItem {
  label: string
  to: string
  icon: 'home' | 'users' | 'calendar' | 'building' | 'broadcast'
  /** If true, also mark active for sub-routes (e.g. /events/123) */
  matchPrefix?: boolean
}

const navItems: NavItem[] = [
  { label: 'My Wall',       to: '/wall',    icon: 'home' },
  { label: 'Friends',       to: '/friends', icon: 'users' },
  { label: 'Events',        to: '/events',  icon: 'calendar',  matchPrefix: true },
  { label: 'Company Pages', to: '/company', icon: 'building',  matchPrefix: true },
  { label: 'Live',          to: '/live',    icon: 'broadcast', matchPrefix: true },
]

function isActive(item: NavItem): boolean {
  if (item.matchPrefix) {
    return route.path === item.to || route.path.startsWith(item.to + '/')
  }
  return route.path === item.to
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
    class="fixed top-16 left-0 bottom-0 z-40 w-64 bg-white border-r border-gray-200 overflow-y-auto transition-transform duration-200"
    :class="open ? 'translate-x-0' : '-translate-x-full lg:translate-x-0'"
  >
    <nav class="p-3 space-y-0.5">
      <RouterLink
        v-for="item in navItems"
        :key="item.to"
        :to="item.to"
        class="flex items-center gap-3 px-3 py-2.5 rounded-xl text-sm font-medium transition-colors"
        :class="isActive(item)
          ? 'bg-indigo-50 text-indigo-700'
          : 'text-gray-700 hover:bg-gray-100'"
        @click="emit('close')"
      >
        <!-- Home / Wall -->
        <svg v-if="item.icon === 'home'" class="h-5 w-5 shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M3 12l2-2m0 0l7-7 7 7M5 10v10a1 1 0 001 1h3m10-11l2 2m-2-2v10a1 1 0 01-1 1h-3m-6 0a1 1 0 001-1v-4a1 1 0 011-1h2a1 1 0 011 1v4a1 1 0 001 1m-6 0h6"/>
        </svg>

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

        <span>{{ item.label }}</span>

        <!-- Live indicator dot -->
        <span
          v-if="item.icon === 'broadcast'"
          class="ml-auto h-2 w-2 rounded-full bg-red-500 animate-pulse"
        />
      </RouterLink>
    </nav>

    <!-- Footer -->
    <div class="absolute bottom-0 inset-x-0 p-4 border-t border-gray-100">
      <p class="text-xs text-gray-400 text-center">© 2025 Omnijoy · No ads, ever.</p>
    </div>
  </aside>
</template>
