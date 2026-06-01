import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { defineComponent } from 'vue'
import { mount, flushPromises, VueWrapper } from '@vue/test-utils'
import { useVersionCheck } from '@/composables/useVersionCheck'

// ── Mock api ──────────────────────────────────────────────────────────────────

const mockGet = vi.hoisted(() => vi.fn())

vi.mock('@/services/api', () => ({
  default: { get: mockGet },
}))

// ── Helpers ───────────────────────────────────────────────────────────────────

function makeVersionResponse(version: string) {
  return { data: { version } }
}

/**
 * Mount a thin wrapper component that calls useVersionCheck() and exposes
 * its return values so tests can read them.
 */
function mountChecker() {
  let exposed!: ReturnType<typeof useVersionCheck>

  const Wrapper = defineComponent({
    setup() {
      exposed = useVersionCheck()
      return {}
    },
    template: '<div />',
  })

  const wrapper = mount(Wrapper, { attachTo: document.body })
  return {
    get result() { return exposed },
    wrapper,
  }
}

function fireVisibility(state: 'hidden' | 'visible') {
  Object.defineProperty(document, 'visibilityState', {
    value: state,
    configurable: true,
    writable: true,
  })
  document.dispatchEvent(new Event('visibilitychange'))
}

// ── Tests ─────────────────────────────────────────────────────────────────────

describe('useVersionCheck', () => {
  // Track wrappers so we unmount after each test — prevents stale event
  // listeners from accumulating and consuming mock return values.
  const mounted: VueWrapper[] = []

  beforeEach(() => {
    vi.useFakeTimers()
    // resetAllMocks clears call history AND any lingering mockResolvedValue/Once
    // queues from previous tests so each test starts with a clean mock.
    vi.resetAllMocks()
    Object.defineProperty(document, 'visibilityState', {
      value: 'visible',
      configurable: true,
      writable: true,
    })
  })

  afterEach(() => {
    // Unmount all components to remove visibilitychange listeners
    mounted.splice(0).forEach(w => w.unmount())
    vi.useRealTimers()
  })

  /** Mounts checker and registers it for auto-cleanup in afterEach. */
  function mount_(label?: string) {
    const c = mountChecker()
    mounted.push(c.wrapper)
    return c
  }

  it('fetches baseline version on mount', async () => {
    mockGet.mockResolvedValue(makeVersionResponse('abc123'))

    const { result } = mount_()
    await flushPromises()

    expect(mockGet).toHaveBeenCalledWith('/api/version')
    expect(result.updateAvailable.value).toBe(false)
  })

  it('sets updateAvailable when version changes after tab re-focus', async () => {
    mockGet
      .mockResolvedValueOnce(makeVersionResponse('v1'))
      .mockResolvedValueOnce(makeVersionResponse('v2'))

    const { result } = mount_()
    await flushPromises()

    // Simulate hiding the tab for more than 60 seconds
    fireVisibility('hidden')
    vi.advanceTimersByTime(61_000)

    // Re-show the tab — triggers the version re-check
    fireVisibility('visible')
    await flushPromises()

    expect(result.updateAvailable.value).toBe(true)
  })

  it('does NOT check if tab was hidden for less than 60 seconds', async () => {
    mockGet.mockResolvedValue(makeVersionResponse('v1'))

    mount_()
    await flushPromises()
    vi.clearAllMocks()

    fireVisibility('hidden')
    vi.advanceTimersByTime(30_000) // only 30 s — below threshold

    fireVisibility('visible')
    await flushPromises()

    expect(mockGet).not.toHaveBeenCalled()
  })

  it('does NOT set updateAvailable when version is unchanged', async () => {
    mockGet.mockResolvedValue(makeVersionResponse('v1'))

    const { result } = mount_()
    await flushPromises()

    fireVisibility('hidden')
    vi.advanceTimersByTime(61_000)
    fireVisibility('visible')
    await flushPromises()

    expect(result.updateAvailable.value).toBe(false)
  })

  it('does NOT set updateAvailable when api call fails', async () => {
    mockGet
      .mockResolvedValueOnce(makeVersionResponse('v1'))
      .mockRejectedValueOnce(new Error('network error'))

    const { result } = mount_()
    await flushPromises()

    fireVisibility('hidden')
    vi.advanceTimersByTime(61_000)
    fireVisibility('visible')
    await flushPromises()

    expect(result.updateAvailable.value).toBe(false)
  })

  it('dismiss() resets updateAvailable to false', async () => {
    mockGet
      .mockResolvedValueOnce(makeVersionResponse('v1'))
      .mockResolvedValueOnce(makeVersionResponse('v2'))

    const { result } = mount_()
    await flushPromises()

    fireVisibility('hidden')
    vi.advanceTimersByTime(61_000)
    fireVisibility('visible')
    await flushPromises()

    expect(result.updateAvailable.value).toBe(true)
    result.dismiss()
    expect(result.updateAvailable.value).toBe(false)
  })

  it('removes visibilitychange listener on unmount', async () => {
    mockGet.mockResolvedValue(makeVersionResponse('v1'))

    const { wrapper } = mount_()
    await flushPromises()

    const removeSpy = vi.spyOn(document, 'removeEventListener')
    wrapper.unmount()
    // Remove from tracked list so afterEach doesn't double-unmount
    const idx = mounted.indexOf(wrapper)
    if (idx >= 0) mounted.splice(idx, 1)
    expect(removeSpy).toHaveBeenCalledWith('visibilitychange', expect.any(Function))
  })
})
