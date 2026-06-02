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
  messages:          {} as Record<string, Message[]>,
  minimizedWindows:  new Set<string>(),
  typingUsers:       {} as Record<string, string[]>,
  joinConversation:  vi.fn().mockResolvedValue(undefined),
  leaveConversation: vi.fn().mockResolvedValue(undefined),
  loadMessages:      vi.fn().mockResolvedValue(undefined),
  sendMessage:       vi.fn().mockResolvedValue(undefined),
  sendTyping:        vi.fn().mockResolvedValue(undefined),
  markRead:          vi.fn().mockResolvedValue(undefined),
  deleteMessage:     vi.fn().mockResolvedValue(undefined),
  toggleMinimize:    vi.fn(),
  closeWindow:       vi.fn(),
}))

vi.mock('@/stores/chat', () => ({
  useChatStore: () => mockChat,
}))

// ── OgPreviewCard stub ────────────────────────────────────────────────────────

vi.mock('@/components/chat/OgPreviewCard.vue', () => ({
  default: {
    name:     'OgPreviewCard',
    props:    ['url'],
    template: '<div data-testid="og-preview-stub" />',
  },
}))

// Import after mocks
import ChatWindow from '../ChatWindow.vue'

// ── Fixtures ──────────────────────────────────────────────────────────────────

function makeConversation(id = 'conv-1'): Conversation {
  return {
    id,
    type:         'Direct',
    participants: [
      { id: 'user-1', displayName: 'Alice' },
      { id: 'user-2', displayName: 'Bob' },
    ],
    unreadCount: 0,
    createdAt:   '2026-01-01T00:00:00Z',
  }
}

function makeMessage(overrides: Partial<Message> = {}): Message {
  return {
    id:             'msg-1',
    conversationId: 'conv-1',
    sender:         { id: 'user-2', displayName: 'Bob' },
    content:        'Hello!',
    messageType:    'Text',
    createdAt:      '2026-01-01T10:00:00Z',
    isDeleted:      false,
    ...overrides,
  }
}

// ── Mount helper ──────────────────────────────────────────────────────────────

function mountWindow(conversation = makeConversation()) {
  return mount(ChatWindow, {
    props:  { conversation },
    global: {
      plugins: [createPinia()],
      stubs:   {
        Teleport: { template: '<div><slot /></div>' },
      },
    },
  })
}

// ── Tests ─────────────────────────────────────────────────────────────────────

describe('ChatWindow', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.clearAllMocks()
    mockAuth.user            = { id: 'user-1', displayName: 'Alice' }
    mockChat.messages        = {}
    mockChat.minimizedWindows = new Set()
    mockChat.typingUsers     = {}
    mockChat.joinConversation.mockResolvedValue(undefined)
    mockChat.loadMessages.mockResolvedValue(undefined)
    mockChat.sendMessage.mockResolvedValue({ id: 'msg-new', conversationId: 'conv-1', sender: { id: 'user-1', displayName: 'Alice' }, content: 'hi', messageType: 'Text', createdAt: new Date().toISOString(), isDeleted: false })
  })

  // ── Mount / lifecycle ─────────────────────────────────────────────────────

  it('calls chatStore.joinConversation with the conversation id on mount', async () => {
    mountWindow()
    await flushPromises()

    expect(mockChat.joinConversation).toHaveBeenCalledOnce()
    expect(mockChat.joinConversation).toHaveBeenCalledWith('conv-1')
  })

  it('calls chatStore.loadMessages on mount when no messages are cached', async () => {
    mockChat.messages = {} // no cache
    mountWindow()
    await flushPromises()

    expect(mockChat.loadMessages).toHaveBeenCalledWith('conv-1')
  })

  it('does NOT call loadMessages on mount when messages are already cached', async () => {
    mockChat.messages = { 'conv-1': [makeMessage()] }
    mountWindow()
    await flushPromises()

    expect(mockChat.loadMessages).not.toHaveBeenCalled()
  })

  // ── Header ────────────────────────────────────────────────────────────────

  it('renders the other participant display name in the header', async () => {
    const wrapper = mountWindow()
    await flushPromises()

    // otherParticipant is Bob (not user-1)
    expect(wrapper.text()).toContain('Bob')
  })

  it('renders initials when other participant has no avatar', async () => {
    const wrapper = mountWindow()
    await flushPromises()

    // Bob → 'B' initial
    expect(wrapper.text()).toContain('B')
  })

  it('renders avatar img when other participant has an avatarUrl', async () => {
    const conv = makeConversation()
    conv.participants[1] = { id: 'user-2', displayName: 'Bob', avatarUrl: 'https://cdn.example.com/bob.jpg' }
    const wrapper = mountWindow(conv)
    await flushPromises()

    const img = wrapper.find('img[alt="Bob"]')
    expect(img.exists()).toBe(true)
    expect(img.attributes('src')).toBe('https://cdn.example.com/bob.jpg')
  })

  // ── Messages rendering ────────────────────────────────────────────────────

  it('shows empty message list when conversation has no cached messages', async () => {
    mockChat.messages = { 'conv-1': [] }
    const wrapper = mountWindow()
    await flushPromises()

    // Message rows use .flex.justify-end or .flex.justify-start — neither should exist
    const ownMsgs      = wrapper.findAll('.flex.justify-end')
    const receivedMsgs = wrapper.findAll('.flex.justify-start')
    expect(ownMsgs.length + receivedMsgs.length).toBe(0)
  })

  it('renders messages from chatStore for the active conversation', async () => {
    mockChat.messages = {
      'conv-1': [
        makeMessage({ id: 'msg-1', content: 'Hello from Bob' }),
        makeMessage({ id: 'msg-2', content: 'Hi back', sender: { id: 'user-1', displayName: 'Alice' } }),
      ],
    }
    const wrapper = mountWindow()
    await flushPromises()

    expect(wrapper.text()).toContain('Hello from Bob')
    expect(wrapper.text()).toContain('Hi back')
  })

  it('renders own messages right-aligned (justify-end)', async () => {
    mockChat.messages = {
      'conv-1': [
        makeMessage({ id: 'msg-mine', sender: { id: 'user-1', displayName: 'Alice' }, content: 'My message' }),
      ],
    }
    const wrapper = mountWindow()
    await flushPromises()

    const bubble = wrapper.find('.justify-end')
    expect(bubble.exists()).toBe(true)
  })

  it('renders received messages left-aligned (justify-start)', async () => {
    mockChat.messages = {
      'conv-1': [
        makeMessage({ id: 'msg-theirs', sender: { id: 'user-2', displayName: 'Bob' }, content: 'Their message' }),
      ],
    }
    const wrapper = mountWindow()
    await flushPromises()

    const bubble = wrapper.find('.justify-start')
    expect(bubble.exists()).toBe(true)
  })

  it('shows "Message deleted" text for isDeleted messages', async () => {
    mockChat.messages = {
      'conv-1': [makeMessage({ isDeleted: true, content: undefined })],
    }
    const wrapper = mountWindow()
    await flushPromises()

    expect(wrapper.text()).toContain('Message deleted')
  })

  // ── Typing indicator ──────────────────────────────────────────────────────

  it('shows typing indicator dots when another user is typing', async () => {
    // user-2 is typing in conv-1
    mockChat.typingUsers = { 'conv-1': ['user-2'] }
    const wrapper = mountWindow()
    await flushPromises()

    // The typing indicator contains animate-bounce spans
    expect(wrapper.find('.animate-bounce').exists()).toBe(true)
  })

  it('does not show typing indicator when only the current user is listed', async () => {
    // Only the current user (user-1) is in the typing list — should NOT show indicator
    mockChat.typingUsers = { 'conv-1': ['user-1'] }
    const wrapper = mountWindow()
    await flushPromises()

    expect(wrapper.find('.animate-bounce').exists()).toBe(false)
  })

  it('does not show typing indicator when no one is typing', async () => {
    mockChat.typingUsers = {}
    const wrapper = mountWindow()
    await flushPromises()

    expect(wrapper.find('.animate-bounce').exists()).toBe(false)
  })

  // ── Sending a message ─────────────────────────────────────────────────────

  it('calls chatStore.sendMessage with the trimmed text when Send is clicked', async () => {
    const wrapper = mountWindow()
    await flushPromises()

    const textarea = wrapper.find('textarea')
    await textarea.setValue('Hello world')

    const sendBtn = wrapper.find('button[aria-label="Send"]')
    await sendBtn.trigger('click')
    await flushPromises()

    expect(mockChat.sendMessage).toHaveBeenCalledOnce()
    const [convId, content] = mockChat.sendMessage.mock.calls[0]
    expect(convId).toBe('conv-1')
    expect(content).toBe('Hello world')
  })

  it('does not send when the textarea is empty', async () => {
    const wrapper = mountWindow()
    await flushPromises()

    const sendBtn = wrapper.find('button[aria-label="Send"]')
    await sendBtn.trigger('click')
    await flushPromises()

    expect(mockChat.sendMessage).not.toHaveBeenCalled()
  })

  it('clears the textarea after sending', async () => {
    const wrapper = mountWindow()
    await flushPromises()

    const textarea = wrapper.find('textarea')
    await textarea.setValue('Hi there')

    const sendBtn = wrapper.find('button[aria-label="Send"]')
    await sendBtn.trigger('click')
    await flushPromises()

    expect((textarea.element as HTMLTextAreaElement).value).toBe('')
  })

  // ── Enter key sends; Shift+Enter inserts newline ──────────────────────────

  it('sends the message when Enter is pressed (without Shift)', async () => {
    const wrapper = mountWindow()
    await flushPromises()

    const textarea = wrapper.find('textarea')
    await textarea.setValue('Enter send test')
    await textarea.trigger('keydown', { key: 'Enter', shiftKey: false })
    await flushPromises()

    expect(mockChat.sendMessage).toHaveBeenCalledOnce()
  })

  it('does NOT send when Shift+Enter is pressed', async () => {
    const wrapper = mountWindow()
    await flushPromises()

    const textarea = wrapper.find('textarea')
    await textarea.setValue('Multi-line\ntest')
    await textarea.trigger('keydown', { key: 'Enter', shiftKey: true })
    await flushPromises()

    expect(mockChat.sendMessage).not.toHaveBeenCalled()
  })

  // ── Minimize / close ──────────────────────────────────────────────────────

  it('calls chatStore.toggleMinimize when the minimize button is clicked', async () => {
    const wrapper = mountWindow()
    await flushPromises()

    const minimizeBtn = wrapper.find('button[aria-label="Minimize"]')
    await minimizeBtn.trigger('click')

    expect(mockChat.toggleMinimize).toHaveBeenCalledWith('conv-1')
  })

  it('calls chatStore.closeWindow when the Close button is clicked', async () => {
    const wrapper = mountWindow()
    await flushPromises()

    const closeBtn = wrapper.find('button[aria-label="Close"]')
    await closeBtn.trigger('click')

    expect(mockChat.closeWindow).toHaveBeenCalledWith('conv-1')
  })

  // ── Minimized state ───────────────────────────────────────────────────────

  it('hides the message area when conversation is minimized', async () => {
    mockChat.minimizedWindows = new Set(['conv-1'])
    const wrapper = mountWindow()
    await flushPromises()

    // The message area is inside v-if="!isMinimized" — the textarea should not exist
    expect(wrapper.find('textarea').exists()).toBe(false)
  })

  it('shows the message area when conversation is NOT minimized', async () => {
    mockChat.minimizedWindows = new Set()
    const wrapper = mountWindow()
    await flushPromises()

    expect(wrapper.find('textarea').exists()).toBe(true)
  })

  // ── Textarea placeholder ──────────────────────────────────────────────────

  it('renders the textarea with "Write a message…" placeholder', async () => {
    const wrapper = mountWindow()
    await flushPromises()

    const textarea = wrapper.find('textarea[placeholder="Write a message…"]')
    expect(textarea.exists()).toBe(true)
  })

  // ── Send button state ─────────────────────────────────────────────────────

  it('Send button is disabled when textarea is empty', async () => {
    const wrapper = mountWindow()
    await flushPromises()

    const sendBtn = wrapper.find('button[aria-label="Send"]')
    expect(sendBtn.attributes('disabled')).toBeDefined()
  })

  it('Send button is enabled when textarea has content', async () => {
    const wrapper = mountWindow()
    await flushPromises()

    const textarea = wrapper.find('textarea')
    await textarea.setValue('Some text')

    const sendBtn = wrapper.find('button[aria-label="Send"]')
    expect(sendBtn.attributes('disabled')).toBeUndefined()
  })

  // ── Load older messages button ────────────────────────────────────────────

  it('renders the "Load older messages" button', async () => {
    const wrapper = mountWindow()
    await flushPromises()

    const buttons = wrapper.findAll('button')
    const loadBtn = buttons.find(b => b.text().includes('Load older messages'))
    expect(loadBtn).toBeDefined()
  })
})
