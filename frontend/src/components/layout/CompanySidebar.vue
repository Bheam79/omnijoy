<script setup lang="ts">
import { computed } from 'vue'
import { RouterLink, useRoute } from 'vue-router'
import { useCompanyModeStore } from '@/stores/companyMode'

defineProps<{ open: boolean }>()
const emit = defineEmits<{ close: [] }>()

const companyMode = useCompanyModeStore()
const route       = useRoute()

// ── Computed helpers ──────────────────────────────────────────────────────────

const companyId = computed(() => companyMode.activeCompany?.id ?? '')

/**
 * True when the current route is an event detail / event sub-page.
 * We show the event section in the sidebar whenever an event is loaded.
 */
const onEventRoute = computed(() =>
  route.path.startsWith('/events/') && !!companyMode.activeEvent
)

const eventId    = computed(() => companyMode.activeEvent?.id ?? '')
const eventTitle = computed(() => companyMode.activeEvent?.title ?? 'Event')

// ── Active-state helpers ───────────────────────────────────────────────────────

function isCompanyActive(exact = false): boolean {
  const basePath = `/company/${companyId.value}`
  if (exact) return route.path === basePath && !Object.keys(route.query).length
  return route.path === basePath
}

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

  <!-- Sidebar panel -->
  <aside
    class="fixed top-24 left-0 bottom-0 z-40 w-64 bg-slate-900 border-r border-slate-700 overflow-y-auto transition-transform duration-200"
    :class="open ? 'translate-x-0' : '-translate-x-full lg:translate-x-0'"
  >
    <nav class="p-3 space-y-0.5">

      <!-- ── Company section ──────────────────────────────────────────────── -->
      <p class="px-3 pt-2 pb-1 text-[10px] font-semibold text-slate-500 uppercase tracking-widest">
        {{ companyMode.activeCompany?.name }}
      </p>

      <!-- Home -->
      <RouterLink
        :to="`/company/${companyId}`"
        data-testid="company-nav-home"
        class="flex items-center gap-3 px-3 py-2.5 rounded-xl text-sm font-medium transition-colors"
        :class="isCompanyActive(true)
          ? 'bg-indigo-900/50 text-indigo-300'
          : 'text-slate-300 hover:bg-slate-700'"
        @click="emit('close')"
      >
        <svg class="h-5 w-5 shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
            d="M19 21V5a2 2 0 00-2-2H7a2 2 0 00-2 2v16m14 0h2m-2 0h-5m-9 0H3m2 0h5M9 7h1m-1 4h1m4-4h1m-1 4h1m-5 10v-5a1 1 0 011-1h2a1 1 0 011 1v5m-4 0h4"/>
        </svg>
        <span>Home</span>
      </RouterLink>

      <!-- Edit Page -->
      <RouterLink
        :to="`/company/${companyId}?action=edit`"
        data-testid="company-nav-edit"
        class="flex items-center gap-3 px-3 py-2.5 rounded-xl text-sm font-medium transition-colors"
        :class="route.query.action === 'edit'
          ? 'bg-indigo-900/50 text-indigo-300'
          : 'text-slate-300 hover:bg-slate-700'"
        @click="emit('close')"
      >
        <svg class="h-5 w-5 shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
            d="M11 5H6a2 2 0 00-2 2v11a2 2 0 002 2h11a2 2 0 002-2v-5m-1.414-9.414a2 2 0 112.828 2.828L11.828 15H9v-2.828l8.586-8.586z"/>
        </svg>
        <span>Edit Page</span>
      </RouterLink>

      <!-- Events -->
      <RouterLink
        :to="`/company/${companyId}?tab=events`"
        data-testid="company-nav-events"
        class="flex items-center gap-3 px-3 py-2.5 rounded-xl text-sm font-medium transition-colors"
        :class="route.query.tab === 'events'
          ? 'bg-indigo-900/50 text-indigo-300'
          : 'text-slate-300 hover:bg-slate-700'"
        @click="emit('close')"
      >
        <svg class="h-5 w-5 shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
            d="M8 7V3m8 4V3m-9 8h10M5 21h14a2 2 0 002-2V7a2 2 0 00-2-2H5a2 2 0 00-2 2v12a2 2 0 002 2z"/>
        </svg>
        <span>Events</span>
      </RouterLink>

      <!-- Settings -->
      <RouterLink
        :to="`/company/${companyId}?tab=settings`"
        data-testid="company-nav-settings"
        class="flex items-center gap-3 px-3 py-2.5 rounded-xl text-sm font-medium transition-colors"
        :class="route.query.tab === 'settings'
          ? 'bg-indigo-900/50 text-indigo-300'
          : 'text-slate-300 hover:bg-slate-700'"
        @click="emit('close')"
      >
        <svg class="h-5 w-5 shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
            d="M10.325 4.317c.426-1.756 2.924-1.756 3.35 0a1.724 1.724 0 002.573 1.066c1.543-.94 3.31.826 2.37 2.37a1.724 1.724 0 001.065 2.572c1.756.426 1.756 2.924 0 3.35a1.724 1.724 0 00-1.066 2.573c.94 1.543-.826 3.31-2.37 2.37a1.724 1.724 0 00-2.572 1.065c-.426 1.756-2.924 1.756-3.35 0a1.724 1.724 0 00-2.573-1.066c-1.543.94-3.31-.826-2.37-2.37a1.724 1.724 0 00-1.065-2.572c-1.756-.426-1.756-2.924 0-3.35a1.724 1.724 0 001.066-2.573c-.94-1.543.826-3.31 2.37-2.37.996.608 2.296.07 2.572-1.065z"/>
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 12a3 3 0 11-6 0 3 3 0 016 0z"/>
        </svg>
        <span>Settings</span>
      </RouterLink>

      <!-- ── Event section (only when viewing an event) ────────────────────── -->
      <template v-if="onEventRoute">
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
