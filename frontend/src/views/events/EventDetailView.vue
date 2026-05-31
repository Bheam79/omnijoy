<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRoute, useRouter, RouterLink } from 'vue-router'
import { eventService, type EventDto, type EventAttendeesResult, type RsvpStatus } from '@/services/eventService'
import { useAuthStore } from '@/stores/auth'

const route  = useRoute()
const router = useRouter()
const auth   = useAuthStore()

const event     = ref<EventDto | null>(null)
const attendees = ref<EventAttendeesResult | null>(null)
const loading   = ref(true)
const error     = ref<string | null>(null)
const rsvpLoading = ref(false)

const isOwn = computed(() => auth.user?.id === event.value?.creator.id)

// ── Edit state ────────────────────────────────────────────────────────────────
const showEditForm   = ref(false)
const editTitle      = ref('')
const editDesc       = ref('')
const editStartAt    = ref('')
const editEndAt      = ref('')
const editLocation   = ref('')
const editPrivacy    = ref<'Everyone' | 'FriendsOfFriends' | 'Friends' | 'OnlyMe'>('Everyone')
const editCoverFile  = ref<File | null>(null)
const editCoverPreview = ref<string | null>(null)
const editSaving     = ref(false)
const editError      = ref<string | null>(null)

function openEdit() {
  if (!event.value) return
  editTitle.value    = event.value.title
  editDesc.value     = event.value.description ?? ''
  // datetime-local input expects "YYYY-MM-DDTHH:mm"
  editStartAt.value  = event.value.startAt.slice(0, 16)
  editEndAt.value    = event.value.endAt ? event.value.endAt.slice(0, 16) : ''
  editLocation.value = event.value.location ?? ''
  editPrivacy.value  = event.value.privacy as typeof editPrivacy.value
  editCoverFile.value    = null
  editCoverPreview.value = null
  editError.value    = null
  showEditForm.value = true
}

function onEditCoverChange(e: Event) {
  const f = (e.target as HTMLInputElement).files?.[0]
  if (!f) return
  editCoverFile.value = f
  const reader = new FileReader()
  reader.onload = (ev) => { editCoverPreview.value = ev.target?.result as string }
  reader.readAsDataURL(f)
}

async function saveEdit() {
  if (!event.value) return
  editError.value = null
  if (!editTitle.value.trim()) { editError.value = 'Title is required.'; return }
  if (!editStartAt.value)      { editError.value = 'Start date/time is required.'; return }
  editSaving.value = true
  try {
    event.value = await eventService.updateEvent(event.value.id, {
      title:       editTitle.value.trim(),
      description: editDesc.value.trim() || undefined,
      startAt:     new Date(editStartAt.value).toISOString(),
      endAt:       editEndAt.value ? new Date(editEndAt.value).toISOString() : undefined,
      location:    editLocation.value.trim() || undefined,
      privacy:     editPrivacy.value,
      coverImage:  editCoverFile.value ?? undefined,
    })
    showEditForm.value = false
  } catch (e: unknown) {
    if (typeof e === 'object' && e !== null) {
      const axiosError = e as { response?: { data?: { error?: string } }; message?: string }
      editError.value = axiosError.response?.data?.error ?? axiosError.message ?? 'Something went wrong.'
    } else {
      editError.value = 'Something went wrong.'
    }
  } finally {
    editSaving.value = false
  }
}

const privacyLabel: Record<string, string> = {
  Everyone:          'Public',
  FriendsOfFriends:  'Friends of friends',
  Friends:           'Friends only',
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
    weekday: 'long',
    year: 'numeric',
    month: 'long',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  })
}

const isUpcoming = computed(() => event.value ? new Date(event.value.startAt) > new Date() : false)

const rsvpOptions: { label: string; value: RsvpStatus; icon: string; color: string; testid: string }[] = [
  { label: 'Going',     value: 'Going',    icon: '✅', color: 'green',  testid: 'rsvp-going' },
  { label: 'Maybe',     value: 'Maybe',    icon: '🤔', color: 'yellow', testid: 'rsvp-maybe' },
  { label: 'Not Going', value: 'NotGoing', icon: '❌', color: 'red',    testid: 'rsvp-not-going' },
]

async function fetchEvent() {
  loading.value = true
  error.value = null
  try {
    const id = route.params.id as string
    event.value = await eventService.getEvent(id)
    attendees.value = await eventService.getAttendees(id)
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

async function handleRsvp(status: RsvpStatus) {
  if (!event.value) return
  rsvpLoading.value = true
  try {
    event.value = await eventService.rsvp(event.value.id, status)
    attendees.value = await eventService.getAttendees(event.value.id)
  } catch (e: unknown) {
    console.error('RSVP failed', e)
  } finally {
    rsvpLoading.value = false
  }
}

async function handleDelete() {
  if (!event.value) return
  if (!confirm('Delete this event? This cannot be undone.')) return
  try {
    await eventService.deleteEvent(event.value.id)
    router.push({ name: 'events' })
  } catch (e: unknown) {
    console.error('Delete failed', e)
  }
}

onMounted(fetchEvent)
</script>

<template>
  <div class="max-w-2xl mx-auto px-4 py-6">
    <!-- Back link -->
    <RouterLink
      to="/events"
      class="inline-flex items-center gap-1.5 text-sm text-gray-500 hover:text-indigo-400 mb-4 transition"
    >
      <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 19l-7-7 7-7"/>
      </svg>
      Back to Events
    </RouterLink>

    <!-- Loading -->
    <div v-if="loading" class="space-y-4 animate-pulse">
      <div class="h-56 bg-slate-700 rounded-2xl" />
      <div class="h-7 bg-slate-700 rounded w-2/3" />
      <div class="h-4 bg-slate-700 rounded w-1/2" />
      <div class="h-4 bg-slate-700 rounded w-1/3" />
    </div>

    <!-- Error -->
    <div
      v-else-if="error"
      class="bg-red-950 border border-red-800 rounded-xl p-6 text-red-400 text-center"
    >
      <div class="text-3xl mb-2">😕</div>
      <p class="font-medium">{{ error }}</p>
      <RouterLink to="/events" class="text-sm text-indigo-400 hover:underline mt-2 inline-block">
        Back to Events
      </RouterLink>
    </div>

    <!-- Event detail -->
    <div v-else-if="event" class="space-y-5">
      <!-- Cover -->
      <div class="relative h-56 rounded-2xl overflow-hidden bg-gradient-to-br from-indigo-500 to-purple-600 shadow">
        <img
          v-if="event.coverImageUrl"
          :src="event.coverImageUrl"
          :alt="event.title"
          class="w-full h-full object-cover"
        />
        <!-- Privacy badge -->
        <div class="absolute top-4 right-4 bg-black/50 text-white text-xs px-2.5 py-1 rounded-full backdrop-blur-sm">
          {{ privacyIcon[event.privacy] }} {{ privacyLabel[event.privacy] }}
        </div>
      </div>

      <!-- Title & meta -->
      <div>
        <h1 data-testid="event-title" class="text-2xl font-bold text-slate-100 mb-1">{{ event.title }}</h1>

        <!-- Organizer: company page or personal creator -->
        <div class="flex items-center gap-2 text-sm text-gray-500 mb-3">
          <span>Organised by</span>
          <template v-if="event.companyPageId">
            <div class="flex items-center gap-1.5">
              <img
                v-if="event.companyPageLogoUrl"
                :src="event.companyPageLogoUrl"
                :alt="event.companyPageName"
                class="w-5 h-5 rounded object-cover"
              />
              <RouterLink
                :to="`/company/${event.companyPageId}`"
                class="font-semibold text-slate-200 hover:text-indigo-400 transition"
              >
                {{ event.companyPageName }}
              </RouterLink>
            </div>
          </template>
          <template v-else>
            <RouterLink
              :to="`/profile/${event.creator.id}`"
              class="font-semibold text-slate-200 hover:text-indigo-400 transition"
            >
              {{ event.creator.displayName }}
            </RouterLink>
          </template>
        </div>

        <!-- Date/time -->
        <div class="flex items-start gap-2 text-sm text-slate-300 mb-2">
          <svg class="h-4 w-4 mt-0.5 shrink-0 text-indigo-500" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M8 7V3m8 4V3m-9 8h10M5 21h14a2 2 0 002-2V7a2 2 0 00-2-2H5a2 2 0 00-2 2v12a2 2 0 002 2z"/>
          </svg>
          <div>
            <div>{{ formatDate(event.startAt) }}</div>
            <div v-if="event.endAt" class="text-gray-500 text-xs mt-0.5">
              Until {{ formatDate(event.endAt) }}
            </div>
          </div>
        </div>

        <!-- Location -->
        <div v-if="event.location" class="flex items-start gap-2 text-sm text-slate-300 mb-3">
          <svg class="h-4 w-4 mt-0.5 shrink-0 text-indigo-500" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M17.657 16.657L13.414 20.9a1.998 1.998 0 01-2.827 0l-4.244-4.243a8 8 0 1111.314 0z"/>
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 11a3 3 0 11-6 0 3 3 0 016 0z"/>
          </svg>
          <span>{{ event.location }}</span>
        </div>
      </div>

      <!-- RSVP section (upcoming events) -->
      <div v-if="isUpcoming" class="bg-indigo-900/50 rounded-xl p-4">
        <h2 class="text-sm font-semibold text-indigo-900 mb-3">Will you attend?</h2>
        <div class="flex gap-2 flex-wrap">
          <button
            v-for="opt in rsvpOptions"
            :key="opt.value"
            :data-testid="opt.testid"
            class="flex items-center gap-1.5 px-4 py-2 rounded-xl text-sm font-medium border-2 transition-all"
            :class="event.myRsvp === opt.value
              ? opt.value === 'Going'    ? 'bg-green-600 text-white border-green-600'
              : opt.value === 'Maybe'    ? 'bg-yellow-500 text-white border-yellow-500'
              :                           'bg-red-500 text-white border-red-500'
              : 'bg-slate-800 text-slate-300 border-slate-700 hover:border-indigo-300 hover:bg-indigo-900/50'"
            :disabled="rsvpLoading"
            @click="handleRsvp(opt.value)"
          >
            {{ opt.icon }} {{ opt.label }}
          </button>
        </div>
        <p v-if="event.myRsvp" class="text-xs text-indigo-300 mt-2">
          You responded: <strong>{{ event.myRsvp }}</strong>
        </p>
      </div>

      <!-- Description -->
      <div v-if="event.description" class="prose prose-sm max-w-none">
        <h2 class="text-base font-semibold text-slate-100 mb-2">About this event</h2>
        <p class="text-slate-300 whitespace-pre-wrap leading-relaxed">{{ event.description }}</p>
      </div>

      <!-- Attendees section -->
      <div v-if="attendees" class="bg-slate-800 border border-slate-700 rounded-xl p-4 shadow-sm">
        <h2 class="text-base font-semibold text-slate-100 mb-4">
          Attendees
          <span class="ml-2 text-sm font-normal text-gray-500">
            {{ event.goingCount }} going · {{ event.maybeCount }} maybe · {{ event.notGoingCount }} not going
          </span>
        </h2>

        <!-- Going -->
        <div v-if="attendees.going.length > 0" class="mb-4">
          <h3 class="text-xs font-semibold text-green-400 uppercase tracking-wide mb-2">Going ({{ attendees.going.length }})</h3>
          <div class="flex flex-wrap gap-2">
            <RouterLink
              v-for="a in attendees.going"
              :key="a.userId"
              :to="`/profile/${a.userId}`"
              class="flex items-center gap-1.5 text-xs text-slate-300 hover:text-indigo-300 transition"
            >
              <img
                v-if="a.avatarUrl"
                :src="a.avatarUrl"
                :alt="a.displayName"
                class="w-6 h-6 rounded-full object-cover"
              />
              <div
                v-else
                class="w-6 h-6 rounded-full bg-green-500 flex items-center justify-center text-white text-xs font-semibold"
              >
                {{ a.displayName.charAt(0).toUpperCase() }}
              </div>
              <span>{{ a.displayName }}</span>
            </RouterLink>
          </div>
        </div>

        <!-- Maybe -->
        <div v-if="attendees.maybe.length > 0" class="mb-4">
          <h3 class="text-xs font-semibold text-yellow-400 uppercase tracking-wide mb-2">Maybe ({{ attendees.maybe.length }})</h3>
          <div class="flex flex-wrap gap-2">
            <RouterLink
              v-for="a in attendees.maybe"
              :key="a.userId"
              :to="`/profile/${a.userId}`"
              class="flex items-center gap-1.5 text-xs text-slate-300 hover:text-indigo-300 transition"
            >
              <img
                v-if="a.avatarUrl"
                :src="a.avatarUrl"
                :alt="a.displayName"
                class="w-6 h-6 rounded-full object-cover"
              />
              <div
                v-else
                class="w-6 h-6 rounded-full bg-yellow-400 flex items-center justify-center text-white text-xs font-semibold"
              >
                {{ a.displayName.charAt(0).toUpperCase() }}
              </div>
              <span>{{ a.displayName }}</span>
            </RouterLink>
          </div>
        </div>

        <!-- Not Going -->
        <div v-if="attendees.notGoing.length > 0">
          <h3 class="text-xs font-semibold text-red-400 uppercase tracking-wide mb-2">Not Going ({{ attendees.notGoing.length }})</h3>
          <div class="flex flex-wrap gap-2">
            <RouterLink
              v-for="a in attendees.notGoing"
              :key="a.userId"
              :to="`/profile/${a.userId}`"
              class="flex items-center gap-1.5 text-xs text-slate-300 hover:text-indigo-300 transition"
            >
              <img
                v-if="a.avatarUrl"
                :src="a.avatarUrl"
                :alt="a.displayName"
                class="w-6 h-6 rounded-full object-cover"
              />
              <div
                v-else
                class="w-6 h-6 rounded-full bg-red-400 flex items-center justify-center text-white text-xs font-semibold"
              >
                {{ a.displayName.charAt(0).toUpperCase() }}
              </div>
              <span>{{ a.displayName }}</span>
            </RouterLink>
          </div>
        </div>

        <div
          v-if="attendees.going.length === 0 && attendees.maybe.length === 0 && attendees.notGoing.length === 0"
          class="text-sm text-gray-500 italic"
        >
          No RSVPs yet. Be the first!
        </div>
      </div>

      <!-- Owner actions -->
      <div v-if="isOwn" class="flex gap-3 pt-2">
        <button
          class="px-4 py-2 text-sm font-medium text-indigo-400 border border-indigo-700 rounded-xl hover:bg-indigo-900/50 transition"
          @click="openEdit"
        >
          Edit Event
        </button>
        <button
          class="px-4 py-2 text-sm font-medium text-red-400 border border-red-800 rounded-xl hover:bg-red-950 transition"
          @click="handleDelete"
        >
          Delete Event
        </button>
      </div>

      <!-- Edit form (inline, toggleable) -->
      <div v-if="showEditForm && isOwn" class="bg-slate-800 border border-slate-700 rounded-xl p-5 space-y-4">
        <div class="flex items-center justify-between">
          <h2 class="text-base font-semibold text-slate-100">Edit Event</h2>
          <button class="text-slate-500 hover:text-slate-300 p-1" @click="showEditForm = false">
            <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12"/>
            </svg>
          </button>
        </div>

        <!-- Cover image -->
        <div>
          <label class="block text-sm font-medium text-slate-300 mb-1">Cover Image</label>
          <div v-if="editCoverPreview" class="relative h-32 rounded-xl overflow-hidden">
            <img :src="editCoverPreview" class="w-full h-full object-cover"/>
            <button type="button" class="absolute top-2 right-2 bg-black/50 text-white rounded-full p-1"
              @click="editCoverFile = null; editCoverPreview = null">
              <svg class="w-3 h-3" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12"/>
              </svg>
            </button>
          </div>
          <label v-else class="flex flex-col items-center justify-center h-20 border-2 border-dashed border-slate-600 rounded-xl cursor-pointer hover:border-indigo-400 transition">
            <span class="text-xs text-gray-500">Click to change cover photo</span>
            <input type="file" class="hidden" accept="image/*" @change="onEditCoverChange"/>
          </label>
        </div>

        <!-- Title -->
        <div>
          <label class="block text-sm font-medium text-slate-300 mb-1">Title <span class="text-red-500">*</span></label>
          <input v-model="editTitle" type="text" maxlength="256"
            class="w-full rounded-lg border border-slate-600 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500"
          />
        </div>

        <!-- Description -->
        <div>
          <label class="block text-sm font-medium text-slate-300 mb-1">Description</label>
          <textarea v-model="editDesc" rows="3"
            class="w-full rounded-lg border border-slate-600 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500 resize-none"
          />
        </div>

        <!-- Start / End -->
        <div class="grid grid-cols-2 gap-3">
          <div>
            <label class="block text-sm font-medium text-slate-300 mb-1">Starts <span class="text-red-500">*</span></label>
            <input v-model="editStartAt" type="datetime-local"
              class="w-full rounded-lg border border-slate-600 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500"
            />
          </div>
          <div>
            <label class="block text-sm font-medium text-slate-300 mb-1">Ends</label>
            <input v-model="editEndAt" type="datetime-local" :min="editStartAt"
              class="w-full rounded-lg border border-slate-600 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500"
            />
          </div>
        </div>

        <!-- Location -->
        <div>
          <label class="block text-sm font-medium text-slate-300 mb-1">Location</label>
          <input v-model="editLocation" type="text" maxlength="512"
            class="w-full rounded-lg border border-slate-600 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500"
          />
        </div>

        <!-- Privacy -->
        <div>
          <label class="block text-sm font-medium text-slate-300 mb-1">Privacy</label>
          <select v-model="editPrivacy"
            class="w-full rounded-lg border border-slate-600 px-3 py-2 text-sm bg-slate-800 focus:outline-none focus:ring-2 focus:ring-indigo-500"
          >
            <option value="Everyone">🌐 Public</option>
            <option value="FriendsOfFriends">👥 Friends of friends</option>
            <option value="Friends">👥 Friends only</option>
            <option value="OnlyMe">🔒 Only me</option>
          </select>
        </div>

        <!-- Error -->
        <div v-if="editError" class="text-sm text-red-400 bg-red-950 rounded-lg px-3 py-2">{{ editError }}</div>

        <!-- Actions -->
        <div class="flex justify-end gap-3">
          <button type="button"
            class="px-4 py-2 text-sm font-medium text-slate-300 border border-slate-600 rounded-lg hover:bg-slate-700 transition"
            @click="showEditForm = false">Cancel</button>
          <button type="button" :disabled="editSaving"
            class="px-4 py-2 text-sm font-medium text-white bg-indigo-600 rounded-lg hover:bg-indigo-700 disabled:opacity-50 transition"
            @click="saveEdit">
            <span v-if="editSaving">Saving…</span>
            <span v-else>Save Changes</span>
          </button>
        </div>
      </div>
    </div>
  </div>
</template>
