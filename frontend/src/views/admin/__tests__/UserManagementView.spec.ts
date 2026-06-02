import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import UserManagementView from '@/views/admin/UserManagementView.vue'

// ── Mock the admin service ────────────────────────────────────────────────────

const mockAdminService = vi.hoisted(() => ({
  listReports:            vi.fn(),
  updateReportStatus:     vi.fn(),
  bulkUpdateReportStatus: vi.fn(),
  banUser:                vi.fn(),
  unbanUser:              vi.fn(),
  changeUserRole:         vi.fn(),
  listUsers:              vi.fn(),
  getAuditLog:            vi.fn(),
}))

vi.mock('@/services/adminService', () => ({
  adminService: mockAdminService,
}))

// ── Mock the auth store ───────────────────────────────────────────────────────
// The view reads auth.user?.role to decide whether to show the role-change
// dropdown (Admin only). Using a plain object mock is sufficient because
// we set the user before each mount; isAdmin only needs to be correct at
// render time, not reactive to changes within a single test.

const mockAuthStore = vi.hoisted(() => ({
  user: null as { role: string } | null,
}))

vi.mock('@/stores/auth', () => ({
  useAuthStore: () => mockAuthStore,
}))

// ── Fixtures ──────────────────────────────────────────────────────────────────

function makeUser(
  id: string,
  overrides: Partial<{
    displayName: string
    email: string
    role: 'User' | 'Moderator' | 'Admin'
    isBanned: boolean
    bannedAt: string | null
    isActive: boolean
  }> = {},
) {
  return {
    id,
    email:       `user${id}@test.com`,
    displayName: `User ${id}`,
    role:        'User' as const,
    isActive:    true,
    isBanned:    false,
    bannedAt:    null,
    createdAt:   '2026-01-01T00:00:00Z',
    ...overrides,
  }
}

// ── Mount helper ──────────────────────────────────────────────────────────────

function mountView() {
  const pinia = createPinia()
  setActivePinia(pinia)
  return mount(UserManagementView, {
    global: { plugins: [pinia] },
  })
}

// ── Tests ─────────────────────────────────────────────────────────────────────

describe('UserManagementView', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    // Default: current user is an Admin
    mockAuthStore.user = { role: 'Admin' }
    mockAdminService.listUsers.mockResolvedValue({
      items: [makeUser('1'), makeUser('2')],
      page: 1,
      pageSize: 20,
      hasMore: false,
    })
  })

  // ── Initial fetch ─────────────────────────────────────────────────────────

  it('calls listUsers on mount', async () => {
    mountView()
    await flushPromises()
    expect(mockAdminService.listUsers).toHaveBeenCalledWith(1, 20, null)
  })

  it('renders a row for each user', async () => {
    const wrapper = mountView()
    await flushPromises()
    expect(wrapper.findAll('[data-testid="user-row"]')).toHaveLength(2)
  })

  // ── User row fields ───────────────────────────────────────────────────────

  it('renders displayName for each user', async () => {
    const wrapper = mountView()
    await flushPromises()
    expect(wrapper.text()).toContain('User 1')
    expect(wrapper.text()).toContain('User 2')
  })

  it('renders email for each user', async () => {
    const wrapper = mountView()
    await flushPromises()
    expect(wrapper.text()).toContain('user1@test.com')
    expect(wrapper.text()).toContain('user2@test.com')
  })

  it('renders the role badge in each row', async () => {
    const wrapper = mountView()
    await flushPromises()
    const rows = wrapper.findAll('[data-testid="user-row"]')
    rows.forEach(row => expect(row.text()).toContain('User'))
  })

  it('renders Active status badge for non-banned users', async () => {
    const wrapper = mountView()
    await flushPromises()
    const rows = wrapper.findAll('[data-testid="user-row"]')
    rows.forEach(row => expect(row.text()).toContain('Active'))
  })

  it('renders Banned status badge for banned users', async () => {
    mockAdminService.listUsers.mockResolvedValue({
      items: [makeUser('3', { isBanned: true, bannedAt: '2026-01-01T00:00:00Z' })],
      page: 1, pageSize: 20, hasMore: false,
    })
    const wrapper = mountView()
    await flushPromises()
    expect(wrapper.find('[data-testid="user-row"]').text()).toContain('Banned')
  })

  // ── Empty state ───────────────────────────────────────────────────────────

  it('shows empty state when no users are returned', async () => {
    mockAdminService.listUsers.mockResolvedValue({
      items: [], page: 1, pageSize: 20, hasMore: false,
    })
    const wrapper = mountView()
    await flushPromises()
    expect(wrapper.text()).toContain('No users found.')
  })

  // ── Loading state ─────────────────────────────────────────────────────────

  it('shows loading indicator while fetching users', async () => {
    let resolve!: (v: unknown) => void
    mockAdminService.listUsers.mockImplementation(
      () => new Promise(r => { resolve = r }),
    )

    const wrapper = mountView()
    await wrapper.vm.$nextTick()

    expect(wrapper.text()).toContain('Loading users')

    resolve({ items: [], page: 1, pageSize: 20, hasMore: false })
    await flushPromises()

    expect(wrapper.text()).not.toContain('Loading users')
  })

  // ── Search ────────────────────────────────────────────────────────────────

  it('submitting the search form with a query calls listUsers with that query', async () => {
    const wrapper = mountView()
    await flushPromises()

    await wrapper.find('[data-testid="user-search-input"]').setValue('alice')
    // Trigger the form's submit event (clicking a type=submit button in jsdom
    // does not automatically bubble to the form's submit handler)
    await wrapper.find('form').trigger('submit')
    await flushPromises()

    expect(mockAdminService.listUsers).toHaveBeenLastCalledWith(1, 20, 'alice')
  })

  it('submitting with an empty query passes null to listUsers', async () => {
    const wrapper = mountView()
    await flushPromises()

    const input = wrapper.find('[data-testid="user-search-input"]')
    await input.setValue('alice')
    await wrapper.find('form').trigger('submit')
    await flushPromises()

    await input.setValue('')
    await wrapper.find('form').trigger('submit')
    await flushPromises()

    expect(mockAdminService.listUsers).toHaveBeenLastCalledWith(1, 20, null)
  })

  // ── Ban user ──────────────────────────────────────────────────────────────

  it('clicking Ban calls banUser for an active user', async () => {
    mockAdminService.banUser.mockResolvedValue(
      makeUser('1', { isBanned: true, bannedAt: '2026-01-02T00:00:00Z' }),
    )

    const wrapper = mountView()
    await flushPromises()

    await wrapper.find('[aria-label="Ban User 1"]').trigger('click')
    await flushPromises()

    expect(mockAdminService.banUser).toHaveBeenCalledWith('1', null)
  })

  it('user row shows Banned status after a successful ban', async () => {
    mockAdminService.banUser.mockResolvedValue(
      makeUser('1', { isBanned: true, bannedAt: '2026-01-02T00:00:00Z' }),
    )

    const wrapper = mountView()
    await flushPromises()

    await wrapper.find('[aria-label="Ban User 1"]').trigger('click')
    await flushPromises()

    // The first row (user 1) should now show Banned
    expect(wrapper.findAll('[data-testid="user-row"]')[0].text()).toContain('Banned')
  })

  it('shows error alert when banUser fails', async () => {
    mockAdminService.banUser.mockRejectedValue({
      response: { data: { error: 'Cannot ban yourself.' } },
    })

    const wrapper = mountView()
    await flushPromises()

    await wrapper.find('[aria-label="Ban User 1"]').trigger('click')
    await flushPromises()

    expect(wrapper.find('[role="alert"]').text()).toContain('Cannot ban yourself.')
  })

  // ── Unban user ────────────────────────────────────────────────────────────

  it('clicking Unban calls unbanUser for a banned user', async () => {
    mockAdminService.listUsers.mockResolvedValue({
      items: [makeUser('1', { isBanned: true })],
      page: 1, pageSize: 20, hasMore: false,
    })
    mockAdminService.unbanUser.mockResolvedValue(makeUser('1'))

    const wrapper = mountView()
    await flushPromises()

    await wrapper.find('[aria-label="Unban User 1"]').trigger('click')
    await flushPromises()

    expect(mockAdminService.unbanUser).toHaveBeenCalledWith('1', null)
  })

  it('user row shows Active status after a successful unban', async () => {
    mockAdminService.listUsers.mockResolvedValue({
      items: [makeUser('1', { isBanned: true })],
      page: 1, pageSize: 20, hasMore: false,
    })
    mockAdminService.unbanUser.mockResolvedValue(makeUser('1', { isBanned: false }))

    const wrapper = mountView()
    await flushPromises()

    await wrapper.find('[aria-label="Unban User 1"]').trigger('click')
    await flushPromises()

    expect(wrapper.find('[data-testid="user-row"]').text()).toContain('Active')
  })

  it('shows error alert when unbanUser fails', async () => {
    mockAdminService.listUsers.mockResolvedValue({
      items: [makeUser('1', { isBanned: true })],
      page: 1, pageSize: 20, hasMore: false,
    })
    mockAdminService.unbanUser.mockRejectedValue({ message: 'Service unavailable.' })

    const wrapper = mountView()
    await flushPromises()

    await wrapper.find('[aria-label="Unban User 1"]').trigger('click')
    await flushPromises()

    expect(wrapper.find('[role="alert"]').text()).toContain('Service unavailable.')
  })

  // ── Role change dropdown ──────────────────────────────────────────────────

  it('role dropdown is rendered when auth user is Admin', async () => {
    const wrapper = mountView()
    await flushPromises()
    expect(wrapper.find('[aria-label="Change role for User 1"]').exists()).toBe(true)
  })

  it('role dropdown is not rendered when auth user is not Admin', async () => {
    mockAuthStore.user = { role: 'Moderator' }

    const wrapper = mountView()
    await flushPromises()

    expect(wrapper.find('[aria-label="Change role for User 1"]').exists()).toBe(false)
  })

  it('changing the role dropdown calls changeUserRole with the new role', async () => {
    mockAdminService.changeUserRole.mockResolvedValue(
      makeUser('1', { role: 'Moderator' }),
    )

    const wrapper = mountView()
    await flushPromises()

    await wrapper.find('[aria-label="Change role for User 1"]').setValue('Moderator')
    await flushPromises()

    expect(mockAdminService.changeUserRole).toHaveBeenCalledWith('1', 'Moderator')
  })

  it('selecting the same role does not call changeUserRole', async () => {
    const wrapper = mountView()
    await flushPromises()

    // User 1 has role 'User'; selecting 'User' again should be a no-op
    await wrapper.find('[aria-label="Change role for User 1"]').setValue('User')
    await flushPromises()

    expect(mockAdminService.changeUserRole).not.toHaveBeenCalled()
  })

  it('shows error alert when changeUserRole fails', async () => {
    mockAdminService.changeUserRole.mockRejectedValue({ message: 'Cannot demote owner.' })

    const wrapper = mountView()
    await flushPromises()

    await wrapper.find('[aria-label="Change role for User 1"]').setValue('Moderator')
    await flushPromises()

    expect(wrapper.find('[role="alert"]').text()).toContain('Cannot demote owner.')
  })

  // ── Pagination ────────────────────────────────────────────────────────────

  it('Previous button is disabled on page 1', async () => {
    const wrapper = mountView()
    await flushPromises()
    const prevBtn = wrapper.findAll('button').find(b => b.text() === 'Previous')
    expect(prevBtn!.attributes('disabled')).toBeDefined()
  })

  it('Next button is disabled when hasMore=false', async () => {
    const wrapper = mountView()
    await flushPromises()
    const nextBtn = wrapper.findAll('button').find(b => b.text() === 'Next')
    expect(nextBtn!.attributes('disabled')).toBeDefined()
  })

  it('Next button is enabled and paginates forward when hasMore=true', async () => {
    mockAdminService.listUsers
      .mockResolvedValueOnce({
        items: [makeUser('1')], page: 1, pageSize: 20, hasMore: true,
      })
      .mockResolvedValueOnce({
        items: [makeUser('2')], page: 2, pageSize: 20, hasMore: false,
      })

    const wrapper = mountView()
    await flushPromises()

    const nextBtn = wrapper.findAll('button').find(b => b.text() === 'Next')
    expect(nextBtn!.attributes('disabled')).toBeUndefined()

    await nextBtn!.trigger('click')
    await flushPromises()

    expect(mockAdminService.listUsers).toHaveBeenLastCalledWith(2, 20, null)
    expect(wrapper.text()).toContain('Page 2')
  })

  it('Previous button paginates backward from page 2', async () => {
    mockAdminService.listUsers
      .mockResolvedValueOnce({ items: [makeUser('1')], page: 1, pageSize: 20, hasMore: true })
      .mockResolvedValueOnce({ items: [makeUser('2')], page: 2, pageSize: 20, hasMore: false })
      .mockResolvedValueOnce({ items: [makeUser('1')], page: 1, pageSize: 20, hasMore: true })

    const wrapper = mountView()
    await flushPromises()

    await wrapper.findAll('button').find(b => b.text() === 'Next')!.trigger('click')
    await flushPromises()
    expect(wrapper.text()).toContain('Page 2')

    const prevBtn = wrapper.findAll('button').find(b => b.text() === 'Previous')
    await prevBtn!.trigger('click')
    await flushPromises()

    expect(mockAdminService.listUsers).toHaveBeenLastCalledWith(1, 20, null)
    expect(wrapper.text()).toContain('Page 1')
  })
})
