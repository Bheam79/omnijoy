/**
 * useVersionCheck
 *
 * Detects backend deploys by polling GET /api/version on tab focus.
 *
 * Behaviour:
 *  - On mount, fetches the current server version and stores it as the
 *    baseline ("version when this tab opened").
 *  - When the document becomes visible again after being hidden for more
 *    than 60 seconds, re-fetches GET /api/version.
 *  - If the version has changed, sets `updateAvailable` to true.
 *  - The caller can render a reload banner based on that flag.
 *
 * Returns:
 *  - updateAvailable  – Ref<boolean>
 *  - dismiss          – hides the banner without reloading
 */
import { ref, onMounted, onUnmounted } from 'vue'
import api from '@/services/api'

const MIN_HIDDEN_MS = 60_000 // only re-check after tab was hidden ≥ 60 s

export function useVersionCheck() {
  const updateAvailable = ref(false)
  const baselineVersion = ref<string | null>(null)
  let hiddenAt: number | null = null

  async function fetchVersion(): Promise<string | null> {
    try {
      const { data } = await api.get<{ version: string }>('/api/version')
      return data.version ?? null
    } catch {
      // Network error or backend down — treat as no change; don't spam the
      // banner when the user's connection is briefly disrupted.
      return null
    }
  }

  async function checkForUpdate() {
    const current = await fetchVersion()
    if (
      current !== null &&
      baselineVersion.value !== null &&
      current !== baselineVersion.value
    ) {
      updateAvailable.value = true
    }
  }

  function onVisibilityChange() {
    if (document.visibilityState === 'hidden') {
      hiddenAt = Date.now()
    } else if (document.visibilityState === 'visible') {
      const elapsed = hiddenAt !== null ? Date.now() - hiddenAt : 0
      hiddenAt = null
      if (elapsed >= MIN_HIDDEN_MS) {
        checkForUpdate()
      }
    }
  }

  function dismiss() {
    updateAvailable.value = false
  }

  onMounted(async () => {
    baselineVersion.value = await fetchVersion()
    document.addEventListener('visibilitychange', onVisibilityChange)
  })

  onUnmounted(() => {
    document.removeEventListener('visibilitychange', onVisibilityChange)
  })

  return { updateAvailable, dismiss }
}
