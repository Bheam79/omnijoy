import { describe, it, expect, vi, beforeEach } from 'vitest'
import { setActivePinia, createPinia } from 'pinia'

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

import { useAdminStore } from '@/stores/admin'

const sampleReport = (id: string) => ({
  id,
  reporterId:   `rep-${id}`,
  reporterName: 'Alice',
  targetType:   'Post' as const,
  targetId:     `tgt-${id}`,
  reason:       'Spam' as const,
  notes:        null,
  status:       'Pending' as const,
  createdAt:    '2026-01-01T00:00:00Z',
})

describe('useAdminStore', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.clearAllMocks()
  })

  it('loadReports populates state and clears selection', async () => {
    mockAdminService.listReports.mockResolvedValue({
      items: [sampleReport('r1'), sampleReport('r2')],
      page: 1, pageSize: 20, hasMore: true,
    })

    const store = useAdminStore()
    store.toggleSelected('stale-id') // pre-existing selection
    await store.loadReports(1)

    expect(store.reports).toHaveLength(2)
    expect(store.reportsPage).toBe(1)
    expect(store.reportsHasMore).toBe(true)
    expect(store.selectionCount).toBe(0)
  })

  it('loadReports passes status + type filters', async () => {
    mockAdminService.listReports.mockResolvedValue({
      items: [], page: 1, pageSize: 20, hasMore: false,
    })

    const store = useAdminStore()
    store.setStatusFilter('Reviewed')
    store.setTypeFilter('User')
    await store.loadReports(2)

    expect(mockAdminService.listReports).toHaveBeenCalledWith(2, 20, 'Reviewed', 'User')
  })

  it('toggleSelected and selectAll/toggleAll work', async () => {
    mockAdminService.listReports.mockResolvedValue({
      items: [sampleReport('a'), sampleReport('b')],
      page: 1, pageSize: 20, hasMore: false,
    })

    const store = useAdminStore()
    await store.loadReports(1)

    store.toggleSelected('a')
    expect(store.selectionCount).toBe(1)
    store.toggleSelected('a')
    expect(store.selectionCount).toBe(0)

    store.selectAll()
    expect(store.allSelected).toBe(true)
    expect(store.selectionCount).toBe(2)

    store.toggleAll()
    expect(store.selectionCount).toBe(0)
    store.toggleAll()
    expect(store.allSelected).toBe(true)
  })

  it('bulkUpdateStatus calls service with selection then reloads', async () => {
    mockAdminService.listReports.mockResolvedValue({
      items: [sampleReport('a')], page: 1, pageSize: 20, hasMore: false,
    })
    mockAdminService.bulkUpdateReportStatus.mockResolvedValue([])

    const store = useAdminStore()
    await store.loadReports(1)
    store.toggleSelected('a')
    await store.bulkUpdateStatus('Dismissed')

    expect(mockAdminService.bulkUpdateReportStatus)
      .toHaveBeenCalledWith(['a'], 'Dismissed')
    expect(mockAdminService.listReports).toHaveBeenCalledTimes(2)
  })

  it('bulkUpdateStatus is a no-op when nothing is selected', async () => {
    const store = useAdminStore()
    await store.bulkUpdateStatus('Reviewed')
    expect(mockAdminService.bulkUpdateReportStatus).not.toHaveBeenCalled()
  })

  it('updateReportStatus replaces the row in-place', async () => {
    mockAdminService.listReports.mockResolvedValue({
      items: [sampleReport('a')], page: 1, pageSize: 20, hasMore: false,
    })
    mockAdminService.updateReportStatus.mockResolvedValue({
      ...sampleReport('a'), status: 'Reviewed',
    })

    const store = useAdminStore()
    await store.loadReports(1)
    await store.updateReportStatus('a', 'Reviewed')

    expect(store.reports[0].status).toBe('Reviewed')
  })

  it('openDrawer / closeDrawer toggle the drawer state', () => {
    const store = useAdminStore()
    const r = sampleReport('a')

    store.openDrawer(r)
    expect(store.drawerOpen).toBe(true)
    expect(store.selectedReport?.id).toBe('a')

    store.closeDrawer()
    expect(store.drawerOpen).toBe(false)
  })

  it('banUser / unbanUser upsert into users list', async () => {
    const banned = {
      id: 'u1', email: 'u@t.com', displayName: 'U',
      role: 'User' as const, isActive: true, isBanned: true,
      bannedAt: '2026-01-01T00:00:00Z', createdAt: '2026-01-01T00:00:00Z',
    }
    const unbanned = { ...banned, isBanned: false, bannedAt: null }
    mockAdminService.banUser.mockResolvedValue(banned)
    mockAdminService.unbanUser.mockResolvedValue(unbanned)

    const store = useAdminStore()
    await store.banUser('u1', 'spam')
    expect(store.users[0].isBanned).toBe(true)

    await store.unbanUser('u1')
    expect(store.users[0].isBanned).toBe(false)
  })

  it('loadUsers populates the users list', async () => {
    mockAdminService.listUsers.mockResolvedValue({
      items: [
        { id: 'u1', email: 'a@t.com', displayName: 'Alice',
          role: 'User', isActive: true, isBanned: false,
          bannedAt: null, createdAt: '2026-01-01T00:00:00Z' },
      ],
      page: 1, pageSize: 20, hasMore: false,
    })

    const store = useAdminStore()
    store.setUsersQuery('alice')
    await store.loadUsers(1)

    expect(mockAdminService.listUsers).toHaveBeenCalledWith(1, 20, 'alice')
    expect(store.users).toHaveLength(1)
  })

  it('changeUserRole calls service and upserts result', async () => {
    mockAdminService.changeUserRole.mockResolvedValue({
      id: 'u1', email: 'a@t.com', displayName: 'Alice',
      role: 'Moderator', isActive: true, isBanned: false,
      bannedAt: null, createdAt: '2026-01-01T00:00:00Z',
    })

    const store = useAdminStore()
    await store.changeUserRole('u1', 'Moderator')

    expect(mockAdminService.changeUserRole).toHaveBeenCalledWith('u1', 'Moderator')
    expect(store.users[0].role).toBe('Moderator')
  })

  it('loadAuditLog populates state', async () => {
    mockAdminService.getAuditLog.mockResolvedValue({
      items: [
        { id: 'l1', actorId: 'a', actorDisplayName: 'A',
          action: 'BanUser', targetType: 'User', targetId: 'u1',
          notes: null, createdAt: '2026-01-01T00:00:00Z' },
      ],
      page: 1, pageSize: 20, hasMore: false,
    })

    const store = useAdminStore()
    store.auditActionFilter = 'BanUser'
    await store.loadAuditLog(1)

    expect(mockAdminService.getAuditLog).toHaveBeenCalledWith(1, 20, null, 'BanUser')
    expect(store.auditLog).toHaveLength(1)
  })

  it('reset clears every section of state', async () => {
    mockAdminService.listReports.mockResolvedValue({
      items: [sampleReport('a')], page: 1, pageSize: 20, hasMore: false,
    })

    const store = useAdminStore()
    await store.loadReports(1)
    store.toggleSelected('a')
    store.openDrawer(sampleReport('a'))

    store.reset()

    expect(store.reports).toHaveLength(0)
    expect(store.selectedIds.size).toBe(0)
    expect(store.drawerOpen).toBe(false)
    expect(store.selectedReport).toBe(null)
  })
})
