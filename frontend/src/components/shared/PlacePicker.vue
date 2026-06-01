<script setup lang="ts">
/**
 * PlacePicker — reusable place / city autocomplete component.
 *
 * Props
 *   modelValue   – PlaceSelection | null  (v-model)
 *   mode         – 'cities' | 'address'   controls the Google Places types filter
 *   label        – field label text
 *   placeholder  – input placeholder
 *   required     – shows asterisk + validation highlight (default false)
 *   disabled     – disables the control
 *
 * Emits
 *   update:modelValue (value: PlaceSelection | null)
 *
 * Session-token lifecycle (Google Places billing):
 *   - Generate a UUID on the first keystroke of each session.
 *   - Reuse it for every subsequent autocomplete call in the same session.
 *   - Pass it on the details call to close the session.
 *   - Generate a fresh token after a selection (or clear) has been made.
 */

import { ref, computed, onUnmounted } from 'vue'
import { placesService } from '@/services/placesService'
import type { PlaceSelection, AutocompleteResult } from '@/types/places'

// ── Props / emits ─────────────────────────────────────────────────────────────

interface Props {
  modelValue: PlaceSelection | null
  mode: 'cities' | 'address'
  label: string
  placeholder?: string
  required?: boolean
  disabled?: boolean
}

const props = withDefaults(defineProps<Props>(), {
  placeholder: 'Search for a location…',
  required: false,
  disabled: false,
})

const emit = defineEmits<{
  (e: 'update:modelValue', value: PlaceSelection | null): void
}>()

// ── State ─────────────────────────────────────────────────────────────────────

/** Text in the search input (only shown when no selection has been made). */
const inputText = ref('')

/** Whether the suggestion dropdown is open. */
const isOpen = ref(false)

/** Autocomplete suggestions from the last API response. */
const suggestions = ref<AutocompleteResult[]>([])

/** True while an autocomplete or details call is in flight. */
const loading = ref(false)

/** True while the details call is in flight (distinguishes from autocomplete). */
const detailsLoading = ref(false)

/** Inline error message to show beneath the control. */
const errorMessage = ref<string | null>(null)

/** Whether the API key is absent (backend returned empty suggestions). */
const apiUnavailable = ref(false)

/** Manual text-entry fallback value (used when apiUnavailable). */
const manualText = ref('')

/**
 * Current session token — null until the first keystroke of a session.
 * Cleared (set to null) after a selection is made so the next keystroke
 * generates a fresh UUID.
 */
let sessionToken: string | null = null

/** Debounce timer handle. */
let debounceTimer: ReturnType<typeof setTimeout> | null = null

// ── Derived ───────────────────────────────────────────────────────────────────

/** The types string forwarded to the backend. */
const typesParam = computed(() => props.mode === 'cities' ? '(cities)' : '(address)')

// ── Session token helpers ─────────────────────────────────────────────────────

function ensureSessionToken(): string {
  if (!sessionToken) {
    sessionToken = crypto.randomUUID()
  }
  return sessionToken
}

function resetSessionToken() {
  sessionToken = null
}

// ── Autocomplete ──────────────────────────────────────────────────────────────

function onInput(event: Event) {
  const value = (event.target as HTMLInputElement).value
  inputText.value = value
  errorMessage.value = null
  apiUnavailable.value = false

  if (!value.trim()) {
    clearSuggestions()
    return
  }

  scheduleAutocomplete(value.trim())
}

function scheduleAutocomplete(input: string) {
  if (debounceTimer) clearTimeout(debounceTimer)

  debounceTimer = setTimeout(async () => {
    const token = ensureSessionToken()
    loading.value = true
    try {
      const results = await placesService.autocomplete(input, typesParam.value, token)
      if (results.length === 0 && input.length >= 2) {
        // Treat zero results as either "no match" or "API key not configured".
        // We set apiUnavailable so the fallback message is shown.
        apiUnavailable.value = true
        suggestions.value = []
        isOpen.value = false
      } else {
        suggestions.value = results.slice(0, 5)
        apiUnavailable.value = false
        isOpen.value = results.length > 0
      }
    } catch {
      errorMessage.value = 'Location search failed. Please try again.'
      suggestions.value = []
      isOpen.value = false
    } finally {
      loading.value = false
    }
  }, 300)
}

// ── Selection ─────────────────────────────────────────────────────────────────

async function selectSuggestion(suggestion: AutocompleteResult) {
  const token = ensureSessionToken()
  clearSuggestions()
  inputText.value = ''
  detailsLoading.value = true
  errorMessage.value = null

  try {
    const selection = await placesService.getDetails(suggestion.placeId, token)
    // Override displayName with the suggestion description for cleaner display
    selection.displayName = suggestion.description
    emit('update:modelValue', selection)
  } catch {
    errorMessage.value = 'Failed to load location details. Please try again.'
  } finally {
    detailsLoading.value = false
    // Session ends after the details call — generate a new token next time
    resetSessionToken()
  }
}

/** Emit a manual selection when the API key is not configured. */
function confirmManual() {
  const text = manualText.value.trim()
  if (!text) return
  const selection: PlaceSelection = {
    placeId:          'manual',
    displayName:      text,
    city:             null,
    country:          null,
    countryCode:      null,
    latitude:         null,
    longitude:        null,
    formattedAddress: null,
  }
  emit('update:modelValue', selection)
  manualText.value = ''
  apiUnavailable.value = false
  inputText.value = ''
}

// ── Clear ─────────────────────────────────────────────────────────────────────

function clearSelection() {
  emit('update:modelValue', null)
  inputText.value = ''
  manualText.value = ''
  errorMessage.value = null
  apiUnavailable.value = false
  clearSuggestions()
  resetSessionToken()
}

// ── Helpers ───────────────────────────────────────────────────────────────────

function clearSuggestions() {
  suggestions.value = []
  isOpen.value = false
}

function closeSuggestions() {
  // Small delay so a click on a suggestion registers before the blur fires
  setTimeout(() => {
    isOpen.value = false
  }, 150)
}

onUnmounted(() => {
  if (debounceTimer) clearTimeout(debounceTimer)
})
</script>

<template>
  <div class="relative" data-testid="place-picker">
    <!-- Label -->
    <label class="block text-sm font-medium text-slate-300 mb-1">
      {{ label }}
      <span v-if="required" class="text-red-500 ml-0.5">*</span>
    </label>

    <!-- Selected chip (shown instead of the input when a value is picked) -->
    <div
      v-if="modelValue"
      class="flex items-center gap-2 rounded-lg border border-slate-600 px-3 py-2 bg-slate-700/50"
      data-testid="selected-chip"
    >
      <!-- Location pin icon -->
      <svg class="h-4 w-4 text-indigo-400 shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
          d="M17.657 16.657L13.414 20.9a1.998 1.998 0 01-2.827 0l-4.244-4.243a8 8 0 1111.314 0z"/>
        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
          d="M15 11a3 3 0 11-6 0 3 3 0 016 0z"/>
      </svg>
      <span class="flex-1 text-sm text-slate-200 truncate" data-testid="selected-display-name">
        {{ modelValue.displayName }}
      </span>
      <button
        v-if="!disabled"
        type="button"
        class="text-slate-500 hover:text-slate-300 transition-colors shrink-0"
        aria-label="Clear location"
        data-testid="clear-btn"
        @click="clearSelection"
      >
        <svg class="h-4 w-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12"/>
        </svg>
      </button>
    </div>

    <!-- Search input (shown when nothing is selected and API is available) -->
    <div v-else-if="!apiUnavailable" class="relative">
      <input
        :value="inputText"
        :placeholder="placeholder"
        :disabled="disabled || detailsLoading"
        :class="[
          'w-full rounded-lg border px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500 focus:border-transparent transition-colors',
          required && !modelValue ? 'border-red-500/60' : 'border-slate-600',
          disabled || detailsLoading ? 'opacity-50 cursor-not-allowed' : '',
        ]"
        autocomplete="off"
        type="text"
        data-testid="place-input"
        @input="onInput"
        @blur="closeSuggestions"
      />

      <!-- Loading spinner (autocomplete in flight) -->
      <span
        v-if="loading || detailsLoading"
        class="absolute right-3 top-1/2 -translate-y-1/2"
        data-testid="loading-spinner"
      >
        <svg class="animate-spin h-4 w-4 text-slate-500" fill="none" viewBox="0 0 24 24">
          <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"/>
          <path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8v8H4z"/>
        </svg>
      </span>

      <!-- Autocomplete dropdown -->
      <ul
        v-if="isOpen && suggestions.length"
        class="absolute z-50 mt-1 w-full rounded-lg border border-slate-600 bg-slate-800 shadow-lg overflow-hidden"
        data-testid="suggestions-list"
      >
        <li
          v-for="suggestion in suggestions"
          :key="suggestion.placeId"
          class="flex flex-col px-3 py-2 cursor-pointer hover:bg-slate-700 transition-colors"
          data-testid="suggestion-item"
          @mousedown.prevent="selectSuggestion(suggestion)"
        >
          <span class="text-sm text-slate-200 leading-tight">{{ suggestion.mainText }}</span>
          <span v-if="suggestion.secondaryText" class="text-xs text-slate-400 mt-0.5 leading-tight">
            {{ suggestion.secondaryText }}
          </span>
        </li>
      </ul>
    </div>

    <!-- Manual fallback input (shown when API key is not configured) -->
    <div v-else class="space-y-1">
      <div class="flex gap-2">
        <input
          v-model="manualText"
          :placeholder="placeholder"
          :disabled="disabled"
          class="flex-1 rounded-lg border border-slate-600 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500 focus:border-transparent"
          type="text"
          data-testid="manual-input"
          @keydown.enter.prevent="confirmManual"
        />
        <button
          type="button"
          :disabled="!manualText.trim() || disabled"
          class="px-3 py-2 text-sm font-medium text-white bg-indigo-600 rounded-lg hover:bg-indigo-700 disabled:opacity-50 disabled:cursor-not-allowed transition-colors shrink-0"
          data-testid="manual-confirm-btn"
          @click="confirmManual"
        >
          Use
        </button>
      </div>
      <p class="text-xs text-amber-400" data-testid="api-unavailable-msg">
        Location search unavailable. You can type a location manually.
      </p>
    </div>

    <!-- Inline error -->
    <p
      v-if="errorMessage"
      class="mt-1 text-xs text-red-400"
      data-testid="error-message"
    >
      {{ errorMessage }}
    </p>
  </div>
</template>
