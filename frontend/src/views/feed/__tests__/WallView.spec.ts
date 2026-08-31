import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import WallView from '../WallView.vue'

// ── SignalR mock ───────────────────────────────────────────────────────────────
//
// vi.hoisted() runs before module imports, so everything the factory needs must
// live here.  HubConnectionBuilder MUST be a regular function (not an arrow
// function) so it can be used with `new`.

const { hubConnectionMock, HubConnectionBuilderCtor } = vi.hoisted(() => {
  const conn = {
    on:     vi.fn(),
    start:  vi.fn().mockResolvedValue(undefined),
    stop:   vi.fn(),
    state:  'Connected',
    invoke: vi.fn().mockResolvedValue(undefined),
  }

  // Regular function — required for `new signalR.HubConnectionBuilder()` to work
  function HubConnectionBuilderCtor(this: unknown) {
    return {
      withUrl:                vi.fn().mockReturnThis(),
      withAutomaticReconnect: vi.fn().mockReturnThis(),
      configureLogging:       vi.fn().mockReturnThis(),
      build:                  vi.fn().mockReturnValue(conn),
    }
  }

  return { hubConnectionMock: conn, HubConnectionBuilderCtor }
})

vi.mock('@microsoft/signalr', () => ({
  HubConnectionBuilder: HubConnectionBuilderCtor,
  LogLevel:             { Warning: 1 },
  HubConnectionState:   { Connected: 'Connected' },
}))

// ── Store mocks ───────────────────────────────────────────────────────────────
//
// Plain objects (no Vue refs).  Each test sets initial state BEFORE mounting, so
// no reactivity is needed — the component reads values on first render.

const feedStore = vi.hoisted(() => ({
  items:       [] as unknown[],
  hasMore:     false,
  loading:     false,
  loadingMore: false,
  error:       null as string | null,
  loadFeed:    vi.fn().mockResolvedValue(undefined),
  loadMore:    vi.fn().mockResolvedValue(undefined),
  reset:       vi.fn(),
  prependPost:       vi.fn(),
  prependSharedPost: vi.fn(),
}))

vi.mock('@/stores/feed', () => ({
  useFeedStore: () => feedStore,
}))

const authStore = vi.hoisted(() => ({
  accessToken: 'test-token' as string | null,
}))

vi.mock('@/stores/auth', () => ({
  useAuthStore: () => authStore,
}))

const reactionsStore = vi.hoisted(() => ({
  applyUpdate: vi.fn(),
  reset:       vi.fn(),
}))

vi.mock('@/stores/reactions', () => ({
  useReactionsStore: () => reactionsStore,
}))

const commentsStore = vi.hoisted(() => ({
  applyNewComment: vi.fn(),
  applyReactionUpdate: vi.fn(),
  reset:           vi.fn(),
}))

vi.mock('@/stores/comments', () => ({
  useCommentsStore: () => commentsStore,
}))

// ── Child component stubs ─────────────────────────────────────────────────────

vi.mock('@/components/post/PostCard.vue', () => ({
  default: {
    name:     'PostCard',
    props:    ['post'],
    template: '<div data-testid="post-card" />',
  },
}))

vi.mock('@/components/post/SharedPostCard.vue', () => ({
  default: {
    name:     'SharedPostCard',
    props:    ['sharedPost'],
    template: '<div data-testid="shared-post-card" />',
  },
}))

vi.mock('@/components/post/PostComposer.vue', () => ({
  default: {
    name:     'PostComposer',
    template: '<div data-testid="post-composer" />',
  },
}))

// ── IntersectionObserver mock ─────────────────────────────────────────────────

type IOCallback = (entries: IntersectionObserverEntry[]) => void

let ioCallback:   IOCallback | null = null
let ioObserve:    ReturnType<typeof vi.fn>
let ioDisconnect: ReturnType<typeof vi.fn>

function installIntersectionObserver() {
  ioCallback   = null
  ioObserve    = vi.fn()
  ioDisconnect = vi.fn()

  // IntersectionObserver is used with `new` in the component — the implementation
  // must be a regular function (arrow functions cannot be constructors).
  vi.stubGlobal(
    'IntersectionObserver',
    vi.fn(function(this: unknown, cb: IOCallback) {
      ioCallback = cb
      // A constructor that explicitly returns an object gives that object as `new`'s result
      return { observe: ioObserve, disconnect: ioDisconnect }
    }),
  )
}

// ── Mount helper ──────────────────────────────────────────────────────────────

function mountView() {
  return mount(WallView, {
    global:   { plugins: [createPinia()] },
    attachTo: document.body,
  })
}

// ── Tests ─────────────────────────────────────────────────────────────────────

describe('WallView', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.clearAllMocks()

    // Reset store state to sensible defaults before each test
    feedStore.items       = []
    feedStore.hasMore     = false
    feedStore.loading     = false
    feedStore.loadingMore = false
    feedStore.error       = null

    authStore.accessToken = 'test-token'

    // Re-apply resolved value implementations after clearAllMocks (clears history, not impl)
    feedStore.loadFeed.mockResolvedValue(undefined)
    feedStore.loadMore.mockResolvedValue(undefined)
    hubConnectionMock.start.mockResolvedValue(undefined)
    hubConnectionMock.invoke.mockResolvedValue(undefined)

    installIntersectionObserver()
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  // ── Mount ──────────────────────────────────────────────────────────────────

  it('calls feed.loadFeed() on mount', async () => {
    mountView()
    await flushPromises()

    expect(feedStore.loadFeed).toHaveBeenCalledOnce()
  })

  it('routes CommentReactionCountsUpdated hub events to the comments store', async () => {
    mountView()
    await flushPromises()
    const registration = hubConnectionMock.on.mock.calls.find(
      ([eventName]) => eventName === 'CommentReactionCountsUpdated',
    )
    const event = {
      postId: 'post-1', commentId: 'comment-2', counts: [{ reactionType: 'Like', count: 3 }], total: 3,
    }

    registration?.[1](event)

    expect(commentsStore.applyReactionUpdate).toHaveBeenCalledWith(event)
  })

  // ── Loading skeleton ───────────────────────────────────────────────────────

  it('shows skeleton cards (animate-pulse divs) while feed.loading is true', async () => {
    feedStore.loading = true
    // Never resolve — keeps the component in the loading state
    feedStore.loadFeed.mockReturnValue(new Promise(() => {}))

    const wrapper = mountView()
    // Let Vue flush synchronous state; the pending promise keeps loading=true
    await flushPromises()

    const skeletons = wrapper.findAll('.animate-pulse')
    expect(skeletons.length).toBeGreaterThanOrEqual(1)
  })

  // ── Error state ────────────────────────────────────────────────────────────

  it('shows error message and Retry button when feed.error is set', async () => {
    feedStore.loading = false
    feedStore.error   = 'Network error'

    const wrapper = mountView()
    await flushPromises()

    expect(wrapper.text()).toContain('Network error')
    const retryBtn = wrapper.findAll('button').find(b => b.text() === 'Retry')
    expect(retryBtn).toBeDefined()
  })

  it('clicking Retry calls feed.loadFeed() again', async () => {
    feedStore.loading = false
    feedStore.error   = 'Some error'

    const wrapper = mountView()
    await flushPromises()

    const callsBefore = (feedStore.loadFeed as ReturnType<typeof vi.fn>).mock.calls.length

    const retryBtn = wrapper.findAll('button').find(b => b.text() === 'Retry')
    await retryBtn!.trigger('click')
    await flushPromises()

    expect((feedStore.loadFeed as ReturnType<typeof vi.fn>).mock.calls.length).toBe(
      callsBefore + 1,
    )
  })

  // ── Empty state ────────────────────────────────────────────────────────────

  it("shows 'Your wall is empty' when feed.items is empty and not loading", async () => {
    feedStore.loading = false
    feedStore.error   = null
    feedStore.items   = []

    const wrapper = mountView()
    await flushPromises()

    expect(wrapper.text()).toContain('Your wall is empty')
  })

  // ── Feed items ─────────────────────────────────────────────────────────────

  it('renders a PostCard for each item with itemType="Post"', async () => {
    feedStore.loading = false
    feedStore.error   = null
    feedStore.items   = [
      { itemType: 'Post', post: { id: 'p1', content: 'Hello' } },
      { itemType: 'Post', post: { id: 'p2', content: 'World' } },
    ]

    const wrapper = mountView()
    await flushPromises()

    expect(wrapper.findAll('[data-testid="post-card"]')).toHaveLength(2)
    expect(wrapper.findAll('[data-testid="shared-post-card"]')).toHaveLength(0)
  })

  it('renders a SharedPostCard for each item with itemType="SharedPost"', async () => {
    feedStore.loading = false
    feedStore.error   = null
    feedStore.items   = [
      { itemType: 'SharedPost', sharedPost: { id: 's1', originalPost: { id: 'p1' } } },
      { itemType: 'SharedPost', sharedPost: { id: 's2', originalPost: { id: 'p2' } } },
    ]

    const wrapper = mountView()
    await flushPromises()

    expect(wrapper.findAll('[data-testid="shared-post-card"]')).toHaveLength(2)
    expect(wrapper.findAll('[data-testid="post-card"]')).toHaveLength(0)
  })

  // ── Unmount / reset ────────────────────────────────────────────────────────

  it('calls feed.reset(), reactionsStore.reset(), and commentsStore.reset() on unmount', async () => {
    const wrapper = mountView()
    await flushPromises()

    wrapper.unmount()

    expect(feedStore.reset).toHaveBeenCalledOnce()
    expect(reactionsStore.reset).toHaveBeenCalledOnce()
    expect(commentsStore.reset).toHaveBeenCalledOnce()
  })

  // ── IntersectionObserver / infinite scroll ─────────────────────────────────

  it('sets up an IntersectionObserver sentinel on mount', async () => {
    // Sentinel is inside <template v-else> — requires items to be non-empty
    feedStore.loading = false
    feedStore.error   = null
    feedStore.items   = [{ itemType: 'Post', post: { id: 'p1' } }]

    mountView()
    await flushPromises()

    expect(IntersectionObserver).toHaveBeenCalled()
    expect(ioObserve).toHaveBeenCalled()
  })

  it('calls feed.loadMore() when sentinel intersects and feed.hasMore is true', async () => {
    feedStore.loading     = false
    feedStore.error       = null
    feedStore.loadingMore = false
    feedStore.hasMore     = true
    feedStore.items       = [{ itemType: 'Post', post: { id: 'p1' } }]

    mountView()
    await flushPromises()

    // Simulate the sentinel entering the viewport
    ioCallback!([{ isIntersecting: true } as IntersectionObserverEntry])
    await flushPromises()

    expect(feedStore.loadMore).toHaveBeenCalledOnce()
  })

  it('does NOT call feed.loadMore() when sentinel intersects but feed.hasMore is false', async () => {
    feedStore.loading     = false
    feedStore.error       = null
    feedStore.loadingMore = false
    feedStore.hasMore     = false
    feedStore.items       = [{ itemType: 'Post', post: { id: 'p1' } }]

    mountView()
    await flushPromises()

    ioCallback!([{ isIntersecting: true } as IntersectionObserverEntry])
    await flushPromises()

    expect(feedStore.loadMore).not.toHaveBeenCalled()
  })
})
