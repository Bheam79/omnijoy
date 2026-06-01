<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRoute, RouterLink } from 'vue-router'
import { eventService, type EventDto, type EventAttendeesResult } from '@/services/eventService'
import { useCompanyModeStore } from '@/stores/companyMode'

const route       = useRoute()
const companyMode = useCompanyModeStore()

const event     = ref<EventDto | null>(null)
const attendees = ref<EventAttendeesResult | null>(null)
const loading   = ref(true)
const error     = ref<string | null>(null)

// ── Invite modal ───────────────────────────────────────────────────────────────
const showInviteModal = ref(false)
const copiedLink      = ref(false)

function copyEventLink() {
  const url = `${window.location.origin}/events/${route.params.id}`
  navigator.clipboard.writeText(url).then(() => {
    copiedLink.value = true
    setTimeout(() => { copiedLink.value = false }, 2000)
  })
}

// Total attendee count helpers
const totalGoing    = computed(() => attendees.value?.going.length ?? 0)
const totalMaybe    = computed(() => attendees.value?.maybe.length ?? 0)
const totalNotGoing = computed(() => attendees.value?.notGoing.length ?? 0)
const totalAttendees = computed(() => totalGoing.value + totalMaybe.value + totalNotGoing.value)

async function fetchData() {
  loading.value = true
  error.value   = null
  try {
    const id = route.params.id as string
    ;[event.value, attendees.value] = await Promise.all([
      eventService.getEvent(id),
      eventService.getAttendees(id),
    ])
    companyMode.setActiveEvent(event.value)
  } catch (e: unknown) {
    if (typeof e === 'object' && e !== null) {
      const axiosError = e as { response?: { status?: number; data?: { error?: string } }; message?: string }
      if (axiosError.response?.status === 404) {
        error.value = 'Event not found.'
      } else if (axiosError.response?.status === 403) {
        error.value = 'You do not have permission to view this event.'
      } else {
        error.value = axiosError.response?.data?.error ?? axiosError.message ?? 'Failed to load event.'
      }
    } else {
      error.value = 'Failed to load event.'
    }
  } finally {
    loading.value = false
  }
}

onMounted(fetchData)
</script>

<template>
  <div class="max-w-2xl mx-auto px-4 py-6">
    <!-- Header row -->
    <div class="flex items-center justify-between mb-6">
      <RouterLink
        :to="`/events/${route.params.id}`"
        class="inline-flex items-center gap-1.5 text-sm text-gray-500 hover:text-indigo-400 transition"
      >
        <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 19l-7-7 7-7"/>
        </svg>
        Back to Event
      </RouterLink>

      <!-- + Invite button -->
      <button
        v-if="!loading && !error"
        class="flex items-center gap-2 px-4 py-2 bg-indigo-600 hover:bg-indigo-700 text-white text-sm font-medium rounded-xl transition shadow-sm"
        @click="showInviteModal = true"
      >
        <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
            d="M18 9v3m0 0v3m0-3h3m-3 0h-3m-2-5a4 4 0 11-8 0 4 4 0 018 0zM3 20a6 6 0 0112 0v1H3v-1z"/>
        </svg>
        + Invite
      </button>
    </div>

    <!-- Loading -->
    <div v-if="loading" class="space-y-4 animate-pulse">
      <div class="h-8 bg-slate-700 rounded w-1/2" />
      <div class="h-40 bg-slate-700 rounded-xl" />
    </div>

    <!-- Error -->
    <div
      v-else-if="error"
      class="bg-red-950 border border-red-800 rounded-xl p-6 text-red-400 text-center"
    >
      <div class="text-3xl mb-2">😕</div>
      <p class="font-medium">{{ error }}</p>
    </div>

    <!-- Participants -->
    <template v-else-if="event && attendees">
      <h1 class="text-xl font-bold text-slate-100 mb-1">Participants</h1>
      <p class="text-sm text-gray-500 mb-6">
        {{ event.title }} ·
        {{ totalAttendees }} {{ totalAttendees === 1 ? 'response' : 'responses' }}
      </p>

      <!-- Empty state -->
      <div
        v-if="totalAttendees === 0"
        class="bg-slate-800 border border-slate-700 rounded-xl p-10 text-center"
      >
        <div class="text-4xl mb-3">🎟️</div>
        <p class="text-slate-400 font-medium">No RSVPs yet</p>
        <p class="text-gray-500 text-sm mt-1">Share the event link to invite people.</p>
      </div>

      <!-- Going -->
      <section v-if="attendees.going.length > 0" class="mb-6">
        <div class="flex items-center gap-2 mb-3">
          <span class="text-xs font-bold text-green-400 uppercase tracking-widest">
            Going
          </span>
          <span class="px-2 py-0.5 rounded-full bg-green-900/40 text-green-400 text-xs font-semibold">
            {{ attendees.going.length }}
          </span>
        </div>
        <div class="bg-slate-800 border border-slate-700 rounded-xl overflow-hidden divide-y divide-slate-700/50">
          <RouterLink
            v-for="a in attendees.going"
            :key="a.userId"
            :to="`/profile/${a.userId}`"
            class="flex items-center gap-3 px-4 py-3 hover:bg-slate-700/40 transition"
          >
            <img
              v-if="a.avatarUrl"
              :src="a.avatarUrl"
              :alt="a.displayName"
              class="w-9 h-9 rounded-full object-cover shrink-0"
            />
            <div
              v-else
              class="w-9 h-9 rounded-full bg-green-600 flex items-center justify-center text-white text-sm font-semibold shrink-0"
            >
              {{ a.displayName.charAt(0).toUpperCase() }}
            </div>
            <span class="flex-1 text-sm font-medium text-slate-200">{{ a.displayName }}</span>
            <span class="text-green-400 text-xs font-medium">✅ Going</span>
          </RouterLink>
        </div>
      </section>

      <!-- Maybe / Interested -->
      <section v-if="attendees.maybe.length > 0" class="mb-6">
        <div class="flex items-center gap-2 mb-3">
          <span class="text-xs font-bold text-yellow-400 uppercase tracking-widest">
            Interested
          </span>
          <span class="px-2 py-0.5 rounded-full bg-yellow-900/40 text-yellow-400 text-xs font-semibold">
            {{ attendees.maybe.length }}
          </span>
        </div>
        <div class="bg-slate-800 border border-slate-700 rounded-xl overflow-hidden divide-y divide-slate-700/50">
          <RouterLink
            v-for="a in attendees.maybe"
            :key="a.userId"
            :to="`/profile/${a.userId}`"
            class="flex items-center gap-3 px-4 py-3 hover:bg-slate-700/40 transition"
          >
            <img
              v-if="a.avatarUrl"
              :src="a.avatarUrl"
              :alt="a.displayName"
              class="w-9 h-9 rounded-full object-cover shrink-0"
            />
            <div
              v-else
              class="w-9 h-9 rounded-full bg-yellow-500 flex items-center justify-center text-white text-sm font-semibold shrink-0"
            >
              {{ a.displayName.charAt(0).toUpperCase() }}
            </div>
            <span class="flex-1 text-sm font-medium text-slate-200">{{ a.displayName }}</span>
            <span class="text-yellow-400 text-xs font-medium">🤔 Interested</span>
          </RouterLink>
        </div>
      </section>

      <!-- Not Going -->
      <section v-if="attendees.notGoing.length > 0" class="mb-6">
        <div class="flex items-center gap-2 mb-3">
          <span class="text-xs font-bold text-red-400 uppercase tracking-widest">
            Not Going
          </span>
          <span class="px-2 py-0.5 rounded-full bg-red-900/40 text-red-400 text-xs font-semibold">
            {{ attendees.notGoing.length }}
          </span>
        </div>
        <div class="bg-slate-800 border border-slate-700 rounded-xl overflow-hidden divide-y divide-slate-700/50">
          <RouterLink
            v-for="a in attendees.notGoing"
            :key="a.userId"
            :to="`/profile/${a.userId}`"
            class="flex items-center gap-3 px-4 py-3 hover:bg-slate-700/40 transition"
          >
            <img
              v-if="a.avatarUrl"
              :src="a.avatarUrl"
              :alt="a.displayName"
              class="w-9 h-9 rounded-full object-cover shrink-0"
            />
            <div
              v-else
              class="w-9 h-9 rounded-full bg-red-500 flex items-center justify-center text-white text-sm font-semibold shrink-0"
            >
              {{ a.displayName.charAt(0).toUpperCase() }}
            </div>
            <span class="flex-1 text-sm font-medium text-slate-200">{{ a.displayName }}</span>
            <span class="text-red-400 text-xs font-medium">❌ Not Going</span>
          </RouterLink>
        </div>
      </section>
    </template>
  </div>

  <!-- Invite modal -->
  <Teleport to="body">
    <Transition name="modal">
      <div
        v-if="showInviteModal"
        class="fixed inset-0 z-50 flex items-center justify-center bg-black/60 backdrop-blur-sm p-4"
        @click.self="showInviteModal = false"
      >
        <div class="bg-slate-800 border border-slate-700 rounded-2xl shadow-2xl w-full max-w-sm">
          <!-- Header -->
          <div class="flex items-center justify-between px-5 py-4 border-b border-slate-700">
            <h2 class="text-base font-semibold text-slate-100">Invite People</h2>
            <button
              class="text-slate-500 hover:text-slate-300 p-1 transition"
              @click="showInviteModal = false"
            >
              <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12"/>
              </svg>
            </button>
          </div>

          <div class="px-5 py-4 space-y-3">
            <p class="text-sm text-slate-300">
              Share this link to invite people to <strong>{{ event?.title }}</strong>:
            </p>

            <!-- Copy link button -->
            <button
              class="w-full flex items-center justify-center gap-2 px-4 py-2.5 rounded-xl border border-indigo-700 text-sm text-indigo-300 hover:bg-indigo-900/40 transition"
              @click="copyEventLink"
            >
              <svg v-if="!copiedLink" class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                  d="M8 16H6a2 2 0 01-2-2V6a2 2 0 012-2h8a2 2 0 012 2v2m-6 12h8a2 2 0 002-2v-8a2 2 0 00-2-2h-8a2 2 0 00-2 2v8a2 2 0 002 2z"/>
              </svg>
              <svg v-else class="w-4 h-4 text-green-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M5 13l4 4L19 7"/>
              </svg>
              <span>{{ copiedLink ? '✓ Link copied!' : 'Copy event link' }}</span>
            </button>

            <p class="text-xs text-gray-500 text-center">
              Anyone with the link can view the event and RSVP.
            </p>
          </div>
        </div>
      </div>
    </Transition>
  </Teleport>
</template>

<style scoped>
.modal-enter-active,
.modal-leave-active {
  transition: opacity 150ms ease;
}
.modal-enter-from,
.modal-leave-to {
  opacity: 0;
}
</style>
