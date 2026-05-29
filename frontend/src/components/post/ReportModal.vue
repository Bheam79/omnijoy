<script setup lang="ts">
import { ref } from 'vue'
import {
  reportService,
  type ReportTargetType,
  type ReportReason,
} from '@/services/reportService'

// ── Props / emits ─────────────────────────────────────────────────────────────

const props = defineProps<{
  modelValue: boolean
  targetType: ReportTargetType
  targetId: string
}>()

const emit = defineEmits<{
  'update:modelValue': [value: boolean]
  reported: []
}>()

// ── State ─────────────────────────────────────────────────────────────────────

const reason = ref<ReportReason>('Spam')
const notes = ref('')
const submitting = ref(false)
const submitted = ref(false)
const error = ref<string | null>(null)

const reasonOptions: { value: ReportReason; label: string }[] = [
  { value: 'Spam',           label: 'Spam' },
  { value: 'Harassment',     label: 'Harassment' },
  { value: 'Misinformation', label: 'Misinformation' },
  { value: 'Nudity',         label: 'Nudity' },
  { value: 'Violence',       label: 'Violence' },
  { value: 'Other',          label: 'Other' },
]

// ── Submit ────────────────────────────────────────────────────────────────────

async function submit() {
  if (submitting.value) return

  submitting.value = true
  error.value = null

  try {
    await reportService.submitReport({
      targetType: props.targetType,
      targetId:   props.targetId,
      reason:     reason.value,
      notes:      notes.value.trim() || undefined,
    })
    submitted.value = true
    emit('reported')
  } catch (e: unknown) {
    error.value = extractError(e)
  } finally {
    submitting.value = false
  }
}

function close() {
  emit('update:modelValue', false)
  // Reset state after close animation completes
  setTimeout(() => {
    reason.value   = 'Spam'
    notes.value    = ''
    submitted.value = false
    error.value    = null
  }, 150)
}

function extractError(e: unknown): string {
  if (typeof e === 'object' && e !== null) {
    const ax = e as { response?: { data?: { error?: string } }; message?: string }
    return ax.response?.data?.error ?? ax.message ?? 'An unexpected error occurred.'
  }
  return 'An unexpected error occurred.'
}

const targetLabel: Record<ReportTargetType, string> = {
  Post:    'post',
  User:    'user',
  Comment: 'comment',
}
</script>

<template>
  <Teleport to="body">
    <div
      v-if="modelValue"
      class="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4"
      @click.self="close"
    >
      <div
        class="bg-slate-800 rounded-2xl shadow-xl w-full max-w-md"
        role="dialog"
        aria-modal="true"
        :aria-label="`Report ${targetLabel[targetType]}`"
      >
        <!-- Header -->
        <div class="flex items-center justify-between px-5 py-4 border-b border-slate-700">
          <h2 class="text-lg font-semibold text-slate-100">
            Report {{ targetLabel[targetType] }}
          </h2>
          <button
            class="text-slate-500 hover:text-slate-400 p-1 rounded-full hover:bg-slate-700 transition"
            aria-label="Close"
            @click="close"
          >
            <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12"/>
            </svg>
          </button>
        </div>

        <!-- Confirmation state -->
        <div v-if="submitted" class="px-5 py-8 text-center">
          <div class="inline-flex items-center justify-center w-12 h-12 rounded-full bg-green-100 mb-3">
            <svg class="w-6 h-6 text-green-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M5 13l4 4L19 7"/>
            </svg>
          </div>
          <p class="text-slate-100 font-medium mb-1">Report submitted</p>
          <p class="text-sm text-gray-500 mb-5">
            Thank you for helping keep the community safe. We'll review your report shortly.
          </p>
          <button
            class="px-5 py-2 text-sm font-semibold text-white bg-blue-600 hover:bg-blue-700 rounded-lg transition"
            @click="close"
          >
            Done
          </button>
        </div>

        <!-- Form state -->
        <template v-else>
          <div class="px-5 py-4 space-y-4">
            <!-- Reason dropdown -->
            <div>
              <label class="block text-sm font-medium text-slate-300 mb-1">
                Reason <span class="text-red-500">*</span>
              </label>
              <select
                v-model="reason"
                class="w-full border border-slate-600 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
                aria-label="Report reason"
              >
                <option
                  v-for="opt in reasonOptions"
                  :key="opt.value"
                  :value="opt.value"
                >
                  {{ opt.label }}
                </option>
              </select>
            </div>

            <!-- Optional notes -->
            <div>
              <label class="block text-sm font-medium text-slate-300 mb-1">
                Additional details
                <span class="font-normal text-slate-500">(optional)</span>
              </label>
              <textarea
                v-model="notes"
                rows="3"
                maxlength="2000"
                placeholder="Tell us more about why you're reporting this…"
                class="w-full border border-slate-600 rounded-lg px-3 py-2 text-sm resize-none focus:outline-none focus:ring-2 focus:ring-blue-500"
                aria-label="Report notes"
              />
            </div>

            <!-- Error -->
            <p
              v-if="error"
              class="text-sm text-red-400 bg-red-950 border border-red-800 rounded-lg px-3 py-2"
            >
              {{ error }}
            </p>
          </div>

          <!-- Footer -->
          <div class="px-5 pb-4 flex justify-end gap-2">
            <button
              class="px-4 py-2 text-sm font-medium text-slate-300 hover:bg-slate-700 rounded-lg transition"
              @click="close"
            >
              Cancel
            </button>
            <button
              class="px-5 py-2 text-sm font-semibold text-white bg-red-600 hover:bg-red-700 rounded-lg transition disabled:opacity-50 disabled:cursor-not-allowed"
              :disabled="submitting"
              @click="submit"
            >
              <span v-if="submitting">Submitting…</span>
              <span v-else>Submit report</span>
            </button>
          </div>
        </template>
      </div>
    </div>
  </Teleport>
</template>
