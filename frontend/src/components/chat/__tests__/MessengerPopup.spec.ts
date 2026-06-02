import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import type { Conversation } from '@/types'

// ── Auth store mock ────────────────────────────────────────────────────────────

const mockAuth = vi.hoisted(() => ({
  isAuthenticated: true,
  user:            { id: 'user-1', displayName: 'Alice' } as { id: string; displayName: string } | null,
}))

vi.mock('@/stores/auth', () => ({
  useAuthStore: () => mockAuth,
}))

// ── Chat store mock ────────────────────────────────────────────────────────────

const mockChat = vi.hoisted(() => ({
  conversations:    [] as Conversation[],
  openWindows:      [] as string[],
  listOpen:         false,
  totalUnread:      0,
  connect:          vi.fn(),
  disconnect:       vi.fn(),
  loadConversations: vi.fn().mockResolvedValue(undefined),
  toggleList:       vi.fn(),
  closeChat:        vi.fn(),
}))

vi.mock('@/stores/chat', () => ({
  useChatStore: () => mockChat,
}))

// ── ChatWindow stub ───────────────────────────────────────────────────────────

vi.mock('@/components/chat/ChatWindow.vue', () => ({
  default: {
    name:     'ChatWindow',
    props:    ['conversation'],
    template: '<div data-testid="chat-window-stub">{{ conversation.id }}</div>',
  },
}))

// ── ConversationList stub ─────────────────────────────────────────────────────

vi.mock('@/components/chat/ConversationList.vue', () => ({
  default: {
    name:     'ConversationList',
    template: '<div data-testid="conversation-list-stub" />',
  },
}))

// Import after mocks
import MessengerPopup from '../MessengerPopup.vue'

// ── Fixtures ──────────────────────────────────────────────────────────────────

function makeConversation(id: string): Conversation {
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

// ── Mount helper ──────────────────────────────────────────────────────────────

function mountPopup() {
  return mount(MessengerPopup, {
    global: {
      plugins: [createPinia()],
      stubs:   {
        Transition: { template: '<div><slot /></div>' },
      },
    },
  })
}

// ── Tests ─────────────────────────────────────────────────────────────────────

describe('MessengerPopup', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.clearAllMocks()
    mockAuth.isAuthenticated  = true
    mockAuth.user             = { id: 'user-1', displayName: 'Alice' }
    mockChat.conversations    = []
    mockChat.openWindows      = []
    mockChat.listOpen         = false
    mockChat.totalUnread      = 0
    mockChat.connect          = vi.fn()
    mockChat.disconnect       = vi.fn()
    mockChat.loadConversations.mockResolvedValue(undefined)
  })

  // ── Authentication guard ──────────────────────────────────────────────────

  it('renders nothing when user is not authenticated', async () => {
    mockAuth.isAuthenticated = false
    const wrapper = mountPopup()
    await flushPromises()

    // The template is wrapped in v-if="auth.isAuthenticated"
    expect(wrapper.find('button[aria-label="Open Messenger"]').exists()).toBe(false)
  })

  it('renders the messenger FAB when authenticated', async () => {
    const wrapper = mountPopup()
    await flushPromises()

    const fab = wrapper.find('button[aria-label="Open Messenger"]')
    expect(fab.exists()).toBe(true)
  })

  // ── Lifecycle ─────────────────────────────────────────────────────────────

  it('calls chatStore.connect on mount when authenticated', async () => {
    mountPopup()
    await flushPromises()

    expect(mockChat.connect).toHaveBeenCalledOnce()
  })

  it('calls chatStore.loadConversations on mount when authenticated', async () => {
    mountPopup()
    await flushPromises()

    expect(mockChat.loadConversations).toHaveBeenCalledOnce()
  })

  it('does NOT call connect or loadConversations when not authenticated', async () => {
    mockAuth.isAuthenticated = false
    mountPopup()
    await flushPromises()

    expect(mockChat.connect).not.toHaveBeenCalled()
    expect(mockChat.loadConversations).not.toHaveBeenCalled()
  })

  // ── Messenger FAB ─────────────────────────────────────────────────────────

  it('calls chatStore.toggleList when the Messenger FAB is clicked', async () => {
    const wrapper = mountPopup()
    await flushPromises()

    const fab = wrapper.find('button[aria-label="Open Messenger"]')
    await fab.trigger('click')

    expect(mockChat.toggleList).toHaveBeenCalledOnce()
  })

  // ── Unread badge on FAB ───────────────────────────────────────────────────

  it('shows the unread badge on the FAB when totalUnread > 0', async () => {
    mockChat.totalUnread = 5
    const wrapper = mountPopup()
    await flushPromises()

    expect(wrapper.text()).toContain('5')
    // The badge uses bg-red-500
    expect(wrapper.find('.bg-red-500').exists()).toBe(true)
  })

  it('shows "99+" when totalUnread is 100 or more', async () => {
    mockChat.totalUnread = 100
    const wrapper = mountPopup()
    await flushPromises()

    expect(wrapper.text()).toContain('99+')
  })

  it('does not render the unread badge when totalUnread is 0', async () => {
    mockChat.totalUnread = 0
    const wrapper = mountPopup()
    await flushPromises()

    expect(wrapper.find('.bg-red-500').exists()).toBe(false)
  })

  // ── ConversationList panel ────────────────────────────────────────────────

  it('shows ConversationList when listOpen is true', async () => {
    mockChat.listOpen = true
    const wrapper = mountPopup()
    await flushPromises()

    expect(wrapper.find('[data-testid="conversation-list-stub"]').exists()).toBe(true)
  })

  it('hides ConversationList when listOpen is false', async () => {
    mockChat.listOpen = false
    const wrapper = mountPopup()
    await flushPromises()

    expect(wrapper.find('[data-testid="conversation-list-stub"]').exists()).toBe(false)
  })

  // ── Click-away to close list ──────────────────────────────────────────────

  it('renders a click-away overlay when listOpen is true', async () => {
    mockChat.listOpen = true
    const wrapper = mountPopup()
    await flushPromises()

    // The click-away is a fixed inset-0 div rendered when listOpen
    const overlay = wrapper.find('.fixed.inset-0')
    expect(overlay.exists()).toBe(true)
  })

  it('calls chatStore.toggleList when the click-away overlay is clicked', async () => {
    mockChat.listOpen = true
    const wrapper = mountPopup()
    await flushPromises()

    const overlay = wrapper.find('.fixed.inset-0')
    await overlay.trigger('click')

    expect(mockChat.toggleList).toHaveBeenCalledOnce()
  })

  // ── ChatWindow panels ─────────────────────────────────────────────────────

  it('renders a ChatWindow for each open conversation', async () => {
    mockChat.conversations = [makeConversation('conv-1'), makeConversation('conv-2')]
    mockChat.openWindows   = ['conv-1', 'conv-2']
    const wrapper = mountPopup()
    await flushPromises()

    const windows = wrapper.findAll('[data-testid="chat-window-stub"]')
    expect(windows).toHaveLength(2)
  })

  it('renders no ChatWindows when openWindows is empty', async () => {
    mockChat.openWindows = []
    const wrapper = mountPopup()
    await flushPromises()

    expect(wrapper.find('[data-testid="chat-window-stub"]').exists()).toBe(false)
  })

  it('passes the correct conversation object to each ChatWindow', async () => {
    mockChat.conversations = [makeConversation('conv-99')]
    mockChat.openWindows   = ['conv-99']
    const wrapper = mountPopup()
    await flushPromises()

    const chatWindow = wrapper.find('[data-testid="chat-window-stub"]')
    // The stub renders the conversation.id as text
    expect(chatWindow.text()).toBe('conv-99')
  })

  it('only renders ChatWindows for conversations that exist in the conversations list', async () => {
    // openWindows has 2 IDs but only 1 matches an actual conversation
    mockChat.conversations = [makeConversation('conv-real')]
    mockChat.openWindows   = ['conv-real', 'conv-missing']
    const wrapper = mountPopup()
    await flushPromises()

    // The computed `openConversations` filters out undefined, so only 1 window
    const windows = wrapper.findAll('[data-testid="chat-window-stub"]')
    expect(windows).toHaveLength(1)
    expect(windows[0].text()).toBe('conv-real')
  })

  // ── ConversationList and ChatWindow can coexist ───────────────────────────

  it('can show both ConversationList and ChatWindows simultaneously', async () => {
    mockChat.conversations = [makeConversation('conv-1')]
    mockChat.openWindows   = ['conv-1']
    mockChat.listOpen      = true
    const wrapper = mountPopup()
    await flushPromises()

    expect(wrapper.find('[data-testid="conversation-list-stub"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="chat-window-stub"]').exists()).toBe(true)
  })
})
