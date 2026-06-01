<script setup lang="ts">
/**
 * Full notifications page — /notifications
 *
 * Features:
 * - Filter bar: All / Friend Requests / Mentions / Events / Reactions / Comments
 * - Mark-all-as-read button
 * - Paginated list (infinite scroll via "Load more" button)
 * - Real-time: new notifications pushed via the shared notifications store
 *   appear at the top (filtered to the active category).
 * - Click navigates to the relevant content and marks the notification read.
 */
import { ref, computed, watch, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { useNotificationsStore } from '@/stores/notifications'
import { notificationService } from '@/services/notificationService'
import type { Notification, NotificationType } from '@/types'

const router = useRouter()
const store  = useNotificationsStore()

// ── Filter categories ────────────────────────────────────────────────────────

export type FilterCategory = 'all' | 'friends' | 'mentions' | 'events' | 'reactions' | 'comments'

interface FilterDef {
  label: string
  types: string[] | null
}

const FILTERS: Record<FilterCategory, FilterDef> = {
  all:       { label: 'All',             types: null },
  friends:   { label: 'Friend Requests', types: ['FriendRequest', 'FriendRequestAccepted', 'FamilyRelationRequest', 'NewFollower'] },
  mentions:  { label: 'Mentions',        types: ['MentionInPost', 'MentionInComment', 'TaggedInPost'] },
  events:    { label: 'Events',          types: ['EventInvite', 'EventReminder', 'EventCreatedByFriend'] },
  reactions: { label: 'Reactions',       types: ['PostLike', 'PostShare', 'CommentLike'] },
  comments:  { label: 'Comments',        types: ['PostComment', 'CommentReply'] },
}

const FILTER_KEYS = Object.keys(FILTERS) as FilterCategory[]

// ── Local page state ─────────────────────────────────────────────────────────

const activeFilter = ref<FilterCategory>('all')
const items        = ref<Notification[]>([])
const page         = ref(1)
const pageSize     = 20
const hasMore      = ref(true)
const loading      = ref(false)
const error        = ref<string | null>(null)

// ── Derived helpers ──────────────────────────────────────────────────────────

const currentTypes = computed(() => FILTERS[activeFilter.value].types)

function notificationMatchesFilter(n: Notification): boolean {
  const types = currentTypes.value
  if (types === null) return true
  return types.includes(n.type as string)
}

// ── Data loading ─────────────────────────────────────────────────────────────

async function loadPage(pg: number) {
  if (loading.value) return
  loading.value = true
  error.value   = null
  try {
    const result = await notificationService.list(pg, pageSize, currentTypes.value ?? undefined)
    if (pg === 1) {
      items.value = result.items
    } else {
      // Deduplicate (SignalR may have prepended items between fetches)
      const knownIds = new Set(items.value.map((n) => n.id))
      items.value.push(...result.items.filter((n) => !knownIds.has(n.id)))
    }
    hasMore.value = result.hasMore
    page.value    = pg
    // Keep the global unread count in sync
    store.unreadCount = result.unreadCount
  } catch {
    error.value = 'Failed to load notifications. Please try again.'
  } finally {
    loading.value = false
  }
}

async function loadMore() {
  if (hasMore.value && !loading.value) {
    await loadPage(page.value + 1)
  }
}

function setFilter(f: FilterCategory) {
  if (f === activeFilter.value) return
  activeFilter.value = f
  loadPage(1)
}

// ── Mark as read ─────────────────────────────────────────────────────────────

async function markAllRead() {
  items.value.forEach((n) => (n.isRead = true))
  store.unreadCount = 0
  try {
    await store.markAllRead()
  } catch {
    // Store's markAllRead already handles revert
  }
}

// ── Click handler ─────────────────────────────────────────────────────────────

async function onClick(n: Notification) {
  if (!n.isRead) await store.markRead(n.id)
  const route = targetRoute(n)
  if (route) router.push(route)
}

// ── Real-time ─────────────────────────────────────────────────────────────────

// When the SignalR hub pushes a new notification, prepend it to this page's
// list if it matches the active filter.
watch(
  () => store.lastReceived,
  (n) => {
    if (!n) return
    if (!notificationMatchesFilter(n)) return
    if (items.value.some((x) => x.id === n.id)) return
    items.value.unshift(n)
  },
)

// ── Lifecycle ────────────────────────────────────────────────────────────────

onMounted(() => {
  loadPage(1)
})

// ── Display helpers (shared with bell dropdown) ───────────────────────────────

function describe(n: Notification): string {
  const who = n.actor?.displayName ?? 'Someone'
  switch (n.type as NotificationType) {
    case 'FriendRequest':         return `${who} sent you a friend request`
    case 'FriendRequestAccepted': return `${who} accepted your friend request`
    case 'NewPostFromFriend':     return `${who} shared a new post`
    case 'EventCreatedByFriend':  return `${who} created an event`
    case 'LiveStreamStarted':     return `${who} is live now`
    case 'MentionInPost':         return `${who} mentioned you in a post`
    case 'MentionInComment':      return `${who} mentioned you in a comment`
    case 'TaggedInPost':          return `${who} tagged you in a post`
    case 'PostLike':              return `${who} liked your post`
    case 'PostComment':           return `${who} commented on your post`
    case 'PostShare':             return `${who} shared your post`
    case 'CommentLike':           return `${who} liked your comment`
    case 'CommentReply':          return `${who} replied to your comment`
    case 'EventInvite':           return `${who} invited you to an event`
    case 'EventReminder':         return 'You have an upcoming event'
    case 'NewMessage':
    case 'MessageReceived':       return `${who} sent you a message`
    case 'NewFollower':           return `${who} started following you`
    case 'FamilyRelationRequest': return `${who} marked you as family`
    case 'CompanyPageInvite':     return `${who} invited you to manage a page`
    default:                       return n.title ?? 'New notification'
  }
}

function targetRoute(n: Notification): string | null {
  switch (n.type as NotificationType) {
    case 'FriendRequestAccepted':
      // Once accepted, the friend request flow is done — jump straight to
      // the new friend's wall instead of the friends list.
      return n.actor?.id ? `/profile/${n.actor.id}` : '/friends'
    case 'FriendRequest':
    case 'NewFollower':
    case 'FamilyRelationRequest':
      return '/friends'
    case 'NewPostFromFriend':
    case 'PostLike':
    case 'PostComment':
    case 'PostShare':
    case 'MentionInPost':
    case 'TaggedInPost':
      return n.referenceId ? `/share/posts/${n.referenceId}` : null
    case 'CommentLike':
    case 'CommentReply':
    case 'MentionInComment':
      return n.referenceId ? `/share/posts/${n.referenceId}` : null
    case 'EventCreatedByFriend':
    case 'EventInvite':
    case 'EventReminder':
      return n.referenceId ? `/events/${n.referenceId}` : '/events'
    case 'LiveStreamStarted':
      return n.referenceId ? `/live/${n.referenceId}` : '/live'
    case 'MessageReceived':
    case 'NewMessage':
      return null // handled by the messenger popup
    default:
      return null
  }
}

function timeAgo(iso: string): string {
  const t = new Date(iso).getTime()
  if (isNaN(t)) return ''
  const diffSec = Math.max(0, Math.floor((Date.now() - t) / 1000))
  if (diffSec < 60)      return 'just now'
  if (diffSec < 3600)    return `${Math.floor(diffSec / 60)}m ago`
  if (diffSec < 86_400)  return `${Math.floor(diffSec / 3600)}h ago`
  return `${Math.floor(diffSec / 86_400)}d ago`
}

const hasUnread = computed(() => items.value.some((n) => !n.isRead))
</script>

<template>
  <div class="space-y-4">
    <!-- Page header -->
    <div class="flex items-center justify-between">
      <h1 class="text-2xl font-bold text-slate-100">Notifications</h1>
      <button
        v-if="hasUnread"
        class="text-sm font-medium text-indigo-400 hover:text-indigo-300 transition-colors"
        @click="markAllRead"
      >
        Mark all as read
      </button>
    </div>

    <!-- Filter bar -->
    <div class="flex gap-1 overflow-x-auto bg-slate-900 p-1 rounded-xl text-sm no-scrollbar">
      <button
        v-for="key in FILTER_KEYS"
        :key="key"
        :class="[
          'shrink-0 py-1.5 px-3 rounded-lg font-medium transition-colors whitespace-nowrap',
          activeFilter === key
            ? 'bg-slate-800 shadow text-indigo-400'
            : 'text-gray-500 hover:text-slate-300',
        ]"
        @click="setFilter(key)"
      >
        {{ FILTERS[key].label }}
      </button>
    </div>

    <!-- Error state -->
    <div v-if="error" class="bg-red-950 border border-red-100 text-red-400 text-sm rounded-xl px-4 py-3">
      {{ error }}
      <button class="underline ml-2" @click="loadPage(1)">Retry</button>
    </div>

    <!-- Loading skeleton (first page only) -->
    <div v-else-if="loading && items.length === 0" class="bg-slate-800 rounded-xl shadow-sm border border-slate-700 divide-y divide-slate-700">
      <div
        v-for="i in 5"
        :key="i"
        class="px-4 py-4 flex items-start gap-3 animate-pulse"
      >
        <div class="h-10 w-10 rounded-full bg-slate-700 shrink-0" />
        <div class="flex-1 space-y-2 pt-1">
          <div class="h-4 bg-slate-700 rounded w-3/4" />
          <div class="h-3 bg-slate-700 rounded w-1/4" />
        </div>
      </div>
    </div>

    <!-- Empty state -->
    <div
      v-else-if="!loading && items.length === 0"
      class="bg-slate-800 rounded-xl shadow-sm border border-slate-700 text-center py-16"
    >
      <p class="text-4xl mb-3">🔔</p>
      <p class="font-semibold text-slate-300">No notifications yet</p>
      <p class="text-sm text-gray-500 mt-1">
        {{ activeFilter === 'all' ? "You're all caught up!" : 'None in this category.' }}
      </p>
    </div>

    <!-- Notification list -->
    <div v-else class="bg-slate-800 rounded-xl shadow-sm border border-slate-700 overflow-hidden">
      <ul class="divide-y divide-slate-700">
        <li
          v-for="n in items"
          :key="n.id"
          class="px-4 py-3.5 flex items-start gap-3 transition-colors hover:bg-slate-700 cursor-pointer"
          :class="{ 'bg-indigo-900/30': !n.isRead }"
          @click="onClick(n)"
        >
          <!-- Avatar -->
          <div class="shrink-0 relative">
            <img
              v-if="n.actor?.avatarUrl"
              :src="n.actor.avatarUrl"
              :alt="n.actor.displayName"
              class="h-10 w-10 rounded-full object-cover"
            />
            <div
              v-else
              class="h-10 w-10 rounded-full bg-indigo-900 text-indigo-400 flex items-center justify-center font-bold text-sm"
            >
              {{ n.actor?.displayName?.[0]?.toUpperCase() ?? '?' }}
            </div>
          </div>

          <!-- Body -->
          <div class="flex-1 min-w-0">
            <p class="text-sm text-slate-100">{{ describe(n) }}</p>
            <p class="text-xs text-gray-500 mt-0.5">{{ timeAgo(n.createdAt) }}</p>
          </div>

          <!-- Unread indicator dot -->
          <span
            v-if="!n.isRead"
            class="mt-2 h-2 w-2 rounded-full bg-indigo-500 shrink-0"
            aria-label="Unread"
          />
        </li>
      </ul>

      <!-- Load more -->
      <div v-if="hasMore" class="px-4 py-3 border-t border-slate-700 text-center">
        <button
          class="text-sm font-medium text-indigo-400 hover:text-indigo-300 disabled:text-slate-500 transition-colors"
          :disabled="loading"
          @click="loadMore"
        >
          {{ loading ? 'Loading…' : 'Show more' }}
        </button>
      </div>
    </div>
  </div>
</template>
