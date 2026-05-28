import api from './api'
import type { UserRole } from '@/types'
import type { ReportDto, ReportTargetType } from './reportService'

// ── DTOs (mirror backend) ─────────────────────────────────────────────────────

export type ReportStatus = 'Pending' | 'Reviewed' | 'Dismissed'

export interface ReportListResult {
  items: ReportDto[]
  page: number
  pageSize: number
  hasMore: boolean
}

export interface AdminUserDto {
  id: string
  email: string
  displayName: string
  role: UserRole
  isActive: boolean
  isBanned: boolean
  bannedAt?: string | null
  createdAt: string
}

export interface AdminUserListResult {
  items: AdminUserDto[]
  page: number
  pageSize: number
  hasMore: boolean
}

export interface ModerationLogDto {
  id: string
  actorId: string
  actorDisplayName: string
  /** "DismissReport" | "RemovePost" | "RemoveComment" | "BanUser" | "UnbanUser" | "ChangeRole" | "ReviewReport" */
  action: string
  targetType: string
  targetId: string
  notes?: string | null
  createdAt: string
}

export interface ModerationLogListResult {
  items: ModerationLogDto[]
  page: number
  pageSize: number
  hasMore: boolean
}

// ── Service ───────────────────────────────────────────────────────────────────

/**
 * Wraps every `/api/admin/*` endpoint exposed by the backend
 * (see `Omnijoy.Api.Controllers.AdminController` and the moderation
 * branch of `ReportsController`).
 *
 * All endpoints require a `Moderator` or `Admin` role on the caller; route-
 * level enforcement lives in the Vue router guard.
 */
export const adminService = {
  // ── Reports queue ────────────────────────────────────────────────────────

  /**
   * GET /api/admin/reports — paginated list.
   * Filters: status (Pending|Reviewed|Dismissed), type (Post|User|Comment).
   */
  async listReports(
    page = 1,
    pageSize = 20,
    status?: ReportStatus | null,
    type?: ReportTargetType | null,
  ): Promise<ReportListResult> {
    const params: Record<string, string | number> = { page, pageSize }
    if (status) params.status = status
    if (type) params.type = type
    const { data } = await api.get<ReportListResult>('/api/admin/reports', { params })
    return data
  },

  /**
   * PATCH /api/admin/reports/{id} — set report status. Writes a moderation
   * log entry under the calling actor.
   */
  async updateReportStatus(id: string, status: ReportStatus): Promise<ReportDto> {
    const { data } = await api.patch<ReportDto>(`/api/admin/reports/${id}`, { status })
    return data
  },

  /** Convenience helper — sequential bulk update. */
  async bulkUpdateReportStatus(ids: string[], status: ReportStatus): Promise<ReportDto[]> {
    const out: ReportDto[] = []
    for (const id of ids) {
      out.push(await this.updateReportStatus(id, status))
    }
    return out
  },

  // ── Users ────────────────────────────────────────────────────────────────

  /**
   * GET /api/admin/users — paginated list, Admin or Moderator.
   * Optional `q` matches case-insensitively against display name or email.
   */
  async listUsers(page = 1, pageSize = 20, q?: string | null): Promise<AdminUserListResult> {
    const params: Record<string, string | number> = { page, pageSize }
    if (q) params.q = q
    const { data } = await api.get<AdminUserListResult>('/api/admin/users', { params })
    return data
  },

  /** PATCH /api/admin/users/{id}/role — Admin only. */
  async changeUserRole(userId: string, role: UserRole): Promise<AdminUserDto> {
    const { data } = await api.patch<AdminUserDto>(
      `/api/admin/users/${userId}/role`,
      { role },
    )
    return data
  },

  /** POST /api/admin/users/{id}/ban — Admin or Moderator. */
  async banUser(userId: string, notes?: string | null): Promise<AdminUserDto> {
    const { data } = await api.post<AdminUserDto>(
      `/api/admin/users/${userId}/ban`,
      { notes: notes ?? null },
    )
    return data
  },

  /** POST /api/admin/users/{id}/unban — Admin or Moderator. */
  async unbanUser(userId: string, notes?: string | null): Promise<AdminUserDto> {
    const { data } = await api.post<AdminUserDto>(
      `/api/admin/users/${userId}/unban`,
      { notes: notes ?? null },
    )
    return data
  },

  // ── Audit log ────────────────────────────────────────────────────────────

  /** GET /api/admin/audit-log — Admin only. */
  async getAuditLog(
    page = 1,
    pageSize = 20,
    actorId?: string | null,
    action?: string | null,
  ): Promise<ModerationLogListResult> {
    const params: Record<string, string | number> = { page, pageSize }
    if (actorId) params.actorId = actorId
    if (action) params.action = action
    const { data } = await api.get<ModerationLogListResult>('/api/admin/audit-log', { params })
    return data
  },
}
