/**
 * CompanySlugPicker integration tests
 *
 * Scoped to the SlugPicker integration inside CompanyView (admins tab).
 * Full company-view behaviour (tabs, admin management, edit modal, etc.)
 * is intentionally out of scope here.
 */

import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { createRouter, createMemoryHistory } from 'vue-router'
import CompanyView from '../CompanyView.vue'

// ── Mocks ─────────────────────────────────────────────────────────────────────

const mockSlugService = vi.hoisted(() => ({
  check:          vi.fn(),
  setUserSlug:    vi.fn(),
  setCompanySlug: vi.fn(),
}))

vi.mock('@/services/slugService', () => ({
  slugService: mockSlugService,
}))

const mockCompanyPageService = vi.hoisted(() => ({
  getPage:    vi.fn(),
  getAdmins:  vi.fn(),
  follow:     vi.fn(),
  unfollow:   vi.fn(),
  updatePage: vi.fn(),
  addAdmin:   vi.fn(),
  removeAdmin: vi.fn(),
}))

vi.mock('@/services/companyPageService', () => ({
  companyPageService: mockCompanyPageService,
}))

const mockPostService = vi.hoisted(() => ({
  getFeed: vi.fn(),
}))

vi.mock('@/services/postService', () => ({
  postService: mockPostService,
}))

vi.mock('@/stores/auth', () => ({
  useAuthStore: () => ({
    user: { id: 'user-owner', displayName: 'Owner', email: 'o@x.com' },
  }),
}))

// ── Fixtures ──────────────────────────────────────────────────────────────────

function makeOwnerPage(urlSlug: string | null = null) {
  return {
    id:            'company-1',
    name:          'Acme Corp',
    description:   'A fine company.',
    followerCount: 42,
    isFollowing:   false,
    myRole:        'Owner' as const,
    createdBy:     { id: 'user-owner', displayName: 'Owner' },
    createdAt:     '2024-01-01T00:00:00Z',
    urlSlug,
  }
}

function makeRouter() {
  return createRouter({
    history: createMemoryHistory(),
    routes: [
      { path: '/company/:id', name: 'company', component: CompanyView },
      { path: '/profile/:userId', name: 'profile', component: { template: '<div/>' } },
    ],
  })
}

let currentRouter: ReturnType<typeof makeRouter> | null = null

async function mountView(urlSlug: string | null = null) {
  const router = makeRouter()
  currentRouter = router
  await router.push('/company/company-1')

  mockCompanyPageService.getPage.mockResolvedValue(makeOwnerPage(urlSlug))
  mockCompanyPageService.getAdmins.mockResolvedValue({
    admins: [{ userId: 'user-owner', displayName: 'Owner', role: 'Owner' }],
  })
  mockPostService.getFeed.mockResolvedValue({ items: [], hasMore: false })

  const wrapper = mount(CompanyView, {
    global: {
      plugins: [createPinia(), router],
      stubs: {
        // Stub PostCard to keep tests fast (not relevant here)
        PostCard: true,
      },
    },
  })
  await flushPromises()
  return wrapper
}

/**
 * The "admins" tab is no longer rendered as an inline tab button — it's
 * reachable only via the sidebar's Settings link (which sets
 * ?tab=settings → activeTab='admins'). Mimic that navigation here.
 *
 * The wrapper parameter is intentionally accepted (even though unused) so
 * call sites read naturally and don't have to be rewritten everywhere.
 */
async function switchToAdminsTab(_wrapper: ReturnType<typeof mount>) {
  if (!currentRouter) throw new Error('Router not initialised — call mountView first')
  await currentRouter.push('/company/company-1?tab=settings')
  await flushPromises()
}

// ── Tests ─────────────────────────────────────────────────────────────────────

describe('CompanyView — SlugPicker integration', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.useFakeTimers()
    vi.clearAllMocks()
    mockSlugService.check.mockResolvedValue({ available: true })
    mockSlugService.setCompanySlug.mockResolvedValue(undefined)
  })

  afterEach(() => {
    vi.useRealTimers()
  })

  it('does NOT render the SlugPicker on the posts tab (default)', async () => {
    const wrapper = await mountView()
    expect(wrapper.find('[data-testid="slug-picker"]').exists()).toBe(false)
  })

  it('renders the SlugPicker when the admins tab is active', async () => {
    const wrapper = await mountView()
    await switchToAdminsTab(wrapper)
    expect(wrapper.find('[data-testid="slug-picker"]').exists()).toBe(true)
  })

  it('shows the Public URL heading inside the slug picker', async () => {
    const wrapper = await mountView()
    await switchToAdminsTab(wrapper)
    expect(wrapper.find('[data-testid="slug-picker"]').text()).toContain('Public URL')
  })

  it('passes null initialSlug when page has no slug — input is empty', async () => {
    const wrapper = await mountView(null)
    await switchToAdminsTab(wrapper)
    const input = wrapper.find('[data-testid="slug-input"]').element as HTMLInputElement
    expect(input.value).toBe('')
  })

  it('passes the current slug — input pre-filled', async () => {
    const wrapper = await mountView('acme-corp')
    await switchToAdminsTab(wrapper)
    const input = wrapper.find('[data-testid="slug-input"]').element as HTMLInputElement
    expect(input.value).toBe('acme-corp')
  })

  it('calls setCompanySlug with the company id when saving', async () => {
    const wrapper = await mountView(null)
    await switchToAdminsTab(wrapper)

    await wrapper.find('[data-testid="slug-input"]').setValue('acme-corp')
    vi.advanceTimersByTime(400)
    await flushPromises()

    await wrapper.find('[data-testid="save-btn"]').trigger('click')
    await flushPromises()

    expect(mockSlugService.setCompanySlug).toHaveBeenCalledWith('company-1', 'acme-corp')
  })

  it('shows the success banner after saving', async () => {
    const wrapper = await mountView(null)
    await switchToAdminsTab(wrapper)

    await wrapper.find('[data-testid="slug-input"]').setValue('acme-corp')
    vi.advanceTimersByTime(400)
    await flushPromises()

    await wrapper.find('[data-testid="save-btn"]').trigger('click')
    await flushPromises()

    expect(wrapper.find('[data-testid="save-success"]').exists()).toBe(true)
  })

  it('calls setCompanySlug(id, null) when clearing the slug', async () => {
    const wrapper = await mountView('acme-corp')
    await switchToAdminsTab(wrapper)

    await wrapper.find('[data-testid="clear-btn"]').trigger('click')
    await flushPromises()

    expect(mockSlugService.setCompanySlug).toHaveBeenCalledWith('company-1', null)
  })
})
