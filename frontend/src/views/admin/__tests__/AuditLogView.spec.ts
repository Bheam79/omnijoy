import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import AuditLogView from '@/views/admin/AuditLogView.vue'

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

// ── Fixtures ──────────────────────────────────────────────────────────────────

function makeEntry(id: string, overrides: Record<string, unknown> = {}) {
  return {
    id,
    actorId:           `actor-${id}`,
    actorDisplayName:  `Admin ${id}`,
    action:            'BanUser',
    targetType:        'User',
    targetId:          `00000000-0000-0000-0000-${id.padStart(12, '0')}`,
    notes:             `Note for ${id}`,
    createdAt:         '2026-01-15T10:30:00Z',
    ...overrides,
  }
}

// ── Mount helper ──────────────────────────────────────────────────────────────

function mountView() {
  const pinia = createPinia()
  setActivePinia(pinia)
  return mount(AuditLogView, {
    global: { plugins: [pinia] },
  })
}

// ── Tests ─────────────────────────────────────────────────────────────────────

describe('AuditLogView', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    mockAdminService.getAuditLog.mockResolvedValue({
      items: [makeEntry('1'), makeEntry('2')],
      page: 1,
      pageSize: 20,
      hasMore: false,
    })
  })

  // ── Initial fetch ─────────────────────────────────────────────────────────

  it('calls getAuditLog on mount', async () => {
    mountView()
    await flushPromises()
    expect(mockAdminService.getAuditLog).toHaveBeenCalledWith(1, 20, null, null)
  })

  it('renders a table row for each audit entry', async () => {
    const wrapper = mountView()
    await flushPromises()
    expect(wrapper.findAll('tbody tr')).toHaveLength(2)
  })

  // ── Entry field rendering ─────────────────────────────────────────────────

  it('renders actor display name in each row', async () => {
    const wrapper = mountView()
    await flushPromises()
    expect(wrapper.text()).toContain('Admin 1')
    expect(wrapper.text()).toContain('Admin 2')
  })

  it('renders the action badge for each entry', async () => {
    const wrapper = mountView()
    await flushPromises()
    const actionCells = wrapper.findAll('tbody tr td:nth-child(3)')
    actionCells.forEach(cell => expect(cell.text()).toContain('BanUser'))
  })

  it('renders the target type for each entry', async () => {
    const wrapper = mountView()
    await flushPromises()
    const targetCells = wrapper.findAll('tbody tr td:nth-child(4)')
    targetCells.forEach(cell => expect(cell.text()).toContain('User'))
  })

  it('renders the first 8 characters of the target ID', async () => {
    const wrapper = mountView()
    await flushPromises()
    // The template trims: entry.targetId.substring(0, 8)
    expect(wrapper.text()).toContain('00000000')
  })

  it('renders notes for entries that have them', async () => {
    const wrapper = mountView()
    await flushPromises()
    expect(wrapper.text()).toContain('Note for 1')
    expect(wrapper.text()).toContain('Note for 2')
  })

  it('renders "—" when notes is null', async () => {
    mockAdminService.getAuditLog.mockResolvedValue({
      items: [makeEntry('3', { notes: null })],
      page: 1, pageSize: 20, hasMore: false,
    })
    const wrapper = mountView()
    await flushPromises()
    expect(wrapper.text()).toContain('—')
  })

  // ── Empty state ───────────────────────────────────────────────────────────

  it('shows empty state when no entries are returned', async () => {
    mockAdminService.getAuditLog.mockResolvedValue({
      items: [], page: 1, pageSize: 20, hasMore: false,
    })
    const wrapper = mountView()
    await flushPromises()
    expect(wrapper.text()).toContain('No audit entries match the current filter.')
  })

  // ── Loading state ─────────────────────────────────────────────────────────

  it('shows loading indicator while the fetch is in progress', async () => {
    let resolve!: (v: unknown) => void
    mockAdminService.getAuditLog.mockImplementation(
      () => new Promise(r => { resolve = r }),
    )

    const wrapper = mountView()
    await wrapper.vm.$nextTick()

    expect(wrapper.text()).toContain('Loading audit log')

    resolve({ items: [], page: 1, pageSize: 20, hasMore: false })
    await flushPromises()

    expect(wrapper.text()).not.toContain('Loading audit log')
  })

  // ── Action filter ─────────────────────────────────────────────────────────

  it('re-fetches with the selected action when the filter select changes', async () => {
    const wrapper = mountView()
    await flushPromises()

    await wrapper.find('[aria-label="Filter by action"]').setValue('BanUser')
    await flushPromises()

    expect(mockAdminService.getAuditLog).toHaveBeenLastCalledWith(1, 20, null, 'BanUser')
  })

  it('re-fetches with null action filter when "All actions" is selected', async () => {
    const wrapper = mountView()
    await flushPromises()

    const select = wrapper.find('[aria-label="Filter by action"]')
    await select.setValue('BanUser')
    await flushPromises()

    // Select "All actions" (value='')
    await select.setValue('')
    await flushPromises()

    expect(mockAdminService.getAuditLog).toHaveBeenLastCalledWith(1, 20, null, null)
  })

  it('passes the ChangeRole filter correctly', async () => {
    const wrapper = mountView()
    await flushPromises()

    await wrapper.find('[aria-label="Filter by action"]').setValue('ChangeRole')
    await flushPromises()

    expect(mockAdminService.getAuditLog).toHaveBeenLastCalledWith(1, 20, null, 'ChangeRole')
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

  it('Next button is enabled when hasMore=true and paginates forward', async () => {
    mockAdminService.getAuditLog
      .mockResolvedValueOnce({
        items: [makeEntry('1')], page: 1, pageSize: 20, hasMore: true,
      })
      .mockResolvedValueOnce({
        items: [makeEntry('2')], page: 2, pageSize: 20, hasMore: false,
      })

    const wrapper = mountView()
    await flushPromises()

    const nextBtn = wrapper.findAll('button').find(b => b.text() === 'Next')
    expect(nextBtn!.attributes('disabled')).toBeUndefined()

    await nextBtn!.trigger('click')
    await flushPromises()

    expect(mockAdminService.getAuditLog).toHaveBeenLastCalledWith(2, 20, null, null)
    expect(wrapper.text()).toContain('Page 2')
  })

  it('Previous button paginates backward from page 2', async () => {
    mockAdminService.getAuditLog
      .mockResolvedValueOnce({ items: [makeEntry('1')], page: 1, pageSize: 20, hasMore: true })
      .mockResolvedValueOnce({ items: [makeEntry('2')], page: 2, pageSize: 20, hasMore: false })
      .mockResolvedValueOnce({ items: [makeEntry('1')], page: 1, pageSize: 20, hasMore: true })

    const wrapper = mountView()
    await flushPromises()

    await wrapper.findAll('button').find(b => b.text() === 'Next')!.trigger('click')
    await flushPromises()
    expect(wrapper.text()).toContain('Page 2')

    const prevBtn = wrapper.findAll('button').find(b => b.text() === 'Previous')
    await prevBtn!.trigger('click')
    await flushPromises()

    expect(mockAdminService.getAuditLog).toHaveBeenLastCalledWith(1, 20, null, null)
    expect(wrapper.text()).toContain('Page 1')
  })

  it('Previous button is disabled after navigating back to page 1', async () => {
    mockAdminService.getAuditLog
      .mockResolvedValueOnce({ items: [makeEntry('1')], page: 1, pageSize: 20, hasMore: true })
      .mockResolvedValueOnce({ items: [makeEntry('2')], page: 2, pageSize: 20, hasMore: false })
      .mockResolvedValueOnce({ items: [makeEntry('1')], page: 1, pageSize: 20, hasMore: true })

    const wrapper = mountView()
    await flushPromises()

    await wrapper.findAll('button').find(b => b.text() === 'Next')!.trigger('click')
    await flushPromises()

    await wrapper.findAll('button').find(b => b.text() === 'Previous')!.trigger('click')
    await flushPromises()

    const prevBtn = wrapper.findAll('button').find(b => b.text() === 'Previous')
    expect(prevBtn!.attributes('disabled')).toBeDefined()
  })

  // ── Filter resets page ────────────────────────────────────────────────────

  it('changing the filter reloads from page 1', async () => {
    mockAdminService.getAuditLog
      .mockResolvedValueOnce({ items: [makeEntry('1')], page: 1, pageSize: 20, hasMore: true })
      .mockResolvedValueOnce({ items: [makeEntry('2')], page: 2, pageSize: 20, hasMore: false })
      .mockResolvedValueOnce({ items: [makeEntry('3')], page: 1, pageSize: 20, hasMore: false })

    const wrapper = mountView()
    await flushPromises()

    // Navigate to page 2
    await wrapper.findAll('button').find(b => b.text() === 'Next')!.trigger('click')
    await flushPromises()
    expect(wrapper.text()).toContain('Page 2')

    // Change filter — should reload from page 1
    await wrapper.find('[aria-label="Filter by action"]').setValue('UnbanUser')
    await flushPromises()

    expect(mockAdminService.getAuditLog).toHaveBeenLastCalledWith(1, 20, null, 'UnbanUser')
  })
})
