import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import type { Conversation, Message } from '@/types'

// ── Auth store mock ────────────────────────────────────────────────────────────

const mockAuth = vi.hoisted(() => ({
  user: { id: 'user-1', displayName: 'Alice' } as { id: string; displayName: string } | null,
}))

vi.mock('@/stores/auth', () => ({
  useAuthStore: () => mockAuth,
}))

// ── Chat store mock ────────────────────────────────────────────────────────────

const mockChat = vi.hoisted(() => ({
  conversations:    [] as Conversation[],
  loading:          false,
  loadConversations: vi.fn().mockResolvedValue(undefined),
  openWindow:       vi.fn(),
  toggleList:       vi.fn(),
}))

vi.mock('@/stores/chat', () => ({
  useChatStore: () => mockChat,
}))

// ── Presence store mock ───────────────────────────────────────────────────────

const mockPresence = vi.hoisted(() => ({
  ensurePresence: vi.fn().mockResolvedValue(undefined),
}))

vi.mock('@/stores/presence', () => ({
  usePresenceStore: () => mockPresence,
}))

// ── PresenceDot stub ──────────────────────────────────────────────────────────

vi.mock('@/components/shared/PresenceDot.vue', () => ({
  default: {
    name:     'PresenceDot',
    props:    ['userId', 'size'],
    template: '<span data-testid="presence-dot-stub" />',
  },
}))

// Import after mocks
import ConversationList from '../ConversationList.vue'

// ── Fixtures ──────────────────────────────────────────────────────────────────

function makeLastMessage(content = 'Hey there'): Message {
  return {
    id:             'msg-1',
    conversationId: 'conv-1',
    sender:         { id: 'user-2', displayName: 'Bob' },
    content,
    messageType:    'Text',
    createdAt:      '2026-06-01T10:00:00Z',
    isDeleted:      false,
  }
}

function makeConversation(overrides: Partial<Conversation> = {}): Conversation {
  return {
    id:           'conv-1',
    type:         'Direct',
    participants: [
      { id: 'user-1', displayName: 'Alice' },
      { id: 'user-2', displayName: 'Bob' },
    ],
    lastMessage: makeLastMessage(),
    unreadCount: 0,
    createdAt:   '2026-01-01T00:00:00Z',
    ...overrides,
  }
}

// ── Mount helper ──────────────────────────────────────────────────────────────

function mountList() {
  return mount(ConversationList, {
    global: {
      plugins: [createPinia()],
    },
  })
}

// ── Tests ─────────────────────────────────────────────────────────────────────

describe('ConversationList', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.clearAllMocks()
    mockAuth.user              = { id: 'user-1', displayName: 'Alice' }
    mockChat.conversations     = []
    mockChat.loading           = false
    mockChat.loadConversations.mockResolvedValue(undefined)
    mockChat.openWindow        = vi.fn()
    mockChat.toggleList        = vi.fn()
  })

  // ── Mount ─────────────────────────────────────────────────────────────────

  it('calls chatStore.loadConversations on mount when conversations list is empty', async () => {
    mockChat.conversations = []
    mountList()
    await flushPromises()

    expect(mockChat.loadConversations).toHaveBeenCalledOnce()
  })

  it('does NOT call loadConversations on mount when conversations are already loaded', async () => {
    mockChat.conversations = [makeConversation()]
    mountList()
    await flushPromises()

    expect(mockChat.loadConversations).not.toHaveBeenCalled()
  })

  // ── Header ────────────────────────────────────────────────────────────────

  it('renders the "Messenger" header text', () => {
    const wrapper = mountList()

    expect(wrapper.text()).toContain('Messenger')
  })

  it('calls chatStore.toggleList when the Close button is clicked', async () => {
    const wrapper = mountList()

    const closeBtn = wrapper.find('button[aria-label="Close"]')
    expect(closeBtn.exists()).toBe(true)
    await closeBtn.trigger('click')

    expect(mockChat.toggleList).toHaveBeenCalledOnce()
  })

  // ── Loading state ─────────────────────────────────────────────────────────

  it('shows "Loading…" text when chat.loading is true', () => {
    mockChat.loading = true
    const wrapper   = mountList()

    expect(wrapper.text()).toContain('Loading…')
  })

  it('hides the loading indicator when loading is false', async () => {
    mockChat.loading = false
    const wrapper = mountList()
    await flushPromises()

    expect(wrapper.text()).not.toContain('Loading…')
  })

  // ── Empty state ───────────────────────────────────────────────────────────

  it('shows "No conversations yet" when the conversations list is empty', async () => {
    mockChat.conversations = []
    const wrapper = mountList()
    await flushPromises()

    expect(wrapper.text()).toContain('No conversations yet')
  })

  it('does not show the empty state when there are conversations', async () => {
    mockChat.conversations = [makeConversation()]
    const wrapper = mountList()
    await flushPromises()

    expect(wrapper.text()).not.toContain('No conversations yet')
  })

  // ── Conversation list ─────────────────────────────────────────────────────

  it('renders the other participant name for each conversation', async () => {
    mockChat.conversations = [
      makeConversation({ id: 'conv-1' }),
      makeConversation({
        id:           'conv-2',
        participants: [
          { id: 'user-1', displayName: 'Alice' },
          { id: 'user-3', displayName: 'Charlie' },
        ],
        lastMessage: makeLastMessage('Hi Charlie'),
      }),
    ]
    const wrapper = mountList()
    await flushPromises()

    expect(wrapper.text()).toContain('Bob')
    expect(wrapper.text()).toContain('Charlie')
  })

  it('renders the last message content preview', async () => {
    mockChat.conversations = [makeConversation()]
    const wrapper = mountList()
    await flushPromises()

    expect(wrapper.text()).toContain('Hey there')
  })

  it('shows "No messages yet" when conversation has no lastMessage', async () => {
    mockChat.conversations = [makeConversation({ lastMessage: undefined })]
    const wrapper = mountList()
    await flushPromises()

    expect(wrapper.text()).toContain('No messages yet')
  })

  it('shows "Message deleted" for deleted last messages', async () => {
    mockChat.conversations = [
      makeConversation({
        lastMessage: { ...makeLastMessage(), isDeleted: true, content: undefined },
      }),
    ]
    const wrapper = mountList()
    await flushPromises()

    expect(wrapper.text()).toContain('Message deleted')
  })

  it('shows "📷 Image" for image-type last messages', async () => {
    mockChat.conversations = [
      makeConversation({
        lastMessage: { ...makeLastMessage(), messageType: 'Image', content: undefined },
      }),
    ]
    const wrapper = mountList()
    await flushPromises()

    expect(wrapper.text()).toContain('📷 Image')
  })

  // ── Unread badge ──────────────────────────────────────────────────────────

  it('shows the unread count badge when unreadCount > 0', async () => {
    mockChat.conversations = [makeConversation({ unreadCount: 3 })]
    const wrapper = mountList()
    await flushPromises()

    expect(wrapper.text()).toContain('3')
    // The unread badge has specific styling
    expect(wrapper.find('.bg-red-500').exists()).toBe(true)
  })

  it('shows "9+" when unreadCount is greater than 9', async () => {
    mockChat.conversations = [makeConversation({ unreadCount: 12 })]
    const wrapper = mountList()
    await flushPromises()

    expect(wrapper.text()).toContain('9+')
  })

  it('does not render the unread badge when unreadCount is 0', async () => {
    mockChat.conversations = [makeConversation({ unreadCount: 0 })]
    const wrapper = mountList()
    await flushPromises()

    // bg-red-500 badge only appears for unread > 0
    const badge = wrapper.find('.bg-red-500')
    expect(badge.exists()).toBe(false)
  })

  // ── Click opens conversation window ───────────────────────────────────────

  it('calls chatStore.openWindow with the conversation id when a row is clicked', async () => {
    mockChat.conversations = [makeConversation({ id: 'conv-42' })]
    const wrapper = mountList()
    await flushPromises()

    // Click the list item (li element)
    const listItem = wrapper.find('li')
    expect(listItem.exists()).toBe(true)
    await listItem.trigger('click')

    expect(mockChat.openWindow).toHaveBeenCalledOnce()
    expect(mockChat.openWindow).toHaveBeenCalledWith('conv-42')
  })

  it('calls openWindow for the correct conversation when multiple exist', async () => {
    mockChat.conversations = [
      makeConversation({ id: 'conv-1' }),
      makeConversation({
        id:           'conv-2',
        participants: [
          { id: 'user-1', displayName: 'Alice' },
          { id: 'user-3', displayName: 'Charlie' },
        ],
      }),
    ]
    const wrapper = mountList()
    await flushPromises()

    const listItems = wrapper.findAll('li')
    expect(listItems).toHaveLength(2)

    await listItems[1].trigger('click')
    expect(mockChat.openWindow).toHaveBeenCalledWith('conv-2')
  })

  // ── Search filter ─────────────────────────────────────────────────────────

  it('renders the search input', () => {
    const wrapper = mountList()

    const input = wrapper.find('input[type="search"]')
    expect(input.exists()).toBe(true)
  })

  it('filters conversations by the other participant display name', async () => {
    mockChat.conversations = [
      makeConversation({ id: 'conv-1' }), // Bob
      makeConversation({
        id:           'conv-2',
        participants: [
          { id: 'user-1', displayName: 'Alice' },
          { id: 'user-3', displayName: 'Charlie' },
        ],
      }),
    ]
    const wrapper = mountList()
    await flushPromises()

    const searchInput = wrapper.find('input[type="search"]')
    await searchInput.setValue('charlie')

    // Only Charlie should remain
    const listItems = wrapper.findAll('li')
    expect(listItems).toHaveLength(1)
    expect(wrapper.text()).toContain('Charlie')
    expect(wrapper.text()).not.toContain('Bob')
  })

  it('shows empty state when search matches no conversations', async () => {
    mockChat.conversations = [makeConversation()]
    const wrapper = mountList()
    await flushPromises()

    const searchInput = wrapper.find('input[type="search"]')
    await searchInput.setValue('zzz-no-match')

    expect(wrapper.text()).toContain('No conversations yet')
  })

  it('shows all conversations when search query is cleared', async () => {
    mockChat.conversations = [
      makeConversation({ id: 'conv-1' }),
      makeConversation({
        id:           'conv-2',
        participants: [
          { id: 'user-1', displayName: 'Alice' },
          { id: 'user-3', displayName: 'Charlie' },
        ],
      }),
    ]
    const wrapper = mountList()
    await flushPromises()

    const searchInput = wrapper.find('input[type="search"]')
    await searchInput.setValue('bob')
    await searchInput.setValue('')

    const listItems = wrapper.findAll('li')
    expect(listItems).toHaveLength(2)
  })

  // ── Avatar fallback ───────────────────────────────────────────────────────

  it('renders avatar img when participant has an avatarUrl', async () => {
    mockChat.conversations = [
      makeConversation({
        participants: [
          { id: 'user-1', displayName: 'Alice' },
          { id: 'user-2', displayName: 'Bob', avatarUrl: 'https://cdn.example.com/bob.jpg' },
        ],
      }),
    ]
    const wrapper = mountList()
    await flushPromises()

    const img = wrapper.find('img[alt="Bob"]')
    expect(img.exists()).toBe(true)
    expect(img.attributes('src')).toBe('https://cdn.example.com/bob.jpg')
  })

  it('renders initial letter when participant has no avatarUrl', async () => {
    mockChat.conversations = [makeConversation()]
    const wrapper = mountList()
    await flushPromises()

    // Bob's initial
    expect(wrapper.find('img[alt="Bob"]').exists()).toBe(false)
    expect(wrapper.text()).toContain('B')
  })
})
