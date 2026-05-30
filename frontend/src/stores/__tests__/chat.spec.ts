import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { setActivePinia, createPinia } from 'pinia'
import { useChatStore } from '@/stores/chat'

// ── Stable hub connection mock (shared across all tests) ──────────────────────

const mockChatHubConnection = vi.hoisted(() => ({
  on:     vi.fn(),
  start:  vi.fn().mockResolvedValue(undefined),
  stop:   vi.fn(),
  state:  'Connected',
  invoke: vi.fn().mockResolvedValue(undefined),
}))

// ── Mock signalR using a real class so `new HubConnectionBuilder()` works ─────

vi.mock('@microsoft/signalr', () => {
  class HubConnectionBuilder {
    withUrl()                { return this }
    withAutomaticReconnect() { return this }
    configureLogging()       { return this }
    build()                  { return mockChatHubConnection }
  }
  return {
    HubConnectionBuilder,
    LogLevel:           { Warning: 2 },
    HubConnectionState: { Connected: 'Connected' },
  }
})

// ── Mock chatService ──────────────────────────────────────────────────────────

const mockChatService = vi.hoisted(() => ({
  getConversations:  vi.fn(),
  getOrCreateDirect: vi.fn(),
  getMessages:       vi.fn(),
  sendMessage:       vi.fn(),
  deleteMessage:     vi.fn(),
  markRead:          vi.fn(),
}))

vi.mock('@/services/chatService', () => ({
  chatService: mockChatService,
}))

// ── Mock auth store ───────────────────────────────────────────────────────────

vi.mock('@/stores/auth', () => ({
  useAuthStore: () => ({
    accessToken: 'test-token',
    user:        { id: 'user-1', displayName: 'Alice' },
  }),
}))

// ── Helpers ───────────────────────────────────────────────────────────────────

function makeConversation(id: string) {
  return {
    id,
    type:         'Direct' as const,
    participants: [{ id: 'user-1', displayName: 'Alice' }, { id: 'user-2', displayName: 'Bob' }],
    unreadCount:  0,
    createdAt:    '2024-01-01T00:00:00Z',
  }
}

function makeMessage(id: string, conversationId: string) {
  return {
    id,
    conversationId,
    sender:      { id: 'user-2', displayName: 'Bob' },
    content:     `Message ${id}`,
    messageType: 'Text' as const,
    createdAt:   '2024-01-01T00:00:00Z',
    isDeleted:   false,
  }
}

// ── Tests ─────────────────────────────────────────────────────────────────────

describe('useChatStore', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.clearAllMocks()
  })

  // ── Window management ──────────────────────────────────────────────────────

  it('openWindow — adds conversation id to openWindows', () => {
    const store = useChatStore()
    store.openWindow('conv-1')
    expect(store.openWindows).toContain('conv-1')
  })

  it('openWindow — does not duplicate windows', () => {
    const store = useChatStore()
    store.openWindow('conv-1')
    store.openWindow('conv-1')
    expect(store.openWindows.filter(id => id === 'conv-1')).toHaveLength(1)
  })

  it('openWindow — evicts oldest window when limit exceeded', () => {
    const store = useChatStore()
    store.openWindow('conv-1')
    store.openWindow('conv-2')
    store.openWindow('conv-3')
    store.openWindow('conv-4') // should evict conv-1

    expect(store.openWindows).not.toContain('conv-1')
    expect(store.openWindows).toContain('conv-4')
    expect(store.openWindows).toHaveLength(3)
  })

  it('closeWindow — removes conversation from openWindows', () => {
    const store = useChatStore()
    store.openWindow('conv-1')
    store.openWindow('conv-2')
    store.closeWindow('conv-1')

    expect(store.openWindows).not.toContain('conv-1')
    expect(store.openWindows).toContain('conv-2')
  })

  it('toggleMinimize — minimizes then restores window', () => {
    const store = useChatStore()
    store.openWindow('conv-1')

    store.toggleMinimize('conv-1')
    expect(store.minimizedWindows.has('conv-1')).toBe(true)

    store.toggleMinimize('conv-1')
    expect(store.minimizedWindows.has('conv-1')).toBe(false)
  })

  it('toggleList — toggles list visibility', () => {
    const store = useChatStore()
    expect(store.listOpen).toBe(false)

    store.toggleList()
    expect(store.listOpen).toBe(true)

    store.toggleList()
    expect(store.listOpen).toBe(false)
  })

  // ── Computed: totalUnread ──────────────────────────────────────────────────

  it('totalUnread — sums unread counts across conversations', async () => {
    const conv1 = { ...makeConversation('conv-1'), unreadCount: 3 }
    const conv2 = { ...makeConversation('conv-2'), unreadCount: 5 }
    mockChatService.getConversations.mockResolvedValue([conv1, conv2])

    const store = useChatStore()
    await store.loadConversations()

    expect(store.totalUnread).toBe(8)
  })

  // ── markRead ───────────────────────────────────────────────────────────────

  it('markRead — zeroes unread count for conversation', async () => {
    const conv = { ...makeConversation('conv-1'), unreadCount: 5 }
    mockChatService.getConversations.mockResolvedValue([conv])
    mockChatService.markRead.mockResolvedValue(undefined)

    const store = useChatStore()
    await store.loadConversations()
    await store.markRead('conv-1')

    expect(store.conversations[0].unreadCount).toBe(0)
  })

  // ── loadMessages ───────────────────────────────────────────────────────────

  it('loadMessages — stores messages keyed by conversationId', async () => {
    const msgs = [makeMessage('msg-1', 'conv-1'), makeMessage('msg-2', 'conv-1')]
    mockChatService.getMessages.mockResolvedValue(msgs)

    const store = useChatStore()
    await store.loadMessages('conv-1')

    expect(store.messages['conv-1']).toHaveLength(2)
  })

  it('loadMessages — prepends older messages when before cursor provided', async () => {
    const newer = [makeMessage('msg-3', 'conv-1')]
    const older = [makeMessage('msg-1', 'conv-1'), makeMessage('msg-2', 'conv-1')]

    mockChatService.getMessages.mockResolvedValueOnce(newer)
    mockChatService.getMessages.mockResolvedValueOnce(older)

    const store = useChatStore()
    await store.loadMessages('conv-1')
    await store.loadMessages('conv-1', '2024-01-01T00:00:00Z')

    expect(store.messages['conv-1']).toHaveLength(3)
    expect(store.messages['conv-1'][0].id).toBe('msg-1') // older first
  })

  // ── sendMessage ────────────────────────────────────────────────────────────

  it('sendMessage — appends message and updates conversation', async () => {
    const conv = makeConversation('conv-1')
    mockChatService.getConversations.mockResolvedValue([conv])

    const newMsg = makeMessage('msg-new', 'conv-1')
    mockChatService.sendMessage.mockResolvedValue(newMsg)

    const store = useChatStore()
    await store.loadConversations()
    await store.sendMessage('conv-1', 'Hello')

    expect(store.messages['conv-1']).toContainEqual(expect.objectContaining({ id: 'msg-new' }))
    expect(store.conversations[0].lastMessage?.id).toBe('msg-new')
  })

  // ── deleteMessage ──────────────────────────────────────────────────────────

  it('deleteMessage — replaces message with deleted version', async () => {
    const msgs = [makeMessage('msg-1', 'conv-1')]
    mockChatService.getMessages.mockResolvedValue(msgs)

    const deletedMsg = { ...makeMessage('msg-1', 'conv-1'), isDeleted: true, content: undefined }
    mockChatService.deleteMessage.mockResolvedValue(deletedMsg)

    const store = useChatStore()
    await store.loadMessages('conv-1')
    await store.deleteMessage('msg-1', 'conv-1')

    expect(store.messages['conv-1'][0].isDeleted).toBe(true)
  })

  // ── openConversationWith (covers upsertConversation) ───────────────────────

  it('openConversationWith — creates DM and opens chat window', async () => {
    const conv = makeConversation('conv-dm')
    mockChatService.getOrCreateDirect.mockResolvedValue(conv)

    const store = useChatStore()
    await store.openConversationWith('user-2')

    expect(store.conversations).toContainEqual(expect.objectContaining({ id: 'conv-dm' }))
    expect(store.openWindows).toContain('conv-dm')
  })

  it('openConversationWith — updates existing conversation in list (upsert)', async () => {
    const updatedConv = { ...makeConversation('conv-dm'), unreadCount: 3 }
    mockChatService.getOrCreateDirect.mockResolvedValue(updatedConv)

    const store = useChatStore()
    // Pre-seed an older copy of the conversation
    store.conversations = [makeConversation('conv-dm')]

    await store.openConversationWith('user-2')

    expect(store.conversations).toHaveLength(1)
    expect(store.conversations[0].unreadCount).toBe(3)
  })
})

// ── SignalR connect event handlers ────────────────────────────────────────────

describe('useChatStore — SignalR event handlers', () => {
  let handlers: Record<string, (...args: unknown[]) => void>

  beforeEach(() => {
    setActivePinia(createPinia())
    vi.clearAllMocks()

    handlers = {}
    // Capture hub.on() registrations so we can invoke callbacks in tests.
    mockChatHubConnection.on.mockReset()
    mockChatHubConnection.on.mockImplementation(
      (event: string, cb: (...args: unknown[]) => void) => {
        handlers[event] = cb
      },
    )
  })

  afterEach(() => {
    // Restore on() to a plain stub so the outer describe's tests are not affected.
    mockChatHubConnection.on.mockReset()
  })

  // ── connect ────────────────────────────────────────────────────────────────

  it('connect — builds hub, registers event handlers, and starts', () => {
    const store = useChatStore()
    store.connect()

    expect(mockChatHubConnection.on).toHaveBeenCalledWith('ReceiveMessage', expect.any(Function))
    expect(mockChatHubConnection.on).toHaveBeenCalledWith('MessageDeleted', expect.any(Function))
    expect(mockChatHubConnection.on).toHaveBeenCalledWith('ConversationCreated', expect.any(Function))
    expect(mockChatHubConnection.on).toHaveBeenCalledWith('UserTyping', expect.any(Function))
    expect(mockChatHubConnection.start).toHaveBeenCalled()
  })

  it('connect — is idempotent (second call is a no-op)', () => {
    const store = useChatStore()
    store.connect()
    store.connect() // second call should not rebuild

    // build() was only called once, so on() was only called for the first connect
    expect(mockChatHubConnection.on).toHaveBeenCalledTimes(6) // 6 registered events
  })

  // ── ReceiveMessage ─────────────────────────────────────────────────────────

  it('ReceiveMessage — appends message to conversation messages', () => {
    const store = useChatStore()
    store.connect()

    const msg = makeMessage('msg-1', 'conv-1')
    handlers['ReceiveMessage']!(msg)

    expect(store.messages['conv-1']).toHaveLength(1)
    expect(store.messages['conv-1'][0].id).toBe('msg-1')
  })

  it('ReceiveMessage — bumps unread count when window is not open', () => {
    const conv = makeConversation('conv-1')
    mockChatService.getConversations.mockResolvedValue([conv])

    const store = useChatStore()
    store.conversations = [conv]
    store.connect()

    const msg = makeMessage('msg-1', 'conv-1')
    handlers['ReceiveMessage']!(msg)

    expect(store.conversations[0].unreadCount).toBe(1)
  })

  it('ReceiveMessage — does not bump unread when window is open and not minimized', () => {
    const conv = makeConversation('conv-1')

    const store = useChatStore()
    store.conversations = [{ ...conv, unreadCount: 0 }]
    store.openWindow('conv-1')
    store.connect()

    const msg = makeMessage('msg-1', 'conv-1')
    handlers['ReceiveMessage']!(msg)

    expect(store.conversations[0].unreadCount).toBe(0)
  })

  it('ReceiveMessage — bumps unread when window is minimized', () => {
    const conv = makeConversation('conv-1')

    const store = useChatStore()
    store.conversations = [{ ...conv, unreadCount: 0 }]
    store.openWindow('conv-1')
    store.toggleMinimize('conv-1')
    store.connect()

    const msg = makeMessage('msg-1', 'conv-1')
    handlers['ReceiveMessage']!(msg)

    expect(store.conversations[0].unreadCount).toBe(1)
  })

  it('ReceiveMessage — does not bump unread for own messages', () => {
    // The auth mock returns user id 'user-1'; a message from 'user-1' is "own"
    const conv = makeConversation('conv-1')

    const store = useChatStore()
    store.conversations = [{ ...conv, unreadCount: 0 }]
    store.connect()

    const ownMsg = {
      ...makeMessage('msg-self', 'conv-1'),
      sender: { id: 'user-1', displayName: 'Alice' }, // same as auth.user.id
    }
    handlers['ReceiveMessage']!(ownMsg)

    expect(store.conversations[0].unreadCount).toBe(0)
  })

  it('ReceiveMessage — moves updated conversation to front of list', () => {
    const conv1 = makeConversation('conv-1')
    const conv2 = makeConversation('conv-2')

    const store = useChatStore()
    store.conversations = [conv1, conv2]
    store.connect()

    const msg = makeMessage('msg-1', 'conv-2')
    handlers['ReceiveMessage']!(msg)

    expect(store.conversations[0].id).toBe('conv-2')
  })

  // ── MessageDeleted ─────────────────────────────────────────────────────────

  it('MessageDeleted — marks message as deleted in messages map', async () => {
    const msgs = [makeMessage('msg-1', 'conv-1')]
    mockChatService.getMessages.mockResolvedValue(msgs)

    const store = useChatStore()
    await store.loadMessages('conv-1')
    store.connect()

    handlers['MessageDeleted']!({ messageId: 'msg-1', conversationId: 'conv-1' })

    expect(store.messages['conv-1'][0].isDeleted).toBe(true)
    expect(store.messages['conv-1'][0].content).toBeUndefined()
    expect(store.messages['conv-1'][0].media).toBeUndefined()
  })

  it('MessageDeleted — no-ops when conversation messages are not loaded', () => {
    const store = useChatStore()
    store.connect()

    // Should not throw even if messages['conv-x'] doesn't exist
    expect(() =>
      handlers['MessageDeleted']!({ messageId: 'msg-x', conversationId: 'conv-x' }),
    ).not.toThrow()
  })

  // ── ConversationCreated ────────────────────────────────────────────────────

  it('ConversationCreated — prepends new conversation to list', () => {
    const store = useChatStore()
    store.connect()

    const newConv = makeConversation('conv-new')
    handlers['ConversationCreated']!(newConv)

    expect(store.conversations[0].id).toBe('conv-new')
  })

  it('ConversationCreated — updates existing conversation (upsert)', () => {
    const existing = makeConversation('conv-1')
    const updated  = { ...existing, unreadCount: 5 }

    const store = useChatStore()
    store.conversations = [existing]
    store.connect()

    handlers['ConversationCreated']!(updated)

    expect(store.conversations).toHaveLength(1)
    expect(store.conversations[0].unreadCount).toBe(5)
  })

  // ── UserTyping ─────────────────────────────────────────────────────────────

  it('UserTyping — adds userId to typingUsers for the conversation', () => {
    const store = useChatStore()
    store.connect()

    handlers['UserTyping']!('conv-1', 'user-99')

    expect(store.typingUsers['conv-1']).toContain('user-99')
  })

  it('UserTyping — does not duplicate the same userId', () => {
    const store = useChatStore()
    store.connect()

    handlers['UserTyping']!('conv-1', 'user-99')
    handlers['UserTyping']!('conv-1', 'user-99') // duplicate

    expect(store.typingUsers['conv-1'].filter((id) => id === 'user-99')).toHaveLength(1)
  })

  it('UserTyping — auto-clears the user after timeout', async () => {
    vi.useFakeTimers()
    const store = useChatStore()
    store.connect()

    handlers['UserTyping']!('conv-1', 'user-99')
    expect(store.typingUsers['conv-1']).toContain('user-99')

    vi.advanceTimersByTime(3100) // slightly past 3 s auto-clear

    expect(store.typingUsers['conv-1'] ?? []).not.toContain('user-99')
    vi.useRealTimers()
  })

  // ── UserOnline / UserOffline (no-op stubs) ────────────────────────────────

  it('UserOnline — does not throw (future presence UI hook)', () => {
    const store = useChatStore()
    store.connect()

    expect(() => handlers['UserOnline']!('user-99')).not.toThrow()
  })

  it('UserOffline — does not throw (future presence UI hook)', () => {
    const store = useChatStore()
    store.connect()

    expect(() => handlers['UserOffline']!('user-99')).not.toThrow()
  })

  // ── joinConversation / leaveConversation / sendTyping ──────────────────────

  it('joinConversation — invokes JoinConversation on connected hub', async () => {
    const store = useChatStore()
    store.connect()

    await store.joinConversation('conv-1')

    expect(mockChatHubConnection.invoke).toHaveBeenCalledWith('JoinConversation', 'conv-1')
  })

  it('leaveConversation — invokes LeaveConversation on connected hub', async () => {
    const store = useChatStore()
    store.connect()

    await store.leaveConversation('conv-1')

    expect(mockChatHubConnection.invoke).toHaveBeenCalledWith('LeaveConversation', 'conv-1')
  })

  it('sendTyping — invokes SendTyping on connected hub', async () => {
    const store = useChatStore()
    store.connect()

    await store.sendTyping('conv-1')

    expect(mockChatHubConnection.invoke).toHaveBeenCalledWith('SendTyping', 'conv-1')
  })

  // ── disconnect ─────────────────────────────────────────────────────────────

  it('disconnect — stops the hub connection', () => {
    const store = useChatStore()
    store.connect()
    store.disconnect()

    expect(mockChatHubConnection.stop).toHaveBeenCalled()
  })

  // ── start().catch swallows errors ──────────────────────────────────────────

  it('connect — swallows hub start errors gracefully', async () => {
    mockChatHubConnection.start.mockRejectedValueOnce(new Error('connection refused'))

    const store = useChatStore()
    // Should not throw even if the hub fails to start
    expect(() => store.connect()).not.toThrow()

    // Give the rejected promise a chance to resolve
    await Promise.resolve()
  })
})
