import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { createRouter, createMemoryHistory } from 'vue-router'
import NotificationSettingsView from '../NotificationSettingsView.vue'
import type { NotificationPreferences } from '@/services/accountService'

// ── Mocks ─────────────────────────────────────────────────────────────────────

const mockAccountService = vi.hoisted(() => ({
  getNotificationPreferences:    vi.fn(),
  updateNotificationPreferences: vi.fn(),
}))

vi.mock('@/services/accountService', () => ({
  accountService: mockAccountService,
}))

const defaultPrefs: NotificationPreferences = {
  likesOnMyPosts:      true,
  commentsOnMyPosts:   true,
  postShares:          true,
  friendRequests:      true,
  newFollower:         true,
  mentions:            true,
  newPostsFromFriends: true,
  familyRelations:     true,
  eventInvites:        true,
  directMessages:      true,
  liveStreams:         true,
  companyPageInvites:  true,
}

// ── Router fixture ────────────────────────────────────────────────────────────

function makeRouter() {
  return createRouter({
    history: createMemoryHistory(),
    routes: [
      { path: '/settings/notifications', component: NotificationSettingsView },
      { path: '/settings', component: { template: '<div>Settings</div>' } },
    ],
  })
}

async function mountView() {
  const router = makeRouter()
  await router.push('/settings/notifications')
  const wrapper = mount(NotificationSettingsView, {
    global: {
      plugins: [createPinia(), router],
      stubs:   { RouterLink: true },
    },
  })
  await flushPromises()
  return wrapper
}

// ── Tests ─────────────────────────────────────────────────────────────────────

describe('NotificationSettingsView', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.clearAllMocks()
    vi.useFakeTimers()
    mockAccountService.getNotificationPreferences.mockResolvedValue({ ...defaultPrefs })
    mockAccountService.updateNotificationPreferences.mockImplementation(
      async (p: NotificationPreferences) => p,
    )
  })

  afterEach(() => {
    vi.useRealTimers()
  })

  // ── Initial load ──────────────────────────────────────────────────────────

  it('fetches preferences on mount', async () => {
    await mountView()
    expect(mockAccountService.getNotificationPreferences).toHaveBeenCalledTimes(1)
  })

  it('renders the page heading', async () => {
    const wrapper = await mountView()
    expect(wrapper.text()).toContain('Notifications')
  })

  it('shows loading spinner before preferences load', async () => {
    mockAccountService.getNotificationPreferences.mockReturnValue(
      new Promise(() => { /* never resolves */ }),
    )
    const router = makeRouter()
    await router.push('/settings/notifications')
    const wrapper = mount(NotificationSettingsView, {
      global: { plugins: [createPinia(), router], stubs: { RouterLink: true } },
    })
    expect(wrapper.find('.animate-spin').exists()).toBe(true)
  })

  // ── Grouping ─────────────────────────────────────────────────────────────

  it('renders all four groups', async () => {
    const wrapper = await mountView()
    expect(wrapper.text()).toContain('Post activity')
    expect(wrapper.text()).toContain('Social')
    expect(wrapper.text()).toContain('Events')
    expect(wrapper.text()).toContain('System')
  })

  it('renders a toggle for every preference key', async () => {
    const wrapper = await mountView()
    for (const key of Object.keys(defaultPrefs)) {
      const toggle = wrapper.find(`[data-testid="toggle-${key}"]`)
      expect(toggle.exists(), `missing toggle for ${key}`).toBe(true)
    }
  })

  it('reflects loaded values on toggle states', async () => {
    mockAccountService.getNotificationPreferences.mockResolvedValue({
      ...defaultPrefs,
      likesOnMyPosts: false,
      eventInvites:   false,
    })
    const wrapper = await mountView()
    const likes  = wrapper.find('[data-testid="toggle-likesOnMyPosts"]').element as HTMLInputElement
    const events = wrapper.find('[data-testid="toggle-eventInvites"]').element as HTMLInputElement
    const dms    = wrapper.find('[data-testid="toggle-directMessages"]').element as HTMLInputElement
    expect(likes.checked).toBe(false)
    expect(events.checked).toBe(false)
    expect(dms.checked).toBe(true)
  })

  // ── Auto-save behaviour ───────────────────────────────────────────────────

  it('does not auto-save synchronously when a toggle is clicked', async () => {
    const wrapper = await mountView()
    const toggle = wrapper.find('[data-testid="toggle-likesOnMyPosts"]')
    await toggle.setValue(false)
    expect(mockAccountService.updateNotificationPreferences).not.toHaveBeenCalled()
  })

  it('auto-saves after the debounce window elapses', async () => {
    const wrapper = await mountView()
    await wrapper.find('[data-testid="toggle-likesOnMyPosts"]').setValue(false)
    expect(mockAccountService.updateNotificationPreferences).not.toHaveBeenCalled()

    vi.advanceTimersByTime(500)
    await flushPromises()

    expect(mockAccountService.updateNotificationPreferences).toHaveBeenCalledTimes(1)
    expect(mockAccountService.updateNotificationPreferences).toHaveBeenCalledWith(
      expect.objectContaining({ likesOnMyPosts: false }),
    )
  })

  it('coalesces rapid toggles into a single PUT', async () => {
    const wrapper = await mountView()
    await wrapper.find('[data-testid="toggle-likesOnMyPosts"]').setValue(false)
    vi.advanceTimersByTime(100)
    await wrapper.find('[data-testid="toggle-commentsOnMyPosts"]').setValue(false)
    vi.advanceTimersByTime(100)
    await wrapper.find('[data-testid="toggle-eventInvites"]').setValue(false)

    expect(mockAccountService.updateNotificationPreferences).not.toHaveBeenCalled()

    vi.advanceTimersByTime(500)
    await flushPromises()

    expect(mockAccountService.updateNotificationPreferences).toHaveBeenCalledTimes(1)
    const payload = mockAccountService.updateNotificationPreferences.mock.calls[0][0]
    expect(payload.likesOnMyPosts).toBe(false)
    expect(payload.commentsOnMyPosts).toBe(false)
    expect(payload.eventInvites).toBe(false)
  })

  it('shows the spinner while saving', async () => {
    // Make update hang so we can observe the in-flight indicator.
    let resolveUpdate!: (p: NotificationPreferences) => void
    mockAccountService.updateNotificationPreferences.mockImplementation(
      () => new Promise<NotificationPreferences>((resolve) => { resolveUpdate = resolve }),
    )

    const wrapper = await mountView()
    await wrapper.find('[data-testid="toggle-likesOnMyPosts"]').setValue(false)
    vi.advanceTimersByTime(500)
    await flushPromises()

    expect(wrapper.find('[data-testid="save-spinner"]').exists()).toBe(true)

    resolveUpdate({ ...defaultPrefs, likesOnMyPosts: false })
    await flushPromises()

    expect(wrapper.find('[data-testid="save-spinner"]').exists()).toBe(false)
  })

  it('shows a saved indicator after a successful PUT', async () => {
    const wrapper = await mountView()
    await wrapper.find('[data-testid="toggle-friendRequests"]').setValue(false)
    vi.advanceTimersByTime(500)
    await flushPromises()
    expect(wrapper.find('[data-testid="saved-indicator"]').exists()).toBe(true)
  })

  // ── Error handling ────────────────────────────────────────────────────────

  it('shows an error banner if the initial load fails', async () => {
    mockAccountService.getNotificationPreferences.mockRejectedValue(new Error('boom'))
    const wrapper = await mountView()
    expect(wrapper.text()).toContain('Failed to load notification preferences')
  })

  it('shows server error when the PUT fails', async () => {
    mockAccountService.updateNotificationPreferences.mockRejectedValue({
      response: { data: { error: 'Server down.' } },
    })
    const wrapper = await mountView()
    await wrapper.find('[data-testid="toggle-mentions"]').setValue(false)
    vi.advanceTimersByTime(500)
    await flushPromises()
    expect(wrapper.find('[data-testid="error-banner"]').text()).toContain('Server down.')
  })

  it('shows a fallback error message when the PUT fails without a server message', async () => {
    mockAccountService.updateNotificationPreferences.mockRejectedValue(new Error('boom'))
    const wrapper = await mountView()
    await wrapper.find('[data-testid="toggle-mentions"]').setValue(false)
    vi.advanceTimersByTime(500)
    await flushPromises()
    expect(wrapper.find('[data-testid="error-banner"]').text()).toContain('Failed to save')
  })
})
