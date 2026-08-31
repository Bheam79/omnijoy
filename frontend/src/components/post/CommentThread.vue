<script setup lang="ts">
/**
 * CommentThread
 *
 * Rendered below a PostCard.  Collapsed by default.
 * Clicking "Comment" in the actions bar expands it.
 *
 * Props:
 *   postId   – GUID of the post
 *   expanded – initial expanded state (optional, two-way via v-model)
 */
import { ref, computed, watch, nextTick, onMounted, onUnmounted } from 'vue'
import * as signalR from '@microsoft/signalr'
import { useAuthStore } from '@/stores/auth'
import { useCommentsStore } from '@/stores/comments'
import CommentComposer from './CommentComposer.vue'
import CommentItem from './CommentItem.vue'
import type { CommentReactionCountsUpdatedEvent } from '@/services/reactionService'

const props = defineProps<{
  postId: string
  expanded?: boolean
}>()

const emit = defineEmits<{
  'update:expanded': [value: boolean]
}>()

const auth = useAuthStore()
const store = useCommentsStore()

const open = ref(props.expanded ?? false)
const composerPosting = ref(false)
let hubConnection: signalR.HubConnection | null = null

const state = computed(() => store.byPost[props.postId])
const comments = computed(() => state.value?.items ?? [])
const hasMore = computed(() => state.value?.hasMore ?? false)
const loading = computed(() => state.value?.loading ?? false)

// ── Sync external expanded prop ───────────────────────────────────────────────

watch(
  () => props.expanded,
  (val) => {
    if (val !== undefined && val !== open.value) {
      open.value = val
    }
  },
)

watch(open, (val) => {
  emit('update:expanded', val)
  if (val) connectReactionUpdates()
  else disconnectReactionUpdates()
})

onMounted(() => {
  if (open.value) connectReactionUpdates()
})
onUnmounted(disconnectReactionUpdates)

// ── Real-time reaction updates ────────────────────────────────────────────────────────────

async function connectReactionUpdates() {
  if (hubConnection || !auth.accessToken) return

  const connection = new signalR.HubConnectionBuilder()
    .withUrl('/hubs/feed', { accessTokenFactory: () => auth.accessToken ?? '' })
    .withAutomaticReconnect()
    .configureLogging(signalR.LogLevel.Warning)
    .build()

  connection.on('CommentReactionCountsUpdated', (event: CommentReactionCountsUpdatedEvent) => {
    if (event.postId === props.postId) store.applyReactionUpdate(event)
  })
  connection.onreconnected(() => connection.invoke('SubscribeToPost', props.postId))

  hubConnection = connection
  try {
    await connection.start()
    if (hubConnection !== connection) {
      await connection.stop()
      return
    }
    await connection.invoke('SubscribeToPost', props.postId)
  } catch {
    await connection.stop().catch(() => undefined)
    if (hubConnection === connection) hubConnection = null
  }
}

function disconnectReactionUpdates() {
  const connection = hubConnection
  hubConnection = null
  connection?.stop().catch(() => undefined)
}

// ── Toggle / load on first open ───────────────────────────────────────────────

async function toggleOpen() {
  open.value = !open.value
  if (open.value && !state.value) {
    await store.loadComments(props.postId)
  }
}

// Exposed for PostCard to call directly (when user clicks the Comment button)
async function openThread() {
  if (!open.value) {
    open.value = true
    if (!state.value) {
      await store.loadComments(props.postId)
    }
    // Focus composer on next tick
    await nextTick()
    composerRef.value?.$el?.querySelector('textarea')?.focus()
  }
}

defineExpose({ openThread, toggleOpen })

// ── Composer ──────────────────────────────────────────────────────────────────

const composerRef = ref<InstanceType<typeof CommentComposer> | null>(null)

async function submitComment(content: string) {
  composerPosting.value = true
  try {
    await store.createComment(props.postId, { content })
  } finally {
    composerPosting.value = false
  }
}
</script>

<template>
  <div data-testid="comment-thread">
    <!-- Toggle bar -->
    <div class="px-4 py-1.5 border-t border-slate-700">
      <button
        class="flex items-center gap-1.5 text-xs font-semibold text-gray-500 hover:text-blue-400 transition"
        data-testid="toggle-thread"
        @click="toggleOpen"
      >
        <svg
          class="w-3.5 h-3.5 transition-transform"
          :class="open ? 'rotate-180' : ''"
          fill="none" stroke="currentColor" viewBox="0 0 24 24"
        >
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 9l-7 7-7-7" />
        </svg>
        <span v-if="!open">
          <span v-if="comments.length > 0">{{ comments.length }} comment{{ comments.length !== 1 ? 's' : '' }}</span>
          <span v-else>Comments</span>
        </span>
        <span v-else>Hide comments</span>
      </button>
    </div>

    <!-- Expanded panel -->
    <div v-if="open" class="px-4 pb-3 space-y-3">
      <!-- Composer (for logged-in users) -->
      <CommentComposer
        v-if="auth.user"
        ref="composerRef"
        :avatar-url="auth.user.avatarUrl"
        :display-name="auth.user.displayName"
        :loading="composerPosting"
        @submitted="submitComment"
      />

      <!-- Loading skeleton -->
      <div v-if="loading && comments.length === 0" class="space-y-2">
        <div
          v-for="i in 3"
          :key="i"
          class="flex gap-2 animate-pulse"
        >
          <div class="w-8 h-8 rounded-full bg-slate-700 shrink-0" />
          <div class="flex-1 space-y-1 pt-1">
            <div class="h-3 bg-slate-700 rounded w-1/4" />
            <div class="h-3 bg-slate-700 rounded w-3/4" />
          </div>
        </div>
      </div>

      <!-- Comments list -->
      <div v-else-if="comments.length > 0" class="space-y-3">
        <CommentItem
          v-for="comment in comments"
          :key="comment.id"
          :comment="comment"
        />
      </div>

      <!-- Empty state -->
      <p
        v-else-if="!loading"
        class="text-sm text-slate-500 text-center py-2"
        data-testid="empty-comments"
      >
        No comments yet. Be the first to comment!
      </p>

      <!-- Load more -->
      <div v-if="hasMore && comments.length > 0" class="text-center pt-1">
        <button
          :disabled="loading"
          class="text-sm font-semibold text-blue-400 hover:text-blue-300 disabled:opacity-50 transition"
          data-testid="load-more"
          @click="store.loadMore(postId)"
        >
          {{ loading ? 'Loading…' : 'Load more comments' }}
        </button>
      </div>
    </div>
  </div>
</template>
