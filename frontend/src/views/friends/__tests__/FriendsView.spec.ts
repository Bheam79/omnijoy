import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount, flushPromises, RouterLinkStub } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'

// ── SignalR mock ──────────────────────────────────────────────────────────────
//
// vi.hoisted() runs before module imports. HubConnectionBuilder must be a
// regular function (not an arrow function) so `new` works.

const { hubConnectionMock, HubConnectionBuilderCtor } = vi.hoisted(() => {
  const conn = {
    on:    vi.fn(),
    start: vi.fn().mockResolvedValue(undefined),
    stop:  vi.fn(),
  }

  function HubConnectionBuilderCtor(this: unknown) {
    return {
      withUrl:                vi.fn().mockReturnThis(),
      withAutomaticReconnect: vi.fn().mockReturnThis(),
      configureLogging:       vi.fn().mockReturnThis(),
      build:                  vi.fn().mockReturnValue(conn),
    }
  }

  return { hubConnectionMock: conn, HubConnectionBuilderCtor }
})

vi.mock('@microsoft/signalr', () => ({
  HubConnectionBuilder: HubConnectionBuilderCtor,
  LogLevel:             { Warning: 1 },
}))

// ── Store mocks ───────────────────────────────────────────────────────────────

const friendsStore = vi.hoisted(() => ({
  friends:             [] as unknown[],
  pendingRequests:     [] as unknown[],
  sentRequests:        [] as unknown[],
  suggestions:         [] as unknown[],
  pendingCount:        0,
  friendsHasMore:      false,
  loadingFriends:      false,
  loadingRequests:     false,
  loadingSuggestions:  false,
  loadFriends:         vi.fn().mockResolvedValue(undefined),
  loadRequests:        vi.fn().mockResolvedValue(undefined),
  loadSuggestions:     vi.fn().mockResolvedValue(undefined),
  acceptRequest:       vi.fn().mockResolvedValue(undefined),
  declineRequest:      vi.fn().mockResolvedValue(undefined),
  cancelRequest:       vi.fn().mockResolvedValue(undefined),
  removeFriend:        vi.fn().mockResolvedValue(undefined),
  onFriendRequestReceived: vi.fn(),
  onFriendRequestAccepted: vi.fn(),
}))

vi.mock('@/stores/friends', () => ({
  useFriendsStore: () => friendsStore,
}))

const authStore = vi.hoisted(() => ({
  accessToken: 'test-token' as string | null,
}))

vi.mock('@/stores/auth', () => ({
  useAuthStore: () => authStore,
}))

// ── Child component stubs ─────────────────────────────────────────────────────

vi.mock('@/components/friends/FriendButton.vue', () => ({
  default: {
    name:     'FriendButton',
    props:    ['userId'],
    template: '<div data-testid="friend-button-stub" />',
  },
}))

vi.mock('@/components/friends/InviteFriendsModal.vue', () => ({
  default: {
    name:     'InviteFriendsModal',
    emits:    ['close'],
    template: '<div data-testid="invite-modal-stub" />',
  },
}))

// Import after mocks
import FriendsView from '@/views/friends/FriendsView.vue'

// ── Mount helper ──────────────────────────────────────────────────────────────

function mountView() {
  return mount(FriendsView, {
    global: {
      plugins: [createPinia()],
      stubs:   { RouterLink: RouterLinkStub },
    },
  })
}

// ── Fixtures ──────────────────────────────────────────────────────────────────

const sampleFriend = (id: string, name: string) => ({
  friendshipId: `fs-${id}`,
  user: { id, displayName: name, avatarUrl: undefined as string | undefined },
  mutualFriendCount: 2,
  friendedAt: '2026-01-01T00:00:00Z',
  familyRelation: undefined as undefined | { relationType: string; subtype: string },
})

const sampleRequest = (id: string, name: string) => ({
  friendshipId: `req-${id}`,
  from: { id, displayName: name, avatarUrl: undefined as string | undefined },
  sentAt: '2026-01-15T00:00:00Z',
})

const sampleSuggestion = (id: string, name: string, mutual = 1) => ({
  user: { id, displayName: name, avatarUrl: undefined as string | undefined },
  mutualFriendCount: mutual,
})

// ── Tests ─────────────────────────────────────────────────────────────────────

describe('FriendsView', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.clearAllMocks()

    // Reset store state to defaults
    friendsStore.friends             = []
    friendsStore.pendingRequests     = []
    friendsStore.sentRequests        = []
    friendsStore.suggestions         = []
    friendsStore.pendingCount        = 0
    friendsStore.friendsHasMore      = false
    friendsStore.loadingFriends      = false
    friendsStore.loadingRequests     = false
    friendsStore.loadingSuggestions  = false

    authStore.accessToken = 'test-token'

    // Reset resolved values cleared by clearAllMocks
    friendsStore.loadFriends.mockResolvedValue(undefined)
    friendsStore.loadRequests.mockResolvedValue(undefined)
    friendsStore.loadSuggestions.mockResolvedValue(undefined)
    friendsStore.acceptRequest.mockResolvedValue(undefined)
    friendsStore.declineRequest.mockResolvedValue(undefined)
    friendsStore.cancelRequest.mockResolvedValue(undefined)
    friendsStore.removeFriend.mockResolvedValue(undefined)
    hubConnectionMock.start.mockResolvedValue(undefined)
  })

  // ── Mount / load ──────────────────────────────────────────────────────────

  it('calls loadFriends, loadRequests, and loadSuggestions on mount', async () => {
    mountView()
    await flushPromises()

    expect(friendsStore.loadFriends).toHaveBeenCalledWith(true)
    expect(friendsStore.loadRequests).toHaveBeenCalled()
    expect(friendsStore.loadSuggestions).toHaveBeenCalled()
  })

  it('renders the page heading and Invite friends button', async () => {
    const wrapper = mountView()
    await flushPromises()

    expect(wrapper.text()).toContain('Friends')
    const inviteBtn = wrapper.findAll('button').find(b => b.text().includes('Invite friends'))
    expect(inviteBtn).toBeDefined()
  })

  // ── Friends list ──────────────────────────────────────────────────────────

  it('renders friend cards when friendsStore.friends is populated', async () => {
    friendsStore.friends = [
      sampleFriend('u1', 'Alice'),
      sampleFriend('u2', 'Bob'),
    ]

    const wrapper = mountView()
    await flushPromises()

    const cards = wrapper.findAll('[data-testid="friend-card"]')
    expect(cards).toHaveLength(2)
    expect(wrapper.text()).toContain('Alice')
    expect(wrapper.text()).toContain('Bob')
  })

  it("shows 'No friends yet' empty state when friends list is empty", async () => {
    friendsStore.friends        = []
    friendsStore.loadingFriends = false

    const wrapper = mountView()
    await flushPromises()

    expect(wrapper.text()).toContain('No friends yet')
  })

  it('clicking Browse suggestions in empty state switches to the suggestions tab', async () => {
    friendsStore.friends     = []
    friendsStore.suggestions = [sampleSuggestion('u9', 'Eve')]

    const wrapper = mountView()
    await flushPromises()

    const browseBtn = wrapper.findAll('button').find(b => b.text().includes('Browse suggestions'))
    expect(browseBtn).toBeDefined()
    await browseBtn!.trigger('click')
    await flushPromises()

    // After switching tabs, the suggestion's display name is visible
    expect(wrapper.text()).toContain('Eve')
  })

  it('clicking Unfriend on a friend card calls friendsStore.removeFriend with the user id', async () => {
    friendsStore.friends = [sampleFriend('u1', 'Alice')]

    const wrapper = mountView()
    await flushPromises()

    const unfriend = wrapper.findAll('button').find(b => b.text() === 'Unfriend')
    expect(unfriend).toBeDefined()
    await unfriend!.trigger('click')

    expect(friendsStore.removeFriend).toHaveBeenCalledWith('u1')
  })

  it('shows a Show more button when friendsHasMore is true and calls loadFriends on click', async () => {
    friendsStore.friends        = [sampleFriend('u1', 'Alice')]
    friendsStore.friendsHasMore = true

    const wrapper = mountView()
    await flushPromises()

    const showMore = wrapper.findAll('button').find(b => b.text().includes('Show more'))
    expect(showMore).toBeDefined()

    const callsBefore = friendsStore.loadFriends.mock.calls.length
    await showMore!.trigger('click')
    expect(friendsStore.loadFriends.mock.calls.length).toBe(callsBefore + 1)
  })

  it('renders the loading skeleton when loadingFriends is true and no friends yet', async () => {
    friendsStore.friends        = []
    friendsStore.loadingFriends = true

    const wrapper = mountView()
    await flushPromises()

    const skeletons = wrapper.findAll('.animate-pulse')
    expect(skeletons.length).toBeGreaterThanOrEqual(1)
  })

  // ── Pending requests ──────────────────────────────────────────────────────

  it('renders pending incoming requests with Accept and Decline buttons', async () => {
    friendsStore.pendingRequests = [
      sampleRequest('r1', 'Charlie'),
      sampleRequest('r2', 'Dana'),
    ]
    friendsStore.pendingCount = 2

    const wrapper = mountView()
    await flushPromises()

    // Switch to the requests tab
    await wrapper.find('[data-testid="requests-tab"]').trigger('click')
    await flushPromises()

    expect(wrapper.find('[data-testid="pending-requests"]').exists()).toBe(true)
    expect(wrapper.text()).toContain('Charlie')
    expect(wrapper.text()).toContain('Dana')

    const acceptButtons = wrapper.findAll('[data-testid="accept-button"]')
    const declineButtons = wrapper.findAll('[data-testid="decline-button"]')
    expect(acceptButtons).toHaveLength(2)
    expect(declineButtons).toHaveLength(2)
  })

  it('clicking Accept calls friendsStore.acceptRequest(id)', async () => {
    friendsStore.pendingRequests = [sampleRequest('r1', 'Charlie')]
    friendsStore.pendingCount    = 1

    const wrapper = mountView()
    await flushPromises()

    await wrapper.find('[data-testid="requests-tab"]').trigger('click')
    await flushPromises()

    await wrapper.find('[data-testid="accept-button"]').trigger('click')
    expect(friendsStore.acceptRequest).toHaveBeenCalledWith('r1')
  })

  it('clicking Decline calls friendsStore.declineRequest(id)', async () => {
    friendsStore.pendingRequests = [sampleRequest('r1', 'Charlie')]
    friendsStore.pendingCount    = 1

    const wrapper = mountView()
    await flushPromises()

    await wrapper.find('[data-testid="requests-tab"]').trigger('click')
    await flushPromises()

    await wrapper.find('[data-testid="decline-button"]').trigger('click')
    expect(friendsStore.declineRequest).toHaveBeenCalledWith('r1')
  })

  it('shows empty state for requests when pendingRequests is empty', async () => {
    friendsStore.pendingRequests = []

    const wrapper = mountView()
    await flushPromises()

    await wrapper.find('[data-testid="requests-tab"]').trigger('click')
    await flushPromises()

    expect(wrapper.text()).toContain('No pending friend requests')
  })

  // ── Sent requests ─────────────────────────────────────────────────────────

  it('renders sent requests and Cancel calls cancelRequest', async () => {
    friendsStore.sentRequests = [sampleRequest('s1', 'Frank')]

    const wrapper = mountView()
    await flushPromises()

    await wrapper.find('[data-testid="sent-tab"]').trigger('click')
    await flushPromises()

    expect(wrapper.text()).toContain('Frank')

    const cancelBtn = wrapper.findAll('button').find(b => b.text() === 'Cancel')
    expect(cancelBtn).toBeDefined()
    await cancelBtn!.trigger('click')
    expect(friendsStore.cancelRequest).toHaveBeenCalledWith('s1')
  })

  it('shows empty state for sent tab when no sent requests', async () => {
    friendsStore.sentRequests = []

    const wrapper = mountView()
    await flushPromises()

    await wrapper.find('[data-testid="sent-tab"]').trigger('click')
    await flushPromises()

    expect(wrapper.text()).toContain('No outgoing friend requests')
  })

  // ── Suggestions ───────────────────────────────────────────────────────────

  it('renders suggested people on the suggestions tab', async () => {
    friendsStore.suggestions = [
      sampleSuggestion('u10', 'Gina', 3),
      sampleSuggestion('u11', 'Hank', 1),
    ]

    const wrapper = mountView()
    await flushPromises()

    await wrapper.find('[data-testid="suggestions-tab"]').trigger('click')
    await flushPromises()

    expect(wrapper.text()).toContain('Gina')
    expect(wrapper.text()).toContain('Hank')
    // mutualFriendCount pluralisation
    expect(wrapper.text()).toContain('3 mutual friends')
    expect(wrapper.text()).toContain('1 mutual friend')

    // One FriendButton stub per suggestion
    expect(wrapper.findAll('[data-testid="friend-button-stub"]')).toHaveLength(2)
  })

  it('shows empty state on suggestions tab when no suggestions', async () => {
    friendsStore.suggestions = []

    const wrapper = mountView()
    await flushPromises()

    await wrapper.find('[data-testid="suggestions-tab"]').trigger('click')
    await flushPromises()

    expect(wrapper.text()).toContain('No suggestions right now')
  })

  // ── Invite modal ──────────────────────────────────────────────────────────

  it('Invite friends button toggles the InviteFriendsModal', async () => {
    const wrapper = mountView()
    await flushPromises()

    expect(wrapper.find('[data-testid="invite-modal-stub"]').exists()).toBe(false)

    const inviteBtn = wrapper.findAll('button').find(b => b.text().includes('Invite friends'))
    await inviteBtn!.trigger('click')
    await flushPromises()

    expect(wrapper.find('[data-testid="invite-modal-stub"]').exists()).toBe(true)
  })

  // ── SignalR ───────────────────────────────────────────────────────────────

  it('connects to the notifications hub on mount when an access token is present', async () => {
    mountView()
    await flushPromises()

    expect(hubConnectionMock.start).toHaveBeenCalled()
    expect(hubConnectionMock.on).toHaveBeenCalledWith('FriendRequestReceived', expect.any(Function))
    expect(hubConnectionMock.on).toHaveBeenCalledWith('FriendRequestAccepted', expect.any(Function))
  })

  it('does NOT connect to SignalR when accessToken is null', async () => {
    authStore.accessToken = null

    mountView()
    await flushPromises()

    expect(hubConnectionMock.start).not.toHaveBeenCalled()
  })

  it('FriendRequestReceived event forwards to store.onFriendRequestReceived', async () => {
    mountView()
    await flushPromises()

    const onCall = hubConnectionMock.on.mock.calls.find(c => c[0] === 'FriendRequestReceived')
    expect(onCall).toBeDefined()
    const handler = onCall![1] as (data: { from: { id: string; displayName: string } }) => void

    handler({ from: { id: 'u42', displayName: 'Ivy' } })
    expect(friendsStore.onFriendRequestReceived).toHaveBeenCalledWith({ id: 'u42', displayName: 'Ivy' })
  })

  it('FriendRequestAccepted event forwards to store.onFriendRequestAccepted', async () => {
    mountView()
    await flushPromises()

    const onCall = hubConnectionMock.on.mock.calls.find(c => c[0] === 'FriendRequestAccepted')
    expect(onCall).toBeDefined()
    const handler = onCall![1] as (data: { by: { id: string; displayName: string } }) => void

    handler({ by: { id: 'u99', displayName: 'Jack' } })
    expect(friendsStore.onFriendRequestAccepted).toHaveBeenCalledWith({ id: 'u99', displayName: 'Jack' })
  })

  it('stops the SignalR connection on unmount', async () => {
    const wrapper = mountView()
    await flushPromises()

    wrapper.unmount()
    expect(hubConnectionMock.stop).toHaveBeenCalled()
  })
})
