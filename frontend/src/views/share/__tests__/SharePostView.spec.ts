import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount, flushPromises, RouterLinkStub } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'

// ── Route mock ────────────────────────────────────────────────────────────────

const mockRoute = vi.hoisted(() => ({
  params: { id: 'post-1' } as Record<string, string>,
}))

vi.mock('vue-router', async (importOriginal) => {
  const actual = await importOriginal<typeof import('vue-router')>()
  return { ...actual, useRoute: () => mockRoute }
})

// ── Service mock ──────────────────────────────────────────────────────────────

const mockPostService = vi.hoisted(() => ({
  getPost: vi.fn(),
}))

vi.mock('@/services/postService', () => ({
  postService: mockPostService,
}))

// ── Auth store mock ───────────────────────────────────────────────────────────

const authStore = vi.hoisted(() => ({
  isAuthenticated: false,
}))

vi.mock('@/stores/auth', () => ({
  useAuthStore: () => authStore,
}))

// Import after mocks
import SharePostView from '@/views/share/SharePostView.vue'
import type { PostDto } from '@/services/postService'

// ── Fixtures ──────────────────────────────────────────────────────────────────

const samplePost: PostDto = {
  id:         'post-1',
  author:     { id: 'u1', displayName: 'Alice', avatarUrl: undefined },
  content:    'Hello from the public share view.',
  postType:   'Text',
  privacy:    'Everyone',
  media:      [],
  createdAt:  '2026-05-01T12:00:00Z',
  updatedAt:  '2026-05-01T12:00:00Z',
}

// ── Mount helper ──────────────────────────────────────────────────────────────

function mountView() {
  return mount(SharePostView, {
    global: {
      plugins: [createPinia()],
      stubs:   { RouterLink: RouterLinkStub },
    },
  })
}

// ── Tests ─────────────────────────────────────────────────────────────────────

describe('SharePostView', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.clearAllMocks()
    authStore.isAuthenticated = false
    mockRoute.params = { id: 'post-1' }
    mockPostService.getPost.mockResolvedValue({ ...samplePost })
  })

  // ── Fetch on mount ────────────────────────────────────────────────────────

  it('fetches the post using the id from route.params on mount', async () => {
    mockRoute.params = { id: 'post-42' }
    mountView()
    await flushPromises()

    expect(mockPostService.getPost).toHaveBeenCalledWith('post-42')
  })

  it('shows a Loading… placeholder before the request resolves', async () => {
    mockPostService.getPost.mockReturnValue(new Promise(() => { /* never */ }))
    const wrapper = mountView()
    // No flushPromises — keep loading
    expect(wrapper.text()).toContain('Loading')
  })

  // ── Successful render ─────────────────────────────────────────────────────

  it('renders the post content and author display name when loaded', async () => {
    const wrapper = mountView()
    await flushPromises()

    expect(wrapper.text()).toContain('Hello from the public share view.')
    expect(wrapper.text()).toContain('Alice')
  })

  it('renders an Image-type post with media images', async () => {
    mockPostService.getPost.mockResolvedValue({
      ...samplePost,
      postType: 'Image',
      media: [
        { id: 'm1', mediaType: 'Image', url: 'https://cdn/img1.jpg', order: 0 },
        { id: 'm2', mediaType: 'Image', url: 'https://cdn/img2.jpg', order: 1 },
      ],
    })

    const wrapper = mountView()
    await flushPromises()

    const imgs = wrapper.findAll('img').filter(i => i.attributes('src')?.startsWith('https://cdn/'))
    expect(imgs).toHaveLength(2)
  })

  it('renders a link preview when post.linkPreview is set', async () => {
    mockPostService.getPost.mockResolvedValue({
      ...samplePost,
      linkPreview: {
        url:         'https://example.com',
        title:       'Example Title',
        description: 'Example description',
        siteName:    'EXAMPLE.COM',
      },
    })

    const wrapper = mountView()
    await flushPromises()

    expect(wrapper.text()).toContain('Example Title')
    expect(wrapper.text()).toContain('Example description')
    expect(wrapper.text()).toContain('EXAMPLE.COM')
  })

  it('renders company-page author when post.companyPage is set', async () => {
    mockPostService.getPost.mockResolvedValue({
      ...samplePost,
      companyPage: { id: 'cp-1', name: 'Acme Inc', logoUrl: undefined },
    })

    const wrapper = mountView()
    await flushPromises()

    expect(wrapper.text()).toContain('Acme Inc')
    expect(wrapper.text()).toContain('posted by Alice')
  })

  // ── Unauthenticated state — sign-in entry points ──────────────────────────

  it('shows the Sign in link in the header when unauthenticated', async () => {
    authStore.isAuthenticated = false
    const wrapper = mountView()
    await flushPromises()

    const links = wrapper.findAllComponents(RouterLinkStub)
    const signInLink = links.find(l => l.props('to') === '/login')
    expect(signInLink).toBeDefined()
    expect(wrapper.text()).toContain('Sign in')
  })

  it('shows the "Create an account" CTA pointing at /register when unauthenticated', async () => {
    authStore.isAuthenticated = false
    const wrapper = mountView()
    await flushPromises()

    const links = wrapper.findAllComponents(RouterLinkStub)
    const cta = links.find(l => l.props('to') === '/register')
    expect(cta).toBeDefined()
    expect(cta!.text()).toContain('Create an account')
  })

  it('does NOT render the "Open Omnijoy" header link when unauthenticated', async () => {
    authStore.isAuthenticated = false
    const wrapper = mountView()
    await flushPromises()

    const links = wrapper.findAllComponents(RouterLinkStub)
    const openLink = links.find(l => l.text() === 'Open Omnijoy')
    expect(openLink).toBeUndefined()
  })

  // ── Authenticated state — different CTAs ──────────────────────────────────

  it('replaces the Sign in link with an Open Omnijoy link when authenticated', async () => {
    authStore.isAuthenticated = true
    const wrapper = mountView()
    await flushPromises()

    const links = wrapper.findAllComponents(RouterLinkStub)
    expect(links.find(l => l.props('to') === '/login')).toBeUndefined()
    expect(links.some(l => l.props('to') === '/wall')).toBe(true)
  })

  it('renders an "Open Omnijoy" CTA (→ /wall) instead of "Create an account" when authenticated', async () => {
    authStore.isAuthenticated = true
    const wrapper = mountView()
    await flushPromises()

    const links = wrapper.findAllComponents(RouterLinkStub)
    expect(links.find(l => l.props('to') === '/register')).toBeUndefined()

    const ctaLinks = links.filter(l => l.props('to') === '/wall' && l.text().includes('Open Omnijoy'))
    // Header link AND the CTA both go to /wall
    expect(ctaLinks.length).toBeGreaterThanOrEqual(1)
  })

  // ── Error paths ───────────────────────────────────────────────────────────

  it('shows the "Post not found" error on a 404 response', async () => {
    mockPostService.getPost.mockRejectedValue({ response: { status: 404 } })
    const wrapper = mountView()
    await flushPromises()

    expect(wrapper.text()).toContain('Post not found')
    expect(wrapper.text()).toContain('does not exist or has been deleted')
  })

  it('shows "This post is not public" on a 403 response', async () => {
    mockPostService.getPost.mockRejectedValue({ response: { status: 403 } })
    const wrapper = mountView()
    await flushPromises()

    expect(wrapper.text()).toContain('This post is not public')
    expect(wrapper.text()).toContain('restricted who can see this post')
  })

  it('shows "This post is not public" with sign-in prompt on a 401 response', async () => {
    mockPostService.getPost.mockRejectedValue({ response: { status: 401 } })
    const wrapper = mountView()
    await flushPromises()

    expect(wrapper.text()).toContain('This post is not public')
    expect(wrapper.text()).toContain('sign in to view this post')
  })

  it('shows the server-supplied error message for a generic failure', async () => {
    mockPostService.getPost.mockRejectedValue({
      response: { status: 500, data: { error: 'Server explosion.' } },
    })
    const wrapper = mountView()
    await flushPromises()

    expect(wrapper.text()).toContain('Could not load post')
    expect(wrapper.text()).toContain('Server explosion.')
  })

  it('falls back to a generic message when the server omits one', async () => {
    mockPostService.getPost.mockRejectedValue(new Error('boom'))
    const wrapper = mountView()
    await flushPromises()

    expect(wrapper.text()).toContain('Could not load post')
    expect(wrapper.text()).toContain('Something went wrong')
  })

  it('always exposes a "Back to Omnijoy" link in error state', async () => {
    mockPostService.getPost.mockRejectedValue({ response: { status: 404 } })
    const wrapper = mountView()
    await flushPromises()

    const links = wrapper.findAllComponents(RouterLinkStub)
    const back = links.find(l => l.props('to') === '/' && l.text().includes('Back to Omnijoy'))
    expect(back).toBeDefined()
  })
})
