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

// Import after mock so friendService picks up the mocked api
import { friendService } from '@/services/friendService'

const makeUser = (id = 'u-2', displayName = 'Bob') => ({ id, displayName })

const makeFriendRequest = (overrides = {}) => ({
  friendshipId: 'fs-1',
  from: makeUser(),
  sentAt: '2026-01-01T00:00:00Z',
  ...overrides,
})

const makeFriend = (overrides = {}) => ({
  friendshipId: 'fs-1',
  user: makeUser(),
  mutualFriendCount: 0,
  friendedAt: '2026-01-01T00:00:00Z',
  ...overrides,
})

describe('friendService', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  // ── sendRequest ──────────────────────────────────────────────────────────

  it('sendRequest → POSTs /api/friends/request/:id', async () => {
    const req = makeFriendRequest()
    mockApi.post.mockResolvedValue({ data: req })

    const result = await friendService.sendRequest('u-2')

    expect(mockApi.post).toHaveBeenCalledWith('/api/friends/request/u-2')
    expect(result.friendshipId).toBe('fs-1')
  })

  // ── acceptRequest ────────────────────────────────────────────────────────

  it('acceptRequest → POSTs /api/friends/accept/:id and returns friend dto', async () => {
    const friend = makeFriend()
    mockApi.post.mockResolvedValue({ data: friend })

    const result = await friendService.acceptRequest('u-2')

    expect(mockApi.post).toHaveBeenCalledWith('/api/friends/accept/u-2')
    expect(result.friendshipId).toBe('fs-1')
  })

  // ── declineRequest ───────────────────────────────────────────────────────

  it('declineRequest → POSTs /api/friends/decline/:id', async () => {
    mockApi.post.mockResolvedValue({ data: undefined })

    await friendService.declineRequest('u-2')

    expect(mockApi.post).toHaveBeenCalledWith('/api/friends/decline/u-2')
  })

  it('declineRequest → resolves without a return value', async () => {
    mockApi.post.mockResolvedValue({ data: undefined })

    const result = await friendService.declineRequest('u-2')

    expect(result).toBeUndefined()
  })

  // ── cancelRequest ────────────────────────────────────────────────────────

  it('cancelRequest → DELETEs /api/friends/request/:id', async () => {
    mockApi.delete.mockResolvedValue({ data: undefined })

    await friendService.cancelRequest('u-2')

    expect(mockApi.delete).toHaveBeenCalledWith('/api/friends/request/u-2')
  })

  it('cancelRequest → resolves without a return value', async () => {
    mockApi.delete.mockResolvedValue({ data: undefined })

    const result = await friendService.cancelRequest('u-2')

    expect(result).toBeUndefined()
  })

  // ── removeFriend ─────────────────────────────────────────────────────────

  it('removeFriend → DELETEs /api/friends/:id', async () => {
    mockApi.delete.mockResolvedValue({ data: undefined })

    await friendService.removeFriend('u-2')

    expect(mockApi.delete).toHaveBeenCalledWith('/api/friends/u-2')
  })

  it('removeFriend → resolves without a return value', async () => {
    mockApi.delete.mockResolvedValue({ data: undefined })

    const result = await friendService.removeFriend('u-2')

    expect(result).toBeUndefined()
  })

  // ── blockUser ────────────────────────────────────────────────────────────

  it('blockUser → POSTs /api/friends/block/:id', async () => {
    mockApi.post.mockResolvedValue({ data: undefined })

    await friendService.blockUser('u-2')

    expect(mockApi.post).toHaveBeenCalledWith('/api/friends/block/u-2')
  })

  it('blockUser → resolves without a return value', async () => {
    mockApi.post.mockResolvedValue({ data: undefined })

    const result = await friendService.blockUser('u-2')

    expect(result).toBeUndefined()
  })

  // ── unblockUser ──────────────────────────────────────────────────────────

  it('unblockUser → DELETEs /api/friends/block/:id', async () => {
    mockApi.delete.mockResolvedValue({ data: undefined })

    await friendService.unblockUser('u-2')

    expect(mockApi.delete).toHaveBeenCalledWith('/api/friends/block/u-2')
  })

  it('unblockUser → resolves without a return value', async () => {
    mockApi.delete.mockResolvedValue({ data: undefined })

    const result = await friendService.unblockUser('u-2')

    expect(result).toBeUndefined()
  })

  // ── getFriends ───────────────────────────────────────────────────────────

  it('getFriends → GETs /api/friends with page + pageSize', async () => {
    const friendsPage = { items: [makeFriend()], page: 1, pageSize: 20, hasMore: false }
    mockApi.get.mockResolvedValue({ data: friendsPage })

    const result = await friendService.getFriends(1, 20)

    expect(mockApi.get).toHaveBeenCalledWith('/api/friends', { params: { page: 1, pageSize: 20 } })
    expect(result.items).toHaveLength(1)
  })

  it('getFriends → uses default page=1, pageSize=20 when omitted', async () => {
    mockApi.get.mockResolvedValue({ data: { items: [], page: 1, pageSize: 20, hasMore: false } })

    await friendService.getFriends()

    expect(mockApi.get).toHaveBeenCalledWith('/api/friends', { params: { page: 1, pageSize: 20 } })
  })

  // ── getPendingRequests ───────────────────────────────────────────────────

  it('getPendingRequests → GETs /api/friends/requests', async () => {
    mockApi.get.mockResolvedValue({ data: [makeFriendRequest()] })

    const result = await friendService.getPendingRequests()

    expect(mockApi.get).toHaveBeenCalledWith('/api/friends/requests')
    expect(result).toHaveLength(1)
  })

  it('getPendingRequests → returns empty array when no pending requests', async () => {
    mockApi.get.mockResolvedValue({ data: [] })

    const result = await friendService.getPendingRequests()

    expect(result).toEqual([])
  })

  // ── getSentRequests ──────────────────────────────────────────────────────

  it('getSentRequests → GETs /api/friends/sent', async () => {
    mockApi.get.mockResolvedValue({ data: [makeFriendRequest()] })

    const result = await friendService.getSentRequests()

    expect(mockApi.get).toHaveBeenCalledWith('/api/friends/sent')
    expect(result).toHaveLength(1)
  })

  // ── getSuggestions ───────────────────────────────────────────────────────

  it('getSuggestions → GETs /api/friends/suggestions with default limit=10', async () => {
    mockApi.get.mockResolvedValue({ data: [] })

    await friendService.getSuggestions()

    expect(mockApi.get).toHaveBeenCalledWith('/api/friends/suggestions', { params: { limit: 10 } })
  })

  it('getSuggestions → passes custom limit', async () => {
    mockApi.get.mockResolvedValue({ data: [] })

    await friendService.getSuggestions(5)

    expect(mockApi.get).toHaveBeenCalledWith('/api/friends/suggestions', { params: { limit: 5 } })
  })

  // ── getStatus ────────────────────────────────────────────────────────────

  it('getStatus → GETs /api/friends/status/:id and returns data.status', async () => {
    mockApi.get.mockResolvedValue({ data: { status: 'Accepted' } })

    const result = await friendService.getStatus('u-2')

    expect(mockApi.get).toHaveBeenCalledWith('/api/friends/status/u-2')
    expect(result).toBe('Accepted')
  })

  it('getStatus → returns None when no friendship exists', async () => {
    mockApi.get.mockResolvedValue({ data: { status: 'None' } })

    const result = await friendService.getStatus('u-99')

    expect(result).toBe('None')
  })

  it('getStatus → returns RequestSent status', async () => {
    mockApi.get.mockResolvedValue({ data: { status: 'RequestSent' } })

    const result = await friendService.getStatus('u-3')

    expect(result).toBe('RequestSent')
  })

  // ── getPendingCount ──────────────────────────────────────────────────────

  it('getPendingCount → GETs /api/friends/requests/count and returns data.count', async () => {
    mockApi.get.mockResolvedValue({ data: { count: 3 } })

    const result = await friendService.getPendingCount()

    expect(mockApi.get).toHaveBeenCalledWith('/api/friends/requests/count')
    expect(result).toBe(3)
  })

  it('getPendingCount → returns 0 when no pending requests', async () => {
    mockApi.get.mockResolvedValue({ data: { count: 0 } })

    const result = await friendService.getPendingCount()

    expect(result).toBe(0)
  })

  // ── setFamilyRelation ────────────────────────────────────────────────────

  it('setFamilyRelation → PUTs /api/friends/:id/relation and returns data.relation', async () => {
    const relation = { relationType: 'Family', subtype: 'Sibling' }
    mockApi.put.mockResolvedValue({ data: { relation } })

    const result = await friendService.setFamilyRelation('u-2', {
      relationType: 'Family',
      subtype: 'Sibling',
    })

    expect(mockApi.put).toHaveBeenCalledWith('/api/friends/u-2/relation', {
      relationType: 'Family',
      subtype: 'Sibling',
    })
    expect(result).toEqual(relation)
  })

  it('setFamilyRelation → returns null when relation is cleared', async () => {
    mockApi.put.mockResolvedValue({ data: { relation: null } })

    const result = await friendService.setFamilyRelation('u-2', { relationType: null, subtype: null })

    expect(result).toBeNull()
  })

  // ── createInviteLink ─────────────────────────────────────────────────────

  it('createInviteLink → POSTs /api/friends/invite/link', async () => {
    const link = {
      token: 'tok-abc',
      inviteUrl: 'https://app.example.com/invite/tok-abc',
      expiresAt: '2026-07-01T00:00:00Z',
    }
    mockApi.post.mockResolvedValue({ data: link })

    const result = await friendService.createInviteLink()

    expect(mockApi.post).toHaveBeenCalledWith('/api/friends/invite/link')
    expect(result.token).toBe('tok-abc')
  })

  // ── inviteByEmail ────────────────────────────────────────────────────────

  it('inviteByEmail → POSTs /api/friends/invite/email with email body', async () => {
    const outcome = { outcome: 'invited' }
    mockApi.post.mockResolvedValue({ data: outcome })

    const result = await friendService.inviteByEmail('friend@example.com')

    expect(mockApi.post).toHaveBeenCalledWith('/api/friends/invite/email', {
      email: 'friend@example.com',
    })
    expect(result.outcome).toBe('invited')
  })

  it('inviteByEmail → returns accepted outcome when user already exists', async () => {
    mockApi.post.mockResolvedValue({ data: { outcome: 'accepted', displayName: 'Alice' } })

    const result = await friendService.inviteByEmail('existing@example.com')

    expect(result.outcome).toBe('accepted')
    expect(result.displayName).toBe('Alice')
  })

  // ── getInviteInfo ────────────────────────────────────────────────────────

  it('getInviteInfo → GETs /api/friends/invite/:token', async () => {
    const info = { inviterDisplayName: 'Alice', inviterId: 'u-1' }
    mockApi.get.mockResolvedValue({ data: info })

    const result = await friendService.getInviteInfo('tok-abc')

    expect(mockApi.get).toHaveBeenCalledWith('/api/friends/invite/tok-abc')
    expect(result.inviterDisplayName).toBe('Alice')
  })

  // ── acceptInvite ─────────────────────────────────────────────────────────

  it('acceptInvite → POSTs /api/friends/invite/:token/accept', async () => {
    const info = { inviterDisplayName: 'Alice', inviterId: 'u-1' }
    mockApi.post.mockResolvedValue({ data: info })

    const result = await friendService.acceptInvite('tok-abc')

    expect(mockApi.post).toHaveBeenCalledWith('/api/friends/invite/tok-abc/accept')
    expect(result.inviterId).toBe('u-1')
  })

  // ── revokeLinkInvites ────────────────────────────────────────────────────

  it('revokeLinkInvites → DELETEs /api/friends/invite/link', async () => {
    mockApi.delete.mockResolvedValue({ data: undefined })

    await friendService.revokeLinkInvites()

    expect(mockApi.delete).toHaveBeenCalledWith('/api/friends/invite/link')
  })

  it('revokeLinkInvites → resolves without a return value', async () => {
    mockApi.delete.mockResolvedValue({ data: undefined })

    const result = await friendService.revokeLinkInvites()

    expect(result).toBeUndefined()
  })
})
