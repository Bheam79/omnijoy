import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { effectScope, nextTick } from 'vue'
import { useSearchSuggest, flattenSuggestions } from '@/composables/useSearchSuggest'
import type { SearchResponse } from '@/services/searchService'

// ── Mock the searchService ────────────────────────────────────────────────────

const mockSearchService = vi.hoisted(() => ({
  search:  vi.fn(),
  suggest: vi.fn(),
}))

vi.mock('@/services/searchService', () => ({
  searchService: mockSearchService,
}))

// ── Helpers ───────────────────────────────────────────────────────────────────

function makeResponse(over: Partial<SearchResponse> = {}): SearchResponse {
  return {
    query:    'test',
    type:     'all',
    users:     [],
    posts:     [],
    events:    [],
    companies: [],
    page:      1,
    pageSize:  5,
    hasMore:   false,
    ...over,
  }
}

/** Run a setup function inside a Vue effect scope so onUnmounted is valid. */
function withScope<T>(fn: () => T): { result: T; stop: () => void } {
  const scope = effectScope()
  const result = scope.run(fn) as T
  return { result, stop: () => scope.stop() }
}

// ── Tests ─────────────────────────────────────────────────────────────────────

describe('flattenSuggestions', () => {
  it('returns empty array for null', () => {
    expect(flattenSuggestions(null)).toEqual([])
  })

  it('flattens users into SuggestItems with profile route', () => {
    const items = flattenSuggestions(makeResponse({
      users: [{ id: 'u1', displayName: 'Alice', bio: 'Hi' }],
    }))
    expect(items).toHaveLength(1)
    expect(items[0]).toMatchObject({
      category: 'user',
      id:       'u1',
      label:    'Alice',
      secondary:'Hi',
      routeTo:  '/profile/u1',
    })
  })

  it('flattens posts with truncated content as label', () => {
    const longContent = 'A'.repeat(100)
    const items = flattenSuggestions(makeResponse({
      posts: [{
        id: 'p1',
        authorId: 'u1',
        authorDisplayName: 'Alice',
        content: longContent,
        postType: 'Text',
        privacy: 'Everyone',
        createdAt: '2024-01-01T00:00:00Z',
      }],
    }))
    expect(items).toHaveLength(1)
    expect(items[0].category).toBe('post')
    expect(items[0].label.endsWith('…')).toBe(true)
    expect(items[0].label.length).toBeLessThanOrEqual(60)
    expect(items[0].routeTo).toBe('/share/posts/p1')
    expect(items[0].secondary).toBe('Alice')
  })

  it('flattens posts with empty content to "Untitled post"', () => {
    const items = flattenSuggestions(makeResponse({
      posts: [{
        id: 'p1',
        authorId: 'u1',
        authorDisplayName: 'Alice',
        content: '',
        postType: 'Text',
        privacy: 'Everyone',
        createdAt: '2024-01-01T00:00:00Z',
      }],
    }))
    expect(items[0].label).toBe('Untitled post')
  })

  it('flattens events including formatted date / location', () => {
    const items = flattenSuggestions(makeResponse({
      events: [{
        id: 'e1',
        title: 'My Event',
        startAt: '2030-06-15T18:00:00Z',
        creatorId: 'u1',
        creatorDisplayName: 'Alice',
        location: 'Stockholm',
      }],
    }))
    expect(items[0]).toMatchObject({
      category: 'event',
      label:    'My Event',
      routeTo:  '/events/e1',
    })
    expect(items[0].secondary).toContain('Stockholm')
  })

  it('flattens companies with follower count secondary', () => {
    const items = flattenSuggestions(makeResponse({
      companies: [
        { id: 'c1', name: 'Acme', followerCount: 1 },
        { id: 'c2', name: 'Globex', followerCount: 5 },
      ],
    }))
    expect(items[0].secondary).toBe('1 follower')
    expect(items[1].secondary).toBe('5 followers')
  })

  it('preserves category order: users → posts → events → companies', () => {
    const items = flattenSuggestions(makeResponse({
      users:     [{ id: 'u1', displayName: 'Alice' }],
      posts:     [{
        id: 'p1', authorId: 'u1', authorDisplayName: 'Alice',
        content: 'hi', postType: 'Text', privacy: 'Everyone',
        createdAt: '2024-01-01T00:00:00Z',
      }],
      events:    [{
        id: 'e1', title: 'Event', startAt: '2030-06-15T18:00:00Z',
        creatorId: 'u1', creatorDisplayName: 'Alice',
      }],
      companies: [{ id: 'c1', name: 'Acme', followerCount: 0 }],
    }))
    expect(items.map(i => i.category)).toEqual(['user', 'post', 'event', 'company'])
  })
})

describe('useSearchSuggest', () => {
  beforeEach(() => {
    vi.useFakeTimers()
    vi.clearAllMocks()
  })

  afterEach(() => {
    vi.useRealTimers()
  })

  it('does not fire request below minChars', async () => {
    const { result: s, stop } = withScope(() =>
      useSearchSuggest({ minChars: 2 }),
    )
    s.query.value = 'a'
    await nextTick()
    vi.advanceTimersByTime(500)
    await nextTick()
    expect(mockSearchService.suggest).not.toHaveBeenCalled()
    stop()
  })

  it('debounces input by debounceMs and fires one call', async () => {
    mockSearchService.suggest.mockResolvedValue(makeResponse())
    const { result: s, stop } = withScope(() =>
      useSearchSuggest({ debounceMs: 300 }),
    )

    s.query.value = 'h'
    await nextTick()
    s.query.value = 'he'
    await nextTick()
    s.query.value = 'hel'
    await nextTick()
    expect(mockSearchService.suggest).not.toHaveBeenCalled()

    vi.advanceTimersByTime(300)
    await Promise.resolve()
    await Promise.resolve()
    expect(mockSearchService.suggest).toHaveBeenCalledTimes(1)
    expect(mockSearchService.suggest).toHaveBeenCalledWith('hel')
    stop()
  })

  it('populates items on successful fetchNow and sets highlighted=0', async () => {
    mockSearchService.suggest.mockResolvedValue(makeResponse({
      users: [{ id: 'u1', displayName: 'Alice' }],
    }))
    const { result: s, stop } = withScope(() => useSearchSuggest())

    await s.fetchNow('alice')
    expect(s.loading.value).toBe(false)
    expect(s.items.value).toHaveLength(1)
    expect(s.highlighted.value).toBe(0)
    expect(s.hasResults.value).toBe(true)
    stop()
  })

  it('ignores stale responses (last request wins)', async () => {
    let resolveFirst!:  (v: SearchResponse) => void
    let resolveSecond!: (v: SearchResponse) => void

    mockSearchService.suggest
      .mockImplementationOnce(() => new Promise<SearchResponse>((res) => { resolveFirst = res }))
      .mockImplementationOnce(() => new Promise<SearchResponse>((res) => { resolveSecond = res }))

    const { result: s, stop } = withScope(() => useSearchSuggest())

    const first  = s.fetchNow('a')
    const second = s.fetchNow('ab')

    // Resolve OUT OF ORDER — second resolves before first
    resolveSecond(makeResponse({ users: [{ id: 'u2', displayName: 'Bob' }] }))
    await second
    expect(s.items.value[0].id).toBe('u2')

    resolveFirst(makeResponse({ users: [{ id: 'u1', displayName: 'Alice' }] }))
    await first
    // Stale response — should NOT overwrite
    expect(s.items.value[0].id).toBe('u2')
    stop()
  })

  it('captures error and clears items on rejection', async () => {
    mockSearchService.suggest.mockRejectedValue(new Error('boom'))
    const { result: s, stop } = withScope(() => useSearchSuggest())
    await s.fetchNow('alice')
    expect(s.error.value).toBe('boom')
    expect(s.items.value).toHaveLength(0)
    stop()
  })

  it('clears state via watcher when query falls below minChars', async () => {
    mockSearchService.suggest.mockResolvedValue(makeResponse({
      users: [{ id: 'u1', displayName: 'Alice' }],
    }))
    const { result: s, stop } = withScope(() => useSearchSuggest({ debounceMs: 100 }))

    // Set query then advance timer to fire the debounced fetch
    s.query.value = 'alice'
    await nextTick()
    vi.advanceTimersByTime(100)
    await Promise.resolve()
    await Promise.resolve()
    expect(s.items.value).toHaveLength(1)

    // Now clear: watcher must wipe state
    s.query.value = ''
    await nextTick()
    expect(s.response.value).toBeNull()
    expect(s.items.value).toHaveLength(0)
    expect(s.open.value).toBe(false)
    stop()
  })

  // ── Keyboard navigation ─────────────────────────────────────────────────────

  it('moveDown / moveUp wrap around the list', async () => {
    mockSearchService.suggest.mockResolvedValue(makeResponse({
      users: [
        { id: 'u1', displayName: 'A' },
        { id: 'u2', displayName: 'B' },
      ],
    }))
    const { result: s, stop } = withScope(() => useSearchSuggest())
    await s.fetchNow('a')
    expect(s.highlighted.value).toBe(0)
    s.moveDown(); expect(s.highlighted.value).toBe(1)
    s.moveDown(); expect(s.highlighted.value).toBe(0)
    s.moveUp();   expect(s.highlighted.value).toBe(1)
    s.moveUp();   expect(s.highlighted.value).toBe(0)
    stop()
  })

  it('moveDown / moveUp on empty list keep highlighted at -1', () => {
    const { result: s, stop } = withScope(() => useSearchSuggest())
    s.moveDown(); expect(s.highlighted.value).toBe(-1)
    s.moveUp();   expect(s.highlighted.value).toBe(-1)
    stop()
  })

  it('selectHighlighted returns the highlighted item or null', async () => {
    mockSearchService.suggest.mockResolvedValue(makeResponse({
      users: [{ id: 'u1', displayName: 'Alice' }],
    }))
    const { result: s, stop } = withScope(() => useSearchSuggest())
    await s.fetchNow('a')
    expect(s.selectHighlighted()?.id).toBe('u1')
    s.highlighted.value = -1
    expect(s.selectHighlighted()).toBeNull()
    s.highlighted.value = 99
    expect(s.selectHighlighted()).toBeNull()
    stop()
  })

  it('reset clears all state', async () => {
    mockSearchService.suggest.mockResolvedValue(makeResponse({
      users: [{ id: 'u1', displayName: 'A' }],
    }))
    const { result: s, stop } = withScope(() => useSearchSuggest())
    await s.fetchNow('a')
    s.reset()
    expect(s.query.value).toBe('')
    expect(s.items.value).toHaveLength(0)
    expect(s.open.value).toBe(false)
    expect(s.highlighted.value).toBe(-1)
    stop()
  })

  it('close hides dropdown but preserves query', async () => {
    mockSearchService.suggest.mockResolvedValue(makeResponse({
      users: [{ id: 'u1', displayName: 'A' }],
    }))
    const { result: s, stop } = withScope(() => useSearchSuggest())
    await s.fetchNow('alice')
    s.open.value = true
    s.close()
    expect(s.open.value).toBe(false)
    expect(s.query.value).toBe('') // reset wasn't called, query unchanged from default ''
    stop()
  })
})
