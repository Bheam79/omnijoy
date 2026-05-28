import { describe, it, expect, vi, beforeEach } from 'vitest'
import { setActivePinia, createPinia } from 'pinia'
import { useFriendsStore } from '@/stores/friends'

// ── Mock friendService ────────────────────────────────────────────────────────

const mockFriendService = vi.hoisted(() => ({
  getFriends:         vi.fn(),
  getPendingRequests: vi.fn(),
  getSentRequests:    vi.fn(),
  getSuggestions:     vi.fn(),
  getPendingCount:    vi.fn(),
  sendRequest:        vi.fn(),
  acceptRequest:      vi.fn(),
  declineRequest:     vi.fn(),
  cancelRequest:      vi.fn(),
  removeFriend:       vi.fn(),
}))

vi.mock('@/services/friendService', () => ({
  friendService: mockFriendService,
}))

// ── Helpers ───────────────────────────────────────────────────────────────────

function makeFriendDto(userId: string, name: string) {
  return {
    friendshipId:      `fr-${userId}`,
    user:              { id: userId, displayName: name },
    mutualFriendCount: 0,
    friendedAt:        '2024-01-01T00:00:00Z',
  }
}

function makeRequestDto(fromId: string, name: string) {
  return {
    friendshipId: `req-${fromId}`,
    from:         { id: fromId, displayName: name },
    sentAt:       '2024-01-01T00:00:00Z',
  }
}

// ── Tests ─────────────────────────────────────────────────────────────────────

describe('useFriendsStore', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.clearAllMocks()
  })

  // ── loadFriends ────────────────────────────────────────────────────────────

  it('loadFriends — appends friends to list', async () => {
    const page1 = { items: [makeFriendDto('u1', 'Bob')], page: 1, pageSize: 20, hasMore: false }
    mockFriendService.getFriends.mockResolvedValue(page1)

    const store = useFriendsStore()
    await store.loadFriends()

    expect(store.friends).toHaveLength(1)
    expect(store.friends[0].user.displayName).toBe('Bob')
    expect(store.loadingFriends).toBe(false)
  })

  it('loadFriends — resets list when reset=true', async () => {
    const page = { items: [makeFriendDto('u2', 'Carol')], page: 1, pageSize: 20, hasMore: false }
    mockFriendService.getFriends.mockResolvedValue(page)

    const store = useFriendsStore()
    // Pre-seed with existing friend
    store.friends.push(makeFriendDto('old', 'OldFriend'))

    await store.loadFriends(true)

    expect(store.friends).toHaveLength(1)
    expect(store.friends[0].user.id).toBe('u2')
  })

  it('loadFriends — stops when hasMore is false (second call skipped)', async () => {
    const page = { items: [], page: 1, pageSize: 20, hasMore: false }
    mockFriendService.getFriends.mockResolvedValue(page)

    const store = useFriendsStore()
    await store.loadFriends()
    await store.loadFriends() // should skip (hasMore=false, no reset)

    expect(mockFriendService.getFriends).toHaveBeenCalledTimes(1)
  })

  // ── loadRequests ───────────────────────────────────────────────────────────

  it('loadRequests — sets pending and sent requests', async () => {
    const incoming = [makeRequestDto('u-bob',   'Bob')]
    const outgoing = [makeRequestDto('u-carol', 'Carol')]
    mockFriendService.getPendingRequests.mockResolvedValue(incoming)
    mockFriendService.getSentRequests.mockResolvedValue(outgoing)

    const store = useFriendsStore()
    await store.loadRequests()

    expect(store.pendingRequests).toHaveLength(1)
    expect(store.sentRequests).toHaveLength(1)
    expect(store.pendingCount).toBe(1)
  })

  // ── acceptRequest ──────────────────────────────────────────────────────────

  it('acceptRequest — removes from pendingRequests and decrements count', async () => {
    mockFriendService.acceptRequest.mockResolvedValue(undefined)
    mockFriendService.getFriends.mockResolvedValue({ items: [], page: 1, pageSize: 20, hasMore: false })

    const store = useFriendsStore()
    store.pendingRequests = [makeRequestDto('u-bob', 'Bob'), makeRequestDto('u-carol', 'Carol')]
    store.pendingCount    = 2

    await store.acceptRequest('u-bob')

    expect(store.pendingRequests).toHaveLength(1)
    expect(store.pendingRequests[0].from.id).toBe('u-carol')
    expect(store.pendingCount).toBe(1)
  })

  // ── declineRequest ─────────────────────────────────────────────────────────

  it('declineRequest — removes from pendingRequests and decrements count', async () => {
    mockFriendService.declineRequest.mockResolvedValue(undefined)

    const store = useFriendsStore()
    store.pendingRequests = [makeRequestDto('u-bob', 'Bob')]
    store.pendingCount    = 1

    await store.declineRequest('u-bob')

    expect(store.pendingRequests).toHaveLength(0)
    expect(store.pendingCount).toBe(0)
  })

  // ── cancelRequest ──────────────────────────────────────────────────────────

  it('cancelRequest — removes from sentRequests', async () => {
    mockFriendService.cancelRequest.mockResolvedValue(undefined)

    const store = useFriendsStore()
    store.sentRequests = [makeRequestDto('u-dave', 'Dave')]

    await store.cancelRequest('u-dave')

    expect(store.sentRequests).toHaveLength(0)
  })

  // ── removeFriend ───────────────────────────────────────────────────────────

  it('removeFriend — removes from friends list', async () => {
    mockFriendService.removeFriend.mockResolvedValue(undefined)

    const store = useFriendsStore()
    store.friends = [makeFriendDto('u1', 'Bob'), makeFriendDto('u2', 'Carol')]

    await store.removeFriend('u1')

    expect(store.friends).toHaveLength(1)
    expect(store.friends[0].user.id).toBe('u2')
  })

  // ── SignalR events ─────────────────────────────────────────────────────────

  it('onFriendRequestReceived — increments count and prepends request', () => {
    const store = useFriendsStore()
    store.pendingCount = 0

    store.onFriendRequestReceived({ id: 'u-new', displayName: 'NewFriend' })

    expect(store.pendingCount).toBe(1)
    expect(store.pendingRequests[0].from.id).toBe('u-new')
  })

  it('onFriendRequestReceived — does not duplicate incoming requests', () => {
    const store = useFriendsStore()
    store.pendingRequests = [makeRequestDto('u-bob', 'Bob')]
    store.pendingCount    = 1

    store.onFriendRequestReceived({ id: 'u-bob', displayName: 'Bob' }) // duplicate

    expect(store.pendingRequests).toHaveLength(1)
    expect(store.pendingCount).toBe(2) // count still increments
  })

  it('onFriendRequestAccepted — removes from sentRequests', async () => {
    mockFriendService.getFriends.mockResolvedValue({ items: [], page: 1, pageSize: 20, hasMore: false })

    const store = useFriendsStore()
    store.sentRequests = [makeRequestDto('u-bob', 'Bob')]

    store.onFriendRequestAccepted({ id: 'u-bob', displayName: 'Bob' })

    expect(store.sentRequests).toHaveLength(0)
  })

  // ── refreshPendingCount ────────────────────────────────────────────────────

  it('refreshPendingCount — updates pendingCount from service', async () => {
    mockFriendService.getPendingCount.mockResolvedValue(7)

    const store = useFriendsStore()
    await store.refreshPendingCount()

    expect(store.pendingCount).toBe(7)
  })

  // ── reset ──────────────────────────────────────────────────────────────────

  it('reset — restores initial state', () => {
    const store = useFriendsStore()
    store.friends      = [makeFriendDto('u1', 'Bob')]
    store.pendingCount = 5

    store.reset()

    expect(store.friends).toHaveLength(0)
    expect(store.pendingCount).toBe(0)
    expect(store.friendsPage).toBe(1)
    expect(store.friendsHasMore).toBe(true)
  })
})
