import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount, flushPromises, RouterLinkStub } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import TopNav from '../TopNav.vue'

// ── vue-router mock ───────────────────────────────────────────────────────────

const mockRouter = vi.hoisted(() => ({ push: vi.fn() }))

vi.mock('vue-router', async () => {
  const actual = await vi.importActual<typeof import('vue-router')>('vue-router')
  return { ...actual, useRouter: () => mockRouter }
})

// ── Store mocks ───────────────────────────────────────────────────────────────

const authStore = vi.hoisted(() => ({
  isAuthenticated: true,
  user: {
    id:          'u1',
    displayName: 'Alice',
    email:       'alice@example.com',
    avatarUrl:   null as string | null,
    role:        'User',
  } as {
    id: string; displayName: string; email: string; avatarUrl: string | null; role: string
  } | null,
  logout: vi.fn().mockResolvedValue(undefined),
}))

vi.mock('@/stores/auth', () => ({ useAuthStore: () => authStore }))

const friendsStore = vi.hoisted(() => ({
  pendingCount:        0,
  refreshPendingCount: vi.fn().mockResolvedValue(undefined),
}))

vi.mock('@/stores/friends', () => ({ useFriendsStore: () => friendsStore }))

const chatStore = vi.hoisted(() => ({
  totalUnread: 0,
  toggleList:  vi.fn(),
}))

vi.mock('@/stores/chat', () => ({ useChatStore: () => chatStore }))

const liveStore = vi.hoisted(() => ({
  streams: [] as unknown[],
}))

vi.mock('@/stores/live', () => ({ useLiveStore: () => liveStore }))

const notificationsStore = vi.hoisted(() => ({
  connect:            vi.fn(),
  disconnect:         vi.fn(),
  refreshUnreadCount: vi.fn().mockResolvedValue(undefined),
}))

vi.mock('@/stores/notifications', () => ({
  useNotificationsStore: () => notificationsStore,
}))

const presenceStore = vi.hoisted(() => ({
  reset: vi.fn(),
}))

vi.mock('@/stores/presence', () => ({ usePresenceStore: () => presenceStore }))

const companyModeStore = vi.hoisted(() => ({
  isActive:      false,
  activeCompany: null as { id: string; name: string } | null,
  deactivate:    vi.fn(),
}))

vi.mock('@/stores/companyMode', () => ({
  useCompanyModeStore: () => companyModeStore,
}))

// ── Child component stubs ─────────────────────────────────────────────────────

vi.mock('../NotificationBell.vue', () => ({
  default: { name: 'NotificationBell', template: '<div data-testid="notification-bell" />' },
}))

vi.mock('../SearchSuggest.vue', () => ({
  default: { name: 'SearchSuggest', template: '<div data-testid="search-suggest" />' },
}))

// ── Mount helper ──────────────────────────────────────────────────────────────

function mountNav(props = { sidebarOpen: false }) {
  return mount(TopNav, {
    props,
    global: {
      plugins: [createPinia()],
      stubs:   { RouterLink: RouterLinkStub, Transition: false },
    },
  })
}

// ── Tests ─────────────────────────────────────────────────────────────────────

describe('TopNav', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.clearAllMocks()

    // Reset store state
    authStore.isAuthenticated    = true
    authStore.user               = {
      id: 'u1', displayName: 'Alice', email: 'alice@example.com', avatarUrl: null, role: 'User',
    }
    friendsStore.pendingCount    = 0
    chatStore.totalUnread        = 0
    liveStore.streams            = []
    companyModeStore.isActive      = false
    companyModeStore.activeCompany = null

    // Re-apply implementations after clearAllMocks
    friendsStore.refreshPendingCount.mockResolvedValue(undefined)
    notificationsStore.refreshUnreadCount.mockResolvedValue(undefined)
    authStore.logout.mockResolvedValue(undefined)
  })

  // ── Logo ───────────────────────────────────────────────────────────────────

  it('renders Omnijoy logo text', async () => {
    const wrapper = mountNav()
    await flushPromises()

    expect(wrapper.text()).toContain('Omnijoy')
  })

  // ── User menu button ───────────────────────────────────────────────────────

  it('renders the user menu button when authenticated', async () => {
    const wrapper = mountNav()
    await flushPromises()

    expect(wrapper.find('[data-testid="user-menu-button"]').exists()).toBe(true)
  })

  it('clicking user-menu-button opens the dropdown with My Profile, Settings, Log out', async () => {
    const wrapper = mountNav()
    await flushPromises()

    await wrapper.find('[data-testid="user-menu-button"]').trigger('click')
    await flushPromises()

    const text = wrapper.text()
    expect(text).toContain('My Profile')
    expect(text).toContain('Settings')
    expect(text).toContain('Log out')
  })

  it("'Log out' button calls auth.logout() and router.push('/')", async () => {
    const wrapper = mountNav()
    await flushPromises()

    // Open the dropdown first
    await wrapper.find('[data-testid="user-menu-button"]').trigger('click')

    const logoutBtn = wrapper.find('[data-testid="logout-button"]')
    await logoutBtn.trigger('click')
    await flushPromises()

    expect(authStore.logout).toHaveBeenCalledOnce()
    expect(mockRouter.push).toHaveBeenCalledWith('/')
  })

  // ── Friend requests badge ──────────────────────────────────────────────────

  it('shows friend-requests badge when pendingCount > 0', async () => {
    friendsStore.pendingCount = 3
    const wrapper = mountNav()
    await flushPromises()

    // Badge span in the friend-requests RouterLink area
    expect(wrapper.text()).toContain('3')
  })

  it('hides friend-requests badge when pendingCount is 0', async () => {
    friendsStore.pendingCount = 0
    const wrapper = mountNav()
    await flushPromises()

    // The badge uses v-if="friendsStore.pendingCount > 0"
    // RouterLink to /friends uses aria-label="Friend requests"
    const friendLink = wrapper.find('[aria-label="Friend requests"]')
    expect(friendLink.exists()).toBe(true)
    // Badge span should not be present
    const badge = friendLink.find('span')
    expect(badge.exists()).toBe(false)
  })

  // ── Chat unread badge ──────────────────────────────────────────────────────

  it('shows chat unread badge when totalUnread > 0', async () => {
    chatStore.totalUnread = 5
    const wrapper = mountNav()
    await flushPromises()

    const msgBtn = wrapper.find('[aria-label="Messages"]')
    expect(msgBtn.exists()).toBe(true)
    expect(msgBtn.text()).toContain('5')
  })

  it('hides chat unread badge when totalUnread is 0', async () => {
    chatStore.totalUnread = 0
    const wrapper = mountNav()
    await flushPromises()

    const msgBtn = wrapper.find('[aria-label="Messages"]')
    const badge = msgBtn.find('span')
    expect(badge.exists()).toBe(false)
  })

  // ── Company-mode banner ────────────────────────────────────────────────────

  it('shows company-mode banner when companyMode.isActive is true', async () => {
    companyModeStore.isActive      = true
    companyModeStore.activeCompany = { id: 'c1', name: 'Acme Corp' }
    const wrapper = mountNav()
    await flushPromises()

    expect(wrapper.text()).toContain('Acme Corp')
  })

  it('hides company-mode banner when companyMode.isActive is false', async () => {
    companyModeStore.isActive      = false
    companyModeStore.activeCompany = null
    const wrapper = mountNav()
    await flushPromises()

    expect(wrapper.text()).not.toContain('Acting as')
  })

  it('Exit button calls companyMode.deactivate()', async () => {
    companyModeStore.isActive      = true
    companyModeStore.activeCompany = { id: 'c1', name: 'Acme Corp' }
    const wrapper = mountNav()
    await flushPromises()

    const exitBtn = wrapper.findAll('button').find(b => b.text() === 'Exit')
    expect(exitBtn).toBeDefined()
    await exitBtn!.trigger('click')

    expect(companyModeStore.deactivate).toHaveBeenCalledOnce()
  })

  // ── Hamburger / toggle-sidebar ─────────────────────────────────────────────

  it("emits 'toggle-sidebar' when the hamburger button is clicked", async () => {
    const wrapper = mountNav({ sidebarOpen: false })
    await flushPromises()

    const hamburger = wrapper.find('[aria-label="Open menu"]')
    expect(hamburger.exists()).toBe(true)
    await hamburger.trigger('click')

    expect(wrapper.emitted('toggle-sidebar')).toBeTruthy()
    expect(wrapper.emitted('toggle-sidebar')).toHaveLength(1)
  })

  // ── onMounted / onUnmounted ────────────────────────────────────────────────

  it('calls notifications.connect() on mount when auth.isAuthenticated', async () => {
    authStore.isAuthenticated = true
    mountNav()
    await flushPromises()

    expect(notificationsStore.connect).toHaveBeenCalledOnce()
  })

  it('does NOT call notifications.connect() when not authenticated', async () => {
    authStore.isAuthenticated = false
    mountNav()
    await flushPromises()

    expect(notificationsStore.connect).not.toHaveBeenCalled()
  })

  it('calls notifications.disconnect() on unmount', async () => {
    const wrapper = mountNav()
    await flushPromises()

    wrapper.unmount()

    expect(notificationsStore.disconnect).toHaveBeenCalled()
  })
})
