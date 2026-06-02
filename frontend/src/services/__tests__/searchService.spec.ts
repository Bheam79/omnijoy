import { describe, it, expect, vi, beforeEach } from 'vitest'

// ── Mock the shared axios instance ───────────────────────────────────────────
const mockApi = vi.hoisted(() => ({
  get:    vi.fn(),
  post:   vi.fn(),
  patch:  vi.fn(),
  put:    vi.fn(),
  delete: vi.fn(),
}))

vi.mock('@/services/api', () => ({
  default: mockApi,
  api:     mockApi,
}))

// Import after mock so searchService picks up the mocked api
import { searchService } from '@/services/searchService'

const makeSearchResponse = (overrides = {}) => ({
  query: 'test',
  type: 'all' as const,
  users: [],
  posts: [],
  events: [],
  companies: [],
  page: 1,
  pageSize: 20,
  hasMore: false,
  ...overrides,
})

describe('searchService', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  // ── search ───────────────────────────────────────────────────────────────

  it('search → GETs /api/search with q and defaults', async () => {
    mockApi.get.mockResolvedValue({ data: makeSearchResponse() })

    const result = await searchService.search({ q: 'alice' })

    expect(mockApi.get).toHaveBeenCalledWith('/api/search', {
      params: { q: 'alice', type: 'all', page: 1, pageSize: 20 },
    })
    expect(result.query).toBe('test')
  })

  it('search → uses type=all by default', async () => {
    mockApi.get.mockResolvedValue({ data: makeSearchResponse() })

    await searchService.search({ q: 'test' })

    const { params } = mockApi.get.mock.calls[0][1]
    expect(params.type).toBe('all')
  })

  it('search → uses page=1, pageSize=20 by default', async () => {
    mockApi.get.mockResolvedValue({ data: makeSearchResponse() })

    await searchService.search({ q: 'test' })

    const { params } = mockApi.get.mock.calls[0][1]
    expect(params.page).toBe(1)
    expect(params.pageSize).toBe(20)
  })

  it('search → passes explicit type, page, pageSize through', async () => {
    mockApi.get.mockResolvedValue({ data: makeSearchResponse({ type: 'users', page: 2, pageSize: 10 }) })

    await searchService.search({ q: 'alice', type: 'users', page: 2, pageSize: 10 })

    expect(mockApi.get).toHaveBeenCalledWith('/api/search', {
      params: { q: 'alice', type: 'users', page: 2, pageSize: 10 },
    })
  })

  it('search → returns a populated users array from the response', async () => {
    const user = { id: 'u-1', displayName: 'Alice', urlSlug: null }
    mockApi.get.mockResolvedValue({
      data: makeSearchResponse({ users: [user] }),
    })

    const result = await searchService.search({ q: 'alice', type: 'users' })

    expect(result.users).toHaveLength(1)
    expect(result.users[0].displayName).toBe('Alice')
  })

  // ── suggest ──────────────────────────────────────────────────────────────

  it('suggest → GETs /api/search/suggest with q param', async () => {
    mockApi.get.mockResolvedValue({ data: makeSearchResponse({ query: 'ali' }) })

    const result = await searchService.suggest('ali')

    expect(mockApi.get).toHaveBeenCalledWith('/api/search/suggest', {
      params: { q: 'ali' },
    })
    expect(result.query).toBe('ali')
  })

  it('suggest → returns empty arrays for all categories when no matches', async () => {
    mockApi.get.mockResolvedValue({ data: makeSearchResponse() })

    const result = await searchService.suggest('zzz')

    expect(result.users).toEqual([])
    expect(result.posts).toEqual([])
    expect(result.events).toEqual([])
    expect(result.companies).toEqual([])
  })
})
