import { describe, it, expect, vi, beforeEach } from 'vitest'
import { setActivePinia, createPinia } from 'pinia'
import { useFeedStore } from '@/stores/feed'

// ── Mock postService ──────────────────────────────────────────────────────────

const mockPostService = vi.hoisted(() => ({
  getFeed:    vi.fn(),
  createPost: vi.fn(),
  deletePost: vi.fn(),
}))

vi.mock('@/services/postService', () => ({
  postService: mockPostService,
}))

// ── Helpers ───────────────────────────────────────────────────────────────────

function makePost(id: string, content = `Post ${id}`) {
  return {
    id,
    author:    { id: 'author-1', displayName: 'Alice' },
    content,
    postType:  'Text' as const,
    privacy:   'Friends' as const,
    media:     [],
    createdAt: '2024-01-01T00:00:00Z',
    updatedAt: '2024-01-01T00:00:00Z',
  }
}

function makeFeedItem(post: ReturnType<typeof makePost>) {
  return { itemType: 'Post' as const, post }
}

// ── Tests ─────────────────────────────────────────────────────────────────────

describe('useFeedStore', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.clearAllMocks()
  })

  // ── loadFeed ───────────────────────────────────────────────────────────────

  it('loadFeed — sets items on success', async () => {
    const feedItems = [makePost('1'), makePost('2')].map(makeFeedItem)
    mockPostService.getFeed.mockResolvedValue({ items: feedItems, page: 1, pageSize: 20, hasMore: false })

    const store = useFeedStore()
    await store.loadFeed()

    expect(store.items).toEqual(feedItems)
    expect(store.hasMore).toBe(false)
    expect(store.loading).toBe(false)
    expect(store.error).toBeNull()
  })

  it('loadFeed — sets error on failure', async () => {
    mockPostService.getFeed.mockRejectedValue(new Error('Network error'))

    const store = useFeedStore()
    await store.loadFeed()

    expect(store.items).toHaveLength(0)
    expect(store.error).toBeTruthy()
  })

  // ── loadMore ───────────────────────────────────────────────────────────────

  it('loadMore — appends items to existing list', async () => {
    const page1 = [makePost('1'), makePost('2')].map(makeFeedItem)
    const page2 = [makePost('3'), makePost('4')].map(makeFeedItem)

    mockPostService.getFeed
      .mockResolvedValueOnce({ items: page1, page: 1, pageSize: 20, hasMore: true })
      .mockResolvedValueOnce({ items: page2, page: 2, pageSize: 20, hasMore: false })

    const store = useFeedStore()
    await store.loadFeed()
    await store.loadMore()

    expect(store.items).toHaveLength(4)
    expect(store.items[2].post?.id).toBe('3')
    expect(store.hasMore).toBe(false)
    expect(store.page).toBe(2)
  })

  it('loadMore — does nothing when hasMore is false', async () => {
    mockPostService.getFeed.mockResolvedValue({ items: [], page: 1, pageSize: 20, hasMore: false })

    const store = useFeedStore()
    await store.loadFeed()
    await store.loadMore()

    // getFeed called only once (for loadFeed, not for loadMore which returned early)
    expect(mockPostService.getFeed).toHaveBeenCalledTimes(1)
  })

  // ── prependPost ────────────────────────────────────────────────────────────

  it('prependPost — adds post to front of list', () => {
    const store = useFeedStore()
    const post = makePost('new-post')

    store.prependPost(post)

    expect(store.items[0].post?.id).toBe('new-post')
  })

  it('prependPost — deduplicates by id', () => {
    const store = useFeedStore()
    const post = makePost('dup')

    store.prependPost(post)
    store.prependPost(post) // second call should not add another

    expect(store.items).toHaveLength(1)
  })

  // ── createPost ─────────────────────────────────────────────────────────────

  it('createPost — calls service and prepends returned post', async () => {
    const newPost = makePost('created')
    mockPostService.createPost.mockResolvedValue(newPost)

    const store = useFeedStore()
    const result = await store.createPost({
      content:  'Hello',
      postType: 'Text',
      privacy:  'Friends',
    })

    expect(result.id).toBe('created')
    expect(store.items[0].post?.id).toBe('created')
  })

  // ── deletePost ─────────────────────────────────────────────────────────────

  it('deletePost — removes post from list', async () => {
    mockPostService.deletePost.mockResolvedValue(undefined)
    mockPostService.getFeed.mockResolvedValue({
      items: [makePost('1'), makePost('2')].map(makeFeedItem),
      page: 1, pageSize: 20, hasMore: false,
    })

    const store = useFeedStore()
    await store.loadFeed()
    await store.deletePost('1')

    expect(store.items).toHaveLength(1)
    expect(store.items[0].post?.id).toBe('2')
  })

  // ── reset ──────────────────────────────────────────────────────────────────

  it('reset — restores initial state', async () => {
    mockPostService.getFeed.mockResolvedValue({
      items: [makePost('1')].map(makeFeedItem),
      page: 1, pageSize: 20, hasMore: false,
    })

    const store = useFeedStore()
    await store.loadFeed()

    store.reset()

    expect(store.items).toHaveLength(0)
    expect(store.page).toBe(1)
    expect(store.hasMore).toBe(true)
    expect(store.error).toBeNull()
  })
})
