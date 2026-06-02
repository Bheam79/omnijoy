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

// Import after mock so commentService picks up the mocked api
import { commentService } from '@/services/commentService'

const makeComment = (overrides = {}) => ({
  id: 'c-1',
  postId: 'p-1',
  author: { id: 'u-1', displayName: 'Alice' },
  parentCommentId: null,
  content: 'Hello!',
  replyCount: 0,
  createdAt: '2026-01-01T00:00:00Z',
  updatedAt: '2026-01-01T00:00:00Z',
  isDeleted: false,
  ...overrides,
})

describe('commentService', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  // ── createComment ────────────────────────────────────────────────────────

  it('createComment → POSTs /api/posts/:id/comments with payload', async () => {
    const comment = makeComment()
    mockApi.post.mockResolvedValue({ data: comment })

    const result = await commentService.createComment('p-1', { content: 'Hello!' })

    expect(mockApi.post).toHaveBeenCalledWith('/api/posts/p-1/comments', { content: 'Hello!' })
    expect(result.id).toBe('c-1')
  })

  it('createComment → includes parentCommentId when replying', async () => {
    const reply = makeComment({ parentCommentId: 'c-0' })
    mockApi.post.mockResolvedValue({ data: reply })

    await commentService.createComment('p-1', {
      content: 'Reply!',
      parentCommentId: 'c-0',
    })

    expect(mockApi.post).toHaveBeenCalledWith('/api/posts/p-1/comments', {
      content: 'Reply!',
      parentCommentId: 'c-0',
    })
  })

  // ── getComments ──────────────────────────────────────────────────────────

  it('getComments → GETs /api/posts/:id/comments with paging params', async () => {
    const page = { items: [makeComment()], page: 1, pageSize: 20, hasMore: false }
    mockApi.get.mockResolvedValue({ data: page })

    const result = await commentService.getComments('p-1', 1, 20)

    expect(mockApi.get).toHaveBeenCalledWith('/api/posts/p-1/comments', {
      params: { page: 1, pageSize: 20 },
    })
    expect(result.items).toHaveLength(1)
  })

  it('getComments → uses default page=1, pageSize=20 when omitted', async () => {
    const page = { items: [], page: 1, pageSize: 20, hasMore: false }
    mockApi.get.mockResolvedValue({ data: page })

    await commentService.getComments('p-2')

    expect(mockApi.get).toHaveBeenCalledWith('/api/posts/p-2/comments', {
      params: { page: 1, pageSize: 20 },
    })
  })

  // ── getReplies ───────────────────────────────────────────────────────────

  it('getReplies → GETs /api/comments/:id/replies', async () => {
    const replies = [makeComment({ parentCommentId: 'c-1' })]
    mockApi.get.mockResolvedValue({ data: replies })

    const result = await commentService.getReplies('c-1')

    expect(mockApi.get).toHaveBeenCalledWith('/api/comments/c-1/replies')
    expect(result).toHaveLength(1)
  })

  it('getReplies → returns empty array when no replies', async () => {
    mockApi.get.mockResolvedValue({ data: [] })

    const result = await commentService.getReplies('c-99')

    expect(result).toEqual([])
  })

  // ── updateComment ────────────────────────────────────────────────────────

  it('updateComment → PUTs /api/comments/:id with payload', async () => {
    const updated = makeComment({ content: 'Updated!' })
    mockApi.put.mockResolvedValue({ data: updated })

    const result = await commentService.updateComment('c-1', { content: 'Updated!' })

    expect(mockApi.put).toHaveBeenCalledWith('/api/comments/c-1', { content: 'Updated!' })
    expect(result.content).toBe('Updated!')
  })

  // ── deleteComment ────────────────────────────────────────────────────────

  it('deleteComment → DELETEs /api/comments/:id', async () => {
    mockApi.delete.mockResolvedValue({ data: undefined })

    await commentService.deleteComment('c-1')

    expect(mockApi.delete).toHaveBeenCalledWith('/api/comments/c-1')
  })

  it('deleteComment → resolves without a return value', async () => {
    mockApi.delete.mockResolvedValue({ data: undefined })

    const result = await commentService.deleteComment('c-99')

    expect(result).toBeUndefined()
  })
})
