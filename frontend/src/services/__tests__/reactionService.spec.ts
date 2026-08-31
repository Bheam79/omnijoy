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

// Import after mock so reactionService picks up the mocked api
import { reactionService } from '@/services/reactionService'

describe('reactionService', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  // ── getReactions ─────────────────────────────────────────────────────────

  it('getReactions → GETs /api/posts/:id/reactions', async () => {
    const payload = { counts: [], totalCount: 0, currentUserReaction: null }
    mockApi.get.mockResolvedValue({ data: payload })

    const result = await reactionService.getReactions('post-1')

    expect(mockApi.get).toHaveBeenCalledWith('/api/posts/post-1/reactions')
    expect(result).toEqual(payload)
  })

  it('getReactions → propagates errors', async () => {
    mockApi.get.mockRejectedValue(new Error('network'))
    await expect(reactionService.getReactions('post-1')).rejects.toThrow('network')
  })

  // ── addOrUpdateReaction ──────────────────────────────────────────────────

  it('addOrUpdateReaction → POSTs /api/posts/:id/reactions with reactionType', async () => {
    const payload = { counts: [{ reactionType: 'Like', count: 1 }], totalCount: 1, currentUserReaction: 'Like' }
    mockApi.post.mockResolvedValue({ data: payload })

    const result = await reactionService.addOrUpdateReaction('post-2', 'Like')

    expect(mockApi.post).toHaveBeenCalledWith('/api/posts/post-2/reactions', { reactionType: 'Like' })
    expect(result.totalCount).toBe(1)
    expect(result.currentUserReaction).toBe('Like')
  })

  it('addOrUpdateReaction → works with all reaction types', async () => {
    mockApi.post.mockResolvedValue({ data: { counts: [], totalCount: 0, currentUserReaction: 'Love' } })

    await reactionService.addOrUpdateReaction('post-3', 'Love')

    expect(mockApi.post).toHaveBeenCalledWith('/api/posts/post-3/reactions', { reactionType: 'Love' })
  })

  it('addOrUpdateReaction → propagates errors', async () => {
    mockApi.post.mockRejectedValue(new Error('forbidden'))
    await expect(reactionService.addOrUpdateReaction('post-1', 'Like')).rejects.toThrow('forbidden')
  })

  // ── removeReaction ───────────────────────────────────────────────────────

  it('removeReaction → DELETEs /api/posts/:id/reactions', async () => {
    const payload = { counts: [], totalCount: 0, currentUserReaction: null }
    mockApi.delete.mockResolvedValue({ data: payload })

    const result = await reactionService.removeReaction('post-4')

    expect(mockApi.delete).toHaveBeenCalledWith('/api/posts/post-4/reactions')
    expect(result.currentUserReaction).toBeNull()
  })

  it('removeReaction → propagates errors', async () => {
    mockApi.delete.mockRejectedValue(new Error('not found'))
    await expect(reactionService.removeReaction('post-4')).rejects.toThrow('not found')
  })

  // ── getReactionWho ───────────────────────────────────────────────────────

  it('getReactionWho → GETs /api/posts/:id/reactions/who', async () => {
    const payload = {
      people: [{ id: 'u-1', displayName: 'Alice', avatarUrl: null, isFriend: true, reactionType: 'Like' }],
      remaining: 0,
    }
    mockApi.get.mockResolvedValue({ data: payload })

    const result = await reactionService.getReactionWho('post-5')

    expect(mockApi.get).toHaveBeenCalledWith('/api/posts/post-5/reactions/who')
    expect(result.people).toHaveLength(1)
    expect(result.people[0].displayName).toBe('Alice')
  })

  it('getReactionWho → returns remaining count', async () => {
    mockApi.get.mockResolvedValue({ data: { people: [], remaining: 42 } })

    const result = await reactionService.getReactionWho('post-6')

    expect(result.remaining).toBe(42)
  })

  it('getReactionWho → propagates errors', async () => {
    mockApi.get.mockRejectedValue(new Error('server error'))
    await expect(reactionService.getReactionWho('post-5')).rejects.toThrow('server error')
  })

  it('routes generic reaction methods to comment targets when requested', async () => {
    const summary = { counts: [], totalCount: 1, currentUserReaction: 'Love' }
    mockApi.get.mockResolvedValue({ data: { people: [], remaining: 0 } })
    mockApi.post.mockResolvedValue({ data: summary })
    mockApi.delete.mockResolvedValue({ data: summary })

    await reactionService.addOrUpdateReaction('comment-1', 'Love', 'comment')
    await reactionService.removeReaction('comment-1', 'comment')
    await reactionService.getReactionWho('comment-1', 'comment')

    expect(mockApi.post).toHaveBeenCalledWith('/api/comments/comment-1/reactions', { reactionType: 'Love' })
    expect(mockApi.delete).toHaveBeenCalledWith('/api/comments/comment-1/reactions')
    expect(mockApi.get).toHaveBeenCalledWith('/api/comments/comment-1/reactions/who')
  })
})
