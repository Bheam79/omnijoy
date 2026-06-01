<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRoute, useRouter, RouterLink } from 'vue-router'
import { eventService, type EventDto, type EventAttendeesResult, type RsvpStatus } from '@/services/eventService'
import { companyPageService, type CompanyPageDto } from '@/services/companyPageService'
import { friendService, type FriendDto } from '@/services/friendService'
import { useAuthStore } from '@/stores/auth'
import type { PostDto } from '@/services/postService'

const route  = useRoute()
const router = useRouter()
const auth   = useAuthStore()

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
// Current user is an Owner of the company that organised the event
const isCompanyOwner = computed(() =>
  !!event.value?.companyPageId &&
  (companyPage.value?.myRole === 'Owner' || companyPage.value?.myRole === 'Admin')
)
// Combined: shows the owner sidebar
const isOwner = computed(() => isPersonalCreator.value || isCompanyOwner.value)

// ── Edit state ────────────────────────────────────────────────────────────────
const showEditForm      = ref(false)
const editTitle         = ref('')
const editDesc          = ref('')
const editStartAt       = ref('')
const editEndAt         = ref('')
const editLocation      = ref('')
const editPrivacy       = ref<'Everyone' | 'FriendsOfFriends' | 'Friends' | 'OnlyMe'>('Everyone')
const editPostingPolicy = ref<'OrganizerOnly' | 'Everyone'>('OrganizerOnly')
const editCoverFile     = ref<File | null>(null)
const editCoverPreview  = ref<string | null>(null)
const editSaving        = ref(false)
const editError         = ref<string | null>(null)

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

/**
 * Converts a UTC ISO string (e.g. "2024-06-15T12:30:00Z") to the local-time
 * string format required by <input type="datetime-local"> ("2024-06-15T14:30"
 * for a CET user).  Without this conversion the input would show the UTC
 * time, which is confusing and causes a double-offset when saving.
 */
function toDatetimeLocalValue(isoUtc: string): string {
  const d = new Date(isoUtc)
  return new Date(d.getTime() - d.getTimezoneOffset() * 60000).toISOString().slice(0, 16)
}

function openEdit() {
  if (!event.value) return
  editTitle.value         = event.value.title
  editDesc.value          = event.value.description ?? ''
  editStartAt.value       = toDatetimeLocalValue(event.value.startAt)
  editEndAt.value         = event.value.endAt ? toDatetimeLocalValue(event.value.endAt) : ''
  editLocation.value      = event.value.location ?? ''
  editPrivacy.value       = event.value.privacy as typeof editPrivacy.value
  editPostingPolicy.value = (event.value.postingPolicy ?? 'OrganizerOnly') as typeof editPostingPolicy.value
  editCoverFile.value     = null
  editCoverPreview.value  = null
  editError.value         = null
  showEditForm.value      = true
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
      title:         editTitle.value.trim(),
      description:   editDesc.value.trim() || undefined,
      startAt:       new Date(editStartAt.value).toISOString(),
      endAt:         editEndAt.value ? new Date(editEndAt.value).toISOString() : undefined,
      location:      editLocation.value.trim() || undefined,
      privacy:       editPrivacy.value,
      postingPolicy: editPostingPolicy.value,
      coverImage:    editCoverFile.value ?? undefined,
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

// ── Add Participants modal ─────────────────────────────────────────────────────
const showParticipantsModal = ref(false)
const friendsLoading = ref(false)
const friends = ref<FriendDto[]>([])
const participantSearch = ref('')
const copiedLink = ref(false)

const filteredFriends = computed(() => {
  const q = participantSearch.value.toLowerCase().trim()
  if (!q) return friends.value
  return friends.value.filter(f => f.user.displayName.toLowerCase().includes(q))
})

// Build a map of userId → rsvp for quick lookups in the modal
const attendeeRsvpMap = computed<Record<string, string>>(() => {
  const map: Record<string, string> = {}
  if (!attendees.value) return map
  for (const a of attendees.value.going)     map[a.userId] = 'Going'
  for (const a of attendees.value.maybe)     map[a.userId] = 'Maybe'
  for (const a of attendees.value.notGoing)  map[a.userId] = 'NotGoing'
  return map
})

async function openParticipants() {
  showParticipantsModal.value = true
  if (friends.value.length === 0 && !friendsLoading.value) {
    friendsLoading.value = true
    try {
      let page = 1
      let hasMore = true
      const all: FriendDto[] = []
      while (hasMore) {
        const result = await friendService.getFriends(page, 50)
        all.push(...result.items)
        hasMore = result.hasMore
        page++
      }
      friends.value = all
    } catch {
      // non-fatal
    } finally {
      friendsLoading.value = false
    }
  }
}

function copyEventLink() {
  const url = window.location.href
  navigator.clipboard.writeText(url).then(() => {
    copiedLink.value = true
    setTimeout(() => { copiedLink.value = false }, 2000)
  })
}

function rsvpLabel(userId: string): string {
  const status = attendeeRsvpMap.value[userId]
  if (status === 'Going')    return '✅ Going'
  if (status === 'Maybe')    return '🤔 Maybe'
  if (status === 'NotGoing') return '❌ Not going'
  return 'No response'
}

function rsvpColor(userId: string): string {
  const status = attendeeRsvpMap.value[userId]
  if (status === 'Going')    return 'text-green-400'
  if (status === 'Maybe')    return 'text-yellow-400'
  if (status === 'NotGoing') return 'text-red-400'
  return 'text-gray-500'
}

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
  <!-- Page wrapper — wider when owner sidebar is shown -->
  <div
    class="mx-auto px-4 py-6"
    :class="isOwner ? 'max-w-5xl' : 'max-w-2xl'"
  >
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

      <!-- ── Left sidebar (owner only) ──────────────────────────────────────── -->
      <aside
        v-if="isOwner"
        class="w-52 shrink-0 sticky top-6"
        data-testid="owner-sidebar"
      >
        <div class="bg-slate-800 border border-slate-700 rounded-xl overflow-hidden">
          <div class="px-4 py-2.5 border-b border-slate-700">
            <p class="text-xs font-semibold text-gray-500 uppercase tracking-widest">Manage Event</p>
          </div>
          <nav>
            <!-- Add Participants -->
            <button
              class="w-full flex items-center gap-3 px-4 py-3 text-sm text-slate-300 hover:bg-slate-700 hover:text-indigo-300 transition text-left"
              data-testid="sidebar-add-participants"
              @click="openParticipants"
            >
              <svg class="w-4 h-4 shrink-0 text-indigo-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                  d="M18 9v3m0 0v3m0-3h3m-3 0h-3m-2-5a4 4 0 11-8 0 4 4 0 018 0zM3 20a6 6 0 0112 0v1H3v-1z"/>
              </svg>
              Add Participants
            </button>

            <!-- Edit Event -->
            <button
              class="w-full flex items-center gap-3 px-4 py-3 text-sm text-slate-300 hover:bg-slate-700 hover:text-indigo-300 transition text-left border-t border-slate-700/50"
              data-testid="sidebar-edit-event"
              @click="openEdit"
            >
              <svg class="w-4 h-4 shrink-0 text-indigo-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                  d="M11 5H6a2 2 0 00-2 2v11a2 2 0 002 2h11a2 2 0 002-2v-5m-1.414-9.414a2 2 0 112.828 2.828L11.828 15H9v-2.828l8.586-8.586z"/>
              </svg>
              Edit Event
            </button>

            <!-- Delete Event -->
            <button
              class="w-full flex items-center gap-3 px-4 py-3 text-sm text-red-400 hover:bg-red-950 hover:text-red-300 transition text-left border-t border-slate-700/50"
              data-testid="sidebar-delete-event"
              @click="handleDelete"
            >
              <svg class="w-4 h-4 shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                  d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16"/>
              </svg>
              Delete Event
            </button>
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
          </div>
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
                    {{ new Date(post.createdAt).toLocaleString(undefined, { month: 'short', day: 'numeric', hour: '2-digit', minute: '2-digit' }) }}
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

        <!-- Inline edit form (triggered from sidebar) -->
        <div v-if="showEditForm && isOwner" class="bg-slate-800 border border-slate-700 rounded-xl p-5 space-y-4">
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

          <!-- Posting policy -->
          <div>
            <label class="block text-sm font-medium text-slate-300 mb-1">Who can post on this event?</label>
            <select v-model="editPostingPolicy"
              class="w-full rounded-lg border border-slate-600 px-3 py-2 text-sm bg-slate-800 focus:outline-none focus:ring-2 focus:ring-indigo-500"
            >
              <option value="OrganizerOnly">✍️ Organizer only</option>
              <option value="Everyone">👥 Anyone who can see it</option>
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
      </div><!-- /main content -->
    </div><!-- /event detail flex row -->
  </div><!-- /page wrapper -->

  <!-- ── Add Participants Modal ────────────────────────────────────────────── -->
  <Teleport to="body">
    <Transition name="modal">
      <div
        v-if="showParticipantsModal"
        class="fixed inset-0 z-50 flex items-center justify-center bg-black/60 backdrop-blur-sm p-4"
        data-testid="participants-modal"
        @click.self="showParticipantsModal = false"
      >
        <div class="bg-slate-800 border border-slate-700 rounded-2xl shadow-2xl w-full max-w-md max-h-[80vh] flex flex-col">
          <!-- Header -->
          <div class="flex items-center justify-between px-5 py-4 border-b border-slate-700">
            <h2 class="text-base font-semibold text-slate-100">Add Participants</h2>
            <button
              class="text-slate-500 hover:text-slate-300 p-1 transition"
              @click="showParticipantsModal = false"
            >
              <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12"/>
              </svg>
            </button>
          </div>

          <!-- Copy link -->
          <div class="px-5 pt-4 pb-3 border-b border-slate-700/50">
            <p class="text-xs text-gray-500 mb-2">Share this event link with anyone you want to invite:</p>
            <button
              class="w-full flex items-center justify-center gap-2 px-4 py-2 rounded-lg border border-indigo-700 text-sm text-indigo-300 hover:bg-indigo-900/40 transition"
              @click="copyEventLink"
            >
              <svg v-if="!copiedLink" class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                  d="M8 16H6a2 2 0 01-2-2V6a2 2 0 012-2h8a2 2 0 012 2v2m-6 12h8a2 2 0 002-2v-8a2 2 0 00-2-2h-8a2 2 0 00-2 2v8a2 2 0 002 2z"/>
              </svg>
              <svg v-else class="w-4 h-4 text-green-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M5 13l4 4L19 7"/>
              </svg>
              <span>{{ copiedLink ? 'Link copied!' : 'Copy event link' }}</span>
            </button>
          </div>

          <!-- Friends list -->
          <div class="px-5 py-3 border-b border-slate-700/50">
            <p class="text-xs font-medium text-gray-400 mb-2">Friends &amp; their RSVP status</p>
            <input
              v-model="participantSearch"
              type="text"
              placeholder="Search friends…"
              class="w-full rounded-lg border border-slate-600 bg-slate-900 px-3 py-2 text-sm text-slate-200 placeholder-gray-600 focus:outline-none focus:ring-2 focus:ring-indigo-500"
            />
          </div>

          <div class="flex-1 overflow-y-auto px-5 py-3">
            <!-- Loading -->
            <div v-if="friendsLoading" class="space-y-2">
              <div v-for="n in 5" :key="n" class="flex items-center gap-3 animate-pulse">
                <div class="w-8 h-8 rounded-full bg-slate-700" />
                <div class="h-4 bg-slate-700 rounded flex-1" />
              </div>
            </div>

            <!-- Friends -->
            <ul v-else-if="filteredFriends.length > 0" class="space-y-1">
              <li
                v-for="f in filteredFriends"
                :key="f.friendshipId"
                class="flex items-center gap-3 py-2 px-1 rounded-lg hover:bg-slate-700/40 transition"
              >
                <img
                  v-if="f.user.avatarUrl"
                  :src="f.user.avatarUrl"
                  :alt="f.user.displayName"
                  class="w-8 h-8 rounded-full object-cover shrink-0"
                />
                <div
                  v-else
                  class="w-8 h-8 rounded-full bg-indigo-600 flex items-center justify-center text-white text-xs font-semibold shrink-0"
                >
                  {{ f.user.displayName.charAt(0).toUpperCase() }}
                </div>
                <div class="flex-1 min-w-0">
                  <p class="text-sm text-slate-200 truncate">{{ f.user.displayName }}</p>
                  <p class="text-xs" :class="rsvpColor(f.user.id)">{{ rsvpLabel(f.user.id) }}</p>
                </div>
              </li>
            </ul>

            <!-- Empty -->
            <div v-else class="text-sm text-gray-500 text-center py-6 italic">
              {{ participantSearch ? 'No friends match your search.' : 'No friends yet.' }}
            </div>
          </div>

          <!-- Footer -->
          <div class="px-5 py-3 border-t border-slate-700">
            <p class="text-xs text-gray-600 text-center">
              Friends can RSVP by opening the event link.
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
