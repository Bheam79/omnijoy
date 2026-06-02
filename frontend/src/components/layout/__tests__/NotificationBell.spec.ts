import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import NotificationBell from '../NotificationBell.vue'
import type { Notification } from '@/types'

// ── vue-router mock ───────────────────────────────────────────────────────────

const mockRouter = vi.hoisted(() => ({ push: vi.fn() }))

vi.mock('vue-router', async () => {
  const actual = await vi.importActual<typeof import('vue-router')>('vue-router')
  return { ...actual, useRouter: () => mockRouter }
})

// ── Notifications store mock ─────────────────────────────────────────────────

const notificationsStore = vi.hoisted(() => ({
  notifications:  [] as Notification[],
  unreadCount:    0,
  dropdownOpen:   false,
  loading:        false,
  hasMore:        false,
  hasUnread:      false,
  toggleDropdown: vi.fn(),
  closeDropdown:  vi.fn(),
  markRead:       vi.fn().mockResolvedValue(undefined),
  markAllRead:    vi.fn().mockResolvedValue(undefined),
  loadMore:       vi.fn().mockResolvedValue(undefined),
}))

vi.mock('@/stores/notifications', () => ({
  useNotificationsStore: () => notificationsStore,
}))

// ── Helpers ───────────────────────────────────────────────────────────────────

function makeNotification(over: Partial<Notification> = {}): Notification {
  return {
    id:        'n1',
    type:      'FriendRequest',
    isRead:    false,
    createdAt: new Date().toISOString(),
    actor:     { id: 'a1', displayName: 'Alice' },
    ...over,
  }
}

function mountBell() {
  return mount(NotificationBell, {
    global: {
      plugins: [createPinia()],
      stubs:   { Transition: false },
    },
  })
}

// ── Tests ─────────────────────────────────────────────────────────────────────

describe('NotificationBell', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.clearAllMocks()

    // Reset store state
    notificationsStore.notifications = []
    notificationsStore.unreadCount   = 0
    notificationsStore.dropdownOpen  = false
    notificationsStore.loading       = false
    notificationsStore.hasMore       = false
    notificationsStore.hasUnread     = false

    notificationsStore.markRead.mockResolvedValue(undefined)
    notificationsStore.markAllRead.mockResolvedValue(undefined)
    notificationsStore.loadMore.mockResolvedValue(undefined)
  })

  // ── Bell button ────────────────────────────────────────────────────────────

  it('renders the bell icon button', () => {
    const wrapper = mountBell()
    const btn = wrapper.find('[data-testid="notification-bell-button"]')
    expect(btn.exists()).toBe(true)
    // SVG icon present inside the button
    expect(btn.find('svg').exists()).toBe(true)
  })

  it('sets aria-label to include the current unread count', () => {
    notificationsStore.unreadCount = 4
    const wrapper = mountBell()
    const btn = wrapper.find('[data-testid="notification-bell-button"]')
    expect(btn.attributes('aria-label')).toBe('Notifications, 4 unread')
  })

  // ── Badge ──────────────────────────────────────────────────────────────────

  it('shows unread count badge when unreadCount > 0', () => {
    notificationsStore.unreadCount = 3
    const wrapper = mountBell()

    const btn = wrapper.find('[data-testid="notification-bell-button"]')
    const badge = btn.find('span')
    expect(badge.exists()).toBe(true)
    expect(badge.text()).toBe('3')
  })

  it('hides unread count badge when unreadCount is 0', () => {
    notificationsStore.unreadCount = 0
    const wrapper = mountBell()

    const btn = wrapper.find('[data-testid="notification-bell-button"]')
    const badge = btn.find('span')
    expect(badge.exists()).toBe(false)
  })

  it("caps badge text at '9+' when unreadCount > 9", () => {
    notificationsStore.unreadCount = 42
    const wrapper = mountBell()

    const btn = wrapper.find('[data-testid="notification-bell-button"]')
    expect(btn.find('span').text()).toBe('9+')
  })

  it('shows exact count when unreadCount is exactly 9', () => {
    notificationsStore.unreadCount = 9
    const wrapper = mountBell()

    const btn = wrapper.find('[data-testid="notification-bell-button"]')
    expect(btn.find('span').text()).toBe('9')
  })

  // ── Click bell → opens dropdown ───────────────────────────────────────────

  it('clicking the bell calls toggleDropdown on the store', async () => {
    const wrapper = mountBell()

    await wrapper.find('[data-testid="notification-bell-button"]').trigger('click')

    expect(notificationsStore.toggleDropdown).toHaveBeenCalledOnce()
  })

  it('does not render the dropdown panel when dropdownOpen is false', () => {
    notificationsStore.dropdownOpen = false
    const wrapper = mountBell()

    expect(wrapper.find('[data-testid="notification-panel"]').exists()).toBe(false)
  })

  it('renders the dropdown panel when dropdownOpen is true', () => {
    notificationsStore.dropdownOpen = true
    const wrapper = mountBell()

    expect(wrapper.find('[data-testid="notification-panel"]').exists()).toBe(true)
  })

  // ── Notification list ─────────────────────────────────────────────────────

  it('renders one row per notification when the dropdown is open', () => {
    notificationsStore.dropdownOpen  = true
    notificationsStore.notifications = [
      makeNotification({ id: 'n1', actor: { id: 'a1', displayName: 'Alice' } }),
      makeNotification({ id: 'n2', actor: { id: 'a2', displayName: 'Bob' } }),
      makeNotification({ id: 'n3', actor: { id: 'a3', displayName: 'Cara' } }),
    ]
    const wrapper = mountBell()

    const items = wrapper.findAll('[data-testid="notification-item"]')
    expect(items).toHaveLength(3)
  })

  it('shows the empty state when there are no notifications', () => {
    notificationsStore.dropdownOpen  = true
    notificationsStore.notifications = []
    notificationsStore.loading       = false
    const wrapper = mountBell()

    expect(wrapper.text()).toContain("You're all caught up")
    expect(wrapper.findAll('[data-testid="notification-item"]')).toHaveLength(0)
  })

  it('shows the loading state when notifications are empty and loading=true', () => {
    notificationsStore.dropdownOpen  = true
    notificationsStore.notifications = []
    notificationsStore.loading       = true
    const wrapper = mountBell()

    expect(wrapper.text()).toContain('Loading')
  })

  // ── Click a notification ──────────────────────────────────────────────────

  it('clicking an unread notification calls markRead on the store', async () => {
    notificationsStore.dropdownOpen  = true
    notificationsStore.notifications = [
      makeNotification({
        id:           'n1',
        type:         'FriendRequest',
        isRead:       false,
        referenceId:  undefined,
      }),
    ]
    const wrapper = mountBell()

    await wrapper.find('[data-testid="notification-item"]').trigger('click')
    await flushPromises()

    expect(notificationsStore.markRead).toHaveBeenCalledWith('n1')
  })

  it('does NOT call markRead when the notification is already read', async () => {
    notificationsStore.dropdownOpen  = true
    notificationsStore.notifications = [
      makeNotification({ id: 'n1', isRead: true }),
    ]
    const wrapper = mountBell()

    await wrapper.find('[data-testid="notification-item"]').trigger('click')
    await flushPromises()

    expect(notificationsStore.markRead).not.toHaveBeenCalled()
  })

  it('navigates to the post page when clicking a PostLike notification with a referenceId', async () => {
    notificationsStore.dropdownOpen  = true
    notificationsStore.notifications = [
      makeNotification({
        id:           'n1',
        type:         'PostLike',
        referenceId:  'post-abc',
        isRead:       false,
      }),
    ]
    const wrapper = mountBell()

    await wrapper.find('[data-testid="notification-item"]').trigger('click')
    await flushPromises()

    expect(mockRouter.push).toHaveBeenCalledWith('/share/posts/post-abc')
  })

  it('closes the dropdown after clicking a notification', async () => {
    notificationsStore.dropdownOpen  = true
    notificationsStore.notifications = [makeNotification({ id: 'n1', isRead: false })]
    const wrapper = mountBell()

    await wrapper.find('[data-testid="notification-item"]').trigger('click')
    await flushPromises()

    expect(notificationsStore.closeDropdown).toHaveBeenCalled()
  })

  // ── Mark all read ─────────────────────────────────────────────────────────

  it("'Mark all read' button shows when hasUnread is true", () => {
    notificationsStore.dropdownOpen  = true
    notificationsStore.hasUnread     = true
    notificationsStore.notifications = [makeNotification({ id: 'n1' })]
    const wrapper = mountBell()

    expect(wrapper.find('[data-testid="mark-all-read"]').exists()).toBe(true)
  })

  it("'Mark all read' button is hidden when hasUnread is false", () => {
    notificationsStore.dropdownOpen  = true
    notificationsStore.hasUnread     = false
    const wrapper = mountBell()

    expect(wrapper.find('[data-testid="mark-all-read"]').exists()).toBe(false)
  })

  it("clicking 'Mark all read' calls markAllRead on the store", async () => {
    notificationsStore.dropdownOpen  = true
    notificationsStore.hasUnread     = true
    notificationsStore.notifications = [makeNotification({ id: 'n1' })]
    const wrapper = mountBell()

    await wrapper.find('[data-testid="mark-all-read"]').trigger('click')
    await flushPromises()

    expect(notificationsStore.markAllRead).toHaveBeenCalledOnce()
  })

  // ── Click-catcher closes dropdown ─────────────────────────────────────────

  it('clicking the outside click-catcher closes the dropdown', async () => {
    notificationsStore.dropdownOpen = true
    const wrapper = mountBell()

    // The click-catcher is the first div with .fixed.inset-0 class
    const catcher = wrapper.find('div.fixed.inset-0')
    expect(catcher.exists()).toBe(true)
    await catcher.trigger('click')

    expect(notificationsStore.closeDropdown).toHaveBeenCalled()
  })
})
