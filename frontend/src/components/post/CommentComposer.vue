<script setup lang="ts">
/**
 * CommentComposer
 *
 * A textarea + submit button for writing a new comment or reply.
 * Emits 'submitted' with the trimmed content string on success.
 *
 * Props:
 *   placeholder – textarea placeholder text
 *   loading     – shows spinner and disables input while parent is posting
 *   avatarUrl   – current user avatar (optional)
 *   compact     – smaller layout used inside reply threads
 */
import { ref, computed } from 'vue'

const props = withDefaults(
  defineProps<{
    placeholder?: string
    loading?: boolean
    avatarUrl?: string
    displayName?: string
    compact?: boolean
  }>(),
  {
    placeholder: 'Write a comment…',
    loading: false,
    compact: false,
  },
)

const emit = defineEmits<{
  submitted: [content: string]
  cancelled: []
}>()

const text = ref('')
const canSubmit = computed(() => text.value.trim().length > 0 && !props.loading)

function onKeydown(e: KeyboardEvent) {
  if (e.key === 'Enter' && !e.shiftKey) {
    e.preventDefault()
    submit()
  }
}

function submit() {
  if (!canSubmit.value) return
  emit('submitted', text.value.trim())
  text.value = ''
}

function cancel() {
  text.value = ''
  emit('cancelled')
}

// Expose clear() so parent can reset on success
defineExpose({ clear: () => { text.value = '' } })
</script>

<template>
  <div class="flex items-start gap-2" :class="compact ? 'py-1' : 'py-2'">
    <!-- Author avatar -->
    <div class="shrink-0" :class="compact ? 'mt-1' : ''">
      <img
        v-if="avatarUrl"
        :src="avatarUrl"
        :alt="displayName ?? 'You'"
        class="rounded-full object-cover"
        :class="compact ? 'w-7 h-7' : 'w-8 h-8'"
      />
      <div
        v-else
        class="rounded-full bg-blue-500 flex items-center justify-center text-white font-semibold"
        :class="compact ? 'w-7 h-7 text-xs' : 'w-8 h-8 text-sm'"
        aria-hidden="true"
      >
        {{ (displayName ?? 'U').charAt(0).toUpperCase() }}
      </div>
    </div>

    <!-- Input area -->
    <div class="flex-1 relative">
      <textarea
        v-model="text"
        :placeholder="placeholder"
        :disabled="loading"
        rows="1"
        data-testid="comment-input"
        class="w-full resize-none rounded-2xl bg-slate-700 border border-transparent focus:border-blue-300 focus:bg-slate-800 focus:outline-none transition px-3 py-2 text-sm leading-snug disabled:opacity-60"
        :class="compact ? 'text-xs' : ''"
        @keydown="onKeydown"
      />

      <!-- Submit button (appears when text is entered) -->
      <div v-if="text.trim().length > 0" class="flex items-center gap-1 mt-1 justify-end">
        <button
          type="button"
          class="text-xs text-gray-500 hover:text-slate-300 px-2 py-1 rounded transition"
          @click="cancel"
        >
          Cancel
        </button>
        <button
          type="button"
          :disabled="!canSubmit"
          data-testid="comment-submit"
          class="flex items-center gap-1.5 bg-blue-600 hover:bg-blue-700 disabled:opacity-50 text-white text-xs font-semibold px-3 py-1 rounded-full transition"
          @click="submit"
        >
          <!-- Spinner while posting -->
          <svg
            v-if="loading"
            class="animate-spin w-3 h-3"
            fill="none"
            viewBox="0 0 24 24"
            aria-hidden="true"
          >
            <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4" />
            <path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z" />
          </svg>
          {{ loading ? 'Posting…' : 'Post' }}
        </button>
      </div>
    </div>
  </div>
</template>
