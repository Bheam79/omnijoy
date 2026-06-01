<script setup lang="ts">
import { ref, computed } from 'vue'
import type { EventDto } from '@/services/eventService'
import { useEventsStore } from '@/stores/events'
import { useCompanyModeStore } from '@/stores/companyMode'

const emit = defineEmits<{
  close: []
  created: [event: EventDto]
}>()

const eventsStore = useEventsStore()
const companyMode = useCompanyModeStore()

// ── Form state ────────────────────────────────────────────────────────────────

const title       = ref('')
const description = ref('')
const startAt     = ref('')
const endAt       = ref('')
const location    = ref('')
const privacy     = ref<'Everyone' | 'FriendsOfFriends' | 'Friends' | 'OnlyMe'>('Everyone')
const coverFile   = ref<File | null>(null)
const coverPreview = ref<string | null>(null)

const submitting = ref(false)
const formError  = ref<string | null>(null)

const minDate = computed(() => {
  // datetime-local inputs expect local time, not UTC.
  // Subtracting the timezone offset converts the UTC ISO representation
  // so the resulting slice is in the user's local wall-clock time.
  const now = new Date()
  return new Date(now.getTime() - now.getTimezoneOffset() * 60000).toISOString().slice(0, 16)
})

function onCoverChange(e: Event) {
  const input = e.target as HTMLInputElement
  const file = input.files?.[0]
  if (!file) return
  coverFile.value = file
  const reader = new FileReader()
  reader.onload = (ev) => {
    coverPreview.value = ev.target?.result as string
  }
  reader.readAsDataURL(file)
}

function removeCover() {
  coverFile.value = null
  coverPreview.value = null
}

async function handleSubmit() {
  formError.value = null

  if (!title.value.trim()) {
    formError.value = 'Title is required.'
    return
  }
  if (!startAt.value) {
    formError.value = 'Start date/time is required.'
    return
  }

  submitting.value = true
  try {
    const ev = await eventsStore.createEvent({
      title:         title.value.trim(),
      description:   description.value.trim() || undefined,
      startAt:       new Date(startAt.value).toISOString(),
      endAt:         endAt.value ? new Date(endAt.value).toISOString() : undefined,
      location:      location.value.trim() || undefined,
      privacy:       privacy.value,
      // Organizer is determined by company mode — no dropdown needed.
      companyPageId: companyMode.activeCompany?.id ?? undefined,
      coverImage:    coverFile.value ?? undefined,
    })
    emit('created', ev)
    emit('close')
  } catch (e: unknown) {
    if (typeof e === 'object' && e !== null) {
      const axiosError = e as { response?: { data?: { error?: string } }; message?: string }
      formError.value = axiosError.response?.data?.error ?? axiosError.message ?? 'Something went wrong.'
    } else {
      formError.value = 'Something went wrong.'
    }
  } finally {
    submitting.value = false
  }
}
</script>

<template>
  <!-- Backdrop -->
  <Teleport to="body">
    <div class="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/50 backdrop-blur-sm" @click.self="emit('close')">
      <div class="bg-slate-800 rounded-2xl shadow-2xl w-full max-w-lg max-h-[90vh] flex flex-col">
        <!-- Header -->
        <div class="flex items-center justify-between px-6 py-4 border-b border-slate-700">
          <div>
            <h2 class="text-lg font-bold text-slate-100">Create Event</h2>
            <p v-if="companyMode.isActive" class="text-xs text-indigo-400 mt-0.5">
              Organizer: {{ companyMode.activeCompany?.name }}
            </p>
          </div>
          <button
            class="text-slate-500 hover:text-slate-400 p-1 rounded-full hover:bg-slate-700 transition"
            aria-label="Close"
            @click="emit('close')"
          >
            <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12"/>
            </svg>
          </button>
        </div>

        <!-- Scrollable form body -->
        <form class="overflow-y-auto flex-1 px-6 py-4 space-y-4" @submit.prevent="handleSubmit">
          <!-- Cover image -->
          <div>
            <label class="block text-sm font-medium text-slate-300 mb-1">Cover Image</label>
            <div
              v-if="coverPreview"
              class="relative h-36 rounded-xl overflow-hidden bg-slate-700"
            >
              <img :src="coverPreview" class="w-full h-full object-cover" alt="Cover preview" />
              <button
                type="button"
                class="absolute top-2 right-2 bg-black/50 text-white rounded-full p-1 hover:bg-black/70 transition"
                @click="removeCover"
              >
                <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12"/>
                </svg>
              </button>
            </div>
            <label
              v-else
              class="flex flex-col items-center justify-center h-24 border-2 border-dashed border-slate-600 rounded-xl cursor-pointer hover:border-indigo-400 hover:bg-indigo-900/50 transition"
            >
              <svg class="w-8 h-8 text-slate-500 mb-1" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5" d="M4 16l4.586-4.586a2 2 0 012.828 0L16 16m-2-2l1.586-1.586a2 2 0 012.828 0L20 14m-6-6h.01M6 20h12a2 2 0 002-2V6a2 2 0 00-2-2H6a2 2 0 00-2 2v12a2 2 0 002 2z"/>
              </svg>
              <span class="text-xs text-gray-500">Click to add a cover photo</span>
              <input type="file" class="hidden" accept="image/*" @change="onCoverChange" />
            </label>
          </div>

          <!-- Title -->
          <div>
            <label class="block text-sm font-medium text-slate-300 mb-1">
              Title <span class="text-red-500">*</span>
            </label>
            <input
              v-model="title"
              type="text"
              class="w-full rounded-lg border border-slate-600 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500 focus:border-transparent"
              placeholder="Event title"
              maxlength="256"
              required
            />
          </div>

          <!-- Description -->
          <div>
            <label class="block text-sm font-medium text-slate-300 mb-1">Description</label>
            <textarea
              v-model="description"
              class="w-full rounded-lg border border-slate-600 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500 focus:border-transparent resize-none"
              placeholder="What's this event about?"
              rows="3"
            />
          </div>

          <!-- Start / End datetime -->
          <div class="grid grid-cols-2 gap-3">
            <div>
              <label class="block text-sm font-medium text-slate-300 mb-1">
                Starts <span class="text-red-500">*</span>
              </label>
              <input
                v-model="startAt"
                type="datetime-local"
                :min="minDate"
                class="w-full rounded-lg border border-slate-600 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500 focus:border-transparent"
                required
              />
            </div>
            <div>
              <label class="block text-sm font-medium text-slate-300 mb-1">Ends</label>
              <input
                v-model="endAt"
                type="datetime-local"
                :min="startAt || minDate"
                class="w-full rounded-lg border border-slate-600 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500 focus:border-transparent"
              />
            </div>
          </div>

          <!-- Location -->
          <div>
            <label class="block text-sm font-medium text-slate-300 mb-1">Location</label>
            <input
              v-model="location"
              type="text"
              class="w-full rounded-lg border border-slate-600 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500 focus:border-transparent"
              placeholder="Where is it happening?"
              maxlength="512"
            />
          </div>

          <!-- Privacy -->
          <div>
            <label class="block text-sm font-medium text-slate-300 mb-1">Privacy</label>
            <select
              v-model="privacy"
              class="w-full rounded-lg border border-slate-600 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500 focus:border-transparent bg-slate-800"
            >
              <option value="Everyone">🌐 Public</option>
              <option value="FriendsOfFriends">👥 Friends of friends</option>
              <option value="Friends">👥 Friends only</option>
              <option value="OnlyMe">🔒 Only me</option>
            </select>
          </div>

          <!-- Error -->
          <div v-if="formError" class="text-sm text-red-400 bg-red-950 rounded-lg px-3 py-2">
            {{ formError }}
          </div>
        </form>

        <!-- Footer -->
        <div class="px-6 py-4 border-t border-slate-700 flex justify-end gap-3">
          <button
            type="button"
            class="px-4 py-2 text-sm font-medium text-slate-300 bg-slate-800 border border-slate-600 rounded-lg hover:bg-slate-700 transition"
            @click="emit('close')"
          >
            Cancel
          </button>
          <button
            type="button"
            class="px-4 py-2 text-sm font-medium text-white bg-indigo-600 rounded-lg hover:bg-indigo-700 transition disabled:opacity-50"
            :disabled="submitting"
            @click="handleSubmit"
          >
            <span v-if="submitting">Creating…</span>
            <span v-else>Create Event</span>
          </button>
        </div>
      </div>
    </div>
  </Teleport>
</template>
