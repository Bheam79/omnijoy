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

// Import after mock so notificationService picks up the mocked api
import { notificationService } from '@/services/notificationService'

describe('notificationService', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  // ── list ─────────────────────────────────────────────────────────────────

  it('list → GETs /api/notifications with page + pageSize', async () => {
    const page = { items: [], page: 1, pageSize: 20, hasMore: false, unreadCount: 0 }
    mockApi.get.mockResolvedValue({ data: page })

    const result = await notificationService.list(1, 20)

    expect(mockApi.get).toHaveBeenCalledWith('/api/notifications', {
      params: { page: 1, pageSize: 20 },
      paramsSerializer: { indexes: null },
    })
    expect(result.items).toEqual([])
  })

  it('list → uses default page=1, pageSize=20 when omitted', async () => {
    const page = { items: [], page: 1, pageSize: 20, hasMore: false, unreadCount: 0 }
    mockApi.get.mockResolvedValue({ data: page })

    await notificationService.list()

    expect(mockApi.get).toHaveBeenCalledWith('/api/notifications', {
      params: { page: 1, pageSize: 20 },
      paramsSerializer: { indexes: null },
    })
  })

  it('list → includes type array when types provided', async () => {
    const page = { items: [], page: 1, pageSize: 20, hasMore: false, unreadCount: 2 }
    mockApi.get.mockResolvedValue({ data: page })

    await notificationService.list(1, 20, ['FriendRequest', 'FriendRequestAccepted'])

    expect(mockApi.get).toHaveBeenCalledWith('/api/notifications', {
      params: { page: 1, pageSize: 20, type: ['FriendRequest', 'FriendRequestAccepted'] },
      paramsSerializer: { indexes: null },
    })
  })

  it('list → omits type key when types array is empty', async () => {
    const page = { items: [], page: 1, pageSize: 20, hasMore: false, unreadCount: 0 }
    mockApi.get.mockResolvedValue({ data: page })

    await notificationService.list(1, 20, [])

    expect(mockApi.get).toHaveBeenCalledWith('/api/notifications', {
      params: { page: 1, pageSize: 20 },
      paramsSerializer: { indexes: null },
    })
  })

  // ── getUnreadCount ───────────────────────────────────────────────────────

  it('getUnreadCount → GETs /api/notifications/unread/count and returns data.count', async () => {
    mockApi.get.mockResolvedValue({ data: { count: 7 } })

    const count = await notificationService.getUnreadCount()

    expect(mockApi.get).toHaveBeenCalledWith('/api/notifications/unread/count')
    expect(count).toBe(7)
  })

  it('getUnreadCount → returns 0 when server reports 0', async () => {
    mockApi.get.mockResolvedValue({ data: { count: 0 } })

    const count = await notificationService.getUnreadCount()

    expect(count).toBe(0)
  })

  // ── markRead ─────────────────────────────────────────────────────────────

  it('markRead → POSTs /api/notifications/:id/read', async () => {
    mockApi.post.mockResolvedValue({ data: undefined })

    await notificationService.markRead('n-42')

    expect(mockApi.post).toHaveBeenCalledWith('/api/notifications/n-42/read')
  })

  it('markRead → resolves without a return value', async () => {
    mockApi.post.mockResolvedValue({ data: undefined })

    const result = await notificationService.markRead('n-1')

    expect(result).toBeUndefined()
  })

  // ── markAllRead ──────────────────────────────────────────────────────────

  it('markAllRead → PATCHes /api/notifications/read-all', async () => {
    mockApi.patch.mockResolvedValue({ data: undefined })

    await notificationService.markAllRead()

    expect(mockApi.patch).toHaveBeenCalledWith('/api/notifications/read-all')
  })

  // ── getPresence ──────────────────────────────────────────────────────────

  it('getPresence → GETs /api/users/presence with joined userIds', async () => {
    const presence = [
      { userId: 'u-1', isOnline: true },
      { userId: 'u-2', isOnline: false },
    ]
    mockApi.get.mockResolvedValue({ data: presence })

    const result = await notificationService.getPresence(['u-1', 'u-2'])

    expect(mockApi.get).toHaveBeenCalledWith('/api/users/presence', {
      params: { userIds: 'u-1,u-2' },
    })
    expect(result).toHaveLength(2)
  })

  it('getPresence → returns [] immediately when userIds is empty (no HTTP call)', async () => {
    const result = await notificationService.getPresence([])

    expect(mockApi.get).not.toHaveBeenCalled()
    expect(result).toEqual([])
  })

  it('getPresence → works with a single user id', async () => {
    mockApi.get.mockResolvedValue({ data: [{ userId: 'u-1', isOnline: true }] })

    const result = await notificationService.getPresence(['u-1'])

    expect(mockApi.get).toHaveBeenCalledWith('/api/users/presence', {
      params: { userIds: 'u-1' },
    })
    expect(result).toHaveLength(1)
  })
})
