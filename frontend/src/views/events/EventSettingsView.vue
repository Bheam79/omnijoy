<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useRoute, useRouter, RouterLink } from 'vue-router'
import { eventService, type EventDto } from '@/services/eventService'
import { useCompanyModeStore } from '@/stores/companyMode'

const route       = useRoute()
const router      = useRouter()
const companyMode = useCompanyModeStore()

const event   = ref<EventDto | null>(null)
const loading = ref(true)
const error   = ref<string | null>(null)
const deleting = ref(false)
const deleteError = ref<string | null>(null)

async function handleDelete() {
  if (!event.value) return
  if (!confirm(`Delete "${event.value.title}"? This cannot be undone.`)) return
  deleting.value  = true
  deleteError.value = null
  try {
    await eventService.deleteEvent(event.value.id)
    // Clear event context from sidebar
    companyMode.setActiveEvent(null)
    // Navigate back to the company events page if we know the company, else events list
    if (companyMode.activeCompany) {
      router.push(`/company/${companyMode.activeCompany.id}?tab=events`)
    } else {
      router.push('/events')
    }
  } catch (e: unknown) {
    if (typeof e === 'object' && e !== null) {
      const axiosError = e as { response?: { data?: { error?: string } }; message?: string }
      deleteError.value = axiosError.response?.data?.error ?? axiosError.message ?? 'Failed to delete event.'
    } else {
      deleteError.value = 'Failed to delete event.'
    }
    deleting.value = false
  }
}

async function fetchEvent() {
  loading.value = true
  error.value   = null
  try {
    const id = route.params.id as string
    event.value = await eventService.getEvent(id)
    companyMode.setActiveEvent(event.value)
  } catch (e: unknown) {
    if (typeof e === 'object' && e !== null) {
      const axiosError = e as { response?: { status?: number; data?: { error?: string } }; message?: string }
      if (axiosError.response?.status === 404) {
        error.value = 'Event not found.'
      } else if (axiosError.response?.status === 403) {
        error.value = 'You do not have permission to manage this event.'
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
    </div>

    <!-- Error -->
    <div
      v-else-if="error"
      class="bg-red-950 border border-red-800 rounded-xl p-6 text-red-400 text-center"
    >
      <div class="text-3xl mb-2">😕</div>
      <p class="font-medium">{{ error }}</p>
    </div>

    <!-- Settings content -->
    <template v-else-if="event">
      <h1 class="text-xl font-bold text-slate-100 mb-1">Event Settings</h1>
      <p class="text-sm text-gray-500 mb-6">{{ event.title }}</p>

      <!-- Danger zone -->
      <div class="bg-slate-800 border border-red-900/50 rounded-xl p-5">
        <h2 class="text-base font-semibold text-red-400 mb-1">Danger Zone</h2>
        <p class="text-sm text-gray-500 mb-4">
          These actions are permanent and cannot be undone.
        </p>

        <div class="flex items-center justify-between bg-slate-900/60 rounded-lg px-4 py-3">
          <div>
            <p class="text-sm font-medium text-slate-200">Delete this event</p>
            <p class="text-xs text-gray-500 mt-0.5">
              All event data, attendees, and posts will be permanently removed.
            </p>
          </div>
          <button
            :disabled="deleting"
            class="ml-4 shrink-0 px-4 py-2 text-sm font-semibold text-white bg-red-600 hover:bg-red-700 disabled:opacity-50 rounded-lg transition"
            data-testid="delete-event-btn"
            @click="handleDelete"
          >
            <span v-if="deleting">Deleting…</span>
            <span v-else>Delete Event</span>
          </button>
        </div>

        <div v-if="deleteError" class="mt-3 text-sm text-red-400 bg-red-950 rounded-lg px-3 py-2">
          {{ deleteError }}
        </div>
      </div>
    </template>
  </div>
</template>
