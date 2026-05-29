<script setup lang="ts">
import { ref, onMounted, computed } from 'vue'
import { useRoute } from 'vue-router'
import { eventService, type EventDto } from '@/services/eventService'
import { useAuthStore } from '@/stores/auth'

const route = useRoute()
const auth = useAuthStore()

const event = ref<EventDto | null>(null)
const loading = ref(true)
const errorMessage = ref<string | null>(null)
const errorTitle = ref<string | null>(null)

onMounted(async () => {
  const id = route.params.id as string
  try {
    event.value = await eventService.getEvent(id)
  } catch (e: unknown) {
    const axiosError = e as { response?: { status?: number; data?: { error?: string } } }
    const status = axiosError.response?.status
    if (status === 404) {
      errorTitle.value = 'Event not found'
      errorMessage.value = 'This event does not exist or has been deleted.'
    } else if (status === 403 || status === 401) {
      errorTitle.value = 'This event is not public'
      errorMessage.value = 'The organiser has restricted who can see this event.'
    } else {
      errorTitle.value = 'Could not load event'
      errorMessage.value = axiosError.response?.data?.error ?? 'Something went wrong.'
    }
  } finally {
    loading.value = false
  }
})

const whenLabel = computed(() => {
  if (!event.value) return ''
  const start = new Date(event.value.startAt)
  const opts: Intl.DateTimeFormatOptions = {
    weekday: 'long', year: 'numeric', month: 'long', day: 'numeric',
    hour: '2-digit', minute: '2-digit',
  }
  return start.toLocaleString(undefined, opts)
})
</script>

<template>
  <main class="min-h-screen bg-slate-700 py-8 px-4">
    <div class="max-w-xl mx-auto">
      <header class="flex items-center justify-between mb-6">
        <RouterLink to="/" class="text-2xl font-extrabold text-blue-400">Omnijoy</RouterLink>
        <RouterLink
          v-if="!auth.isAuthenticated"
          to="/login"
          class="text-sm font-semibold text-blue-400 hover:underline"
        >Sign in</RouterLink>
        <RouterLink
          v-else
          to="/wall"
          class="text-sm font-semibold text-blue-400 hover:underline"
        >Open Omnijoy</RouterLink>
      </header>

      <div v-if="loading" class="bg-slate-800 rounded-xl shadow-sm border border-slate-700 p-8 text-center text-gray-500">
        Loading…
      </div>

      <div
        v-else-if="errorMessage"
        class="bg-slate-800 rounded-xl shadow-sm border border-slate-700 p-8 text-center"
      >
        <h1 class="text-xl font-semibold text-slate-100 mb-2">{{ errorTitle }}</h1>
        <p class="text-gray-500">{{ errorMessage }}</p>
        <RouterLink
          to="/"
          class="inline-block mt-6 px-5 py-2 rounded-lg bg-blue-600 text-white font-semibold hover:bg-blue-700 transition"
        >Back to Omnijoy</RouterLink>
      </div>

      <article
        v-else-if="event"
        class="bg-slate-800 rounded-xl shadow-sm border border-slate-700 overflow-hidden"
      >
        <img
          v-if="event.coverImageUrl"
          :src="event.coverImageUrl"
          :alt="event.title"
          class="w-full max-h-64 object-cover bg-slate-700"
        />
        <div class="p-6">
          <h1 class="text-2xl font-bold text-slate-100">{{ event.title }}</h1>
          <p class="text-sm text-gray-500 mt-1">
            Hosted by <span class="font-semibold">{{ event.creator.displayName }}</span>
          </p>

          <dl class="mt-5 space-y-3 text-sm text-slate-300">
            <div class="flex gap-2">
              <dt class="font-semibold w-20">When</dt>
              <dd>{{ whenLabel }}</dd>
            </div>
            <div v-if="event.location" class="flex gap-2">
              <dt class="font-semibold w-20">Where</dt>
              <dd>{{ event.location }}</dd>
            </div>
            <div class="flex gap-2">
              <dt class="font-semibold w-20">Going</dt>
              <dd>{{ event.goingCount }} · {{ event.maybeCount }} maybe</dd>
            </div>
          </dl>

          <p
            v-if="event.description"
            class="mt-5 text-slate-300 leading-relaxed whitespace-pre-wrap"
          >{{ event.description }}</p>

          <div class="mt-6 flex gap-3">
            <RouterLink
              :to="auth.isAuthenticated ? `/events/${event.id}` : '/register'"
              class="px-5 py-2 rounded-lg bg-blue-600 text-white font-semibold hover:bg-blue-700 transition"
            >
              {{ auth.isAuthenticated ? 'View event' : 'Sign up to RSVP' }}
            </RouterLink>
          </div>
        </div>
      </article>
    </div>
  </main>
</template>
