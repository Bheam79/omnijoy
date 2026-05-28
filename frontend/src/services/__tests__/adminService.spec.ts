import { describe, it, expect, vi, beforeEach } from 'vitest'

// ── Mock the shared axios instance ───────────────────────────────────────────
const mockApi = vi.hoisted(() => ({
  get:   vi.fn(),
  post:  vi.fn(),
  patch: vi.fn(),
  put:   vi.fn(),
  delete: vi.fn(),
}))

vi.mock('@/services/api', () => ({
  default: mockApi,
  api:     mockApi,
}))

// Import after mock so adminService picks up the mocked api
import { adminService } from '@/services/adminService'

describe('adminService', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  // ── listReports ──────────────────────────────────────────────────────────

  it('listReports → GETs /api/admin/reports with paging params', async () => {
    mockApi.get.mockResolvedValue({
      data: { items: [], page: 1, pageSize: 20, hasMore: false },
    })

    const result = await adminService.listReports(1, 20)

    expect(mockApi.get).toHaveBeenCalledWith('/api/admin/reports', {
      params: { page: 1, pageSize: 20 },
    })
    expect(result.items).toEqual([])
  })

  it('listReports → includes status and type filters when provided', async () => {
    mockApi.get.mockResolvedValue({
      data: { items: [], page: 1, pageSize: 20, hasMore: false },
    })

    await adminService.listReports(2, 50, 'Pending', 'Post')

    expect(mockApi.get).toHaveBeenCalledWith('/api/admin/reports', {
      params: { page: 2, pageSize: 50, status: 'Pending', type: 'Post' },
    })
  })

  it('listReports → omits null filters', async () => {
    mockApi.get.mockResolvedValue({
      data: { items: [], page: 1, pageSize: 20, hasMore: false },
    })

    await adminService.listReports(1, 20, null, null)

    expect(mockApi.get).toHaveBeenCalledWith('/api/admin/reports', {
      params: { page: 1, pageSize: 20 },
    })
  })

  // ── updateReportStatus ───────────────────────────────────────────────────

  it('updateReportStatus → PATCHes /api/admin/reports/:id with status', async () => {
    mockApi.patch.mockResolvedValue({ data: { id: 'r-1', status: 'Reviewed' } })

    const r = await adminService.updateReportStatus('r-1', 'Reviewed')

    expect(mockApi.patch).toHaveBeenCalledWith('/api/admin/reports/r-1', {
      status: 'Reviewed',
    })
    expect(r.status).toBe('Reviewed')
  })

  it('bulkUpdateReportStatus → PATCHes once per id', async () => {
    mockApi.patch.mockImplementation((url: string) =>
      Promise.resolve({ data: { id: url.split('/').pop(), status: 'Dismissed' } }),
    )

    const out = await adminService.bulkUpdateReportStatus(['a', 'b', 'c'], 'Dismissed')

    expect(mockApi.patch).toHaveBeenCalledTimes(3)
    expect(out).toHaveLength(3)
  })

  // ── User endpoints ───────────────────────────────────────────────────────

  it('changeUserRole → PATCHes /api/admin/users/:id/role', async () => {
    mockApi.patch.mockResolvedValue({ data: { id: 'u-1', role: 'Moderator' } })

    await adminService.changeUserRole('u-1', 'Moderator')

    expect(mockApi.patch).toHaveBeenCalledWith('/api/admin/users/u-1/role', {
      role: 'Moderator',
    })
  })

  it('banUser → POSTs /api/admin/users/:id/ban with notes', async () => {
    mockApi.post.mockResolvedValue({ data: { id: 'u-1', isBanned: true } })

    await adminService.banUser('u-1', 'spam')

    expect(mockApi.post).toHaveBeenCalledWith('/api/admin/users/u-1/ban', {
      notes: 'spam',
    })
  })

  it('banUser → sends null notes when not provided', async () => {
    mockApi.post.mockResolvedValue({ data: { id: 'u-1', isBanned: true } })

    await adminService.banUser('u-1')

    expect(mockApi.post).toHaveBeenCalledWith('/api/admin/users/u-1/ban', {
      notes: null,
    })
  })

  it('unbanUser → POSTs /api/admin/users/:id/unban', async () => {
    mockApi.post.mockResolvedValue({ data: { id: 'u-1', isBanned: false } })

    await adminService.unbanUser('u-1', 'appeal')

    expect(mockApi.post).toHaveBeenCalledWith('/api/admin/users/u-1/unban', {
      notes: 'appeal',
    })
  })

  it('listUsers → GETs /api/admin/users with q + paging', async () => {
    mockApi.get.mockResolvedValue({
      data: { items: [], page: 1, pageSize: 20, hasMore: false },
    })

    await adminService.listUsers(1, 20, 'alice')

    expect(mockApi.get).toHaveBeenCalledWith('/api/admin/users', {
      params: { page: 1, pageSize: 20, q: 'alice' },
    })
  })

  it('listUsers → omits q when null/empty', async () => {
    mockApi.get.mockResolvedValue({
      data: { items: [], page: 1, pageSize: 20, hasMore: false },
    })

    await adminService.listUsers(1, 20, null)

    expect(mockApi.get).toHaveBeenCalledWith('/api/admin/users', {
      params: { page: 1, pageSize: 20 },
    })
  })

  // ── Audit log ────────────────────────────────────────────────────────────

  it('getAuditLog → GETs /api/admin/audit-log with paging only', async () => {
    mockApi.get.mockResolvedValue({
      data: { items: [], page: 1, pageSize: 20, hasMore: false },
    })

    await adminService.getAuditLog(1, 20)

    expect(mockApi.get).toHaveBeenCalledWith('/api/admin/audit-log', {
      params: { page: 1, pageSize: 20 },
    })
  })

  it('getAuditLog → includes actorId and action filters', async () => {
    mockApi.get.mockResolvedValue({
      data: { items: [], page: 1, pageSize: 20, hasMore: false },
    })

    await adminService.getAuditLog(1, 20, 'actor-abc', 'BanUser')

    expect(mockApi.get).toHaveBeenCalledWith('/api/admin/audit-log', {
      params: { page: 1, pageSize: 20, actorId: 'actor-abc', action: 'BanUser' },
    })
  })
})
