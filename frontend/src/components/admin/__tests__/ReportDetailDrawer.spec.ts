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

import ReportDetailDrawer from '@/components/admin/ReportDetailDrawer.vue'
import { useAdminStore } from '@/stores/admin'

type Status = 'Pending' | 'Reviewed' | 'Dismissed'
type TargetType = 'Post' | 'User' | 'Comment'

function makeReport(opts: Partial<{
  id: string
  targetType: TargetType
  targetId: string
  status: Status
  notes: string
}> = {}) {
  return {
    id:           opts.id ?? 'r1',
    reporterId:   'rep-1',
    reporterName: 'Alice',
    targetType:   opts.targetType ?? ('Post' as TargetType),
    targetId:     opts.targetId ?? 'tgt-1',
    reason:       'Spam' as const,
    notes:        opts.notes ?? 'looks spammy',
    status:       opts.status ?? ('Pending' as Status),
    createdAt:    '2026-01-01T00:00:00Z',
  }
}

function mountDrawer() {
  const pinia = createPinia()
  setActivePinia(pinia)

  return mount(ReportDetailDrawer, {
    global: {
      plugins: [pinia],
      stubs:   { Teleport: true, Transition: true },
    },
    attachTo: document.body,
  })
}

describe('ReportDetailDrawer', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    mockAdminService.listReports.mockResolvedValue({
      items: [], page: 1, pageSize: 20, hasMore: false,
    })
  })

  it('renders nothing when drawer is closed', () => {
    const wrapper = mountDrawer()
    expect(wrapper.text()).not.toContain('Report detail')
  })

  it('opens with selected report and shows reason + reporter', async () => {
    const wrapper = mountDrawer()
    const store = useAdminStore()
    store.openDrawer(makeReport({ notes: 'bad post' }))
    await flushPromises()

    expect(wrapper.text()).toContain('Report detail')
    expect(wrapper.text()).toContain('Spam')
    expect(wrapper.text()).toContain('Alice')
    expect(wrapper.text()).toContain('bad post')
  })

  it('shows prior-report count from listReports', async () => {
    const r = makeReport({ id: 'current', targetId: 'tgt-shared' })
    // Mock listReports to return 3 reports on the same target (one of which
    // is `current`) → 2 priors.
    mockAdminService.listReports.mockResolvedValue({
      items: [
        { ...r, id: 'current' },
        { ...r, id: 'other-1' },
        { ...r, id: 'other-2' },
      ],
      page: 1, pageSize: 20, hasMore: false,
    })

    const wrapper = mountDrawer()
    const store = useAdminStore()
    store.openDrawer(r)
    await flushPromises()

    expect(wrapper.text()).toContain('2 other reports')
  })

  it('Dismiss action calls updateReportStatus(Dismissed)', async () => {
    mockAdminService.updateReportStatus.mockResolvedValue(
      { ...makeReport(), status: 'Dismissed' },
    )

    const wrapper = mountDrawer()
    const store = useAdminStore()
    store.openDrawer(makeReport())
    await flushPromises()

    const dismissBtn = wrapper.findAll('button').find(b => b.text() === 'Dismiss')
    await dismissBtn!.trigger('click')
    await flushPromises()

    expect(mockAdminService.updateReportStatus).toHaveBeenCalledWith('r1', 'Dismissed')
    expect(wrapper.text()).toContain('Report dismissed.')
  })

  it('Mark Reviewed action calls updateReportStatus(Reviewed)', async () => {
    mockAdminService.updateReportStatus.mockResolvedValue(
      { ...makeReport(), status: 'Reviewed' },
    )

    const wrapper = mountDrawer()
    const store = useAdminStore()
    store.openDrawer(makeReport())
    await flushPromises()

    const reviewedBtn = wrapper.findAll('button').find(b => b.text() === 'Mark Reviewed')
    await reviewedBtn!.trigger('click')
    await flushPromises()

    expect(mockAdminService.updateReportStatus).toHaveBeenCalledWith('r1', 'Reviewed')
  })

  it('Ban User is disabled when target type is Post', async () => {
    const wrapper = mountDrawer()
    const store = useAdminStore()
    store.openDrawer(makeReport({ targetType: 'Post' }))
    await flushPromises()

    const banBtn = wrapper.findAll('button').find(b => b.text() === 'Ban User')
    expect(banBtn!.attributes('disabled')).toBeDefined()
  })

  it('Ban User on User target calls banUser + updateReportStatus', async () => {
    mockAdminService.banUser.mockResolvedValue({
      id: 'tgt-user',
      email: 'u@t.com',
      displayName: 'Bad user',
      role: 'User',
      isActive: true,
      isBanned: true,
      bannedAt: '2026-01-01T00:00:00Z',
      createdAt: '2026-01-01T00:00:00Z',
    })
    mockAdminService.updateReportStatus.mockResolvedValue(
      { ...makeReport({ targetType: 'User' }), status: 'Reviewed' },
    )

    const wrapper = mountDrawer()
    const store = useAdminStore()
    store.openDrawer(makeReport({ targetType: 'User', targetId: 'tgt-user' }))
    await flushPromises()

    const banBtn = wrapper.findAll('button').find(b => b.text() === 'Ban User')
    expect(banBtn!.attributes('disabled')).toBeUndefined()
    await banBtn!.trigger('click')
    await flushPromises()

    expect(mockAdminService.banUser).toHaveBeenCalledWith('tgt-user', expect.any(String))
    expect(mockAdminService.updateReportStatus).toHaveBeenCalledWith('r1', 'Reviewed')
    expect(wrapper.text()).toContain('User banned and report marked reviewed.')
  })

  it('Remove action falls back to marking Reviewed and surfaces guidance', async () => {
    mockAdminService.updateReportStatus.mockResolvedValue(
      { ...makeReport(), status: 'Reviewed' },
    )

    const wrapper = mountDrawer()
    const store = useAdminStore()
    store.openDrawer(makeReport({ targetType: 'Post' }))
    await flushPromises()

    const removeBtn = wrapper.findAll('button').find(b => b.text() === 'Remove')
    await removeBtn!.trigger('click')
    await flushPromises()

    expect(mockAdminService.updateReportStatus).toHaveBeenCalledWith('r1', 'Reviewed')
    expect(wrapper.text()).toContain('Use the content removal flow')
  })

  it('shows error banner when an action fails', async () => {
    mockAdminService.updateReportStatus.mockRejectedValue({
      response: { data: { error: 'Server boom' } },
    })

    const wrapper = mountDrawer()
    const store = useAdminStore()
    store.openDrawer(makeReport())
    await flushPromises()

    const dismissBtn = wrapper.findAll('button').find(b => b.text() === 'Dismiss')
    await dismissBtn!.trigger('click')
    await flushPromises()

    expect(wrapper.text()).toContain('Server boom')
  })

  it('Close button closes the drawer', async () => {
    const wrapper = mountDrawer()
    const store = useAdminStore()
    store.openDrawer(makeReport())
    await flushPromises()

    expect(store.drawerOpen).toBe(true)

    const closeBtn = wrapper.find('[aria-label="Close report drawer"]')
    await closeBtn.trigger('click')

    expect(store.drawerOpen).toBe(false)
  })
})
