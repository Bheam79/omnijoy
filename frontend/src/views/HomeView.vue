<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { RouterLink } from 'vue-router'
import { eventService, type EventDto, type EventsPageResult } from '@/services/eventService'
import { usePublicLocation } from '@/composables/usePublicLocation'
import PublicEventCard from '@/components/events/PublicEventCard.vue'
import PublicLandingModal from '@/components/shared/PublicLandingModal.vue'

// ── SEO / Open Graph meta tags ───────────────────────────────────────────────
const META = {
  title:       'Omnijoy — Public events near you, without ads',
  description: 'See what is happening near you on Omnijoy. Public events, no ads, no forced content. Sign in to RSVP and follow your friends.',
  image:       '/og-image.png',
}

// ── Location + welcome modal state ──────────────────────────────────────────
const { publicLocation, setLocation } = usePublicLocation()

// Show the welcome modal automatically the first time a guest lands here
// (before they have picked a location or explicitly skipped it).
const HAS_SEEN_MODAL_KEY = 'omnijoy_public_landing_seen'
const showModal = ref(false)

// ── Events feed state ───────────────────────────────────────────────────────
const events       = ref<EventDto[]>([])
const loading      = ref(false)
const loadingMore  = ref(false)
const hasMore      = ref(false)
const page         = ref(1)
const errorMessage = ref<string | null>(null)

const feedHeading = computed(() =>
  publicLocation.value
    ? `Public events near ${publicLocation.value}`
    : 'Public events',
)

const feedSubheading = computed(() =>
  publicLocation.value
    ? 'Spotted in the public feed for your area — open for anyone to RSVP after signing in.'
    : 'A peek at what is happening on Omnijoy. Set a location to narrow it down to your area.',
)

// ── Lifecycle ───────────────────────────────────────────────────────────────
onMounted(() => {
  applyMetaTags()

  // Open the modal on first visit (regardless of whether they will choose a
  // location or just dismiss). After dismissal it stays closed unless the
  // visitor clicks "Change location".
  try {
    const seen = localStorage.getItem(HAS_SEEN_MODAL_KEY)
    if (!seen) showModal.value = true
  } catch {
    showModal.value = true
  }

  void loadEvents()
})

// Refresh the feed whenever the active location changes (modal save, manual
// reset, etc.).
watch(publicLocation, () => {
  void loadEvents()
})

// ── Data loading ────────────────────────────────────────────────────────────
async function loadEvents() {
  loading.value      = true
  errorMessage.value = null
  page.value         = 1
  events.value       = []
  try {
    const result: EventsPageResult = await eventService.getPublicEvents({
      location: publicLocation.value,
      page:     1,
      pageSize: 12,
    })
    events.value  = result.items
    hasMore.value = result.hasMore
  } catch (e: unknown) {
    errorMessage.value = extractError(e)
  } finally {
    loading.value = false
  }
}

async function loadMore() {
  if (!hasMore.value || loadingMore.value) return
  loadingMore.value = true
  try {
    const nextPage = page.value + 1
    const result = await eventService.getPublicEvents({
      location: publicLocation.value,
      page:     nextPage,
      pageSize: 12,
    })
    events.value.push(...result.items)
    hasMore.value = result.hasMore
    page.value    = nextPage
  } catch (e: unknown) {
    errorMessage.value = extractError(e)
  } finally {
    loadingMore.value = false
  }
}

// ── Modal handling ──────────────────────────────────────────────────────────
function dismissModal() {
  showModal.value = false
  try {
    localStorage.setItem(HAS_SEEN_MODAL_KEY, '1')
  } catch {
    /* ignore */
  }
}

function reopenLocationModal() {
  showModal.value = true
}

function clearLocation() {
  setLocation(null)
}

// ── Helpers ─────────────────────────────────────────────────────────────────
function extractError(e: unknown): string {
  if (typeof e === 'object' && e !== null) {
    const ae = e as { response?: { data?: { error?: string } }; message?: string }
    return ae.response?.data?.error ?? ae.message ?? 'Could not load events.'
  }
  return 'Could not load events.'
}

function applyMetaTags() {
  document.title = META.title

  const setMeta = (key: string, content: string, attr = 'name') => {
    let el = document.querySelector(`meta[${attr}="${key}"]`) as HTMLMetaElement | null
    if (!el) {
      el = document.createElement('meta')
      el.setAttribute(attr, key)
      document.head.appendChild(el)
    }
    el.setAttribute('content', content)
  }

  setMeta('description', META.description)
  setMeta('keywords', 'social media, no ads, friends, community, omnijoy, events')

  setMeta('og:title',       META.title,            'property')
  setMeta('og:description', META.description,      'property')
  setMeta('og:type',        'website',             'property')
  setMeta('og:url',         window.location.origin, 'property')
  setMeta('og:image',       META.image,            'property')
  setMeta('og:site_name',   'Omnijoy',             'property')

  setMeta('twitter:card',        'summary_large_image')
  setMeta('twitter:title',       META.title)
  setMeta('twitter:description', META.description)
  setMeta('twitter:image',       META.image)
}
</script>

<template>
  <div class="min-h-screen bg-gray-50 flex flex-col">

    <!-- ── Top navigation ───────────────────────────────────────────────── -->
    <nav class="sticky top-0 z-30 bg-white/90 backdrop-blur border-b border-gray-100">
      <div class="max-w-6xl mx-auto px-4 h-16 flex items-center justify-between">
        <div class="flex items-center gap-2">
          <div class="h-8 w-8 rounded-lg bg-indigo-600 flex items-center justify-center">
            <span class="text-white font-bold text-sm">OJ</span>
          </div>
          <span class="text-lg font-bold text-gray-900 tracking-tight">Omnijoy</span>
        </div>

        <div class="flex items-center gap-3">
          <RouterLink
            to="/login"
            class="text-sm font-medium text-gray-700 hover:text-indigo-600 transition-colors px-3 py-2"
          >
            Log in
          </RouterLink>
          <RouterLink
            to="/register"
            class="text-sm font-medium bg-indigo-600 hover:bg-indigo-700 text-white px-4 py-2 rounded-lg transition-colors"
          >
            Sign up
          </RouterLink>
        </div>
      </div>
    </nav>

    <!-- ── Hero ─────────────────────────────────────────────────────────── -->
    <section class="bg-gradient-to-br from-indigo-50 via-white to-violet-50 border-b border-gray-100">
      <div class="max-w-4xl mx-auto px-4 py-10 sm:py-14 text-center">
        <div class="inline-flex items-center gap-2 bg-indigo-50 text-indigo-700 border border-indigo-100 rounded-full px-3 py-1 text-xs font-medium mb-5">
          <span class="h-1.5 w-1.5 rounded-full bg-indigo-500 animate-pulse" />
          No ads. No forced feed. Just people and events.
        </div>
        <h1 class="text-3xl sm:text-4xl font-extrabold text-gray-900 tracking-tight mb-3">
          {{ feedHeading }}
        </h1>
        <p class="text-base text-gray-600 max-w-2xl mx-auto">
          {{ feedSubheading }}
        </p>
      </div>
    </section>

    <!-- ── Feed area ────────────────────────────────────────────────────── -->
    <section class="flex-1 max-w-6xl w-full mx-auto px-4 py-8">
      <!-- Toolbar -->
      <div class="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-3 mb-5">
        <div class="flex items-center gap-2 text-sm">
          <span class="text-gray-500">Location:</span>
          <span
            v-if="publicLocation"
            class="inline-flex items-center gap-1 bg-indigo-50 text-indigo-700 border border-indigo-100 rounded-full px-3 py-1 text-xs font-semibold"
          >
            <svg class="h-3 w-3" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M17.657 16.657L13.414 20.9a1.998 1.998 0 01-2.827 0l-4.244-4.243a8 8 0 1111.314 0z"/>
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 11a3 3 0 11-6 0 3 3 0 016 0z"/>
            </svg>
            {{ publicLocation }}
          </span>
          <span v-else class="text-xs italic text-gray-400">Showing public events from everywhere</span>
        </div>
        <div class="flex items-center gap-2">
          <button
            type="button"
            class="text-xs font-medium text-indigo-700 hover:text-indigo-900 underline-offset-2 hover:underline"
            @click="reopenLocationModal"
          >
            {{ publicLocation ? 'Change location' : 'Set a location' }}
          </button>
          <button
            v-if="publicLocation"
            type="button"
            class="text-xs font-medium text-gray-500 hover:text-gray-700"
            @click="clearLocation"
          >
            Clear
          </button>
        </div>
      </div>

      <!-- Loading skeleton -->
      <div v-if="loading" class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4">
        <div
          v-for="i in 6"
          :key="i"
          class="bg-white rounded-xl border border-gray-100 overflow-hidden animate-pulse"
        >
          <div class="h-40 bg-gray-200" />
          <div class="p-4 space-y-2">
            <div class="h-4 bg-gray-200 rounded w-3/4" />
            <div class="h-3 bg-gray-100 rounded w-1/2" />
            <div class="h-3 bg-gray-100 rounded w-2/3" />
          </div>
        </div>
      </div>

      <!-- Error -->
      <div
        v-else-if="errorMessage"
        class="bg-red-50 border border-red-200 rounded-xl p-4 text-sm text-red-700 flex items-center gap-2"
      >
        <svg class="w-5 h-5 shrink-0" fill="currentColor" viewBox="0 0 20 20">
          <path fill-rule="evenodd" d="M18 10a8 8 0 11-16 0 8 8 0 0116 0zm-7 4a1 1 0 11-2 0 1 1 0 012 0zm-1-9a1 1 0 00-1 1v4a1 1 0 102 0V6a1 1 0 00-1-1z" clip-rule="evenodd"/>
        </svg>
        {{ errorMessage }}
        <button class="ml-auto text-red-700 underline text-xs hover:text-red-900" @click="loadEvents()">
          Retry
        </button>
      </div>

      <!-- Empty -->
      <div
        v-else-if="events.length === 0"
        class="bg-white rounded-2xl border border-gray-100 p-10 text-center"
      >
        <div class="text-4xl mb-3">📅</div>
        <h3 class="text-lg font-semibold text-gray-800 mb-1">No upcoming public events</h3>
        <p class="text-sm text-gray-500 max-w-md mx-auto">
          <template v-if="publicLocation">
            Nothing public coming up around <strong>{{ publicLocation }}</strong> yet.
            Try clearing the location filter — or sign up and host your own.
          </template>
          <template v-else>
            Nothing public on the schedule right now. Be the first — sign up and create one.
          </template>
        </p>
        <div class="mt-5 flex justify-center gap-2">
          <button
            v-if="publicLocation"
            type="button"
            class="px-4 py-2 text-xs font-medium text-indigo-700 border border-indigo-200 rounded-xl hover:bg-indigo-50 transition"
            @click="clearLocation"
          >
            Clear location
          </button>
          <RouterLink
            to="/register"
            class="px-4 py-2 text-xs font-semibold text-white bg-indigo-600 rounded-xl hover:bg-indigo-700 transition"
          >
            Sign up to host an event
          </RouterLink>
        </div>
      </div>

      <!-- Grid of public events -->
      <div v-else class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4">
        <PublicEventCard
          v-for="event in events"
          :key="event.id"
          :event="event"
          @require-auth="reopenLocationModal"
        />
      </div>

      <!-- Load more -->
      <div v-if="!loading && events.length > 0 && hasMore" class="mt-6 text-center">
        <button
          type="button"
          class="px-6 py-2 text-sm font-medium text-indigo-700 border border-indigo-200 rounded-xl hover:bg-indigo-50 transition disabled:opacity-50"
          :disabled="loadingMore"
          @click="loadMore"
        >
          <span v-if="loadingMore">Loading…</span>
          <span v-else>Load more</span>
        </button>
      </div>
    </section>

    <!-- ── Inline CTA ───────────────────────────────────────────────────── -->
    <section class="bg-gradient-to-br from-indigo-600 to-violet-600">
      <div class="max-w-3xl mx-auto px-4 py-12 text-center">
        <h2 class="text-2xl sm:text-3xl font-bold text-white mb-2">
          Join Omnijoy to RSVP, follow friends, and host your own events
        </h2>
        <p class="text-indigo-100 text-sm sm:text-base mb-6">
          Free forever. No ads. Real-time updates. No data sold.
        </p>
        <div class="flex flex-col sm:flex-row gap-3 justify-center">
          <RouterLink
            to="/register"
            class="inline-flex items-center justify-center bg-white hover:bg-gray-50 text-indigo-700 font-semibold px-6 py-3 rounded-xl text-sm transition-colors shadow"
          >
            Create a free account
          </RouterLink>
          <RouterLink
            to="/login"
            class="inline-flex items-center justify-center border border-indigo-300 hover:border-indigo-100 text-white font-semibold px-6 py-3 rounded-xl text-sm transition-colors"
          >
            Sign in
          </RouterLink>
        </div>
      </div>
    </section>

    <!-- ── Footer ───────────────────────────────────────────────────────── -->
    <footer class="bg-gray-900 text-gray-400 py-8">
      <div class="max-w-6xl mx-auto px-4 flex flex-col sm:flex-row items-center justify-between gap-3">
        <div class="flex items-center gap-2">
          <div class="h-6 w-6 rounded-md bg-indigo-500 flex items-center justify-center">
            <span class="text-white font-bold text-xs">OJ</span>
          </div>
          <span class="text-sm font-medium text-gray-300">Omnijoy</span>
        </div>
        <p class="text-xs">© 2025 Omnijoy · No ads. No data sales. Ever.</p>
        <div class="flex gap-4 text-xs">
          <a href="#" class="hover:text-gray-200 transition-colors">Privacy</a>
          <a href="#" class="hover:text-gray-200 transition-colors">Terms</a>
          <a href="#" class="hover:text-gray-200 transition-colors">About</a>
        </div>
      </div>
    </footer>

    <!-- ── Welcome / location modal ─────────────────────────────────────── -->
    <PublicLandingModal v-if="showModal" @dismiss="dismissModal" />
  </div>
</template>
