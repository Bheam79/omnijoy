<script setup lang="ts">
import { computed } from 'vue'
import { RouterLink } from 'vue-router'
import type { EventDto, RsvpStatus } from '@/services/eventService'
import { eventService } from '@/services/eventService'
import { useEventsStore } from '@/stores/events'

const props = defineProps<{ event: EventDto }>()

const eventsStore = useEventsStore()

const privacyLabel: Record<string, string> = {
  Everyone:          'Public',
  FriendsOfFriends:  'Friends of friends',
  Friends:           'Friends',
  OnlyMe:            'Only me',
}

const privacyIcon: Record<string, string> = {
  Everyone:         '🌐',
  FriendsOfFriends: '👥',
  Friends:          '👥',
  OnlyMe:           '🔒',
}

function formatDate(iso: string) {
  return new Date(iso).toLocaleString(undefined, {
    weekday: 'short',
    year: 'numeric',
    month: 'short',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
    hour12: false,
  })
}

function formatShortDate(iso: string) {
  return new Date(iso).toLocaleDateString(undefined, {
    month: 'short',
    day: 'numeric',
  })
}

function formatShortTime(iso: string) {
  return new Date(iso).toLocaleTimeString(undefined, {
    hour: '2-digit',
    minute: '2-digit',
    hour12: false,
  })
}

const isUpcoming = computed(() => new Date(props.event.startAt) > new Date())

const rsvpOptions: { label: string; value: RsvpStatus; icon: string }[] = [
  { label: 'Going',     value: 'Going',     icon: '✅' },
  { label: 'Maybe',     value: 'Maybe',     icon: '🤔' },
  { label: 'Not going', value: 'NotGoing',  icon: '❌' },
]

async function handleRsvp(status: RsvpStatus) {
  const updated = await eventService.rsvp(props.event.id, status)
  eventsStore.updateEventInList(updated)
}
</script>

<template>
  <article data-testid="event-card" class="bg-slate-800 rounded-xl shadow-sm border border-slate-700 overflow-hidden hover:shadow-md transition-shadow">
    <!-- Cover image -->
    <RouterLink :to="`/events/${event.id}`" class="block">
      <div
        class="relative h-40 bg-gradient-to-br from-indigo-500 to-purple-600 overflow-hidden"
      >
        <img
          v-if="event.coverImageUrl"
          :src="event.coverImageUrl"
          :alt="event.title"
          class="w-full h-full object-cover"
        />
        <!-- Date overlay -->
        <div class="absolute top-3 left-3 bg-slate-800/90 backdrop-blur-sm rounded-lg px-2.5 py-1.5 text-center shadow">
          <div class="text-xs font-semibold text-indigo-400 uppercase leading-none">
            {{ formatShortDate(event.startAt) }}
          </div>
          <div class="text-xs text-gray-500 mt-0.5">{{ formatShortTime(event.startAt) }}</div>
        </div>
        <!-- Privacy badge -->
        <div class="absolute top-3 right-3 bg-black/50 text-white text-xs px-2 py-0.5 rounded-full">
          {{ privacyIcon[event.privacy] }} {{ privacyLabel[event.privacy] }}
        </div>
      </div>
    </RouterLink>

    <!-- Body -->
    <div class="p-4">
      <!-- Title + creator -->
      <div class="flex items-start justify-between gap-2 mb-1">
        <RouterLink :to="`/events/${event.id}`" class="block group min-w-0 flex-1">
          <h3 class="font-semibold text-slate-100 text-base group-hover:text-indigo-300 transition-colors leading-snug">
            {{ event.title }}
          </h3>
        </RouterLink>
        <a
          v-if="event.ticketUrl"
          :href="event.ticketUrl"
          target="_blank"
          rel="noopener noreferrer"
          data-testid="event-card-buy-tickets"
          class="shrink-0 inline-flex items-center gap-1 px-2.5 py-1 rounded-md text-xs font-semibold text-white bg-emerald-600 hover:bg-emerald-500 shadow transition"
        >
          <svg class="h-3 w-3" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
              d="M15 5v2m0 4v2m0 4v2M5 5a2 2 0 00-2 2v3a2 2 0 110 4v3a2 2 0 002 2h14a2 2 0 002-2v-3a2 2 0 110-4V7a2 2 0 00-2-2H5z"/>
          </svg>
          Get tickets
        </a>
      </div>

      <!-- Organizer: company page or personal creator -->
      <div class="flex items-center gap-1.5 text-xs text-gray-500 mb-2">
        <template v-if="event.companyPageId">
          <img
            v-if="event.companyPageLogoUrl"
            :src="event.companyPageLogoUrl"
            :alt="event.companyPageName"
            class="w-4 h-4 rounded object-cover"
          />
          <RouterLink :to="`/company/${event.companyPageId}`" class="hover:underline font-medium text-slate-300">
            {{ event.companyPageName }}
          </RouterLink>
        </template>
        <template v-else>
          <RouterLink :to="`/profile/${event.creator.id}`" class="hover:underline font-medium text-slate-300">
            {{ event.creator.displayName }}
          </RouterLink>
        </template>
      </div>

      <!-- Date/time details -->
      <div class="flex items-start gap-1.5 text-xs text-gray-500 mb-1">
        <svg class="h-3.5 w-3.5 mt-0.5 shrink-0 text-indigo-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M8 7V3m8 4V3m-9 8h10M5 21h14a2 2 0 002-2V7a2 2 0 00-2-2H5a2 2 0 00-2 2v12a2 2 0 002 2z"/>
        </svg>
        <span>
          {{ formatDate(event.startAt) }}
          <template v-if="event.endAt"> – {{ formatDate(event.endAt) }}</template>
        </span>
      </div>

      <!-- Location -->
      <div v-if="event.location" class="flex items-start gap-1.5 text-xs text-gray-500 mb-3">
        <svg class="h-3.5 w-3.5 mt-0.5 shrink-0 text-indigo-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M17.657 16.657L13.414 20.9a1.998 1.998 0 01-2.827 0l-4.244-4.243a8 8 0 1111.314 0z"/>
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 11a3 3 0 11-6 0 3 3 0 016 0z"/>
        </svg>
        <span>{{ event.location }}</span>
      </div>

      <!-- Attendee counts -->
      <div class="flex items-center gap-3 text-xs text-gray-500 mb-3">
        <span v-if="event.goingCount > 0">
          <span class="font-semibold text-green-400">{{ event.goingCount }}</span> going
        </span>
        <span v-if="event.maybeCount > 0">
          <span class="font-semibold text-yellow-400">{{ event.maybeCount }}</span> maybe
        </span>
        <span v-if="event.goingCount === 0 && event.maybeCount === 0" class="italic">
          No RSVPs yet
        </span>
      </div>

      <!-- RSVP buttons (only for upcoming events) -->
      <div v-if="isUpcoming" class="flex gap-1.5 flex-wrap">
        <button
          v-for="opt in rsvpOptions"
          :key="opt.value"
          class="flex items-center gap-1 px-2.5 py-1 rounded-full text-xs font-medium border transition-all"
          :class="event.myRsvp === opt.value
            ? 'bg-indigo-600 text-white border-indigo-600 shadow-sm'
            : 'bg-slate-800 text-slate-400 border-slate-700 hover:border-indigo-300 hover:text-indigo-400'"
          @click="handleRsvp(opt.value)"
        >
          <span>{{ opt.icon }}</span>
          <span>{{ opt.label }}</span>
        </button>
      </div>
    </div>
  </article>
</template>
