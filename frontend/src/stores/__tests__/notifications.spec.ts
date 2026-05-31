import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { setActivePinia, createPinia } from 'pinia'
import { useNotificationsStore } from '@/stores/notifications'
import type { Notification } from '@/types'

// ── Mock SignalR ──────────────────────────────────────────────────────────────

const mockHubConnection = vi.hoisted(() => ({
  on:     vi.fn(),
  start:  vi.fn().mockResolvedValue(undefined),
  stop:   vi.fn().mockResolvedValue(undefined),
  state:  'Disconnected',
}))

// Track how many times build() was called (for the idempotency test).
let buildCallCount = 0

vi.mock('@microsoft/signalr', () => {
  // Use a real class so that `new HubConnectionBuilder()` works.
  class HubConnectionBuilder {
    withUrl()                { return this }
    withAutomaticReconnect() { return this }
    configureLogging()       { return this }
    build() {
      buildCallCount++
      return mockHubConnection
    }
  }
  return { HubConnectionBuilder, LogLevel: { Warning: 2 } }
})

// ── Mock notificationService ──────────────────────────────────────────────────

const mockNotificationService = vi.hoisted(() => ({
  list:          vi.fn(),
  getUnreadCount: vi.fn(),
  markRead:      vi.fn(),
  markAllRead:   vi.fn(),
}))

vi.mock('@/services/notificationService', () => ({
  notificationService: mockNotificationService,
}))

// ── Mock auth store ───────────────────────────────────────────────────────────

vi.mock('@/stores/auth', () => ({
  useAuthStore: () => ({
    accessToken:     'test-token',
    isAuthenticated: true,
  }),
}))

// ── Mock presence/friends stores ─────────────────────────────────────────────

vi.mock('@/stores/presence', () => ({
  usePresenceStore: () => ({
    setOnline:  vi.fn(),
    setOffline: vi.fn(),
  }),
}))

vi.mock('@/stores/friends', () => ({
  useFriendsStore: () => ({
    onFriendRequestReceived: vi.fn(),
    onFriendRequestAccepted: vi.fn(),
    refreshPendingCount:     vi.fn().mockResolvedValue(undefined),
  }),
}))

// ── Helpers ───────────────────────────────────────────────────────────────────

function makeNotification(id: string, overrides: Partial<Notification> = {}): Notification {
  return {
    id,
    type:      'PostLike',
    isRead:    false,
    createdAt: new Date().toISOString(),
    ...overrides,
  }
}

function makePage(items: Notification[], hasMore = false, unreadCount = 0) {
  return { items, page: 1, pageSize: 20, hasMore, unreadCount }
}

// ── Tests ─────────────────────────────────────────────────────────────────────

describe('useNotificationsStore', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.clearAllMocks()
    buildCallCount = 0
  })

  // ── loadFirstPage ──────────────────────────────────────────────────────────

  it('loadFirstPage — sets notifications and unreadCount', async () => {
    const n = makeNotification('n1')
    mockNotificationService.list.mockResolvedValue(makePage([n], false, 1))

    const store = useNotificationsStore()
    await store.loadFirstPage()

    expect(store.notifications).toHaveLength(1)
    expect(store.notifications[0].id).toBe('n1')
    expect(store.unreadCount).toBe(1)
    expect(store.hasMore).toBe(false)
    expect(store.loading).toBe(false)
  })

  it('loadFirstPage — resets page to 1', async () => {
    mockNotificationService.list.mockResolvedValue(makePage([], false, 0))

    const store = useNotificationsStore()
    await store.loadFirstPage()

    expect(mockNotificationService.list).toHaveBeenCalledWith(1, 20)
  })

  // ── loadMore ───────────────────────────────────────────────────────────────

  it('loadMore — appends next page, deduplicates', async () => {
    const n1 = makeNotification('n1')
    const n2 = makeNotification('n2')
    mockNotificationService.list
      .mockResolvedValueOnce(makePage([n1], true, 1))
      .mockResolvedValueOnce(makePage([n1, n2], false, 1)) // n1 is a duplicate

    const store = useNotificationsStore()
    await store.loadFirstPage()
    await store.loadMore()

    expect(store.notifications).toHaveLength(2)
    expect(store.notifications.map((n) => n.id)).toEqual(['n1', 'n2'])
  })

  it('loadMore — no-ops when hasMore is false', async () => {
    mockNotificationService.list.mockResolvedValue(makePage([], false, 0))

    const store = useNotificationsStore()
    await store.loadFirstPage()
    await store.loadMore()

    expect(mockNotificationService.list).toHaveBeenCalledTimes(1)
  })

  // ── refreshUnreadCount ─────────────────────────────────────────────────────

  it('refreshUnreadCount — updates badge count', async () => {
    mockNotificationService.getUnreadCount.mockResolvedValue(5)

    const store = useNotificationsStore()
    await store.refreshUnreadCount()

    expect(store.unreadCount).toBe(5)
  })

  it('refreshUnreadCount — silently swallows errors', async () => {
    mockNotificationService.getUnreadCount.mockRejectedValue(new Error('oops'))

    const store = useNotificationsStore()
    await expect(store.refreshUnreadCount()).resolves.toBeUndefined()
  })

  // ── markRead ───────────────────────────────────────────────────────────────

  it('markRead — marks notification as read and decrements count', async () => {
    mockNotificationService.markRead.mockResolvedValue(undefined)

    const store = useNotificationsStore()
    store.notifications = [makeNotification('n1')]
    store.unreadCount   = 1

    await store.markRead('n1')

    expect(store.notifications[0].isRead).toBe(true)
    expect(store.unreadCount).toBe(0)
  })

  it('markRead — no-ops if already read', async () => {
    const store = useNotificationsStore()
    store.notifications = [makeNotification('n1', { isRead: true })]
    store.unreadCount   = 0

    await store.markRead('n1')

    expect(mockNotificationService.markRead).not.toHaveBeenCalled()
  })

  it('markRead — reverts on API failure', async () => {
    mockNotificationService.markRead.mockRejectedValue(new Error('fail'))

    const store = useNotificationsStore()
    store.notifications = [makeNotification('n1')]
    store.unreadCount   = 1

    await store.markRead('n1')

    expect(store.notifications[0].isRead).toBe(false)
    expect(store.unreadCount).toBe(1)
  })

  // ── markAllRead ────────────────────────────────────────────────────────────

  it('markAllRead — marks all as read optimistically', async () => {
    mockNotificationService.markAllRead.mockResolvedValue(undefined)
    mockNotificationService.getUnreadCount.mockResolvedValue(0)

    const store = useNotificationsStore()
    store.notifications = [makeNotification('n1'), makeNotification('n2')]
    store.unreadCount   = 2

    await store.markAllRead()

    expect(store.notifications.every((n) => n.isRead)).toBe(true)
    expect(store.unreadCount).toBe(0)
  })

  it('markAllRead — reverts on API failure', async () => {
    mockNotificationService.markAllRead.mockRejectedValue(new Error('fail'))
    mockNotificationService.getUnreadCount.mockResolvedValue(2)

    const store = useNotificationsStore()
    store.notifications = [makeNotification('n1'), makeNotification('n2')]
    store.unreadCount   = 2

    await store.markAllRead()

    // Reverted — both notifications are unread again
    expect(store.notifications.every((n) => !n.isRead)).toBe(true)
  })

  // ── toggleDropdown / closeDropdown ─────────────────────────────────────────

  it('toggleDropdown — opens dropdown and loads notifications', async () => {
    mockNotificationService.list.mockResolvedValue(makePage([], false, 0))

    const store = useNotificationsStore()
    store.toggleDropdown()

    expect(store.dropdownOpen).toBe(true)
    // loadFirstPage is called asynchronously
    await vi.waitFor(() => expect(mockNotificationService.list).toHaveBeenCalledTimes(1))
  })

  it('closeDropdown — sets dropdownOpen to false', () => {
    const store = useNotificationsStore()
    store.dropdownOpen = true
    store.closeDropdown()
    expect(store.dropdownOpen).toBe(false)
  })

  // ── hasUnread ──────────────────────────────────────────────────────────────

  it('hasUnread — returns true when unreadCount > 0', () => {
    const store = useNotificationsStore()
    store.unreadCount = 3
    expect(store.hasUnread).toBe(true)
  })

  it('hasUnread — returns false when unreadCount is 0', () => {
    const store = useNotificationsStore()
    store.unreadCount = 0
    expect(store.hasUnread).toBe(false)
  })

  // ── lastReceived ───────────────────────────────────────────────────────────

  it('lastReceived — initialises as null', () => {
    const store = useNotificationsStore()
    expect(store.lastReceived).toBeNull()
  })

  // ── connect / disconnect ───────────────────────────────────────────────────

  it('connect — idempotent (only builds one connection)', () => {
    const store = useNotificationsStore()
    store.connect()
    store.connect()

    // build() should have been called exactly once (second connect is a no-op)
    expect(buildCallCount).toBe(1)
  })

  it('disconnect — stops the hub connection', () => {
    const store = useNotificationsStore()
    store.connect()
    store.disconnect()

    expect(mockHubConnection.stop).toHaveBeenCalled()
  })

  // ── SignalR event handlers ─────────────────────────────────────────────────

  describe('SignalR event handlers', () => {
    let handlers: Record<string, (...args: unknown[]) => void>

    beforeEach(() => {
      handlers = {}
      // Capture all hub.on() registrations so we can invoke them in tests.
      mockHubConnection.on.mockReset()
      mockHubConnection.on.mockImplementation(
        (event: string, cb: (...args: unknown[]) => void) => {
          handlers[event] = cb
        },
      )
    })

    afterEach(() => {
      // Restore on() to a plain stub so outer tests are not affected.
      mockHubConnection.on.mockReset()
    })

    // ── NotificationReceived ────────────────────────────────────────────────

    it('NotificationReceived — prepends notification and increments unreadCount', () => {
      const store = useNotificationsStore()
      store.connect()

      handlers['NotificationReceived']!(makeNotification('n-new'))

      expect(store.notifications[0].id).toBe('n-new')
      expect(store.unreadCount).toBe(1)
      expect(store.lastReceived?.id).toBe('n-new')
    })

    it('NotificationReceived — does not increment count for already-read notification', () => {
      const store = useNotificationsStore()
      store.connect()

      handlers['NotificationReceived']!(makeNotification('n-read', { isRead: true }))

      expect(store.notifications[0].id).toBe('n-read')
      expect(store.unreadCount).toBe(0)
    })

    it('NotificationReceived — truncates list to MAX_KEPT (100) entries', () => {
      const store = useNotificationsStore()
      // Pre-fill with 100 existing notifications.
      store.notifications = Array.from({ length: 100 }, (_, i) =>
        makeNotification(`old-${i}`, { isRead: true }),
      )
      store.connect()

      handlers['NotificationReceived']!(makeNotification('extra'))

      expect(store.notifications).toHaveLength(100)
      expect(store.notifications[0].id).toBe('extra')
    })

    it('NotificationReceived — does not add duplicate notification', () => {
      const store = useNotificationsStore()
      store.notifications = [makeNotification('dup')]
      store.connect()

      handlers['NotificationReceived']!(makeNotification('dup'))

      expect(store.notifications).toHaveLength(1)
    })

    // ── UnreadCountChanged ──────────────────────────────────────────────────

    it('UnreadCountChanged — updates unreadCount from server payload', () => {
      const store = useNotificationsStore()
      store.connect()

      handlers['UnreadCountChanged']!({ unread: 7 })

      expect(store.unreadCount).toBe(7)
    })

    // ── UserOnline / UserOffline ────────────────────────────────────────────

    it('UserOnline — forwards to presence store without throwing', () => {
      const store = useNotificationsStore()
      store.connect()

      expect(() =>
        handlers['UserOnline']!({ userId: 'u-1', at: new Date().toISOString() }),
      ).not.toThrow()
    })

    it('UserOffline — forwards to presence store without throwing', () => {
      const store = useNotificationsStore()
      store.connect()

      expect(() =>
        handlers['UserOffline']!({ userId: 'u-1', at: new Date().toISOString() }),
      ).not.toThrow()
    })

    // ── FriendRequestReceived (legacy hub event) ────────────────────────────

    it('FriendRequestReceived — forwards to friends store without throwing', () => {
      const store = useNotificationsStore()
      store.connect()

      expect(() =>
        handlers['FriendRequestReceived']!({ from: { id: 'u-2', displayName: 'Bob' } }),
      ).not.toThrow()
    })

    // ── FriendRequestAccepted (legacy hub event) ────────────────────────────

    it('FriendRequestAccepted — forwards to friends store without throwing', () => {
      const store = useNotificationsStore()
      store.connect()

      expect(() =>
        handlers['FriendRequestAccepted']!({ by: { id: 'u-2', displayName: 'Bob' } }),
      ).not.toThrow()
    })

    // ── forwardToRelatedStores ──────────────────────────────────────────────

    it('forwardToRelatedStores — FriendRequest with actor calls friends store', () => {
      const store = useNotificationsStore()
      store.connect()

      const n = makeNotification('n-fr', {
        type:  'FriendRequest',
        actor: { id: 'u-sender', displayName: 'Carol' },
      })

      expect(() => handlers['NotificationReceived']!(n)).not.toThrow()
      expect(store.notifications[0].id).toBe('n-fr')
    })

    it('forwardToRelatedStores — FriendRequest without actor refreshes pending count', () => {
      const store = useNotificationsStore()
      store.connect()

      const n = makeNotification('n-fr-no-actor', { type: 'FriendRequest' })

      expect(() => handlers['NotificationReceived']!(n)).not.toThrow()
      expect(store.notifications[0].id).toBe('n-fr-no-actor')
    })

    it('forwardToRelatedStores — FriendRequestAccepted with actor calls friends store', () => {
      const store = useNotificationsStore()
      store.connect()

      const n = makeNotification('n-fra', {
        type:  'FriendRequestAccepted',
        actor: { id: 'u-acceptor', displayName: 'Dave' },
      })

      expect(() => handlers['NotificationReceived']!(n)).not.toThrow()
      expect(store.notifications[0].id).toBe('n-fra')
    })

    it('forwardToRelatedStores — FriendRequestAccepted without actor is a no-op', () => {
      const store = useNotificationsStore()
      store.connect()

      const n = makeNotification('n-fra-no-actor', {
        type:  'FriendRequestAccepted',
        actor: undefined,
      })

      expect(() => handlers['NotificationReceived']!(n)).not.toThrow()
    })

    it('forwardToRelatedStores — unrelated notification type hits default branch', () => {
      const store = useNotificationsStore()
      store.connect()

      const n = makeNotification('n-like', { type: 'PostLike' })

      expect(() => handlers['NotificationReceived']!(n)).not.toThrow()
      expect(store.notifications[0].id).toBe('n-like')
    })

    // ── MessageReceived (no-op legacy hook) ────────────────────────────────

    it('MessageReceived — no-op handler does not throw', () => {
      const store = useNotificationsStore()
      store.connect()

      expect(() =>
        handlers['MessageReceived']!({
          conversationId: 'conv-1',
          messageId:      'msg-1',
          senderId:       'user-2',
        }),
      ).not.toThrow()
    })

    // ── start().catch swallows errors ──────────────────────────────────────

    it('connect — swallows hub start errors gracefully', async () => {
      mockHubConnection.start.mockRejectedValueOnce(new Error('connection refused'))

      const store = useNotificationsStore()
      // Should not throw even if the hub fails to start
      expect(() => store.connect()).not.toThrow()

      // Give the rejected promise a chance to resolve
      await Promise.resolve()
    })
  })
})
