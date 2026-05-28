<script setup lang="ts">
import { ref, onMounted, onBeforeUnmount } from 'vue'
import { RouterLink } from 'vue-router'
import { accountService, type NotificationPreferences } from '@/services/accountService'

// ── State ─────────────────────────────────────────────────────────────────────

const prefs = ref<NotificationPreferences | null>(null)
const loading = ref(true)
const saving = ref(false)
const error = ref<string | null>(null)
const lastSavedAt = ref<number | null>(null)

// Debounce timer for auto-save. We coalesce multiple rapid toggles into a
// single PUT — the form is "save on every change", but the network request
// only fires after ~400ms of inactivity.
const SAVE_DELAY_MS = 400
let saveTimer: ReturnType<typeof setTimeout> | null = null

// ── Field grouping (mirrors backend NotificationPreferencesDto) ───────────────

interface Field {
  key: keyof NotificationPreferences
  label: string
  description: string
}

interface Group {
  title: string
  description: string
  fields: Field[]
}

const groups: Group[] = [
  {
    title: 'Post activity',
    description: 'Reactions, comments and shares on content you posted.',
    fields: [
      { key: 'likesOnMyPosts',    label: 'Likes',     description: 'Someone reacts to your post or comment.' },
      { key: 'commentsOnMyPosts', label: 'Comments',  description: 'Someone comments on your post or replies to your comment.' },
      { key: 'postShares',        label: 'Shares',    description: 'Someone shares your post with their network.' },
    ],
  },
  {
    title: 'Social',
    description: 'Friend activity, follows, mentions and tags.',
    fields: [
      { key: 'friendRequests',      label: 'Friend requests',       description: 'New friend requests and accepted requests.' },
      { key: 'newFollower',         label: 'New followers',         description: 'Someone starts following you.' },
      { key: 'mentions',            label: 'Mentions and tags',     description: 'You are @-mentioned or tagged in a post or comment.' },
      { key: 'newPostsFromFriends', label: 'New posts from friends', description: 'A friend publishes a new post.' },
      { key: 'familyRelations',     label: 'Family relations',      description: 'Someone proposes a family relationship link.' },
    ],
  },
  {
    title: 'Events',
    description: 'Event invitations, reminders and creations.',
    fields: [
      { key: 'eventInvites', label: 'Event invites', description: 'Invitations, reminders, and events created by friends.' },
    ],
  },
  {
    title: 'System',
    description: 'Messages, live streams and page activity.',
    fields: [
      { key: 'directMessages',    label: 'Direct messages',     description: 'New messages from other users.' },
      { key: 'liveStreams',       label: 'Live streams',        description: 'A friend or followed page goes live.' },
      { key: 'companyPageInvites', label: 'Company page invites', description: 'You are invited to manage a company page.' },
    ],
  },
]

// ── Lifecycle ─────────────────────────────────────────────────────────────────

onMounted(async () => {
  try {
    prefs.value = await accountService.getNotificationPreferences()
  } catch {
    error.value = 'Failed to load notification preferences.'
  } finally {
    loading.value = false
  }
})

onBeforeUnmount(() => {
  if (saveTimer) clearTimeout(saveTimer)
})

// ── Auto-save ─────────────────────────────────────────────────────────────────

function scheduleSave() {
  if (!prefs.value) return
  if (saveTimer) clearTimeout(saveTimer)
  saveTimer = setTimeout(save, SAVE_DELAY_MS)
}

async function save() {
  if (!prefs.value) return
  saving.value = true
  error.value = null
  try {
    prefs.value = await accountService.updateNotificationPreferences({ ...prefs.value })
    lastSavedAt.value = Date.now()
  } catch (e: unknown) {
    const ax = e as { response?: { data?: { error?: string } } }
    error.value = ax.response?.data?.error ?? 'Failed to save notification preferences.'
  } finally {
    saving.value = false
  }
}

function onToggle() {
  scheduleSave()
}
</script>

<template>
  <div>
    <!-- Back link -->
    <div class="flex items-center gap-2 mb-6">
      <RouterLink
        to="/settings"
        class="flex items-center gap-1.5 text-sm text-gray-500 hover:text-gray-700 transition-colors"
      >
        <svg class="h-4 w-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 19l-7-7 7-7" />
        </svg>
        Settings
      </RouterLink>
    </div>

    <div class="flex items-start justify-between mb-6 gap-4">
      <div>
        <h1 class="text-2xl font-bold text-gray-900 mb-1">Notifications</h1>
        <p class="text-sm text-gray-500">Choose which events should ping you. Changes save automatically.</p>
      </div>

      <!-- Save indicator (spinner during PUT, subtle "Saved" tick afterwards) -->
      <div class="flex items-center gap-2 text-xs text-gray-500 h-5" data-testid="save-status">
        <template v-if="saving">
          <div
            class="h-4 w-4 border-2 border-indigo-500 border-t-transparent rounded-full animate-spin"
            data-testid="save-spinner"
          />
          <span>Saving…</span>
        </template>
        <template v-else-if="lastSavedAt">
          <svg class="h-4 w-4 text-green-500" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M5 13l4 4L19 7" />
          </svg>
          <span data-testid="saved-indicator">Saved</span>
        </template>
      </div>
    </div>

    <!-- Loading skeleton -->
    <div v-if="loading" class="flex justify-center py-12">
      <div class="h-7 w-7 border-2 border-indigo-500 border-t-transparent rounded-full animate-spin" />
    </div>

    <div
      v-if="!loading && error"
      class="mb-5 rounded-lg bg-red-50 border border-red-200 px-4 py-3 text-sm text-red-700"
      data-testid="error-banner"
    >
      {{ error }}
    </div>

    <template v-if="!loading && prefs">
      <section
        v-for="group in groups"
        :key="group.title"
        class="bg-white rounded-xl border border-gray-100 px-5 py-5 mb-5"
        :data-testid="`group-${group.title}`"
      >
        <h2 class="text-base font-semibold text-gray-900 mb-1">{{ group.title }}</h2>
        <p class="text-xs text-gray-500 mb-4">{{ group.description }}</p>

        <div class="divide-y divide-gray-100">
          <label
            v-for="field in group.fields"
            :key="field.key"
            class="flex items-center justify-between gap-4 py-3 cursor-pointer"
            :data-testid="`row-${field.key}`"
          >
            <div class="min-w-0">
              <p class="text-sm font-medium text-gray-900">{{ field.label }}</p>
              <p class="text-xs text-gray-500">{{ field.description }}</p>
            </div>

            <!-- Toggle switch -->
            <span class="relative inline-flex shrink-0">
              <input
                type="checkbox"
                class="sr-only peer"
                :data-testid="`toggle-${field.key}`"
                :checked="prefs[field.key]"
                @change="(e) => { prefs![field.key] = (e.target as HTMLInputElement).checked; onToggle() }"
              />
              <span
                class="w-10 h-6 rounded-full bg-gray-200 peer-checked:bg-indigo-600 transition-colors"
              />
              <span
                class="absolute top-0.5 left-0.5 h-5 w-5 rounded-full bg-white shadow transition-transform peer-checked:translate-x-4"
              />
            </span>
          </label>
        </div>
      </section>
    </template>
  </div>
</template>
