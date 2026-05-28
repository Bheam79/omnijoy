import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount, flushPromises, RouterLinkStub } from '@vue/test-utils'
import SearchResultsView from '@/views/search/SearchResultsView.vue'

// ── Mock the searchService ────────────────────────────────────────────────────

const mockSearchService = vi.hoisted(() => ({
  search:  vi.fn(),
  suggest: vi.fn(),
}))

vi.mock('@/services/searchService', () => ({
  searchService: mockSearchService,
}))

// ── Mock vue-router ───────────────────────────────────────────────────────────

const mockRouteState = vi.hoisted(() => ({
  query: { q: 'hello' } as Record<string, string>,
}))
const mockReplace = vi.hoisted(() => vi.fn())

vi.mock('vue-router', async () => {
  const actual = await vi.importActual<typeof import('vue-router')>('vue-router')
  return {
    ...actual,
    // Return a fresh object each time useRoute() is called so watchers re-read
    useRoute:  () => ({ get query() { return mockRouteState.query } }),
    useRouter: () => ({ replace: mockReplace, push: vi.fn() }),
  }
})

// ── Helpers ───────────────────────────────────────────────────────────────────

function makeResponse(over: Partial<{
  users: unknown[]; posts: unknown[]; events: unknown[]; companies: unknown[]; hasMore: boolean
}> = {}) {
  return {
    query:     'hello',
    type:      'all',
    users:     over.users     ?? [],
    posts:     over.posts     ?? [],
    events:    over.events    ?? [],
    companies: over.companies ?? [],
    page:      1,
    pageSize:  5,
    hasMore:   over.hasMore   ?? false,
  }
}

function mountView() {
  return mount(SearchResultsView, {
    global: {
      stubs: { RouterLink: RouterLinkStub },
    },
  })
}

// ── Tests ─────────────────────────────────────────────────────────────────────

describe('SearchResultsView', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    mockRouteState.query = { q: 'hello' }
    mockSearchService.search.mockResolvedValue(makeResponse())
  })

  // ── Header / empty query ──────────────────────────────────────────────────

  it('shows the search query in header when q is present', async () => {
    const wrapper = mountView()
    await flushPromises()
    expect(wrapper.text()).toContain('hello')
  })

  it('shows the empty-query placeholder when q is missing', async () => {
    mockRouteState.query = {}
    const wrapper = mountView()
    await flushPromises()
    expect(wrapper.text()).toContain('Search Omnijoy')
    expect(mockSearchService.search).not.toHaveBeenCalled()
  })

  // ── "all" tab rendering ───────────────────────────────────────────────────

  it('calls /api/search with type=all on mount when q is present', async () => {
    mountView()
    await flushPromises()
    expect(mockSearchService.search).toHaveBeenCalledWith({
      q: 'hello', type: 'all', pageSize: 5,
    })
  })

  it('renders results for each populated category on the "all" tab', async () => {
    mockSearchService.search.mockResolvedValueOnce(makeResponse({
      users:     [{ id: 'u1', displayName: 'Alice', bio: 'hi' }],
      posts:     [{
        id: 'p1', authorId: 'u1', authorDisplayName: 'Alice',
        content: 'Hello world', postType: 'Text', privacy: 'Everyone',
        createdAt: '2024-06-01T00:00:00Z',
      }],
      events:    [{
        id: 'e1', title: 'My Event', startAt: '2030-06-15T18:00:00Z',
        creatorId: 'u1', creatorDisplayName: 'Alice',
      }],
      companies: [{ id: 'c1', name: 'Acme', followerCount: 0 }],
    }))
    const wrapper = mountView()
    await flushPromises()
    const text = wrapper.text()
    expect(text).toContain('Alice')
    expect(text).toContain('Hello world')
    expect(text).toContain('My Event')
    expect(text).toContain('Acme')
  })

  it('shows "No results found" on empty response', async () => {
    mockSearchService.search.mockResolvedValueOnce(makeResponse())
    const wrapper = mountView()
    await flushPromises()
    expect(wrapper.text()).toContain('No results found')
  })

  it('shows the error banner if search rejects', async () => {
    mockSearchService.search.mockRejectedValueOnce(
      Object.assign(new Error('boom'), { response: { data: { error: 'Server error' } } }),
    )
    const wrapper = mountView()
    await flushPromises()
    expect(wrapper.text()).toContain('Server error')
  })

  // ── Tab switching ─────────────────────────────────────────────────────────

  it('clicking a tab calls router.replace with the new tab query', async () => {
    const wrapper = mountView()
    await flushPromises()
    await wrapper.find('[data-test="tab-users"]').trigger('click')
    expect(mockReplace).toHaveBeenCalledWith({
      name:  'search',
      query: expect.objectContaining({ q: 'hello', tab: 'users' }),
    })
  })

  it('renders a People grid when the users tab is active', async () => {
    mockRouteState.query = { q: 'hello', tab: 'users' }
    mockSearchService.search.mockResolvedValueOnce(makeResponse({
      users: [
        { id: 'u1', displayName: 'Alice', bio: 'Bio A' },
        { id: 'u2', displayName: 'Bob' },
      ],
      hasMore: true,
    }))
    const wrapper = mountView()
    await flushPromises()
    expect(mockSearchService.search).toHaveBeenCalledWith({
      q: 'hello', type: 'users', page: 1, pageSize: 20,
    })
    expect(wrapper.text()).toContain('Alice')
    expect(wrapper.text()).toContain('Bob')
    expect(wrapper.find('[data-test="load-more"]').exists()).toBe(true)
  })

  it('shows empty state when a category tab has no items', async () => {
    mockRouteState.query = { q: 'hello', tab: 'posts' }
    mockSearchService.search.mockResolvedValueOnce(makeResponse())
    const wrapper = mountView()
    await flushPromises()
    expect(wrapper.text()).toContain('No posts found')
  })

  it('clicking "Load more" requests the next page and appends', async () => {
    mockRouteState.query = { q: 'hello', tab: 'companies' }
    mockSearchService.search
      .mockResolvedValueOnce(makeResponse({
        companies: [{ id: 'c1', name: 'Acme', followerCount: 5 }],
        hasMore:   true,
      }))
      .mockResolvedValueOnce(makeResponse({
        companies: [{ id: 'c2', name: 'Globex', followerCount: 0 }],
        hasMore:   false,
      }))
    const wrapper = mountView()
    await flushPromises()
    expect(wrapper.text()).toContain('Acme')
    await wrapper.find('[data-test="load-more"]').trigger('click')
    await flushPromises()
    expect(mockSearchService.search).toHaveBeenLastCalledWith({
      q: 'hello', type: 'companies', page: 2, pageSize: 20,
    })
    expect(wrapper.text()).toContain('Globex')
    // First page items should still be present
    expect(wrapper.text()).toContain('Acme')
  })

  it('renders events with formatted dates on the events tab', async () => {
    mockRouteState.query = { q: 'hello', tab: 'events' }
    mockSearchService.search.mockResolvedValueOnce(makeResponse({
      events: [{
        id: 'e1', title: 'My Event', startAt: '2030-06-15T18:00:00Z',
        creatorId: 'u1', creatorDisplayName: 'Alice', location: 'Stockholm',
      }],
    }))
    const wrapper = mountView()
    await flushPromises()
    expect(wrapper.text()).toContain('My Event')
    expect(wrapper.text()).toContain('Stockholm')
  })

  it('renders posts tab content (author + body)', async () => {
    mockRouteState.query = { q: 'hello', tab: 'posts' }
    mockSearchService.search.mockResolvedValueOnce(makeResponse({
      posts: [{
        id: 'p1', authorId: 'u1', authorDisplayName: 'Alice',
        content: 'A post', postType: 'Text', privacy: 'Everyone',
        createdAt: '2024-06-01T00:00:00Z',
      }],
    }))
    const wrapper = mountView()
    await flushPromises()
    expect(wrapper.text()).toContain('A post')
    expect(wrapper.text()).toContain('Alice')
  })
})
