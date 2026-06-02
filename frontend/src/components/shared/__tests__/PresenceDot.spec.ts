import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { usePresenceStore } from '@/stores/presence'
import PresenceDot from '@/components/shared/PresenceDot.vue'

// ── Mocks ─────────────────────────────────────────────────────────────────────

// Prevent ensurePresence from making real HTTP calls
vi.mock('@/services/notificationService', () => ({
  notificationService: {
    getPresence: vi.fn().mockResolvedValue([]),
  },
}))

// ── Tests ─────────────────────────────────────────────────────────────────────

describe('PresenceDot', () => {
  // pinia and store are created fresh per test in beforeEach so each test
  // starts with an empty presence map. The `!` assertion tells TypeScript
  // these are always assigned before any test body runs.
  let pinia!: ReturnType<typeof createPinia>
  let store!: ReturnType<typeof usePresenceStore>

  beforeEach(() => {
    pinia = createPinia()
    setActivePinia(pinia)
    store = usePresenceStore()
    vi.clearAllMocks()
  })

  // Mount helper shares the same Pinia instance as the store set up above,
  // so store.setOnline/setOffline calls propagate into the mounted component.
  function mountDot(
    userId: string,
    options: { size?: 'xs' | 'sm' | 'md' | 'lg'; showOffline?: boolean } = {},
  ) {
    return mount(PresenceDot, {
      props: { userId, ...options },
      global: { plugins: [pinia] },
    })
  }

  // ── Prop → store delegation ────────────────────────────────────────────────

  it('passes :user-id prop to presence.isOnline()', async () => {
    store.setOnline('user-abc')
    const isOnlineSpy = vi.spyOn(store, 'isOnline')
    mountDot('user-abc')
    await flushPromises()
    expect(isOnlineSpy).toHaveBeenCalledWith('user-abc')
  })

  // ── Online state ──────────────────────────────────────────────────────────

  it('renders a green dot when the user is online', () => {
    store.setOnline('user-1')
    const wrapper = mountDot('user-1')
    const span = wrapper.find('span')
    expect(span.exists()).toBe(true)
    expect(span.classes()).toContain('bg-emerald-500')
    expect(span.attributes('title')).toBe('Online')
    expect(span.attributes('aria-label')).toBe('Online')
  })

  // ── Offline state — showOffline=false (default) ───────────────────────────

  it('renders nothing when the user is offline and showOffline is false (default)', () => {
    store.setOffline('user-2')
    const wrapper = mountDot('user-2')
    expect(wrapper.find('span').exists()).toBe(false)
  })

  it('renders nothing for an untracked user when showOffline is false', () => {
    // No setOnline / setOffline — user is absent from the presence map
    const wrapper = mountDot('unknown-user')
    expect(wrapper.find('span').exists()).toBe(false)
  })

  // ── Offline state — showOffline=true ──────────────────────────────────────

  it('renders a grey dot when the user is offline and showOffline is true', () => {
    store.setOffline('user-3')
    const wrapper = mountDot('user-3', { showOffline: true })
    const span = wrapper.find('span')
    expect(span.exists()).toBe(true)
    expect(span.classes()).toContain('bg-slate-600')
    expect(span.attributes('title')).toBe('Offline')
    expect(span.attributes('aria-label')).toBe('Offline')
  })

  // ── Reactivity ────────────────────────────────────────────────────────────

  it('re-renders to green when the user transitions from offline to online', async () => {
    store.setOffline('user-4')
    const wrapper = mountDot('user-4', { showOffline: true })
    expect(wrapper.find('span').classes()).toContain('bg-slate-600')

    store.setOnline('user-4')
    await wrapper.vm.$nextTick()

    expect(wrapper.find('span').classes()).toContain('bg-emerald-500')
  })

  it('re-renders to grey when the user transitions from online to offline', async () => {
    store.setOnline('user-5')
    const wrapper = mountDot('user-5', { showOffline: true })
    expect(wrapper.find('span').classes()).toContain('bg-emerald-500')

    store.setOffline('user-5')
    await wrapper.vm.$nextTick()

    expect(wrapper.find('span').classes()).toContain('bg-slate-600')
  })

  it('hides the dot when the user goes offline and showOffline is false', async () => {
    store.setOnline('user-6')
    const wrapper = mountDot('user-6')
    expect(wrapper.find('span').exists()).toBe(true)

    store.setOffline('user-6')
    await wrapper.vm.$nextTick()

    expect(wrapper.find('span').exists()).toBe(false)
  })

  // ── Lazy ensurePresence on mount ──────────────────────────────────────────

  it('calls ensurePresence on mount for an untracked userId', async () => {
    const ensureSpy = vi.spyOn(store, 'ensurePresence')
    mountDot('fresh-user')
    await flushPromises()
    expect(ensureSpy).toHaveBeenCalledWith(['fresh-user'])
  })

  it('does not call ensurePresence when the userId is already tracked', async () => {
    store.setOnline('tracked-user')
    const ensureSpy = vi.spyOn(store, 'ensurePresence')
    mountDot('tracked-user')
    await flushPromises()
    expect(ensureSpy).not.toHaveBeenCalled()
  })

  // ── Size variants ─────────────────────────────────────────────────────────

  it('applies xs size classes when size="xs"', () => {
    store.setOnline('user-xs')
    const wrapper = mountDot('user-xs', { size: 'xs' })
    const span = wrapper.find('span')
    expect(span.classes()).toContain('h-2')
    expect(span.classes()).toContain('w-2')
  })

  it('applies sm size classes when size="sm" (default)', () => {
    store.setOnline('user-sm')
    const wrapper = mountDot('user-sm')
    const span = wrapper.find('span')
    expect(span.classes()).toContain('h-2.5')
    expect(span.classes()).toContain('w-2.5')
  })

  it('applies md size classes when size="md"', () => {
    store.setOnline('user-md')
    const wrapper = mountDot('user-md', { size: 'md' })
    const span = wrapper.find('span')
    expect(span.classes()).toContain('h-3')
    expect(span.classes()).toContain('w-3')
  })

  it('applies lg size classes when size="lg"', () => {
    store.setOnline('user-lg')
    const wrapper = mountDot('user-lg', { size: 'lg' })
    const span = wrapper.find('span')
    expect(span.classes()).toContain('h-3.5')
    expect(span.classes()).toContain('w-3.5')
  })
})
