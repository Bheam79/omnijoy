<script setup lang="ts">
import { onMounted, onUnmounted, ref } from 'vue'
import * as signalR from '@microsoft/signalr'
import { useEventsStore } from '@/stores/events'
import { useAuthStore } from '@/stores/auth'
import type { EventDto } from '@/services/eventService'
import EventCard from '@/components/events/EventCard.vue'
import EventCreateModal from '@/components/events/EventCreateModal.vue'

const eventsStore = useEventsStore()
const auth = useAuthStore()

type FilterOption = 'mine' | 'friends' | 'company' | null

const showCreateModal = ref(false)
const activeFilter = ref<FilterOption>(null)
let hubConnection: signalR.HubConnection | null = null

const filterOptions: { label: string; value: FilterOption }[] = [
  { label: 'All',     value: null },
  { label: 'Mine',    value: 'mine' },
  { label: 'Friends', value: 'friends' },
  { label: 'Company', value: 'company' },
]

onMounted(async () => {
  await eventsStore.loadEvents(null)
  connectSignalR()
})

onUnmounted(() => {
  hubConnection?.stop()
  eventsStore.reset()
})

// ── SignalR ───────────────────────────────────────────────────────────────────

function connectSignalR() {
  if (!auth.accessToken) return

  hubConnection = new signalR.HubConnectionBuilder()
    .withUrl('/hubs/feed', {
      accessTokenFactory: () => auth.accessToken ?? '',
    })
    .withAutomaticReconnect()
    .configureLogging(signalR.LogLevel.Warning)
    .build()

  hubConnection.on('EventCreated', (ev: EventDto) => {
    eventsStore.prependEvent(ev)
  })

  hubConnection.start().catch(console.error)
}

function setFilter(f: FilterOption) {
  activeFilter.value = f
  eventsStore.loadEvents(f)
}
</script>

<template>
  <div class="max-w-3xl mx-auto px-4 py-6">
    <!-- Header -->
    <div class="flex items-center justify-between mb-6">
      <div>
        <h1 class="text-2xl font-bold text-slate-100">Events</h1>
        <p class="text-sm text-gray-500 mt-0.5">Upcoming events from friends and pages you follow</p>
      </div>
      <button
        data-testid="create-event-button"
        class="flex items-center gap-2 px-4 py-2 bg-indigo-600 hover:bg-indigo-700 text-white text-sm font-medium rounded-xl transition shadow-sm"
        @click="showCreateModal = true"
      >
        <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 4v16m8-8H4"/>
        </svg>
        Create Event
      </button>
    </div>

    <!-- Filter tabs -->
    <div class="flex gap-1 mb-6 bg-slate-700 rounded-xl p-1 w-fit">
      <button
        v-for="opt in filterOptions"
        :key="String(opt.value)"
        class="px-4 py-1.5 rounded-lg text-sm font-medium transition-all"
        :class="activeFilter === opt.value
          ? 'bg-slate-800 text-indigo-300 shadow-sm'
          : 'text-slate-400 hover:text-slate-200'"
        @click="setFilter(opt.value)"
      >
        {{ opt.label }}
      </button>
    </div>

    <!-- Loading skeleton -->
    <div v-if="eventsStore.loading" class="grid grid-cols-1 sm:grid-cols-2 gap-4">
      <div
        v-for="i in 4"
        :key="i"
        class="bg-slate-800 rounded-xl border border-slate-700 overflow-hidden animate-pulse"
      >
        <div class="h-40 bg-slate-700" />
        <div class="p-4 space-y-2">
          <div class="h-4 bg-slate-700 rounded w-3/4" />
          <div class="h-3 bg-slate-700 rounded w-1/2" />
          <div class="h-3 bg-slate-700 rounded w-2/3" />
        </div>
      </div>
    </div>

    <!-- Error -->
    <div
      v-else-if="eventsStore.error"
      class="bg-red-950 border border-red-800 rounded-xl p-4 text-red-400 text-sm"
    >
      {{ eventsStore.error }}
    </div>

    <!-- Empty state -->
    <div
      v-else-if="eventsStore.events.length === 0"
      class="text-center py-16"
    >
      <div class="text-5xl mb-3">📅</div>
      <h3 class="text-lg font-semibold text-slate-300 mb-1">No upcoming events</h3>
      <p class="text-sm text-gray-500 mb-4">
        <template v-if="activeFilter === 'mine'">You haven't created any events yet.</template>
        <template v-else-if="activeFilter === 'friends'">No upcoming events from your friends.</template>
        <template v-else>No upcoming events to show. Create one!</template>
      </p>
      <button
        class="px-4 py-2 bg-indigo-600 text-white text-sm font-medium rounded-xl hover:bg-indigo-700 transition"
        @click="showCreateModal = true"
      >
        Create an Event
      </button>
    </div>

    <!-- Events grid -->
    <div v-else data-testid="events-list" class="grid grid-cols-1 sm:grid-cols-2 gap-4">
      <EventCard
        v-for="event in eventsStore.events"
        :key="event.id"
        :event="event"
      />
    </div>

    <!-- Load more -->
    <div v-if="eventsStore.hasMore && !eventsStore.loading" class="mt-6 text-center">
      <button
        class="px-6 py-2 text-sm font-medium text-indigo-400 border border-indigo-700 rounded-xl hover:bg-indigo-900/50 transition"
        :disabled="eventsStore.loadingMore"
        @click="eventsStore.loadMore()"
      >
        <span v-if="eventsStore.loadingMore">Loading…</span>
        <span v-else>Load more</span>
      </button>
    </div>
  </div>

  <!-- Create event modal -->
  <EventCreateModal
    v-if="showCreateModal"
    @close="showCreateModal = false"
    @created="showCreateModal = false"
  />
</template>
