import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'

// ── Mock the admin service ───────────────────────────────────────────────────
const mockAdminService = vi.hoisted(() => ({
  listReports:           vi.fn(),
  updateReportStatus:    vi.fn(),
  bulkUpdateReportStatus: vi.fn(),
  banUser:               vi.fn(),
  unbanUser:             vi.fn(),
  changeUserRole:        vi.fn(),
  listUsers:             vi.fn(),
  getAuditLog:           vi.fn(),
}))

vi.mock('@/services/adminService', () => ({
  adminService: mockAdminService,
}))

// Stub the detail drawer so it doesn't try to Teleport in tests
vi.mock('@/components/admin/ReportDetailDrawer.vue', () => ({
  default: { template: '<div data-test="drawer-stub" />' },
}))

import ReportQueueView from '@/views/admin/ReportQueueView.vue'

const samplePending = (id: string, name = 'Reporter') => ({
  id,
  reporterId:   `reporter-${id}`,
  reporterName: name,
  targetType:   'Post' as const,
  targetId:     `tgt-${id}`,
  reason:       'Spam' as const,
  notes:        `notes for ${id}`,
  status:       'Pending' as const,
  createdAt:    '2026-01-01T00:00:00Z',
})

function mountView() {
  const pinia = createPinia()
  setActivePinia(pinia)

  return mount(ReportQueueView, {
    global: {
      plugins: [pinia],
      stubs:   { RouterLink: true, Teleport: true },
    },
  })
}

describe('ReportQueueView', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    mockAdminService.listReports.mockResolvedValue({
      items: [samplePending('r1', 'Alice'), samplePending('r2', 'Bob')],
      page: 1,
      pageSize: 20,
      hasMore: false,
    })
  })

  it('calls listReports on mount and renders rows', async () => {
    const wrapper = mountView()
    await flushPromises()

    expect(mockAdminService.listReports).toHaveBeenCalledWith(1, 20, 'Pending', null)
    expect(wrapper.text()).toContain('Alice')
    expect(wrapper.text()).toContain('Bob')
    expect(wrapper.findAll('tbody tr')).toHaveLength(2)
  })

  it('renders a target-type badge for each row', async () => {
    const wrapper = mountView()
    await flushPromises()

    const badges = wrapper.findAll('tbody tr td:nth-child(2)')
    expect(badges).toHaveLength(2)
    badges.forEach(b => expect(b.text()).toBe('Post'))
  })

  it('changing status filter re-fetches reports', async () => {
    const wrapper = mountView()
    await flushPromises()

    const select = wrapper.find('[aria-label="Filter by status"]')
    await select.setValue('Dismissed')
    await flushPromises()

    expect(mockAdminService.listReports).toHaveBeenLastCalledWith(1, 20, 'Dismissed', null)
  })

  it('changing type filter re-fetches reports', async () => {
    const wrapper = mountView()
    await flushPromises()

    const select = wrapper.find('[aria-label="Filter by type"]')
    await select.setValue('User')
    await flushPromises()

    expect(mockAdminService.listReports).toHaveBeenLastCalledWith(1, 20, 'Pending', 'User')
  })

  it('checking a row enables the bulk action toolbar', async () => {
    const wrapper = mountView()
    await flushPromises()

    expect(wrapper.text()).not.toContain('selected')

    const firstCheckbox = wrapper.find('[aria-label="Select report r1"]')
    await firstCheckbox.setValue(true)
    await flushPromises()

    expect(wrapper.text()).toContain('1 selected')
    expect(wrapper.find('[aria-label="Bulk actions"]').exists()).toBe(true)
  })

  it('"Select all" checkbox selects every row', async () => {
    const wrapper = mountView()
    await flushPromises()

    const selectAll = wrapper.find('[aria-label="Select all reports"]')
    await selectAll.setValue(true)
    await flushPromises()

    expect(wrapper.text()).toContain('2 selected')
  })

  it('bulk "Mark Reviewed" sends one PATCH per selected report', async () => {
    mockAdminService.bulkUpdateReportStatus.mockResolvedValue([])

    const wrapper = mountView()
    await flushPromises()

    await wrapper.find('[aria-label="Select report r1"]').setValue(true)
    await wrapper.find('[aria-label="Select report r2"]').setValue(true)

    const reviewBtn = wrapper.findAll('button').find(b => b.text() === 'Mark Reviewed')
    await reviewBtn!.trigger('click')
    await flushPromises()

    expect(mockAdminService.bulkUpdateReportStatus).toHaveBeenCalledWith(
      ['r1', 'r2'],
      'Reviewed',
    )
  })

  it('bulk "Dismiss" passes Dismissed status', async () => {
    mockAdminService.bulkUpdateReportStatus.mockResolvedValue([])

    const wrapper = mountView()
    await flushPromises()

    await wrapper.find('[aria-label="Select report r1"]').setValue(true)

    const dismissBtn = wrapper.findAll('button').find(b => b.text() === 'Dismiss')
    await dismissBtn!.trigger('click')
    await flushPromises()

    expect(mockAdminService.bulkUpdateReportStatus).toHaveBeenCalledWith(['r1'], 'Dismissed')
  })

  it('Clear button empties the selection', async () => {
    const wrapper = mountView()
    await flushPromises()

    await wrapper.find('[aria-label="Select report r1"]').setValue(true)
    expect(wrapper.text()).toContain('1 selected')

    const clearBtn = wrapper.findAll('button').find(b => b.text() === 'Clear')
    await clearBtn!.trigger('click')
    await flushPromises()

    expect(wrapper.text()).not.toContain('1 selected')
  })

  it('shows empty-state message when no reports come back', async () => {
    mockAdminService.listReports.mockResolvedValue({
      items: [], page: 1, pageSize: 20, hasMore: false,
    })

    const wrapper = mountView()
    await flushPromises()

    expect(wrapper.text()).toContain('No reports match the current filters.')
  })

  it('shows error banner when load fails', async () => {
    mockAdminService.listReports.mockRejectedValue({
      response: { data: { error: 'Server down' } },
    })

    const wrapper = mountView()
    await flushPromises()

    expect(wrapper.text()).toContain('Server down')
  })

  it('clicking a row opens the drawer with that report', async () => {
    const wrapper = mountView()
    await flushPromises()

    const firstRow = wrapper.findAll('tbody tr')[0]
    await firstRow.trigger('click')
    await flushPromises()

    // Pinia store updates drive the (stubbed) drawer state — we verify by
    // checking the store ref directly via the wrapper's vm tree.
    const { useAdminStore } = await import('@/stores/admin')
    const store = useAdminStore()
    expect(store.drawerOpen).toBe(true)
    expect(store.selectedReport?.id).toBe('r1')
  })

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

  it('Next button paginates forward when hasMore=true', async () => {
    mockAdminService.listReports
      .mockResolvedValueOnce({
        items: [samplePending('r1')],
        page: 1, pageSize: 20, hasMore: true,
      })
      .mockResolvedValueOnce({
        items: [samplePending('r3')],
        page: 2, pageSize: 20, hasMore: false,
      })

    const wrapper = mountView()
    await flushPromises()

    const nextBtn = wrapper.findAll('button').find(b => b.text() === 'Next')
    expect(nextBtn!.attributes('disabled')).toBeUndefined()
    await nextBtn!.trigger('click')
    await flushPromises()

    expect(mockAdminService.listReports).toHaveBeenLastCalledWith(2, 20, 'Pending', null)
  })
})
