import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { createRouter, createMemoryHistory } from 'vue-router'
import SlugResolverView from '../SlugResolverView.vue'

// ── Mock slugService ───────────────────────────────────────────────────────────

const mockSlugService = vi.hoisted(() => ({
  resolve: vi.fn(),
}))

vi.mock('@/services/slugService', () => ({
  slugService: mockSlugService,
}))

// ── Mock authStore (used by router guard only in prod router, not needed here) ─

vi.mock('@/stores/auth', () => ({
  useAuthStore: () => ({
    isAuthenticated: true,
    accessToken: 'fake-token',
  }),
}))

// ── Router helpers ─────────────────────────────────────────────────────────────

function makeRouter(initialSlug: string) {
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [
      {
        path: '/:slug',
        name: 'slug-resolver',
        component: SlugResolverView,
      },
      {
        path: '/profile/:userId',
        name: 'profile',
        component: { template: '<div>Profile</div>' },
      },
      {
        path: '/company/:id',
        name: 'company',
        component: { template: '<div>Company</div>' },
      },
    ],
  })
  router.push(`/${initialSlug}`)
  return router
}

// ── Tests ──────────────────────────────────────────────────────────────────────

describe('SlugResolverView', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.clearAllMocks()
  })

  it('shows a loading spinner while the request is in-flight', async () => {
    // Never resolves during the test — spinner stays visible.
    mockSlugService.resolve.mockReturnValue(new Promise(() => {}))

    const router = makeRouter('johndoe')
    await router.isReady()

    const wrapper = mount(SlugResolverView, {
      global: { plugins: [router] },
    })

    expect(wrapper.find('[data-testid="slug-spinner"]').exists()).toBe(true)
    expect(wrapper.text()).not.toContain('404')
  })

  it('redirects to the profile route on a user hit', async () => {
    mockSlugService.resolve.mockResolvedValue({ type: 'user', id: 'user-uuid-123' })

    const router = makeRouter('johndoe')
    await router.isReady()

    const replaceSpy = vi.spyOn(router, 'replace')

    mount(SlugResolverView, {
      global: { plugins: [router] },
    })

    await flushPromises()

    expect(replaceSpy).toHaveBeenCalledWith({
      name: 'profile',
      params: { userId: 'user-uuid-123' },
    })
  })

  it('redirects to the company route on a company hit', async () => {
    mockSlugService.resolve.mockResolvedValue({ type: 'company', id: 'company-uuid-456' })

    const router = makeRouter('acmecorp')
    await router.isReady()

    const replaceSpy = vi.spyOn(router, 'replace')

    mount(SlugResolverView, {
      global: { plugins: [router] },
    })

    await flushPromises()

    expect(replaceSpy).toHaveBeenCalledWith({
      name: 'company',
      params: { id: 'company-uuid-456' },
    })
  })

  it('renders 404 content when slug resolves to null (not found)', async () => {
    mockSlugService.resolve.mockResolvedValue(null)

    const router = makeRouter('unknown')
    await router.isReady()

    const wrapper = mount(SlugResolverView, {
      global: { plugins: [router] },
    })

    await flushPromises()

    expect(wrapper.find('[data-testid="slug-spinner"]').exists()).toBe(false)
    expect(wrapper.text()).toContain('404')
    expect(wrapper.text()).toContain('Page not found')
  })

  it('renders 404 content when the API throws an error', async () => {
    mockSlugService.resolve.mockRejectedValue(new Error('network error'))

    const router = makeRouter('broken')
    await router.isReady()

    const wrapper = mount(SlugResolverView, {
      global: { plugins: [router] },
    })

    await flushPromises()

    expect(wrapper.text()).toContain('404')
  })
})
