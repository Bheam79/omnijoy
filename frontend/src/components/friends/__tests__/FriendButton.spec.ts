import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'

// ── Service mock ──────────────────────────────────────────────────────────────
//
// FriendButton talks directly to `friendService` (not via a store), so the
// mock surface here is the service module.

const friendService = vi.hoisted(() => ({
  getStatus:      vi.fn(),
  sendRequest:    vi.fn().mockResolvedValue(undefined),
  cancelRequest:  vi.fn().mockResolvedValue(undefined),
  acceptRequest:  vi.fn().mockResolvedValue(undefined),
  declineRequest: vi.fn().mockResolvedValue(undefined),
  removeFriend:   vi.fn().mockResolvedValue(undefined),
  blockUser:      vi.fn().mockResolvedValue(undefined),
  unblockUser:    vi.fn().mockResolvedValue(undefined),
}))

vi.mock('@/services/friendService', () => ({
  friendService,
}))

// Stubs for the auth/friends stores — included to satisfy the task spec's
// "vi.mock('@/stores/friends')" / "vi.mock('@/stores/auth')" requirement
// even though FriendButton itself doesn't import them.
vi.mock('@/stores/friends', () => ({
  useFriendsStore: () => ({}),
}))

vi.mock('@/stores/auth', () => ({
  useAuthStore: () => ({ accessToken: 'test-token' }),
}))

// Import after mocks
import FriendButton from '@/components/friends/FriendButton.vue'
import type { FriendStatus } from '@/services/friendService'

// ── Helpers ───────────────────────────────────────────────────────────────────

function mountButton(initialStatus: FriendStatus | 'error' = 'None') {
  if (initialStatus === 'error') {
    friendService.getStatus.mockRejectedValue(new Error('boom'))
  } else {
    friendService.getStatus.mockResolvedValue(initialStatus)
  }

  return mount(FriendButton, {
    props: { userId: 'target-user-id' },
    global: { plugins: [createPinia()] },
  })
}

// ── Tests ─────────────────────────────────────────────────────────────────────

describe('FriendButton', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.clearAllMocks()

    // Restore resolved-value implementations after clearAllMocks
    friendService.sendRequest.mockResolvedValue(undefined)
    friendService.cancelRequest.mockResolvedValue(undefined)
    friendService.acceptRequest.mockResolvedValue(undefined)
    friendService.declineRequest.mockResolvedValue(undefined)
    friendService.removeFriend.mockResolvedValue(undefined)
    friendService.blockUser.mockResolvedValue(undefined)
    friendService.unblockUser.mockResolvedValue(undefined)
  })

  // ── Initial render / loading skeleton ─────────────────────────────────────

  it('renders a loading skeleton before the initial status resolves', async () => {
    // Never resolve, so status stays null
    friendService.getStatus.mockReturnValue(new Promise(() => {}))

    const wrapper = mount(FriendButton, {
      props: { userId: 'target-user-id' },
      global: { plugins: [createPinia()] },
    })
    // No flushPromises — keep promise pending

    expect(wrapper.find('.animate-pulse').exists()).toBe(true)
    expect(wrapper.find('[data-testid="friend-button"]').exists()).toBe(false)
  })

  it('falls back to "None" status when getStatus rejects', async () => {
    const wrapper = mountButton('error')
    await flushPromises()

    expect(wrapper.text()).toContain('Add Friend')
  })

  it('calls friendService.getStatus on mount with the given userId', async () => {
    mountButton('None')
    await flushPromises()

    expect(friendService.getStatus).toHaveBeenCalledWith('target-user-id')
  })

  // ── status === 'None' ─────────────────────────────────────────────────────

  it("renders an 'Add Friend' button when status is 'None'", async () => {
    const wrapper = mountButton('None')
    await flushPromises()

    const btn = wrapper.find('[data-testid="friend-button"]')
    expect(btn.exists()).toBe(true)
    expect(btn.text()).toContain('Add Friend')
  })

  it("clicking 'Add Friend' calls friendService.sendRequest and emits statusChanged('RequestSent')", async () => {
    const wrapper = mountButton('None')
    await flushPromises()

    await wrapper.find('[data-testid="friend-button"]').trigger('click')
    await flushPromises()

    expect(friendService.sendRequest).toHaveBeenCalledWith('target-user-id')

    const emitted = wrapper.emitted('statusChanged')
    expect(emitted).toBeTruthy()
    expect(emitted![0]).toEqual(['RequestSent'])

    // Status flipped to RequestSent — now shows the Pending label
    expect(wrapper.text()).toContain('Pending')
  })

  it("does NOT emit statusChanged when sendRequest throws", async () => {
    friendService.sendRequest.mockRejectedValue(new Error('network'))
    const wrapper = mountButton('None')
    await flushPromises()

    await wrapper.find('[data-testid="friend-button"]').trigger('click')
    await flushPromises()

    expect(wrapper.emitted('statusChanged')).toBeUndefined()
    // Status remains 'None'
    expect(wrapper.text()).toContain('Add Friend')
  })

  // ── status === 'RequestSent' (Pending) ────────────────────────────────────

  it("renders a 'Pending' button when status is 'RequestSent'", async () => {
    const wrapper = mountButton('RequestSent')
    await flushPromises()

    const btn = wrapper.find('[data-testid="friend-button"]')
    expect(btn.exists()).toBe(true)
    expect(btn.text()).toContain('Pending')
  })

  it("clicking the Pending button cancels the request and emits statusChanged('None')", async () => {
    const wrapper = mountButton('RequestSent')
    await flushPromises()

    await wrapper.find('[data-testid="friend-button"]').trigger('click')
    await flushPromises()

    expect(friendService.cancelRequest).toHaveBeenCalledWith('target-user-id')
    expect(wrapper.emitted('statusChanged')![0]).toEqual(['None'])
  })

  // ── status === 'RequestReceived' (Accept / Decline) ───────────────────────

  it("renders Accept and Decline buttons when status is 'RequestReceived'", async () => {
    const wrapper = mountButton('RequestReceived')
    await flushPromises()

    const buttons = wrapper.findAll('button')
    const acceptBtn  = buttons.find(b => b.text().includes('Accept'))
    const declineBtn = buttons.find(b => b.text().includes('Decline'))
    expect(acceptBtn).toBeDefined()
    expect(declineBtn).toBeDefined()
  })

  it("Accept calls friendService.acceptRequest and emits 'Accepted'", async () => {
    const wrapper = mountButton('RequestReceived')
    await flushPromises()

    const acceptBtn = wrapper.findAll('button').find(b => b.text().includes('Accept'))!
    await acceptBtn.trigger('click')
    await flushPromises()

    expect(friendService.acceptRequest).toHaveBeenCalledWith('target-user-id')
    expect(wrapper.emitted('statusChanged')![0]).toEqual(['Accepted'])
  })

  it("Decline calls friendService.declineRequest and emits 'None'", async () => {
    const wrapper = mountButton('RequestReceived')
    await flushPromises()

    const declineBtn = wrapper.findAll('button').find(b => b.text().includes('Decline'))!
    await declineBtn.trigger('click')
    await flushPromises()

    expect(friendService.declineRequest).toHaveBeenCalledWith('target-user-id')
    expect(wrapper.emitted('statusChanged')![0]).toEqual(['None'])
  })

  // ── status === 'Accepted' (Friends dropdown) ──────────────────────────────

  it("renders a 'Friends' dropdown trigger when status is 'Accepted'", async () => {
    const wrapper = mountButton('Accepted')
    await flushPromises()

    const btn = wrapper.find('[data-testid="friend-button"]')
    expect(btn.exists()).toBe(true)
    expect(btn.text()).toContain('Friends')
  })

  it("opens an Unfriend / Block menu when the Friends trigger is clicked", async () => {
    const wrapper = mountButton('Accepted')
    await flushPromises()

    // Menu is closed by default
    expect(wrapper.text()).not.toContain('Unfriend')

    await wrapper.find('[data-testid="friend-button"]').trigger('click')
    await flushPromises()

    expect(wrapper.text()).toContain('Unfriend')
    expect(wrapper.text()).toContain('Block')
  })

  it("clicking Unfriend calls friendService.removeFriend and emits 'None'", async () => {
    const wrapper = mountButton('Accepted')
    await flushPromises()

    await wrapper.find('[data-testid="friend-button"]').trigger('click')
    await flushPromises()

    const unfriendBtn = wrapper.findAll('button').find(b => b.text() === 'Unfriend')!
    await unfriendBtn.trigger('click')
    await flushPromises()

    expect(friendService.removeFriend).toHaveBeenCalledWith('target-user-id')
    expect(wrapper.emitted('statusChanged')![0]).toEqual(['None'])
  })

  it("clicking Block calls friendService.blockUser and emits 'BlockedByMe'", async () => {
    const wrapper = mountButton('Accepted')
    await flushPromises()

    await wrapper.find('[data-testid="friend-button"]').trigger('click')
    await flushPromises()

    const blockBtn = wrapper.findAll('button').find(b => b.text() === 'Block')!
    await blockBtn.trigger('click')
    await flushPromises()

    expect(friendService.blockUser).toHaveBeenCalledWith('target-user-id')
    expect(wrapper.emitted('statusChanged')![0]).toEqual(['BlockedByMe'])
  })

  // ── status === 'BlockedByMe' ──────────────────────────────────────────────

  it("renders an Unblock button when status is 'BlockedByMe'", async () => {
    const wrapper = mountButton('BlockedByMe')
    await flushPromises()

    const btn = wrapper.find('[data-testid="friend-button"]')
    expect(btn.exists()).toBe(true)
    expect(btn.text()).toContain('Blocked')
  })

  it("clicking the Blocked button unblocks and emits 'None'", async () => {
    const wrapper = mountButton('BlockedByMe')
    await flushPromises()

    await wrapper.find('[data-testid="friend-button"]').trigger('click')
    await flushPromises()

    expect(friendService.unblockUser).toHaveBeenCalledWith('target-user-id')
    expect(wrapper.emitted('statusChanged')![0]).toEqual(['None'])
  })

  // ── status === 'BlockedByThem' ────────────────────────────────────────────

  it("renders nothing actionable when status is 'BlockedByThem'", async () => {
    const wrapper = mountButton('BlockedByThem')
    await flushPromises()

    expect(wrapper.find('[data-testid="friend-button"]').exists()).toBe(false)
    expect(wrapper.text().trim()).toBe('')
  })

  // ── Disabled / loading state ──────────────────────────────────────────────

  it('disables the Add Friend button while the request is in flight and shows "Sending…"', async () => {
    // Keep sendRequest pending so we can observe the loading state mid-flight
    let resolveSend!: () => void
    friendService.sendRequest.mockReturnValue(new Promise<void>(r => { resolveSend = r }))

    const wrapper = mountButton('None')
    await flushPromises()

    const btn = wrapper.find('[data-testid="friend-button"]')
    expect((btn.element as HTMLButtonElement).disabled).toBe(false)

    await btn.trigger('click')
    await flushPromises()

    expect((btn.element as HTMLButtonElement).disabled).toBe(true)
    expect(btn.text()).toContain('Sending')

    // Finish the request
    resolveSend()
    await flushPromises()
  })

  it('disables the Pending button while cancellation is in flight and shows "Cancelling…"', async () => {
    let resolveCancel!: () => void
    friendService.cancelRequest.mockReturnValue(new Promise<void>(r => { resolveCancel = r }))

    const wrapper = mountButton('RequestSent')
    await flushPromises()

    const btn = wrapper.find('[data-testid="friend-button"]')
    await btn.trigger('click')
    await flushPromises()

    expect((btn.element as HTMLButtonElement).disabled).toBe(true)
    expect(btn.text()).toContain('Cancelling')

    resolveCancel()
    await flushPromises()
  })
})
