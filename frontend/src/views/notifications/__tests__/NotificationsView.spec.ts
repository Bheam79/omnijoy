import { describe, it, expect, vi, beforeEach } from 'vitest'
import { nextTick } from 'vue'
import { mount, flushPromises } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { createRouter, createMemoryHistory } from 'vue-router'
import NotificationsView from '../NotificationsView.vue'
import type { Notification } from '@/types'

// ── Mocks ─────────────────────────────────────────────────────────────────────

const mockNotificationService = vi.hoisted(() => ({
  list:        vi.fn(),
  markRead:    vi.fn(),
  markAllRead: vi.fn(),
}))

vi.mock('@/services/notificationService', () => ({
  notificationService: mockNotificationService,
}))

// Minimal notifications store stub
const mockStore = vi.hoisted(() => ({
  unreadCount:   0,
  lastReceived:  null as Notification | null,
  markRead:      vi.fn(),
  markAllRead:   vi.fn(),
}))

vi.mock('@/stores/notifications', () => ({
  useNotificationsStore: () => mockStore,
}))

// ── Router fixture ────────────────────────────────────────────────────────────

function makeRouter() {
  return createRouter({
    history: createMemoryHistory(),
    routes:  [{ path: '/notifications', component: NotificationsView }],
  })
}

// ── Helpers ───────────────────────────────────────────────────────────────────

function makeNotification(id: string, overrides: Partial<Notification> = {}): Notification {
  return {
    id,
    type:      'PostLike',
    isRead:    false,
    createdAt: new Date().toISOString(),
    actor:     { id: 'actor-1', displayName: 'Alice' },
    ...overrides,
  }
}

function makePage(items: Notification[], hasMore = false, unreadCount = 0) {
  return { items, page: 1, pageSize: 20, hasMore, unreadCount }
}

async function mountView() {
  const router = makeRouter()
  await router.push('/notifications')
  const wrapper = mount(NotificationsView, {
    global: {
      plugins: [createPinia(), router],
      stubs:   { RouterLink: true },
    },
  })
  await flushPromises()
  return wrapper
}

// ── Tests ─────────────────────────────────────────────────────────────────────

describe('NotificationsView', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.clearAllMocks()
    mockStore.unreadCount  = 0
    mockStore.lastReceived = null
    mockStore.markRead.mockResolvedValue(undefined)
    mockStore.markAllRead.mockResolvedValue(undefined)
  })

  // ── Rendering ──────────────────────────────────────────────────────────────

  it('renders the page heading', async () => {
    mockNotificationService.list.mockResolvedValue(makePage([]))

    const wrapper = await mountView()

    expect(wrapper.text()).toContain('Notifications')
  })

  it('shows skeleton while loading', async () => {
    // Don't resolve the promise yet — loading state will stay true
    mockNotificationService.list.mockReturnValue(new Promise(() => {}))

    const router = makeRouter()
    const wrapper = mount(NotificationsView, {
      global: { plugins: [createPinia(), router], stubs: { RouterLink: true } },
    })

    // Allow Vue to process the onMounted → loadPage → loading=true re-render
    await nextTick()

    expect(wrapper.find('.animate-pulse').exists()).toBe(true)
  })

  it('shows empty state when there are no notifications', async () => {
    mockNotificationService.list.mockResolvedValue(makePage([]))

    const wrapper = await mountView()

    expect(wrapper.text()).toContain('No notifications')
  })

  it('renders notifications list', async () => {
    const n1 = makeNotification('n1', { type: 'PostLike' })
    const n2 = makeNotification('n2', { type: 'PostComment' })
    mockNotificationService.list.mockResolvedValue(makePage([n1, n2]))

    const wrapper = await mountView()

    expect(wrapper.findAll('li')).toHaveLength(2)
  })

  it('shows unread dot for unread notifications', async () => {
    const n = makeNotification('n1', { isRead: false })
    mockNotificationService.list.mockResolvedValue(makePage([n]))

    const wrapper = await mountView()

    // The unread indicator has aria-label="Unread"
    expect(wrapper.find('[aria-label="Unread"]').exists()).toBe(true)
  })

  it('does not show unread dot for read notifications', async () => {
    const n = makeNotification('n1', { isRead: true })
    mockNotificationService.list.mockResolvedValue(makePage([n]))

    const wrapper = await mountView()

    expect(wrapper.find('[aria-label="Unread"]').exists()).toBe(false)
  })

  // ── Filter bar ─────────────────────────────────────────────────────────────

  it('renders all filter buttons', async () => {
    mockNotificationService.list.mockResolvedValue(makePage([]))

    const wrapper = await mountView()
    const buttons = wrapper.findAll('button').filter((b) =>
      ['All', 'Friend Requests', 'Mentions', 'Events', 'Reactions', 'Comments'].includes(b.text()),
    )

    expect(buttons).toHaveLength(6)
  })

  it('clicking a filter reloads with the correct types', async () => {
    mockNotificationService.list.mockResolvedValue(makePage([]))

    const wrapper = await mountView()
    vi.clearAllMocks()
    mockNotificationService.list.mockResolvedValue(makePage([]))

    // Click "Mentions" filter
    const mentionsBtn = wrapper.findAll('button').find((b) => b.text() === 'Mentions')
    await mentionsBtn!.trigger('click')
    await flushPromises()

    expect(mockNotificationService.list).toHaveBeenCalledWith(
      1,
      20,
      expect.arrayContaining(['MentionInPost', 'MentionInComment', 'TaggedInPost']),
    )
  })

  it('clicking a filter twice does not trigger a second load', async () => {
    mockNotificationService.list.mockResolvedValue(makePage([]))

    const wrapper = await mountView()
    const allBtn = wrapper.findAll('button').find((b) => b.text() === 'All')
    vi.clearAllMocks()
    mockNotificationService.list.mockResolvedValue(makePage([]))

    // Click "All" twice (it's already active)
    await allBtn!.trigger('click')
    await flushPromises()

    expect(mockNotificationService.list).not.toHaveBeenCalled()
  })

  // ── Mark all as read ───────────────────────────────────────────────────────

  it('shows "Mark all as read" button when there are unread items', async () => {
    const n = makeNotification('n1', { isRead: false })
    mockNotificationService.list.mockResolvedValue(makePage([n]))

    const wrapper = await mountView()

    expect(wrapper.text()).toContain('Mark all as read')
  })

  it('does not show "Mark all as read" when all are read', async () => {
    const n = makeNotification('n1', { isRead: true })
    mockNotificationService.list.mockResolvedValue(makePage([n]))

    const wrapper = await mountView()

    expect(wrapper.text()).not.toContain('Mark all as read')
  })

  it('clicking "Mark all as read" marks items and calls store', async () => {
    const n = makeNotification('n1', { isRead: false })
    mockNotificationService.list.mockResolvedValue(makePage([n], false, 1))

    const wrapper = await mountView()
    const btn = wrapper.findAll('button').find((b) => b.text() === 'Mark all as read')
    await btn!.trigger('click')
    await flushPromises()

    expect(mockStore.markAllRead).toHaveBeenCalled()
    // The button should now be hidden
    expect(wrapper.text()).not.toContain('Mark all as read')
  })

  // ── Load more ──────────────────────────────────────────────────────────────

  it('shows "Show more" button when hasMore is true', async () => {
    const n = makeNotification('n1')
    mockNotificationService.list.mockResolvedValue(makePage([n], true))

    const wrapper = await mountView()

    expect(wrapper.text()).toContain('Show more')
  })

  it('clicking "Show more" loads the next page', async () => {
    const n1 = makeNotification('n1')
    const n2 = makeNotification('n2')
    mockNotificationService.list
      .mockResolvedValueOnce(makePage([n1], true))
      .mockResolvedValueOnce(makePage([n2], false))

    const wrapper = await mountView()
    const showMoreBtn = wrapper.findAll('button').find((b) => b.text() === 'Show more')
    await showMoreBtn!.trigger('click')
    await flushPromises()

    expect(wrapper.findAll('li')).toHaveLength(2)
    expect(wrapper.text()).not.toContain('Show more')
  })

  // ── Click handler ──────────────────────────────────────────────────────────

  it('clicking a notification calls markRead and navigates', async () => {
    const n = makeNotification('n1', { type: 'PostLike', referenceId: 'post-42', isRead: false })
    mockNotificationService.list.mockResolvedValue(makePage([n]))

    const router = makeRouter()
    const pushSpy = vi.spyOn(router, 'push').mockResolvedValue(undefined as any)
    await router.push('/notifications')

    const wrapper = mount(NotificationsView, {
      global: { plugins: [createPinia(), router], stubs: { RouterLink: true } },
    })
    await flushPromises()

    await wrapper.find('li').trigger('click')
    await flushPromises()

    expect(mockStore.markRead).toHaveBeenCalledWith('n1')
    expect(pushSpy).toHaveBeenCalledWith('/share/posts/post-42')
  })

  it('clicking a read notification does not call markRead', async () => {
    const n = makeNotification('n1', { type: 'PostLike', isRead: true })
    mockNotificationService.list.mockResolvedValue(makePage([n]))

    const wrapper = await mountView()
    await wrapper.find('li').trigger('click')
    await flushPromises()

    expect(mockStore.markRead).not.toHaveBeenCalled()
  })

  // ── Error state ────────────────────────────────────────────────────────────

  it('shows error message on load failure', async () => {
    mockNotificationService.list.mockRejectedValue(new Error('Network error'))

    const wrapper = await mountView()

    expect(wrapper.text()).toContain('Failed to load')
  })

  it('retry button triggers reload', async () => {
    mockNotificationService.list
      .mockRejectedValueOnce(new Error('Network error'))
      .mockResolvedValueOnce(makePage([]))

    const wrapper = await mountView()
    const retryBtn = wrapper.find('button.underline')
    await retryBtn.trigger('click')
    await flushPromises()

    expect(wrapper.text()).not.toContain('Failed to load')
  })

  // ── Display helpers ────────────────────────────────────────────────────────

  it('renders a descriptive message for PostLike', async () => {
    const n = makeNotification('n1', { type: 'PostLike', actor: { id: 'a1', displayName: 'Bob' } })
    mockNotificationService.list.mockResolvedValue(makePage([n]))

    const wrapper = await mountView()

    expect(wrapper.text()).toContain('Bob liked your post')
  })

  it('renders a descriptive message for FriendRequest', async () => {
    const n = makeNotification('n1', { type: 'FriendRequest', actor: { id: 'a1', displayName: 'Carol' } })
    mockNotificationService.list.mockResolvedValue(makePage([n]))

    const wrapper = await mountView()

    expect(wrapper.text()).toContain('Carol sent you a friend request')
  })

  it('renders avatar initials when no avatarUrl', async () => {
    const n = makeNotification('n1', { actor: { id: 'a1', displayName: 'Dave' } })
    mockNotificationService.list.mockResolvedValue(makePage([n]))

    const wrapper = await mountView()

    expect(wrapper.text()).toContain('D')
  })

  it('renders image when avatarUrl is present', async () => {
    const n = makeNotification('n1', {
      actor: { id: 'a1', displayName: 'Eve', avatarUrl: 'https://example.com/e.jpg' },
    })
    mockNotificationService.list.mockResolvedValue(makePage([n]))

    const wrapper = await mountView()

    const img = wrapper.find('img')
    expect(img.exists()).toBe(true)
    expect(img.attributes('src')).toBe('https://example.com/e.jpg')
  })
})
