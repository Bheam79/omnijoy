<script setup lang="ts">
/**
 * CommentItem
 *
 * Renders a single comment (or reply).  Handles:
 *  - Author avatar + name + relative timestamp
 *  - Content (masked "[deleted]" for soft-deleted comments)
 *  - Reply button (only shown for top-level comments)
 *  - Edit / delete actions (only for comment owner)
 *  - Inline edit form
 *  - Inline reply composer (for top-level comments)
 *  - Nested reply list with "Show N replies" / "Hide replies" toggle
 */
import { ref, computed } from 'vue'
import type { CommentDto } from '@/services/commentService'
import { useAuthStore } from '@/stores/auth'
import { useCommentsStore } from '@/stores/comments'
import CommentComposer from './CommentComposer.vue'

const props = defineProps<{
  comment: CommentDto
  /** When true this is already a reply — suppress nested reply button */
  isReply?: boolean
}>()

const auth = useAuthStore()
const store = useCommentsStore()

// ── Derived state ─────────────────────────────────────────────────────────────

const isOwn = computed(() => auth.user?.id === props.comment.author.id)
const isDeleted = computed(() => props.comment.isDeleted)
const postState = computed(() => store.byPost[props.comment.postId])

const replies = computed(
  () => postState.value?.replies[props.comment.id] ?? [],
)
const repliesLoaded = computed(
  () => props.comment.id in (postState.value?.replies ?? {}),
)
const repliesLoading = computed(
  () => postState.value?.repliesLoading[props.comment.id] ?? false,
)

// ── UI state ──────────────────────────────────────────────────────────────────

const showReplies = ref(false)
const showReplyComposer = ref(false)
const replyPosting = ref(false)

const editing = ref(false)
const editText = ref('')
const editPosting = ref(false)

// ── Helpers ───────────────────────────────────────────────────────────────────

function formatRelativeTime(iso: string): string {
  const diff = (Date.now() - new Date(iso).getTime()) / 1000
  if (diff < 60) return 'just now'
  if (diff < 3600) return `${Math.floor(diff / 60)}m`
  if (diff < 86400) return `${Math.floor(diff / 3600)}h`
  if (diff < 604800) return `${Math.floor(diff / 86400)}d`
  return new Date(iso).toLocaleDateString(undefined, { month: 'short', day: 'numeric' })
}

// ── Reply actions ─────────────────────────────────────────────────────────────

async function toggleReplies() {
  if (!showReplies.value) {
    showReplies.value = true
    if (!repliesLoaded.value) {
      await store.loadReplies(props.comment.postId, props.comment.id)
    }
  } else {
    showReplies.value = false
  }
}

async function submitReply(content: string) {
  replyPosting.value = true
  try {
    await store.createComment(props.comment.postId, {
      content,
      parentCommentId: props.comment.id,
    })
    showReplyComposer.value = false
    // Make sure replies are visible after posting
    if (!repliesLoaded.value) {
      await store.loadReplies(props.comment.postId, props.comment.id)
    }
    showReplies.value = true
  } finally {
    replyPosting.value = false
  }
}

// ── Edit actions ──────────────────────────────────────────────────────────────

function startEdit() {
  editText.value = props.comment.content
  editing.value = true
}

async function submitEdit() {
  if (!editText.value.trim()) return
  editPosting.value = true
  try {
    await store.updateComment(props.comment.postId, props.comment.id, editText.value.trim())
    editing.value = false
  } finally {
    editPosting.value = false
  }
}

function cancelEdit() {
  editing.value = false
  editText.value = ''
}

// ── Delete action ─────────────────────────────────────────────────────────────

async function handleDelete() {
  if (!confirm('Delete this comment?')) return
  await store.deleteComment(props.comment.postId, props.comment.id)
}
</script>

<template>
  <div data-testid="comment-item" class="flex gap-2" :class="isReply ? 'ml-9' : ''">
    <!-- Avatar -->
    <div class="shrink-0 mt-0.5">
      <img
        v-if="comment.author.avatarUrl"
        :src="comment.author.avatarUrl"
        :alt="comment.author.displayName"
        class="rounded-full object-cover"
        :class="isReply ? 'w-7 h-7' : 'w-8 h-8'"
      />
      <div
        v-else
        class="rounded-full bg-blue-500 flex items-center justify-center text-white font-semibold"
        :class="isReply ? 'w-7 h-7 text-xs' : 'w-8 h-8 text-sm'"
        aria-hidden="true"
      >
        {{ comment.author.displayName.charAt(0).toUpperCase() }}
      </div>
    </div>

    <div class="flex-1 min-w-0">
      <!-- Comment bubble -->
      <div
        class="inline-block rounded-2xl px-3 py-2 max-w-full"
        :class="isDeleted ? 'bg-slate-950 text-slate-500 italic' : 'bg-slate-700'"
        data-testid="comment-bubble"
      >
        <!-- Author name (hidden for deleted) -->
        <RouterLink
          v-if="!isDeleted"
          :to="`/profile/${comment.author.id}`"
          class="font-semibold text-slate-100 text-xs hover:underline"
        >
          {{ comment.author.displayName }}
        </RouterLink>
        <!-- Inline edit -->
        <div v-if="editing" class="mt-1">
          <textarea
            v-model="editText"
            rows="2"
            data-testid="edit-input"
            class="w-full resize-none rounded-lg bg-slate-800 border border-blue-300 focus:outline-none px-2 py-1 text-sm"
            @keydown.enter.exact.prevent="submitEdit"
            @keydown.esc="cancelEdit"
          />
          <div class="flex gap-1 mt-1">
            <button
              class="text-xs text-gray-500 hover:text-slate-300 px-2 py-0.5 rounded transition"
              @click="cancelEdit"
            >Cancel</button>
            <button
              :disabled="!editText.trim() || editPosting"
              data-testid="edit-save"
              class="text-xs bg-blue-600 text-white font-semibold px-2 py-0.5 rounded-full disabled:opacity-50 transition"
              @click="submitEdit"
            >Save</button>
          </div>
        </div>
        <!-- Content -->
        <p v-else class="text-sm text-slate-200 mt-0.5 whitespace-pre-wrap break-words">
          {{ comment.content }}
        </p>
      </div>

      <!-- Actions row -->
      <div class="flex items-center gap-3 mt-0.5 ml-1" v-if="!isDeleted">
        <!-- Timestamp -->
        <span class="text-xs text-slate-500" :title="new Date(comment.createdAt).toLocaleString()">
          {{ formatRelativeTime(comment.createdAt) }}
        </span>

        <!-- Reply button (top-level only) -->
        <button
          v-if="!isReply && auth.user"
          class="text-xs font-semibold text-gray-500 hover:text-blue-400 transition"
          data-testid="reply-button"
          @click="showReplyComposer = !showReplyComposer"
        >
          Reply
        </button>

        <!-- Edit / Delete (own comment) -->
        <template v-if="isOwn && !editing">
          <button
            class="text-xs font-semibold text-gray-500 hover:text-blue-400 transition"
            data-testid="edit-button"
            @click="startEdit"
          >
            Edit
          </button>
          <button
            class="text-xs font-semibold text-gray-500 hover:text-red-400 transition"
            data-testid="delete-button"
            @click="handleDelete"
          >
            Delete
          </button>
        </template>
      </div>

      <!-- Show N replies toggle -->
      <button
        v-if="!isReply && comment.replyCount > 0"
        class="flex items-center gap-1 text-xs font-semibold text-blue-400 hover:text-blue-300 ml-1 mt-1 transition"
        data-testid="toggle-replies"
        @click="toggleReplies"
      >
        <svg
          class="w-3.5 h-3.5 transition-transform"
          :class="showReplies ? 'rotate-180' : ''"
          fill="none" stroke="currentColor" viewBox="0 0 24 24"
        >
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 9l-7 7-7-7" />
        </svg>
        <span v-if="!showReplies">
          {{ repliesLoading ? 'Loading…' : `Show ${comment.replyCount} ${comment.replyCount === 1 ? 'reply' : 'replies'}` }}
        </span>
        <span v-else>Hide replies</span>
      </button>

      <!-- Reply composer -->
      <div v-if="showReplyComposer && !isReply && auth.user" class="mt-2">
        <CommentComposer
          :avatar-url="auth.user.avatarUrl"
          :display-name="auth.user.displayName"
          placeholder="Write a reply…"
          :loading="replyPosting"
          compact
          @submitted="submitReply"
          @cancelled="showReplyComposer = false"
        />
      </div>

      <!-- Nested replies -->
      <div v-if="showReplies && !isReply" class="mt-2 space-y-2">
        <div v-if="repliesLoading" class="ml-9 text-xs text-slate-500 animate-pulse">Loading replies…</div>
        <CommentItem
          v-for="reply in replies"
          :key="reply.id"
          :comment="reply"
          is-reply
        />
      </div>
    </div>
  </div>
</template>
