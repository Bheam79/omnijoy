import { beforeEach, describe, expect, it, vi } from 'vitest'
import { nextTick, reactive } from 'vue'
import { shallowMount } from '@vue/test-utils'

const route = reactive({ path: '/wall', meta: {} as Record<string, unknown> })
const auth = reactive({
  user: { id: 'user-1' } as { id: string } | null,
  isAuthenticated: true,
})
const savedPosts = {
  reset: vi.fn(),
  reconcile: vi.fn().mockResolvedValue(undefined),
}

vi.mock('vue-router', async (importOriginal) => ({
  ...await importOriginal<typeof import('vue-router')>(),
  useRoute: () => route,
}))
vi.mock('@/stores/auth', () => ({ useAuthStore: () => auth }))
vi.mock('@/stores/savedPosts', () => ({ useSavedPostsStore: () => savedPosts }))
vi.mock('@/stores/live', () => ({
  useLiveStore: () => ({ loadActiveStreams: vi.fn().mockResolvedValue(undefined) }),
}))
vi.mock('@/stores/companyMode', () => ({
  useCompanyModeStore: () => reactive({ isActive: false }),
}))
vi.mock('@/composables/useVersionCheck', () => ({
  useVersionCheck: () => ({ updateAvailable: false, dismiss: vi.fn() }),
}))

import AppShell from '@/components/layout/AppShell.vue'

describe('AppShell saved-post lifecycle', () => {
  beforeEach(() => {
    auth.user = { id: 'user-1' }
    auth.isAuthenticated = true
    vi.clearAllMocks()
  })

  it('clears private saved state on logout and account change', async () => {
    const wrapper = shallowMount(AppShell)

    auth.user = { id: 'user-2' }
    await nextTick()
    expect(savedPosts.reset).toHaveBeenCalledOnce()

    auth.user = null
    auth.isAuthenticated = false
    await nextTick()
    expect(savedPosts.reset).toHaveBeenCalledTimes(2)
    wrapper.unmount()
  })

  it('reconciles on window focus only while authenticated', async () => {
    const wrapper = shallowMount(AppShell)

    window.dispatchEvent(new Event('focus'))
    expect(savedPosts.reconcile).toHaveBeenCalledOnce()

    auth.isAuthenticated = false
    window.dispatchEvent(new Event('focus'))
    expect(savedPosts.reconcile).toHaveBeenCalledOnce()

    wrapper.unmount()
    auth.isAuthenticated = true
    window.dispatchEvent(new Event('focus'))
    expect(savedPosts.reconcile).toHaveBeenCalledOnce()
  })
})
