<script setup lang="ts">
/**
 * SlugPicker — reusable vanity-URL input component.
 *
 * Props
 *   initialSlug  – current slug (null if unset)
 *   scope        – 'user' | 'company'
 *   targetId     – required when scope === 'company' (the company page ID)
 *
 * Emits
 *   updated(newSlug: string | null) — after a successful save or clear
 */

import { ref, computed, watch, onMounted, onUnmounted } from 'vue'
import { slugService } from '@/services/slugService'

// ── Slug validation (mirrors SlugValidator.cs) ────────────────────────────────

/**
 * Validates a candidate slug client-side.
 * Returns null if valid, or an error message string if not.
 *
 * Rules (must stay in sync with backend SlugValidator.cs):
 *  - 3–30 characters
 *  - lowercase a–z, 0–9, hyphen, underscore
 *  - must start with a letter
 *  - no consecutive separators (-- or __)
 *  - no trailing separator
 */
function validateSlug(raw: string): string | null {
  const s = raw.toLowerCase()
  if (s.length < 3) return 'Must be at least 3 characters.'
  if (s.length > 30) return 'Must be 30 characters or fewer.'
  if (!/^[a-z]/.test(s)) return 'Must start with a letter.'
  if (!/^[a-z][a-z0-9_-]*$/.test(s)) return 'Only letters, numbers, hyphens and underscores allowed.'
  if (/--|__/.test(s)) return 'No consecutive hyphens or underscores.'
  if (/[-_]$/.test(s)) return 'Cannot end with a hyphen or underscore.'
  return null
}

// ── Props / emits ─────────────────────────────────────────────────────────────

interface Props {
  initialSlug: string | null
  scope: 'user' | 'company'
  targetId?: string
}

const props = defineProps<Props>()
const emit = defineEmits<{
  (e: 'updated', slug: string | null): void
}>()

// ── State ─────────────────────────────────────────────────────────────────────

/** The raw value in the text input (not necessarily lowercase yet). */
const inputValue = ref(props.initialSlug ?? '')

/** Status of the async availability check. */
type CheckStatus = 'idle' | 'checking' | 'available' | 'invalid' | 'reserved' | 'taken' | 'error'
const checkStatus = ref<CheckStatus>('idle')
const checkReason = ref<string>('')

/** Whether a save operation is in flight. */
const saving = ref(false)
const saveError = ref<string | null>(null)
const saveSuccess = ref(false)

// ── Derived ───────────────────────────────────────────────────────────────────

/** Normalised (lowercased) slug value, used for API calls and display. */
const normalised = computed(() => inputValue.value.trim().toLowerCase())

/** Client-side validation error (before the API call). */
const clientError = computed(() => {
  if (normalised.value === '') return null
  return validateSlug(normalised.value)
})

/** True when the Save button should be enabled. */
const canSave = computed(
  () => !saving.value && checkStatus.value === 'available',
)

/** True when the Clear button should be shown (user currently has a slug or has a non-empty input). */
const hasCurrent = computed(() => props.initialSlug !== null && props.initialSlug !== '')

/** The canonical public URL to display. */
const previewUrl = computed(() => {
  const slug = normalised.value || props.initialSlug
  if (!slug) return null
  return `omnijoy.com/${slug}`
})

/** Label for the current canonical URL above the input. */
const currentUrl = computed(() => {
  if (!props.initialSlug) return null
  return `omnijoy.com/${props.initialSlug}`
})

// ── Debounced availability check ──────────────────────────────────────────────

let debounceTimer: ReturnType<typeof setTimeout> | null = null

function scheduleCheck() {
  if (debounceTimer) clearTimeout(debounceTimer)

  const val = normalised.value

  // Empty input — reset to idle
  if (val === '') {
    checkStatus.value = 'idle'
    checkReason.value = ''
    return
  }

  // Client-side validation failure — no need to hit the API
  if (clientError.value) {
    checkStatus.value = 'invalid'
    checkReason.value = clientError.value
    return
  }

  // Same as the already-saved slug — show available immediately
  if (val === props.initialSlug) {
    checkStatus.value = 'available'
    checkReason.value = ''
    return
  }

  checkStatus.value = 'checking'
  checkReason.value = ''

  debounceTimer = setTimeout(async () => {
    try {
      const result = await slugService.check(normalised.value)
      if (result.available) {
        checkStatus.value = 'available'
        checkReason.value = ''
      } else {
        checkStatus.value = (result.reason ?? 'taken') as CheckStatus
        checkReason.value =
          result.reason === 'reserved' ? 'That slug is reserved.'
          : result.reason === 'invalid' ? 'That slug is not valid.'
          : 'That slug is already taken.'
      }
    } catch {
      checkStatus.value = 'error'
      checkReason.value = 'Could not check availability.'
    }
  }, 400)
}

watch(inputValue, scheduleCheck, { immediate: false })

onMounted(() => {
  // If we start with a non-empty slug, set status to available immediately
  // (it's the user's own current slug — no need to re-check it against the API).
  if (normalised.value && normalised.value === props.initialSlug) {
    checkStatus.value = 'available'
  }
})

onUnmounted(() => {
  if (debounceTimer) clearTimeout(debounceTimer)
})

// ── Save ──────────────────────────────────────────────────────────────────────

async function save() {
  if (!canSave.value) return
  saving.value = true
  saveError.value = null
  saveSuccess.value = false

  try {
    if (props.scope === 'user') {
      await slugService.setUserSlug(normalised.value)
    } else {
      if (!props.targetId) throw new Error('targetId is required for company scope.')
      await slugService.setCompanySlug(props.targetId, normalised.value)
    }
    saveSuccess.value = true
    emit('updated', normalised.value)
  } catch (e: unknown) {
    const ax = e as { response?: { data?: { error?: string } } }
    saveError.value = ax.response?.data?.error ?? 'Failed to save.'
  } finally {
    saving.value = false
  }
}

// ── Clear ─────────────────────────────────────────────────────────────────────

async function clear() {
  saving.value = true
  saveError.value = null
  saveSuccess.value = false

  try {
    if (props.scope === 'user') {
      await slugService.setUserSlug(null)
    } else {
      if (!props.targetId) throw new Error('targetId is required for company scope.')
      await slugService.setCompanySlug(props.targetId, null)
    }
    inputValue.value = ''
    checkStatus.value = 'idle'
    checkReason.value = ''
    emit('updated', null)
  } catch (e: unknown) {
    const ax = e as { response?: { data?: { error?: string } } }
    saveError.value = ax.response?.data?.error ?? 'Failed to clear.'
  } finally {
    saving.value = false
  }
}
</script>

<template>
  <div class="bg-white rounded-xl border border-gray-100 px-5 py-5" data-testid="slug-picker">
    <h2 class="text-base font-semibold text-gray-900 mb-1">Public URL</h2>
    <p class="text-xs text-gray-500 mb-4">
      Choose a vanity URL so people can find you at <span class="font-mono text-gray-700">omnijoy.com/&lt;slug&gt;</span>.
    </p>

    <!-- Current canonical URL -->
    <div v-if="currentUrl" class="mb-3 flex items-center gap-1.5 text-sm text-gray-600" data-testid="current-url">
      <svg class="h-4 w-4 text-gray-400 shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M13.828 10.172a4 4 0 00-5.656 0l-4 4a4 4 0 105.656 5.656l1.102-1.101m-.758-4.899a4 4 0 005.656 0l4-4a4 4 0 00-5.656-5.656l-1.1 1.1"/>
      </svg>
      Your current public URL: <span class="font-mono font-medium text-indigo-600">{{ currentUrl }}</span>
    </div>

    <!-- Input + status icon row -->
    <div class="flex items-center gap-2 mb-2">
      <div class="flex-1 relative">
        <span class="absolute left-3 top-1/2 -translate-y-1/2 text-sm text-gray-400 select-none pointer-events-none">
          omnijoy.com/
        </span>
        <input
          v-model="inputValue"
          type="text"
          maxlength="30"
          autocomplete="off"
          autocapitalize="none"
          spellcheck="false"
          placeholder="your-slug"
          data-testid="slug-input"
          class="w-full rounded-lg border px-3 py-2 pl-[6.5rem] text-sm font-mono focus:outline-none focus:ring-2 focus:ring-indigo-500 focus:border-transparent transition-colors"
          :class="{
            'border-gray-300':  checkStatus === 'idle' || checkStatus === 'checking',
            'border-green-400': checkStatus === 'available',
            'border-red-400':   checkStatus === 'invalid' || checkStatus === 'reserved' || checkStatus === 'taken' || checkStatus === 'error',
          }"
        />
        <!-- Spinner while checking -->
        <span v-if="checkStatus === 'checking'" class="absolute right-3 top-1/2 -translate-y-1/2" data-testid="check-spinner">
          <svg class="animate-spin h-4 w-4 text-gray-400" fill="none" viewBox="0 0 24 24">
            <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"/>
            <path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8v8H4z"/>
          </svg>
        </span>
        <!-- Available check -->
        <span v-else-if="checkStatus === 'available'" class="absolute right-3 top-1/2 -translate-y-1/2 text-green-500 font-bold" data-testid="status-available">✓</span>
        <!-- Unavailable × -->
        <span v-else-if="checkStatus === 'invalid' || checkStatus === 'reserved' || checkStatus === 'taken' || checkStatus === 'error'" class="absolute right-3 top-1/2 -translate-y-1/2 text-red-500 font-bold" data-testid="status-unavailable">✗</span>
      </div>
    </div>

    <!-- Live preview / feedback -->
    <p
      v-if="checkStatus === 'available' && previewUrl"
      class="text-xs text-green-600 mb-3"
      data-testid="feedback-available"
    >
      ✓ Available — your URL will be <span class="font-mono font-medium">{{ previewUrl }}</span>
    </p>
    <p
      v-else-if="(checkStatus === 'invalid' || checkStatus === 'reserved' || checkStatus === 'taken' || checkStatus === 'error') && checkReason"
      class="text-xs text-red-600 mb-3"
      data-testid="feedback-error"
    >
      {{ checkReason }}
    </p>
    <p
      v-else-if="checkStatus === 'checking'"
      class="text-xs text-gray-400 mb-3"
      data-testid="feedback-checking"
    >
      Checking availability…
    </p>
    <p
      v-else-if="checkStatus === 'idle' && !inputValue"
      class="text-xs text-gray-400 mb-3"
    >
      Enter a slug to see availability.
    </p>
    <div v-else class="mb-3" />

    <!-- Save / Clear error -->
    <div
      v-if="saveError"
      class="mb-3 rounded-lg bg-red-50 border border-red-200 px-3 py-2 text-sm text-red-700"
      data-testid="save-error"
    >
      {{ saveError }}
    </div>

    <!-- Save success -->
    <div
      v-if="saveSuccess"
      class="mb-3 rounded-lg bg-green-50 border border-green-200 px-3 py-2 text-sm text-green-700"
      data-testid="save-success"
    >
      Public URL saved!
    </div>

    <!-- Action buttons -->
    <div class="flex gap-2">
      <button
        type="button"
        :disabled="!canSave"
        class="px-4 py-2 text-sm font-medium text-white bg-indigo-600 rounded-lg hover:bg-indigo-700 disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
        data-testid="save-btn"
        @click="save"
      >
        <span v-if="saving">Saving…</span>
        <span v-else>Save URL</span>
      </button>

      <button
        v-if="hasCurrent"
        type="button"
        :disabled="saving"
        class="px-4 py-2 text-sm font-medium text-gray-700 border border-gray-300 rounded-lg hover:bg-gray-50 disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
        data-testid="clear-btn"
        @click="clear"
      >
        Clear
      </button>
    </div>
  </div>
</template>
