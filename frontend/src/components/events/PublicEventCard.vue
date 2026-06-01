<script setup lang="ts">
import { RouterLink } from 'vue-router'
import type { EventDto } from '@/services/eventService'

/**
 * Display-only event card for unauthenticated visitors on the public front
 * page. The card body (cover + title) links to the public share/detail page
 * where guests can read the discussion and follow the Get tickets link.
 * RSVP still requires sign-in — the in-card RSVP button emits requireAuth
 * so the parent can prompt registration.
 */
const props = defineProps<{ event: EventDto }>()
const emit = defineEmits<{ requireAuth: [] }>()

function formatDate(iso: string) {
  return new Date(iso).toLocaleString(undefined, {
    weekday: 'short',
    year:    'numeric',
    month:   'short',
    day:     'numeric',
    hour:    '2-digit',
    minute:  '2-digit',
    hour12:  false,
  })
}

function formatShortDate(iso: string) {
  return new Date(iso).toLocaleDateString(undefined, {
    month: 'short',
    day:   'numeric',
  })
}

function formatShortTime(iso: string) {
  return new Date(iso).toLocaleTimeString(undefined, {
    hour:   '2-digit',
    minute: '2-digit',
    hour12: false,
  })
}

function handleAction() {
  emit('requireAuth')
}

// Bridge "unused props" lint while keeping the explicit destructure pattern.
void props
</script>

<template>
  <article data-testid="event-card" class="bg-slate-800 rounded-xl shadow-sm border border-slate-700 overflow-hidden hover:shadow-md transition-shadow">
    <!-- Cover (links to public detail page so guests can read the discussion) -->
    <RouterLink :to="`/share/events/${event.id}`" class="block">
      <div class="relative h-40 bg-gradient-to-br from-indigo-500 to-purple-600 overflow-hidden">
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
        <div class="absolute top-3 right-3 bg-black/50 text-white text-xs px-2 py-0.5 rounded-full">
          🌐 Public
        </div>
      </div>
    </RouterLink>

    <!-- Body -->
    <div class="p-4">
      <div class="flex items-start justify-between gap-2 mb-1">
        <RouterLink :to="`/share/events/${event.id}`" class="block group min-w-0 flex-1">
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
          @click.stop
        >
          <svg class="h-3 w-3" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
              d="M15 5v2m0 4v2m0 4v2M5 5a2 2 0 00-2 2v3a2 2 0 110 4v3a2 2 0 002 2h14a2 2 0 002-2v-3a2 2 0 110-4V7a2 2 0 00-2-2H5z"/>
          </svg>
          Get tickets
        </a>
      </div>

      <p class="text-xs text-gray-500 mb-2">
        by <span class="font-medium text-slate-300">{{ event.creator.displayName }}</span>
      </p>

      <div class="flex items-start gap-1.5 text-xs text-gray-500 mb-1">
        <svg class="h-3.5 w-3.5 mt-0.5 shrink-0 text-indigo-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M8 7V3m8 4V3m-9 8h10M5 21h14a2 2 0 002-2V7a2 2 0 00-2-2H5a2 2 0 00-2 2v12a2 2 0 002 2z"/>
        </svg>
        <span>
          {{ formatDate(event.startAt) }}
          <template v-if="event.endAt"> – {{ formatDate(event.endAt) }}</template>
        </span>
      </div>

      <div v-if="event.location" class="flex items-start gap-1.5 text-xs text-gray-500 mb-3">
        <svg class="h-3.5 w-3.5 mt-0.5 shrink-0 text-indigo-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M17.657 16.657L13.414 20.9a1.998 1.998 0 01-2.827 0l-4.244-4.243a8 8 0 1111.314 0z"/>
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 11a3 3 0 11-6 0 3 3 0 016 0z"/>
        </svg>
        <span>{{ event.location }}</span>
      </div>

      <p v-if="event.description" class="text-xs text-slate-400 line-clamp-2 mb-3">
        {{ event.description }}
      </p>

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

      <button
        type="button"
        class="w-full text-xs font-semibold text-indigo-300 bg-indigo-900/50 hover:bg-indigo-900 rounded-lg px-3 py-2 transition-colors"
        @click="handleAction"
      >
        Sign in to RSVP
      </button>
    </div>
  </article>
</template>
