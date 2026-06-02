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

// Import after mock so shareService picks up the mocked api
import { shareService } from '@/services/shareService'
import type { CreateSharePayload } from '@/services/shareService'

// Minimal SharedPostFeedItemDto shape sufficient for tests
const makeSharedItem = (overrides: Record<string, unknown> = {}) => ({
  id:           'share-1',
  sharer:       { id: 'u-1', displayName: 'Alice' },
  targetType:   'OwnWall',
  originalPost: { id: 'post-1', content: 'Hello' },
  message:      null,
  createdAt:    '2024-06-01T12:00:00Z',
  ...overrides,
})

describe('shareService', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  // ── sharePost ────────────────────────────────────────────────────────────

  it('sharePost → POSTs /api/posts/:id/share with OwnWall payload', async () => {
    const payload: CreateSharePayload = { targetType: 'OwnWall' }
    mockApi.post.mockResolvedValue({ data: makeSharedItem() })

    const result = await shareService.sharePost('post-1', payload)

    expect(mockApi.post).toHaveBeenCalledWith('/api/posts/post-1/share', payload)
    expect(result.id).toBe('share-1')
  })

  it('sharePost → POSTs with FriendWall payload including targetId', async () => {
    const payload: CreateSharePayload = { targetType: 'FriendWall', targetId: 'u-2' }
    mockApi.post.mockResolvedValue({ data: makeSharedItem({ targetType: 'FriendWall' }) })

    await shareService.sharePost('post-2', payload)

    expect(mockApi.post).toHaveBeenCalledWith('/api/posts/post-2/share', payload)
  })

  it('sharePost → POSTs with CompanyPage payload including targetId', async () => {
    const payload: CreateSharePayload = { targetType: 'CompanyPage', targetId: 'page-1' }
    mockApi.post.mockResolvedValue({ data: makeSharedItem({ targetType: 'CompanyPage' }) })

    await shareService.sharePost('post-3', payload)

    expect(mockApi.post).toHaveBeenCalledWith('/api/posts/post-3/share', payload)
  })

  it('sharePost → includes optional message in payload', async () => {
    const payload: CreateSharePayload = { targetType: 'OwnWall', message: 'Check this out!' }
    mockApi.post.mockResolvedValue({ data: makeSharedItem({ message: 'Check this out!' }) })

    const result = await shareService.sharePost('post-4', payload)

    expect(mockApi.post).toHaveBeenCalledWith('/api/posts/post-4/share', payload)
    expect(result.message).toBe('Check this out!')
  })

  it('sharePost → propagates errors', async () => {
    mockApi.post.mockRejectedValue(new Error('forbidden'))

    await expect(
      shareService.sharePost('post-1', { targetType: 'OwnWall' }),
    ).rejects.toThrow('forbidden')
  })
})
