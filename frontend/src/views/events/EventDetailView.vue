<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRoute, RouterLink } from 'vue-router'
import { eventService, type EventDto, type EventAttendeesResult, type RsvpStatus } from '@/services/eventService'
import { companyPageService, type CompanyPageDto } from '@/services/companyPageService'
import { useAuthStore } from '@/stores/auth'
import { useCompanyModeStore } from '@/stores/companyMode'
import type { PostDto } from '@/services/postService'

const route       = useRoute()
const auth        = useAuthStore()
const companyMode = useCompanyModeStore()

const event     = ref<EventDto | null>(null)
const attendees = ref<EventAttendeesResult | null>(null)
const loading   = ref(true)
const error     = ref<string | null>(null)
const rsvpLoading = ref(false)

// Company page (loaded when event.companyPageId is set, to determine ownership)
const companyPage = ref<CompanyPageDto | null>(null)

// Current user is the personal creator
const isPersonalCreator = computed(() =>
  !!auth.user && event.value?.creator.id === auth.user.id
)
// Current user is an Owner/Admin of the company that organised the event
const isCompanyOwner = computed(() =>
  !!event.value?.companyPageId &&
  (companyPage.value?.myRole === 'Owner' || companyPage.value?.myRole === 'Admin')
)
// Combined: shows the owner sidebar (only when NOT in company mode — the
// CompanySidebar handles management actions in company mode)
const isOwner = computed(() => isPersonalCreator.value || isCompanyOwner.value)

// ── Event posts ───────────────────────────────────────────────────────────────
const eventPosts     = ref<PostDto[]>([])
const postsLoading   = ref(false)
const postsHasMore   = ref(false)
const postsPage      = ref(1)
const newPostContent = ref('')
const postSubmitting = ref(false)
const postError      = ref<string | null>(null)

// Can the current user post on this event wall?
const canPost = computed(() => {
  if (!auth.isAuthenticated || !event.value) return false
  if (event.value.postingPolicy === 'Everyone') return true
  return isOwner.value
})

// ── Shared helpers ─────────────────────────────────────────────────────────────

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
    hour12: false,
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

    // Register event with company mode store so the CompanySidebar can show it
    companyMode.setActiveEvent(event.value)

    // Load company page if relevant, to check the current user's role
    if (event.value.companyPageId && auth.isAuthenticated) {
      try {
        companyPage.value = await companyPageService.getPage(event.value.companyPageId)
      } catch {
        // ignore — we just won't show the owner sidebar for this company
      }
    }
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

async function fetchEventPosts() {
  if (!event.value) return
  postsLoading.value = true
  try {
    const result = await eventService.getEventPosts(event.value.id, postsPage.value)
    if (postsPage.value === 1) {
      eventPosts.value = result.items
    } else {
      eventPosts.value.push(...result.items)
    }
    postsHasMore.value = result.hasMore
  } catch (e: unknown) {
    console.error('Failed to load event posts', e)
  } finally {
    postsLoading.value = false
  }
}

async function loadMorePosts() {
  postsPage.value++
  await fetchEventPosts()
}

async function submitPost() {
  if (!event.value || !newPostContent.value.trim()) return
  postError.value = null
  postSubmitting.value = true
  try {
    const post = await eventService.createEventPost(event.value.id, newPostContent.value.trim())
    eventPosts.value.unshift(post)
    newPostContent.value = ''
  } catch (e: unknown) {
    if (typeof e === 'object' && e !== null) {
      const axiosError = e as { response?: { data?: { error?: string } }; message?: string }
      postError.value = axiosError.response?.data?.error ?? axiosError.message ?? 'Failed to post.'
    } else {
      postError.value = 'Failed to post.'
    }
  } finally {
    postSubmitting.value = false
  }
}

onMounted(async () => {
  await fetchEvent()
  await fetchEventPosts()
})
</script>

<template>
  <!-- Page wrapper — width + padding come from AppShell -->
  <div>
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
    <div v-else-if="event" class="flex items-start gap-6">

      <!-- ── Left sidebar (owner only, hidden in company mode where the
           global CompanySidebar handles these actions) ──────────────────── -->
      <aside
        v-if="isOwner && !companyMode.isActive"
        class="w-52 shrink-0 sticky top-6"
        data-testid="owner-sidebar"
      >
        <div class="bg-slate-800 border border-slate-700 rounded-xl overflow-hidden">
          <div class="px-4 py-2.5 border-b border-slate-700">
            <p class="text-xs font-semibold text-gray-500 uppercase tracking-widest">Manage Event</p>
          </div>
          <nav>
            <!-- Participants -->
            <RouterLink
              :to="`/events/${event.id}/participants`"
              class="w-full flex items-center gap-3 px-4 py-3 text-sm text-slate-300 hover:bg-slate-700 hover:text-indigo-300 transition text-left"
              data-testid="sidebar-add-participants"
            >
              <svg class="w-4 h-4 shrink-0 text-indigo-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                  d="M17 20h5v-2a3 3 0 00-5.356-1.857M17 20H7m10 0v-2c0-.656-.126-1.283-.356-1.857M7 20H2v-2a3 3 0 015.356-1.857M7 20v-2c0-.656.126-1.283.356-1.857m0 0a5.002 5.002 0 019.288 0M15 7a3 3 0 11-6 0 3 3 0 016 0zm6 3a2 2 0 11-4 0 2 2 0 014 0zM7 10a2 2 0 11-4 0 2 2 0 014 0z"/>
              </svg>
              Participants
            </RouterLink>

            <!-- Edit Event -->
            <RouterLink
              :to="`/events/${event.id}/edit`"
              class="w-full flex items-center gap-3 px-4 py-3 text-sm text-slate-300 hover:bg-slate-700 hover:text-indigo-300 transition text-left border-t border-slate-700/50"
              data-testid="sidebar-edit-event"
            >
              <svg class="w-4 h-4 shrink-0 text-indigo-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                  d="M11 5H6a2 2 0 00-2 2v11a2 2 0 002 2h11a2 2 0 002-2v-5m-1.414-9.414a2 2 0 112.828 2.828L11.828 15H9v-2.828l8.586-8.586z"/>
              </svg>
              Edit Event
            </RouterLink>

            <!-- Event Settings (includes delete) -->
            <RouterLink
              :to="`/events/${event.id}/settings`"
              class="w-full flex items-center gap-3 px-4 py-3 text-sm text-slate-300 hover:bg-slate-700 hover:text-indigo-300 transition text-left border-t border-slate-700/50"
              data-testid="sidebar-event-settings"
            >
              <svg class="w-4 h-4 shrink-0 text-indigo-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                  d="M10.325 4.317c.426-1.756 2.924-1.756 3.35 0a1.724 1.724 0 002.573 1.066c1.543-.94 3.31.826 2.37 2.37a1.724 1.724 0 001.065 2.572c1.756.426 1.756 2.924 0 3.35a1.724 1.724 0 00-1.066 2.573c.94 1.543-.826 3.31-2.37 2.37a1.724 1.724 0 00-2.572 1.065c-.426 1.756-2.924 1.756-3.35 0a1.724 1.724 0 00-2.573-1.066c-1.543.94-3.31-.826-2.37-2.37a1.724 1.724 0 00-1.065-2.572c-1.756-.426-1.756-2.924 0-3.35a1.724 1.724 0 001.066-2.573c-.94-1.543.826-3.31 2.37-2.37.996.608 2.296.07 2.572-1.065z"/>
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 12a3 3 0 11-6 0 3 3 0 016 0z"/>
              </svg>
              Event Settings
            </RouterLink>
          </nav>
        </div>
      </aside>

      <!-- ── Main content ──────────────────────────────────────────────────── -->
      <div class="flex-1 min-w-0 space-y-5">
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

          <!-- Organizer -->
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
            <a
              v-if="event.locationLatitude != null && event.locationLongitude != null"
              :href="`https://maps.google.com/?q=${event.locationLatitude},${event.locationLongitude}`"
              target="_blank"
              rel="noopener noreferrer"
              class="text-indigo-400 hover:text-indigo-300 text-xs underline transition shrink-0"
              data-testid="event-open-in-maps"
            >
              Open in Maps
            </a>
          </div>

          <!-- Buy tickets button -->
          <a
            v-if="event.ticketUrl"
            :href="event.ticketUrl"
            target="_blank"
            rel="noopener noreferrer"
            data-testid="event-buy-tickets"
            class="inline-flex items-center gap-2 px-4 py-2 mt-1 rounded-lg text-sm font-semibold text-white bg-emerald-600 hover:bg-emerald-500 shadow transition"
          >
            <svg class="h-4 w-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                d="M15 5v2m0 4v2m0 4v2M5 5a2 2 0 00-2 2v3a2 2 0 110 4v3a2 2 0 002 2h14a2 2 0 002-2v-3a2 2 0 110-4V7a2 2 0 00-2-2H5z"/>
            </svg>
            Buy tickets
            <svg class="h-3.5 w-3.5 opacity-75" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                d="M14 5l7 7m0 0l-7 7m7-7H3"/>
            </svg>
          </a>
        </div>

        <!-- RSVP section (upcoming events) -->
        <div v-if="isUpcoming" class="bg-slate-800 border border-slate-700 rounded-xl p-4">
          <h2 class="text-sm font-semibold text-slate-100 mb-3">Will you attend?</h2>
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
                : 'bg-slate-700 text-slate-200 border-slate-600 hover:border-indigo-400 hover:bg-slate-600'"
              :disabled="rsvpLoading"
              @click="handleRsvp(opt.value)"
            >
              {{ opt.icon }} {{ opt.label }}
            </button>
          </div>
          <p v-if="event.myRsvp" class="text-xs text-slate-400 mt-2">
            You responded: <strong class="text-slate-200">{{ event.myRsvp }}</strong>
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

        <!-- ── Event Wall Posts ──────────────────────────────────────────── -->
        <div class="bg-slate-800 border border-slate-700 rounded-xl p-4 shadow-sm space-y-4">
          <div class="flex items-center justify-between">
            <h2 class="text-base font-semibold text-slate-100">
              Event Wall
              <span class="ml-1 text-xs font-normal text-gray-500">
                ({{ event.postingPolicy === 'Everyone' ? 'anyone can post' : 'organizer only' }})
              </span>
            </h2>
          </div>

          <!-- Post composer (shown when user can post) -->
          <div v-if="canPost" class="space-y-2">
            <textarea
              v-model="newPostContent"
              rows="3"
              placeholder="Write something about this event…"
              class="w-full rounded-lg border border-slate-600 bg-slate-900 px-3 py-2 text-sm text-slate-200 placeholder-gray-600 focus:outline-none focus:ring-2 focus:ring-indigo-500 resize-none"
            />
            <div v-if="postError" class="text-xs text-red-400 bg-red-950 rounded px-2 py-1">{{ postError }}</div>
            <div class="flex justify-end">
              <button
                type="button"
                :disabled="postSubmitting || !newPostContent.trim()"
                class="px-4 py-2 text-sm font-medium text-white bg-indigo-600 rounded-lg hover:bg-indigo-700 disabled:opacity-50 transition"
                @click="submitPost"
              >
                <span v-if="postSubmitting">Posting…</span>
                <span v-else>Post</span>
              </button>
            </div>
          </div>

          <!-- No posts yet -->
          <p
            v-if="!postsLoading && eventPosts.length === 0"
            class="text-sm text-gray-500 italic"
          >
            No posts yet.
            <span v-if="canPost">Be the first to post!</span>
          </p>

          <!-- Post list -->
          <div v-if="eventPosts.length > 0" class="space-y-3">
            <div
              v-for="post in eventPosts"
              :key="post.id"
              class="flex gap-3 p-3 rounded-lg bg-slate-900/60"
            >
              <!-- Avatar -->
              <RouterLink :to="`/profile/${post.author.id}`" class="shrink-0">
                <img
                  v-if="post.author.avatarUrl"
                  :src="post.author.avatarUrl"
                  :alt="post.author.displayName"
                  class="w-8 h-8 rounded-full object-cover"
                />
                <div
                  v-else
                  class="w-8 h-8 rounded-full bg-indigo-600 flex items-center justify-center text-white text-xs font-semibold"
                >
                  {{ post.author.displayName.charAt(0).toUpperCase() }}
                </div>
              </RouterLink>
              <!-- Content -->
              <div class="flex-1 min-w-0">
                <div class="flex items-baseline gap-2 mb-0.5">
                  <RouterLink
                    :to="`/profile/${post.author.id}`"
                    class="text-sm font-semibold text-slate-200 hover:text-indigo-400 transition"
                  >
                    {{ post.author.displayName }}
                  </RouterLink>
                  <span class="text-xs text-gray-600">
                    {{ new Date(post.createdAt).toLocaleString(undefined, { month: 'short', day: 'numeric', hour: '2-digit', minute: '2-digit', hour12: false }) }}
                  </span>
                </div>
                <p class="text-sm text-slate-300 whitespace-pre-wrap leading-relaxed">{{ post.content }}</p>
              </div>
            </div>
          </div>

          <!-- Load more -->
          <div v-if="postsHasMore" class="text-center">
            <button
              type="button"
              :disabled="postsLoading"
              class="text-sm text-indigo-400 hover:text-indigo-300 transition disabled:opacity-50"
              @click="loadMorePosts"
            >
              {{ postsLoading ? 'Loading…' : 'Load more posts' }}
            </button>
          </div>

          <!-- Loading skeleton -->
          <div v-if="postsLoading && eventPosts.length === 0" class="space-y-3">
            <div v-for="n in 2" :key="n" class="flex gap-3 animate-pulse">
              <div class="w-8 h-8 rounded-full bg-slate-700 shrink-0" />
              <div class="flex-1 space-y-1.5">
                <div class="h-3 bg-slate-700 rounded w-1/4" />
                <div class="h-3 bg-slate-700 rounded w-3/4" />
              </div>
            </div>
          </div>
        </div>

      </div><!-- /main content -->
    </div><!-- /event detail flex row -->
  </div><!-- /page wrapper -->
</template>
