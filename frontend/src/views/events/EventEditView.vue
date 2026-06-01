<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useRoute, useRouter, RouterLink } from 'vue-router'
import { eventService, type EventDto } from '@/services/eventService'
import { useCompanyModeStore } from '@/stores/companyMode'
import PlacePicker from '@/components/shared/PlacePicker.vue'
import type { PlaceSelection } from '@/types/places'

const route       = useRoute()
const router      = useRouter()
const companyMode = useCompanyModeStore()

const event   = ref<EventDto | null>(null)
const loading = ref(true)
const error   = ref<string | null>(null)

// ── Edit form state ────────────────────────────────────────────────────────────
const editTitle             = ref('')
const editDesc              = ref('')
const editStartAt           = ref('')
const editEndAt             = ref('')
const editLocationSelection = ref<PlaceSelection | null>(null)
const editPrivacy           = ref<'Everyone' | 'FriendsOfFriends' | 'Friends' | 'OnlyMe'>('Everyone')
const editPostingPolicy     = ref<'OrganizerOnly' | 'Everyone'>('OrganizerOnly')
const editTicketUrl         = ref('')
const editCoverFile         = ref<File | null>(null)
const editCoverPreview      = ref<string | null>(null)
const editSaving            = ref(false)
const editError             = ref<string | null>(null)

function toDatetimeLocalValue(isoUtc: string): string {
  const d = new Date(isoUtc)
  return new Date(d.getTime() - d.getTimezoneOffset() * 60000).toISOString().slice(0, 16)
}

function populateForm(ev: EventDto) {
  editTitle.value         = ev.title
  editDesc.value          = ev.description ?? ''
  editStartAt.value       = toDatetimeLocalValue(ev.startAt)
  editEndAt.value         = ev.endAt ? toDatetimeLocalValue(ev.endAt) : ''
  // Populate PlacePicker from structured fields if available, or fall back to legacy text
  if (ev.location) {
    editLocationSelection.value = {
      placeId:          ev.locationPlaceId ?? 'manual',
      displayName:      ev.location,
      city:             ev.locationCity    ?? null,
      country:          ev.locationCountry ?? null,
      countryCode:      null,
      latitude:         ev.locationLatitude  ?? null,
      longitude:        ev.locationLongitude ?? null,
      formattedAddress: null,
    }
  } else {
    editLocationSelection.value = null
  }
  editPrivacy.value       = ev.privacy as typeof editPrivacy.value
  editPostingPolicy.value = (ev.postingPolicy ?? 'OrganizerOnly') as typeof editPostingPolicy.value
  editTicketUrl.value     = ev.ticketUrl ?? ''
  editCoverFile.value     = null
  editCoverPreview.value  = null
  editError.value         = null
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
  const trimmedTicket = editTicketUrl.value.trim()
  if (trimmedTicket && !/^https?:\/\//i.test(trimmedTicket)) {
    editError.value = 'Ticket link must start with http:// or https://.'
    return
  }
  editSaving.value = true
  try {
    const updated = await eventService.updateEvent(event.value.id, {
      title:               editTitle.value.trim(),
      description:         editDesc.value.trim() || undefined,
      startAt:             new Date(editStartAt.value).toISOString(),
      endAt:               editEndAt.value ? new Date(editEndAt.value).toISOString() : undefined,
      location:            editLocationSelection.value?.displayName || undefined,
      locationPlaceId:     editLocationSelection.value?.placeId !== 'manual' ? (editLocationSelection.value?.placeId ?? undefined) : undefined,
      locationCity:        editLocationSelection.value?.city ?? undefined,
      locationCountry:     editLocationSelection.value?.country ?? undefined,
      locationLatitude:    editLocationSelection.value?.latitude ?? undefined,
      locationLongitude:   editLocationSelection.value?.longitude ?? undefined,
      privacy:             editPrivacy.value,
      postingPolicy:       editPostingPolicy.value,
      ticketUrl:           trimmedTicket,
      coverImage:          editCoverFile.value ?? undefined,
    })
    event.value = updated
    companyMode.setActiveEvent(updated)
    // Navigate back to event detail after save
    router.push({ path: `/events/${event.value.id}` })
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

async function fetchEvent() {
  loading.value = true
  error.value   = null
  try {
    const id = route.params.id as string
    event.value = await eventService.getEvent(id)
    companyMode.setActiveEvent(event.value)
    populateForm(event.value)
  } catch (e: unknown) {
    if (typeof e === 'object' && e !== null) {
      const axiosError = e as { response?: { status?: number; data?: { error?: string } }; message?: string }
      if (axiosError.response?.status === 404) {
        error.value = 'Event not found.'
      } else if (axiosError.response?.status === 403) {
        error.value = 'You do not have permission to edit this event.'
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

onMounted(fetchEvent)
</script>

<template>
  <div class="max-w-3xl mx-auto px-4 py-6">
    <!-- Back link -->
    <RouterLink
      :to="`/events/${route.params.id}`"
      class="inline-flex items-center gap-1.5 text-sm text-gray-500 hover:text-indigo-400 mb-6 transition"
    >
      <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 19l-7-7 7-7"/>
      </svg>
      Back to Event
    </RouterLink>

    <!-- Loading -->
    <div v-if="loading" class="space-y-4 animate-pulse">
      <div class="h-8 bg-slate-700 rounded w-1/3" />
      <div class="h-32 bg-slate-700 rounded-xl" />
      <div class="h-10 bg-slate-700 rounded-xl" />
      <div class="h-10 bg-slate-700 rounded-xl" />
    </div>

    <!-- Error -->
    <div
      v-else-if="error"
      class="bg-red-950 border border-red-800 rounded-xl p-6 text-red-400 text-center"
    >
      <div class="text-3xl mb-2">😕</div>
      <p class="font-medium">{{ error }}</p>
    </div>

    <!-- Edit form -->
    <div v-else-if="event" class="bg-slate-800 border border-slate-700 rounded-xl p-5 space-y-4">
      <h1 class="text-xl font-bold text-slate-100">Edit Event</h1>

      <!-- Cover image -->
      <div>
        <label class="block text-sm font-medium text-slate-300 mb-1">Cover Image</label>
        <div v-if="editCoverPreview" class="relative h-40 rounded-xl overflow-hidden">
          <img :src="editCoverPreview" class="w-full h-full object-cover"/>
          <button
            type="button"
            class="absolute top-2 right-2 bg-black/50 text-white rounded-full p-1.5"
            @click="editCoverFile = null; editCoverPreview = null"
          >
            <svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12"/>
            </svg>
          </button>
        </div>
        <div v-else class="relative h-40 rounded-xl overflow-hidden bg-slate-700">
          <img
            v-if="event.coverImageUrl"
            :src="event.coverImageUrl"
            class="w-full h-full object-cover opacity-60"
          />
          <label class="absolute inset-0 flex items-center justify-center cursor-pointer hover:bg-black/10 transition">
            <span class="text-xs text-slate-300 bg-slate-800/80 px-3 py-1.5 rounded-full">
              Click to change cover photo
            </span>
            <input type="file" class="hidden" accept="image/*" @change="onEditCoverChange"/>
          </label>
        </div>
      </div>

      <!-- Title -->
      <div>
        <label class="block text-sm font-medium text-slate-300 mb-1">
          Title <span class="text-red-500">*</span>
        </label>
        <input
          v-model="editTitle"
          type="text"
          maxlength="256"
          placeholder="Event title"
          class="w-full rounded-lg border border-slate-600 bg-slate-900 px-3 py-2 text-sm text-slate-200 placeholder-gray-600 focus:outline-none focus:ring-2 focus:ring-indigo-500"
        />
      </div>

      <!-- Description -->
      <div>
        <label class="block text-sm font-medium text-slate-300 mb-1">Description</label>
        <textarea
          v-model="editDesc"
          rows="4"
          placeholder="Describe the event…"
          class="w-full rounded-lg border border-slate-600 bg-slate-900 px-3 py-2 text-sm text-slate-200 placeholder-gray-600 focus:outline-none focus:ring-2 focus:ring-indigo-500 resize-none"
        />
      </div>

      <!-- Start / End -->
      <div class="grid grid-cols-2 gap-3">
        <div>
          <label class="block text-sm font-medium text-slate-300 mb-1">
            Starts <span class="text-red-500">*</span>
          </label>
          <input
            v-model="editStartAt"
            type="datetime-local"
            class="w-full rounded-lg border border-slate-600 bg-slate-900 px-3 py-2 text-sm text-slate-200 focus:outline-none focus:ring-2 focus:ring-indigo-500"
          />
        </div>
        <div>
          <label class="block text-sm font-medium text-slate-300 mb-1">Ends</label>
          <input
            v-model="editEndAt"
            type="datetime-local"
            :min="editStartAt"
            class="w-full rounded-lg border border-slate-600 bg-slate-900 px-3 py-2 text-sm text-slate-200 focus:outline-none focus:ring-2 focus:ring-indigo-500"
          />
        </div>
      </div>

      <!-- Location -->
      <PlacePicker
        v-model="editLocationSelection"
        mode="address"
        label="Location"
        placeholder="Where is this event?"
      />

      <!-- Ticket URL -->
      <div>
        <label class="block text-sm font-medium text-slate-300 mb-1">Ticket link</label>
        <input
          v-model="editTicketUrl"
          type="url"
          maxlength="2048"
          data-testid="event-edit-ticket-url-input"
          placeholder="https://tickets.example.com/your-event"
          class="w-full rounded-lg border border-slate-600 bg-slate-900 px-3 py-2 text-sm text-slate-200 placeholder-gray-600 focus:outline-none focus:ring-2 focus:ring-indigo-500"
        />
        <p class="text-xs text-gray-500 mt-1">Optional. Leave empty to remove the existing link.</p>
      </div>

      <!-- Privacy -->
      <div>
        <label class="block text-sm font-medium text-slate-300 mb-1">Privacy</label>
        <select
          v-model="editPrivacy"
          class="w-full rounded-lg border border-slate-600 bg-slate-800 px-3 py-2 text-sm text-slate-200 focus:outline-none focus:ring-2 focus:ring-indigo-500"
        >
          <option value="Everyone">🌐 Public</option>
          <option value="FriendsOfFriends">👥 Friends of friends</option>
          <option value="Friends">👥 Friends only</option>
          <option value="OnlyMe">🔒 Only me</option>
        </select>
      </div>

      <!-- Posting policy -->
      <div>
        <label class="block text-sm font-medium text-slate-300 mb-1">Who can post on this event?</label>
        <select
          v-model="editPostingPolicy"
          class="w-full rounded-lg border border-slate-600 bg-slate-800 px-3 py-2 text-sm text-slate-200 focus:outline-none focus:ring-2 focus:ring-indigo-500"
        >
          <option value="OrganizerOnly">✍️ Organizer only</option>
          <option value="Everyone">👥 Anyone who can see it</option>
        </select>
      </div>

      <!-- Error -->
      <div v-if="editError" class="text-sm text-red-400 bg-red-950 rounded-lg px-3 py-2">
        {{ editError }}
      </div>

      <!-- Actions -->
      <div class="flex justify-end gap-3 pt-2">
        <RouterLink
          :to="`/events/${route.params.id}`"
          class="px-4 py-2 text-sm font-medium text-slate-300 border border-slate-600 rounded-lg hover:bg-slate-700 transition"
        >
          Cancel
        </RouterLink>
        <button
          type="button"
          :disabled="editSaving"
          class="px-5 py-2 text-sm font-medium text-white bg-indigo-600 rounded-lg hover:bg-indigo-700 disabled:opacity-50 transition"
          @click="saveEdit"
        >
          <span v-if="editSaving">Saving…</span>
          <span v-else>Save Changes</span>
        </button>
      </div>
    </div>
  </div>
</template>
