<script setup lang="ts">
/**
 * Notification bell + dropdown panel for the TopNav.
 *
 * - Badge: shows the unread count (capped at "9+").
 * - Click: toggles a dropdown; opening triggers a fresh fetch.
 * - Mark-as-read: clicking an unread row + a "Mark all read" button.
 * - The bell pulls its state from the notifications Pinia store, which owns
 *   the SignalR connection — this component is purely presentational.
 */
import { computed } from 'vue'
import { useRouter } from 'vue-router'
import { useNotificationsStore } from '@/stores/notifications'
import type { Notification, NotificationType } from '@/types'

const router = useRouter()
const store  = useNotificationsStore()

const badge = computed(() =>
  store.unreadCount > 9 ? '9+' : String(store.unreadCount),
)

function close() {
  store.closeDropdown()
}

// ── Per-type display helpers ─────────────────────────────────────────────────

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
    case 'FriendRequest':
    case 'FriendRequestAccepted':
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

async function onClick(n: Notification) {
  if (!n.isRead) await store.markRead(n.id)
  const route = targetRoute(n)
  close()
  if (route) router.push(route)
}

function timeAgo(iso: string): string {
  const t = new Date(iso).getTime()
  if (isNaN(t)) return ''
  const diffSec = Math.max(0, Math.floor((Date.now() - t) / 1000))
  if (diffSec < 60)   return 'just now'
  if (diffSec < 3600) return `${Math.floor(diffSec / 60)}m`
  if (diffSec < 86_400) return `${Math.floor(diffSec / 3600)}h`
  return `${Math.floor(diffSec / 86_400)}d`
}
</script>

<template>
  <!-- Click-catcher to close the dropdown when clicking outside -->
  <div
    v-if="store.dropdownOpen"
    class="fixed inset-0 z-40"
    @click="close"
  />

  <div class="relative z-50">
    <button
      class="relative p-2 rounded-full text-slate-400 hover:bg-slate-700 transition-colors"
      :aria-label="`Notifications, ${store.unreadCount} unread`"
      :aria-expanded="store.dropdownOpen"
      @click.stop="store.toggleDropdown()"
    >
      <svg class="h-6 w-6" fill="none" stroke="currentColor" viewBox="0 0 24 24">
        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
          d="M15 17h5l-1.405-1.405A2.032 2.032 0 0118 14.158V11a6.002 6.002 0 00-4-5.659V5a2 2 0 10-4 0v.341C7.67 6.165 6 8.388 6 11v3.159c0 .538-.214 1.055-.595 1.436L4 17h5m6 0v1a3 3 0 11-6 0v-1m6 0H9"/>
      </svg>
      <span
        v-if="store.unreadCount > 0"
        class="absolute top-0.5 right-0.5 min-w-[1.1rem] h-[1.1rem] rounded-full bg-red-500 ring-2 ring-slate-900 flex items-center justify-center text-white text-[10px] font-bold px-0.5"
      >
        {{ badge }}
      </span>
    </button>

    <Transition
      enter-active-class="transition ease-out duration-100 origin-top-right"
      enter-from-class="opacity-0 scale-95"
      enter-to-class="opacity-100 scale-100"
      leave-active-class="transition ease-in duration-75 origin-top-right"
      leave-from-class="opacity-100 scale-100"
      leave-to-class="opacity-0 scale-95"
    >
      <div
        v-if="store.dropdownOpen"
        class="absolute right-0 top-full mt-1 w-80 max-w-[calc(100vw-2rem)] bg-slate-800 rounded-xl shadow-lg border border-slate-700 overflow-hidden flex flex-col"
        style="max-height: min(70vh, 32rem)"
      >
        <!-- Header -->
        <div class="flex items-center justify-between px-4 py-3 border-b border-slate-700">
          <p class="text-sm font-semibold text-slate-100">Notifications</p>
          <button
            v-if="store.hasUnread"
            class="text-xs font-medium text-indigo-400 hover:text-indigo-300 transition-colors"
            @click.stop="store.markAllRead()"
          >
            Mark all read
          </button>
        </div>

        <!-- List -->
        <div class="flex-1 overflow-y-auto">
          <template v-if="store.notifications.length === 0">
            <div v-if="store.loading" class="px-4 py-6 text-center text-sm text-gray-500">
              Loading…
            </div>
            <div v-else class="px-4 py-8 text-center text-sm text-gray-500">
              You're all caught up.
            </div>
          </template>

          <ul v-else class="divide-y divide-slate-700">
            <li
              v-for="n in store.notifications"
              :key="n.id"
              class="px-4 py-3 hover:bg-slate-700 cursor-pointer transition-colors flex items-start gap-3"
              :class="{ 'bg-indigo-900/30': !n.isRead }"
              @click.stop="onClick(n)"
            >
              <!-- Avatar -->
              <div
                v-if="n.actor?.avatarUrl"
                class="h-9 w-9 rounded-full overflow-hidden flex-shrink-0"
              >
                <img :src="n.actor.avatarUrl" :alt="n.actor.displayName" class="h-full w-full object-cover" />
              </div>
              <div
                v-else
                class="h-9 w-9 rounded-full bg-indigo-900 text-indigo-400 flex items-center justify-center text-sm font-bold flex-shrink-0"
              >
                {{ n.actor?.displayName?.[0]?.toUpperCase() ?? '?' }}
              </div>

              <!-- Body -->
              <div class="flex-1 min-w-0">
                <p class="text-sm text-slate-100 truncate">{{ describe(n) }}</p>
                <p class="text-xs text-gray-500 mt-0.5">{{ timeAgo(n.createdAt) }}</p>
              </div>

              <!-- Unread dot -->
              <span
                v-if="!n.isRead"
                class="mt-1.5 h-2 w-2 rounded-full bg-indigo-500 flex-shrink-0"
                aria-label="Unread"
              />
            </li>
          </ul>

          <button
            v-if="store.hasMore && store.notifications.length > 0"
            class="w-full px-4 py-2 text-sm text-indigo-400 hover:bg-slate-700 transition-colors disabled:text-slate-500"
            :disabled="store.loading"
            @click.stop="store.loadMore()"
          >
            {{ store.loading ? 'Loading…' : 'Show more' }}
          </button>
        </div>
      </div>
    </Transition>
  </div>
</template>
