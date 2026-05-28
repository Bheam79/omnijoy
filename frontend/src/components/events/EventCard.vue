<script setup lang="ts">
import { computed } from 'vue'
import { RouterLink } from 'vue-router'
import type { EventDto, RsvpStatus } from '@/services/eventService'
import { useAuthStore } from '@/stores/auth'
import { eventService } from '@/services/eventService'
import { useEventsStore } from '@/stores/events'

const props = defineProps<{ event: EventDto }>()
const emit = defineEmits<{ deleted: [id: string] }>()

const auth = useAuthStore()
const eventsStore = useEventsStore()

const isOwn = computed(() => auth.user?.id === props.event.creator.id)

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

async function handleDelete() {
  if (!confirm('Delete this event?')) return
  await eventsStore.deleteEvent(props.event.id)
  emit('deleted', props.event.id)
}
</script>

<template>
  <article class="bg-white rounded-xl shadow-sm border border-gray-100 overflow-hidden hover:shadow-md transition-shadow">
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
        <div class="absolute top-3 left-3 bg-white/90 backdrop-blur-sm rounded-lg px-2.5 py-1.5 text-center shadow">
          <div class="text-xs font-semibold text-indigo-600 uppercase leading-none">
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
      <RouterLink :to="`/events/${event.id}`" class="block group">
        <h3 class="font-semibold text-gray-900 text-base group-hover:text-indigo-700 transition-colors leading-snug mb-1">
          {{ event.title }}
        </h3>
      </RouterLink>

      <!-- Creator -->
      <div class="flex items-center gap-1.5 text-xs text-gray-500 mb-2">
        <RouterLink :to="`/profile/${event.creator.id}`" class="hover:underline font-medium text-gray-700">
          {{ event.creator.displayName }}
        </RouterLink>
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
          <span class="font-semibold text-green-600">{{ event.goingCount }}</span> going
        </span>
        <span v-if="event.maybeCount > 0">
          <span class="font-semibold text-yellow-600">{{ event.maybeCount }}</span> maybe
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
            : 'bg-white text-gray-600 border-gray-200 hover:border-indigo-300 hover:text-indigo-600'"
          @click="handleRsvp(opt.value)"
        >
          <span>{{ opt.icon }}</span>
          <span>{{ opt.label }}</span>
        </button>
      </div>

      <!-- Owner actions -->
      <div v-if="isOwn" class="mt-3 flex gap-2 border-t border-gray-100 pt-3">
        <RouterLink
          :to="`/events/${event.id}`"
          class="text-xs text-indigo-600 hover:text-indigo-800 font-medium"
        >
          Manage
        </RouterLink>
        <button
          class="text-xs text-red-500 hover:text-red-700 font-medium"
          @click="handleDelete"
        >
          Delete
        </button>
      </div>
    </div>
  </article>
</template>
