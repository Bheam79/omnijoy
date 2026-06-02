import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount, flushPromises, RouterLinkStub } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'

// ── Route mock ────────────────────────────────────────────────────────────────

const mockRoute = vi.hoisted(() => ({
  params: { id: 'user-1' } as Record<string, string>,
}))

vi.mock('vue-router', async (importOriginal) => {
  const actual = await importOriginal<typeof import('vue-router')>()
  return { ...actual, useRoute: () => mockRoute }
})

// ── Service mock ──────────────────────────────────────────────────────────────

const mockUserService = vi.hoisted(() => ({
  getProfile: vi.fn(),
}))

vi.mock('@/services/userService', () => ({
  userService: mockUserService,
}))

// ── Auth store mock ───────────────────────────────────────────────────────────

const authStore = vi.hoisted(() => ({
  isAuthenticated: false,
}))

vi.mock('@/stores/auth', () => ({
  useAuthStore: () => authStore,
}))

// Import after mocks
import ShareUserView from '@/views/share/ShareUserView.vue'
import type { UserProfile } from '@/services/userService'

// ── Fixtures ──────────────────────────────────────────────────────────────────

const sampleProfile: UserProfile = {
  id:             'user-1',
  displayName:    'Alice Wonderland',
  avatarUrl:      undefined,
  coverUrl:       undefined,
  bio:            'Curious traveller, software builder.',
  gender:         'NotDisclosed',
  showBirthDate:  false,
  friendCount:    128,
  isOwnProfile:   false,
  isFriend:       false,
  createdAt:      '2024-01-01T00:00:00Z',
}

// ── Mount helper ──────────────────────────────────────────────────────────────

function mountView() {
  return mount(ShareUserView, {
    global: {
      plugins: [createPinia()],
      stubs:   { RouterLink: RouterLinkStub },
    },
  })
}

// ── Tests ─────────────────────────────────────────────────────────────────────

describe('ShareUserView', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.clearAllMocks()
    authStore.isAuthenticated = false
    mockRoute.params = { id: 'user-1' }
    mockUserService.getProfile.mockResolvedValue({ ...sampleProfile })
  })

  // ── Fetch on mount ────────────────────────────────────────────────────────

  it('fetches the profile using the id from route.params on mount', async () => {
    mockRoute.params = { id: 'user-77' }
    mountView()
    await flushPromises()

    expect(mockUserService.getProfile).toHaveBeenCalledWith('user-77')
  })

  it('shows a Loading… placeholder before the request resolves', async () => {
    mockUserService.getProfile.mockReturnValue(new Promise(() => { /* never */ }))
    const wrapper = mountView()
    expect(wrapper.text()).toContain('Loading')
  })

  // ── Successful render ─────────────────────────────────────────────────────

  it('renders the display name, friend count, and bio when loaded', async () => {
    const wrapper = mountView()
    await flushPromises()

    expect(wrapper.find('h1').text()).toBe('Alice Wonderland')
    expect(wrapper.text()).toContain('128 friends')
    expect(wrapper.text()).toContain('Curious traveller, software builder.')
  })

  it('renders the initial in an avatar placeholder when avatarUrl is missing', async () => {
    const wrapper = mountView()
    await flushPromises()

    // The avatar placeholder shows the first letter of the display name
    expect(wrapper.text()).toContain('A')
  })

  it('renders the avatar <img> when avatarUrl is present', async () => {
    mockUserService.getProfile.mockResolvedValue({
      ...sampleProfile,
      avatarUrl: 'https://cdn/avatar.jpg',
    })

    const wrapper = mountView()
    await flushPromises()

    const img = wrapper.find('img[src="https://cdn/avatar.jpg"]')
    expect(img.exists()).toBe(true)
    expect(img.attributes('alt')).toBe('Alice Wonderland')
  })

  it('omits the bio paragraph when bio is empty', async () => {
    mockUserService.getProfile.mockResolvedValue({ ...sampleProfile, bio: undefined })

    const wrapper = mountView()
    await flushPromises()

    expect(wrapper.text()).not.toContain('Curious traveller')
  })

  // ── Unauth state — "Sign up to connect" ──────────────────────────────────

  it('renders the "Sign up to connect" CTA (→ /register) when unauthenticated', async () => {
    authStore.isAuthenticated = false
    const wrapper = mountView()
    await flushPromises()

    const links = wrapper.findAllComponents(RouterLinkStub)
    const cta = links.find(l => l.props('to') === '/register')
    expect(cta).toBeDefined()
    expect(cta!.text()).toContain('Sign up to connect')
  })

  it('shows the Sign in header link when unauthenticated', async () => {
    authStore.isAuthenticated = false
    const wrapper = mountView()
    await flushPromises()

    const links = wrapper.findAllComponents(RouterLinkStub)
    expect(links.find(l => l.props('to') === '/login')).toBeDefined()
  })

  // ── Auth state — "View full profile" and friend/message entry points ──────

  it('renders the "View full profile" CTA pointing at /profile/:id when authenticated', async () => {
    authStore.isAuthenticated = true
    const wrapper = mountView()
    await flushPromises()

    const links = wrapper.findAllComponents(RouterLinkStub)
    const cta = links.find(l => l.props('to') === '/profile/user-1')
    expect(cta).toBeDefined()
    expect(cta!.text()).toContain('View full profile')
  })

  it('replaces the Sign in link with Open Omnijoy when authenticated', async () => {
    authStore.isAuthenticated = true
    const wrapper = mountView()
    await flushPromises()

    const links = wrapper.findAllComponents(RouterLinkStub)
    expect(links.find(l => l.props('to') === '/login')).toBeUndefined()
    expect(links.find(l => l.props('to') === '/wall' && l.text().includes('Open Omnijoy'))).toBeDefined()
  })

  it('does NOT render /register CTA when authenticated', async () => {
    authStore.isAuthenticated = true
    const wrapper = mountView()
    await flushPromises()

    const links = wrapper.findAllComponents(RouterLinkStub)
    expect(links.find(l => l.props('to') === '/register')).toBeUndefined()
  })

  // ── Error paths ───────────────────────────────────────────────────────────

  it('shows "User not found" on a 404 response', async () => {
    mockUserService.getProfile.mockRejectedValue({ response: { status: 404 } })
    const wrapper = mountView()
    await flushPromises()

    expect(wrapper.text()).toContain('User not found')
    expect(wrapper.text()).toContain('does not exist')
  })

  it('shows "This profile is not public" on a 403/401 response', async () => {
    mockUserService.getProfile.mockRejectedValue({ response: { status: 403 } })
    const wrapper = mountView()
    await flushPromises()

    expect(wrapper.text()).toContain('This profile is not public')
  })

  it('shows the server-supplied error message on other failures', async () => {
    mockUserService.getProfile.mockRejectedValue({
      response: { status: 500, data: { error: 'Server explosion.' } },
    })
    const wrapper = mountView()
    await flushPromises()

    expect(wrapper.text()).toContain('Could not load profile')
    expect(wrapper.text()).toContain('Server explosion.')
  })

  it('falls back to a generic message when the server omits one', async () => {
    mockUserService.getProfile.mockRejectedValue(new Error('boom'))
    const wrapper = mountView()
    await flushPromises()

    expect(wrapper.text()).toContain('Could not load profile')
    expect(wrapper.text()).toContain('Something went wrong')
  })

  it('exposes a "Back to Omnijoy" link in error state', async () => {
    mockUserService.getProfile.mockRejectedValue({ response: { status: 404 } })
    const wrapper = mountView()
    await flushPromises()

    const links = wrapper.findAllComponents(RouterLinkStub)
    const back = links.find(l => l.props('to') === '/' && l.text().includes('Back to Omnijoy'))
    expect(back).toBeDefined()
  })
})
