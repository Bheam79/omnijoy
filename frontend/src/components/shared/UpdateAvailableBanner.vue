<script setup lang="ts">
/**
 * UpdateAvailableBanner
 *
 * A fixed top-of-page banner shown when a new deploy is detected.
 * Positioned below the 64 px TopNav (top-16).
 *
 * Props:
 *   visible – controls visibility (bind to updateAvailable from useVersionCheck)
 *
 * Events:
 *   reload   – user clicked "Reload now"  → caller should call window.location.reload()
 *   dismiss  – user dismissed without reloading
 */
defineProps<{ visible: boolean }>()
const emit = defineEmits<{
  reload: []
  dismiss: []
}>()

function reload() {
  emit('reload')
  window.location.reload()
}
</script>

<template>
  <Transition
    enter-active-class="transition ease-out duration-300"
    enter-from-class="opacity-0 -translate-y-full"
    enter-to-class="opacity-100 translate-y-0"
    leave-active-class="transition ease-in duration-200"
    leave-from-class="opacity-100 translate-y-0"
    leave-to-class="opacity-0 -translate-y-full"
  >
    <div
      v-if="visible"
      class="fixed top-16 inset-x-0 z-40 flex items-center justify-center gap-3 px-4 py-2 bg-indigo-700 text-white text-sm shadow-lg"
      role="status"
      aria-live="polite"
      data-testid="update-available-banner"
    >
      <span>🚀 A new version of Omnijoy is available.</span>

      <button
        class="px-3 py-1 rounded-full bg-white text-indigo-700 font-semibold text-xs hover:bg-indigo-100 transition-colors shrink-0"
        data-testid="update-banner-reload"
        @click="reload"
      >
        Reload now
      </button>

      <button
        class="p-1 rounded-full text-indigo-200 hover:text-white hover:bg-indigo-600 transition-colors shrink-0"
        aria-label="Dismiss"
        data-testid="update-banner-dismiss"
        @click="emit('dismiss')"
      >
        <svg class="h-4 w-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12"/>
        </svg>
      </button>
    </div>
  </Transition>
</template>
