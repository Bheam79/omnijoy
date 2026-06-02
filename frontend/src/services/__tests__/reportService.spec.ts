import { describe, it, expect, vi, beforeEach } from 'vitest'

// ── Mock the shared axios instance ───────────────────────────────────────────
const mockApi = vi.hoisted(() => ({
  get:    vi.fn(),
  post:   vi.fn(),
  patch:  vi.fn(),
  put:    vi.fn(),
  delete: vi.fn(),
}))

vi.mock('@/services/api', () => ({
  default: mockApi,
  api:     mockApi,
}))

// Import after mock so reportService picks up the mocked api
import { reportService } from '@/services/reportService'
import type { SubmitReportPayload } from '@/services/reportService'

describe('reportService', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  // ── submitReport ─────────────────────────────────────────────────────────

  it('submitReport → POSTs /api/reports with full payload', async () => {
    const payload: SubmitReportPayload = {
      targetType: 'Post',
      targetId:   'post-1',
      reason:     'Spam',
      notes:      'Duplicate content',
    }
    const dto = {
      id:           'r-1',
      reporterId:   'u-1',
      reporterName: 'Alice',
      targetType:   'Post',
      targetId:     'post-1',
      reason:       'Spam',
      notes:        'Duplicate content',
      status:       'Pending',
      createdAt:    '2024-06-01T12:00:00Z',
    }
    mockApi.post.mockResolvedValue({ data: dto })

    const result = await reportService.submitReport(payload)

    expect(mockApi.post).toHaveBeenCalledWith('/api/reports', payload)
    expect(result.id).toBe('r-1')
    expect(result.status).toBe('Pending')
  })

  it('submitReport → works without optional notes field', async () => {
    const payload: SubmitReportPayload = {
      targetType: 'User',
      targetId:   'u-bad',
      reason:     'Harassment',
    }
    mockApi.post.mockResolvedValue({
      data: {
        id: 'r-2', reporterId: 'u-1', reporterName: 'Alice',
        targetType: 'User', targetId: 'u-bad', reason: 'Harassment',
        notes: null, status: 'Pending', createdAt: '2024-06-01T12:00:00Z',
      },
    })

    const result = await reportService.submitReport(payload)

    expect(mockApi.post).toHaveBeenCalledWith('/api/reports', payload)
    expect(result.notes).toBeNull()
  })

  it('submitReport → works for Comment target type', async () => {
    const payload: SubmitReportPayload = {
      targetType: 'Comment',
      targetId:   'c-1',
      reason:     'Violence',
    }
    mockApi.post.mockResolvedValue({
      data: {
        id: 'r-3', reporterId: 'u-1', reporterName: 'Alice',
        targetType: 'Comment', targetId: 'c-1', reason: 'Violence',
        notes: null, status: 'Pending', createdAt: '2024-06-01T12:00:00Z',
      },
    })

    await reportService.submitReport(payload)

    expect(mockApi.post).toHaveBeenCalledWith('/api/reports', payload)
  })

  it('submitReport → propagates errors', async () => {
    mockApi.post.mockRejectedValue(new Error('server error'))

    await expect(
      reportService.submitReport({ targetType: 'Post', targetId: 'p-1', reason: 'Spam' }),
    ).rejects.toThrow('server error')
  })
})
