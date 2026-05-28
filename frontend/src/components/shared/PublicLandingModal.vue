<script setup lang="ts">
import { ref } from 'vue'
import { RouterLink } from 'vue-router'
import { usePublicLocation } from '@/composables/usePublicLocation'

/**
 * Welcome modal shown to unauthenticated visitors on the public front page.
 *
 * Visitors get three ways forward:
 *   1. Log in (existing account)
 *   2. Sign up (create an account)
 *   3. Stay on the public page after choosing a location — either via the
 *      browser geolocation API or by typing a city manually.
 *
 * Emits "dismiss" when the visitor opts to keep browsing publicly. The parent
 * decides whether the modal can be reopened (e.g. via a "Change location"
 * button) and persists the chosen location through usePublicLocation().
 */
const emit = defineEmits<{
  dismiss: []
}>()

const { publicLocation, setLocation, detectFromBrowser } = usePublicLocation()

const manualInput = ref(publicLocation.value ?? '')
const detecting = ref(false)
const errorMessage = ref<string | null>(null)
const view = ref<'choose' | 'location'>('choose')

async function handleAutoDetect() {
  errorMessage.value = null
  detecting.value = true
  try {
    const city = await detectFromBrowser()
    manualInput.value = city
    emit('dismiss')
  } catch (e: unknown) {
    errorMessage.value = e instanceof Error ? e.message : 'Could not detect your location.'
  } finally {
    detecting.value = false
  }
}

function handleManualSave() {
  errorMessage.value = null
  const value = manualInput.value.trim()
  if (!value) {
    errorMessage.value = 'Please type a city name or skip this step.'
    return
  }
  setLocation(value)
  emit('dismiss')
}

function handleSkipLocation() {
  // Allow browsing without filtering by location — shows public events globally.
  setLocation(null)
  emit('dismiss')
}
</script>

<template>
  <Teleport to="body">
    <div
      class="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/60 backdrop-blur-sm"
    >
      <div
        class="bg-white rounded-2xl shadow-2xl w-full max-w-md max-h-[90vh] overflow-y-auto"
        role="dialog"
        aria-modal="true"
        aria-labelledby="public-landing-title"
      >
        <!-- Header -->
        <div class="px-6 pt-6 pb-2 text-center">
          <div class="mx-auto h-12 w-12 rounded-xl bg-indigo-600 flex items-center justify-center mb-3">
            <span class="text-white font-bold text-lg">OJ</span>
          </div>
          <h2 id="public-landing-title" class="text-xl font-bold text-gray-900">
            Welcome to Omnijoy
          </h2>
          <p class="text-sm text-gray-500 mt-1">
            Social media without ads or forced content.
          </p>
        </div>

        <!-- Choose-your-path view -->
        <div v-if="view === 'choose'" class="px-6 py-4 space-y-3">
          <RouterLink
            to="/login"
            class="block w-full text-center rounded-xl bg-indigo-600 hover:bg-indigo-700 text-white font-semibold text-sm px-4 py-2.5 transition-colors"
          >
            Log in
          </RouterLink>
          <RouterLink
            to="/register"
            class="block w-full text-center rounded-xl bg-white border border-gray-300 hover:border-indigo-400 hover:text-indigo-700 text-gray-800 font-semibold text-sm px-4 py-2.5 transition-colors"
          >
            Create a free account
          </RouterLink>

          <div class="flex items-center gap-2 pt-1">
            <div class="flex-1 h-px bg-gray-200" />
            <span class="text-xs uppercase tracking-wider text-gray-400">or</span>
            <div class="flex-1 h-px bg-gray-200" />
          </div>

          <button
            type="button"
            class="block w-full rounded-xl border border-gray-200 hover:border-indigo-300 hover:bg-indigo-50 text-left px-4 py-3 transition-colors"
            @click="view = 'location'"
          >
            <div class="flex items-start gap-3">
              <div class="h-8 w-8 rounded-lg bg-indigo-50 text-indigo-600 flex items-center justify-center shrink-0">
                <svg class="h-4 w-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M17.657 16.657L13.414 20.9a1.998 1.998 0 01-2.827 0l-4.244-4.243a8 8 0 1111.314 0z"/>
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 11a3 3 0 11-6 0 3 3 0 016 0z"/>
                </svg>
              </div>
              <div class="min-w-0">
                <div class="text-sm font-semibold text-gray-900">
                  Keep browsing as a guest
                </div>
                <div class="text-xs text-gray-500 mt-0.5">
                  See public events near where you live.
                </div>
              </div>
            </div>
          </button>
        </div>

        <!-- Location-picker view -->
        <div v-else class="px-6 py-4 space-y-4">
          <button
            type="button"
            class="text-xs text-gray-500 hover:text-gray-700 flex items-center gap-1"
            @click="view = 'choose'; errorMessage = null"
          >
            <svg class="h-3 w-3" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 19l-7-7 7-7"/>
            </svg>
            Back
          </button>

          <div>
            <h3 class="text-sm font-semibold text-gray-900 mb-1">
              Where are you?
            </h3>
            <p class="text-xs text-gray-500">
              We'll use this to show public events near you. You can change or
              clear it any time.
            </p>
          </div>

          <button
            type="button"
            class="w-full flex items-center justify-center gap-2 rounded-xl bg-indigo-600 hover:bg-indigo-700 disabled:opacity-60 text-white font-semibold text-sm px-4 py-2.5 transition-colors"
            :disabled="detecting"
            @click="handleAutoDetect"
          >
            <svg v-if="detecting" class="h-4 w-4 animate-spin" fill="none" viewBox="0 0 24 24">
              <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"/>
              <path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z"/>
            </svg>
            <svg v-else class="h-4 w-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 2v2m0 16v2m10-10h-2M4 12H2m15.657-7.657l-1.414 1.414M6.757 17.243l-1.414 1.414m12.314 0l-1.414-1.414M6.757 6.757L5.343 5.343M16 12a4 4 0 11-8 0 4 4 0 018 0z"/>
            </svg>
            <span>{{ detecting ? 'Detecting…' : 'Use my current location' }}</span>
          </button>

          <div class="flex items-center gap-2">
            <div class="flex-1 h-px bg-gray-200" />
            <span class="text-xs uppercase tracking-wider text-gray-400">or</span>
            <div class="flex-1 h-px bg-gray-200" />
          </div>

          <div>
            <label class="block text-xs font-medium text-gray-700 mb-1">
              Type a city
            </label>
            <input
              v-model="manualInput"
              type="text"
              placeholder="e.g. Stockholm"
              class="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500 focus:border-transparent"
              @keyup.enter="handleManualSave"
            />
          </div>

          <div
            v-if="errorMessage"
            class="text-xs text-red-600 bg-red-50 border border-red-100 rounded-lg px-3 py-2"
          >
            {{ errorMessage }}
          </div>

          <div class="flex flex-col gap-2 pt-1">
            <button
              type="button"
              class="w-full rounded-xl bg-white border border-indigo-300 hover:bg-indigo-50 text-indigo-700 font-semibold text-sm px-4 py-2.5 transition-colors"
              @click="handleManualSave"
            >
              Save location
            </button>
            <button
              type="button"
              class="w-full text-xs text-gray-500 hover:text-gray-700 py-1"
              @click="handleSkipLocation"
            >
              Skip — show all public events
            </button>
          </div>
        </div>

        <!-- Footer -->
        <div class="px-6 py-4 border-t border-gray-100 text-center">
          <p class="text-[11px] text-gray-400">
            We don't store your coordinates — just the city name you pick,
            saved locally in your browser.
          </p>
        </div>
      </div>
    </div>
  </Teleport>
</template>
