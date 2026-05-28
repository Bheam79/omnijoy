import { describe, it, expect, vi, beforeEach } from 'vitest'
import { setActivePinia, createPinia } from 'pinia'
import { useChatStore } from '@/stores/chat'

// ── Mock signalR ──────────────────────────────────────────────────────────────

vi.mock('@microsoft/signalr', () => ({
  HubConnectionBuilder: vi.fn(() => ({
    withUrl:                vi.fn().mockReturnThis(),
    withAutomaticReconnect: vi.fn().mockReturnThis(),
    configureLogging:       vi.fn().mockReturnThis(),
    build: vi.fn(() => ({
      on:     vi.fn(),
      start:  vi.fn().mockResolvedValue(undefined),
      stop:   vi.fn().mockResolvedValue(undefined),
      state:  'Connected',
      invoke: vi.fn().mockResolvedValue(undefined),
    })),
  })),
  LogLevel:            { Warning: 2 },
  HubConnectionState:  { Connected: 'Connected' },
}))

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
})
