import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount, flushPromises, RouterLinkStub } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'

// ── Route mock ────────────────────────────────────────────────────────────────

const mockRoute = vi.hoisted(() => ({
  params: { id: 'evt-1' } as Record<string, string>,
}))

vi.mock('vue-router', async (importOriginal) => {
  const actual = await importOriginal<typeof import('vue-router')>()
  return { ...actual, useRoute: () => mockRoute }
})

// ── Service mock ──────────────────────────────────────────────────────────────

const mockEventService = vi.hoisted(() => ({
  getEvent:      vi.fn(),
  getEventPosts: vi.fn(),
}))

vi.mock('@/services/eventService', () => ({
  eventService: mockEventService,
}))

// ── Auth store mock ───────────────────────────────────────────────────────────

const authStore = vi.hoisted(() => ({
  isAuthenticated: false,
}))

vi.mock('@/stores/auth', () => ({
  useAuthStore: () => authStore,
}))

// Import after mocks
import ShareEventView from '@/views/share/ShareEventView.vue'
import type { EventDto } from '@/services/eventService'
import type { PostDto } from '@/services/postService'

// ── Fixtures ──────────────────────────────────────────────────────────────────

const sampleEvent: EventDto = {
  id:        'evt-1',
  creator:   { id: 'u1', displayName: 'Alice', avatarUrl: undefined },
  title:     'Vue Conf 2026',
  description: 'A gathering of Vue enthusiasts.',
  startAt:   '2026-06-15T18:00:00Z',
  location:  'Stockholm, Sweden',
  privacy:   'Everyone',
  postingPolicy: 'Everyone',
  goingCount: 42,
  maybeCount: 7,
  notGoingCount: 1,
  createdAt: '2026-01-01T00:00:00Z',
}

const samplePost = (id: string, name: string, content: string): PostDto => ({
  id,
  author:    { id: `u-${id}`, displayName: name, avatarUrl: undefined },
  content,
  postType:  'Text',
  privacy:   'Everyone',
  media:     [],
  isSavedByMe: false,
  createdAt: '2026-05-10T09:00:00Z',
  updatedAt: '2026-05-10T09:00:00Z',
})

const emptyPostsPage = { items: [] as PostDto[], page: 1, pageSize: 20, hasMore: false }

// ── Mount helper ──────────────────────────────────────────────────────────────

function mountView() {
  return mount(ShareEventView, {
    global: {
      plugins: [createPinia()],
      stubs:   { RouterLink: RouterLinkStub },
    },
  })
}

// ── Tests ─────────────────────────────────────────────────────────────────────

describe('ShareEventView', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.clearAllMocks()
    authStore.isAuthenticated = false
    mockRoute.params = { id: 'evt-1' }
    mockEventService.getEvent.mockResolvedValue({ ...sampleEvent })
    mockEventService.getEventPosts.mockResolvedValue({ ...emptyPostsPage })
  })

  // ── Fetch on mount ────────────────────────────────────────────────────────

  it('fetches the event using the id from route.params on mount', async () => {
    mockRoute.params = { id: 'evt-99' }
    mountView()
    await flushPromises()

    expect(mockEventService.getEvent).toHaveBeenCalledWith('evt-99')
  })

  it('also fetches event posts after the event loads', async () => {
    mountView()
    await flushPromises()

    expect(mockEventService.getEventPosts).toHaveBeenCalledWith('evt-1', 1)
  })

  it('shows a Loading… placeholder before the event resolves', async () => {
    mockEventService.getEvent.mockReturnValue(new Promise(() => { /* never */ }))
    const wrapper = mountView()
    expect(wrapper.text()).toContain('Loading')
  })

  // ── Successful render ─────────────────────────────────────────────────────

  it('renders the event title, host, location, and counts when loaded', async () => {
    const wrapper = mountView()
    await flushPromises()

    expect(wrapper.find('h1').text()).toBe('Vue Conf 2026')
    expect(wrapper.text()).toContain('Hosted by')
    expect(wrapper.text()).toContain('Alice')
    expect(wrapper.text()).toContain('Stockholm, Sweden')
    expect(wrapper.text()).toContain('Going 42')
    expect(wrapper.text()).toContain('Maybe 7')
  })

  it('renders the event description when present', async () => {
    const wrapper = mountView()
    await flushPromises()

    expect(wrapper.text()).toContain('A gathering of Vue enthusiasts.')
  })

  it('renders the formatted start date (year + month) in the When row', async () => {
    const wrapper = mountView()
    await flushPromises()

    // Date formatting is locale-dependent; assert the year + month name only
    expect(wrapper.text()).toContain('2026')
    expect(wrapper.text().toLowerCase()).toContain('june')
  })

  it('omits the Where row when the event has no location', async () => {
    mockEventService.getEvent.mockResolvedValue({ ...sampleEvent, location: undefined })

    const wrapper = mountView()
    await flushPromises()

    expect(wrapper.text()).not.toContain('Where')
  })

  // ── Get tickets ──────────────────────────────────────────────────────────

  it('renders the Get tickets link when ticketUrl is set', async () => {
    mockEventService.getEvent.mockResolvedValue({
      ...sampleEvent,
      ticketUrl: 'https://tickets.example.com/vueconf',
    })

    const wrapper = mountView()
    await flushPromises()

    const ticket = wrapper.find('[data-testid="event-buy-tickets"]')
    expect(ticket.exists()).toBe(true)
    expect(ticket.attributes('href')).toBe('https://tickets.example.com/vueconf')
    expect(ticket.attributes('target')).toBe('_blank')
  })

  it('does not render Get tickets when ticketUrl is missing', async () => {
    const wrapper = mountView()
    await flushPromises()

    expect(wrapper.find('[data-testid="event-buy-tickets"]').exists()).toBe(false)
  })

  // ── Unauth state — "Sign up to RSVP" ──────────────────────────────────────

  it('renders the "Sign up to RSVP" CTA (→ /register) when unauthenticated', async () => {
    authStore.isAuthenticated = false
    const wrapper = mountView()
    await flushPromises()

    const links = wrapper.findAllComponents(RouterLinkStub)
    const cta = links.find(l => l.props('to') === '/register')
    expect(cta).toBeDefined()
    expect(cta!.text()).toContain('Sign up to RSVP')
  })

  it('shows the Sign in link in the header when unauthenticated', async () => {
    authStore.isAuthenticated = false
    const wrapper = mountView()
    await flushPromises()

    const links = wrapper.findAllComponents(RouterLinkStub)
    expect(links.find(l => l.props('to') === '/login')).toBeDefined()
  })

  // ── Auth state — "View event" goes to the real event page ─────────────────

  it('renders the "View event" CTA pointing at /events/:id when authenticated', async () => {
    authStore.isAuthenticated = true
    const wrapper = mountView()
    await flushPromises()

    const links = wrapper.findAllComponents(RouterLinkStub)
    const cta = links.find(l => l.props('to') === '/events/evt-1')
    expect(cta).toBeDefined()
    expect(cta!.text()).toContain('View event')
  })

  it('does NOT render /register CTA when authenticated', async () => {
    authStore.isAuthenticated = true
    const wrapper = mountView()
    await flushPromises()

    const links = wrapper.findAllComponents(RouterLinkStub)
    expect(links.find(l => l.props('to') === '/register')).toBeUndefined()
  })

  // ── Discussion / event posts ──────────────────────────────────────────────

  it('renders the Discussion section with the "No posts yet." empty state', async () => {
    const wrapper = mountView()
    await flushPromises()

    const discussion = wrapper.find('[data-testid="event-discussion"]')
    expect(discussion.exists()).toBe(true)
    expect(discussion.text()).toContain('No posts yet.')
  })

  it('renders event-post items when the discussion has posts', async () => {
    mockEventService.getEventPosts.mockResolvedValue({
      items: [samplePost('p1', 'Bob', 'Looking forward to it!')],
      page: 1, pageSize: 20, hasMore: false,
    })

    const wrapper = mountView()
    await flushPromises()

    const discussion = wrapper.find('[data-testid="event-discussion"]')
    expect(discussion.text()).toContain('Bob')
    expect(discussion.text()).toContain('Looking forward to it!')
  })

  it('shows the Load more posts button when hasMore is true and loads the next page on click', async () => {
    mockEventService.getEventPosts.mockResolvedValueOnce({
      items: [samplePost('p1', 'Bob', 'First')],
      page: 1, pageSize: 20, hasMore: true,
    })
    mockEventService.getEventPosts.mockResolvedValueOnce({
      items: [samplePost('p2', 'Carol', 'Second')],
      page: 2, pageSize: 20, hasMore: false,
    })

    const wrapper = mountView()
    await flushPromises()

    const loadMore = wrapper.findAll('button').find(b => b.text().includes('Load more posts'))
    expect(loadMore).toBeDefined()

    await loadMore!.trigger('click')
    await flushPromises()

    expect(mockEventService.getEventPosts).toHaveBeenLastCalledWith('evt-1', 2)
    expect(wrapper.text()).toContain('First')
    expect(wrapper.text()).toContain('Second')
  })

  it('a failed event-posts fetch does not break the page', async () => {
    mockEventService.getEventPosts.mockRejectedValue(new Error('boom'))

    const wrapper = mountView()
    await flushPromises()

    // The event itself still renders fine
    expect(wrapper.find('h1').text()).toBe('Vue Conf 2026')
    // Discussion is empty/no posts
    expect(wrapper.text()).toContain('No posts yet.')
  })

  // ── Error paths ───────────────────────────────────────────────────────────

  it('shows "Event not found" on a 404 response', async () => {
    mockEventService.getEvent.mockRejectedValue({ response: { status: 404 } })
    const wrapper = mountView()
    await flushPromises()

    expect(wrapper.text()).toContain('Event not found')
  })

  it('shows "This event is not public" on a 403/401 response', async () => {
    mockEventService.getEvent.mockRejectedValue({ response: { status: 403 } })
    const wrapper = mountView()
    await flushPromises()

    expect(wrapper.text()).toContain('This event is not public')
  })

  it('shows a generic error with the server message on other failures', async () => {
    mockEventService.getEvent.mockRejectedValue({
      response: { status: 500, data: { error: 'Boom server' } },
    })
    const wrapper = mountView()
    await flushPromises()

    expect(wrapper.text()).toContain('Could not load event')
    expect(wrapper.text()).toContain('Boom server')
  })
})
