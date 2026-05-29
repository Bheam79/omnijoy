import api from './api'

// ── Request payloads ──────────────────────────────────────────────────────────

export type ReportTargetType = 'Post' | 'User' | 'Comment'

export type ReportReason =
  | 'Spam'
  | 'Harassment'
  | 'Misinformation'
  | 'Nudity'
  | 'Violence'
  | 'Other'

export interface SubmitReportPayload {
  targetType: ReportTargetType
  targetId: string
  reason: ReportReason
  notes?: string
}

// ── Response types ────────────────────────────────────────────────────────────

export interface ReportDto {
  id: string
  reporterId: string
  reporterName: string
  targetType: ReportTargetType
  targetId: string
  reason: ReportReason
  notes?: string | null
  status: 'Pending' | 'Reviewed' | 'Dismissed'
  createdAt: string
}

// ── Service ───────────────────────────────────────────────────────────────────

export const reportService = {
  /**
   * Submit a report against a Post, User, or Comment.
   * POST /api/reports
   */
  async submitReport(payload: SubmitReportPayload): Promise<ReportDto> {
    const { data } = await api.post<ReportDto>('/api/reports', payload)
    return data
  },
}
