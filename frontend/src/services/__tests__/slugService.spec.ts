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

// Import after mock so slugService picks up the mocked api
import { slugService } from '@/services/slugService'

describe('slugService', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  // ── resolve ──────────────────────────────────────────────────────────────

  it('resolve → GETs /api/slugs/resolve/:slug and returns dto on success', async () => {
    const dto = { type: 'user' as const, id: 'u-1' }
    mockApi.get.mockResolvedValue({ data: dto })

    const result = await slugService.resolve('alice')

    expect(mockApi.get).toHaveBeenCalledWith('/api/slugs/resolve/alice')
    expect(result).toEqual(dto)
  })

  it('resolve → returns null when server responds 404', async () => {
    const err = Object.assign(new Error('Not Found'), { response: { status: 404 } })
    mockApi.get.mockRejectedValue(err)

    const result = await slugService.resolve('unknown-slug')

    expect(result).toBeNull()
  })

  it('resolve → rethrows non-404 errors', async () => {
    const err = Object.assign(new Error('Server Error'), { response: { status: 500 } })
    mockApi.get.mockRejectedValue(err)

    await expect(slugService.resolve('bad-slug')).rejects.toThrow('Server Error')
  })

  it('resolve → rethrows network errors (no response property)', async () => {
    const err = new Error('network error')
    mockApi.get.mockRejectedValue(err)

    await expect(slugService.resolve('any-slug')).rejects.toThrow('network error')
  })

  it('resolve → URL-encodes special characters in slug', async () => {
    mockApi.get.mockResolvedValue({ data: { type: 'user', id: 'u-1' } })

    await slugService.resolve('hello world')

    expect(mockApi.get).toHaveBeenCalledWith('/api/slugs/resolve/hello%20world')
  })

  it('resolve → works for company type', async () => {
    mockApi.get.mockResolvedValue({ data: { type: 'company', id: 'c-1' } })

    const result = await slugService.resolve('acme')

    expect(result).toEqual({ type: 'company', id: 'c-1' })
  })

  // ── check ────────────────────────────────────────────────────────────────

  it('check → GETs /api/slugs/check with slug as query param', async () => {
    mockApi.get.mockResolvedValue({ data: { available: true } })

    const result = await slugService.check('newslug')

    expect(mockApi.get).toHaveBeenCalledWith('/api/slugs/check', { params: { slug: 'newslug' } })
    expect(result.available).toBe(true)
  })

  it('check → returns unavailable + reason when slug is taken', async () => {
    mockApi.get.mockResolvedValue({ data: { available: false, reason: 'taken' } })

    const result = await slugService.check('alice')

    expect(result.available).toBe(false)
    expect(result.reason).toBe('taken')
  })

  it('check → returns unavailable + reason when slug is reserved', async () => {
    mockApi.get.mockResolvedValue({ data: { available: false, reason: 'reserved' } })

    const result = await slugService.check('admin')

    expect(result.available).toBe(false)
    expect(result.reason).toBe('reserved')
  })

  it('check → propagates errors', async () => {
    mockApi.get.mockRejectedValue(new Error('server error'))

    await expect(slugService.check('foo')).rejects.toThrow('server error')
  })

  // ── setUserSlug ──────────────────────────────────────────────────────────

  it('setUserSlug → PUTs /api/users/me/slug with slug value', async () => {
    mockApi.put.mockResolvedValue({ data: {} })

    await slugService.setUserSlug('myslug')

    expect(mockApi.put).toHaveBeenCalledWith('/api/users/me/slug', { slug: 'myslug' })
  })

  it('setUserSlug → PUTs null to clear the slug', async () => {
    mockApi.put.mockResolvedValue({ data: {} })

    await slugService.setUserSlug(null)

    expect(mockApi.put).toHaveBeenCalledWith('/api/users/me/slug', { slug: null })
  })

  it('setUserSlug → propagates errors', async () => {
    mockApi.put.mockRejectedValue(new Error('conflict'))

    await expect(slugService.setUserSlug('taken')).rejects.toThrow('conflict')
  })

  // ── setCompanySlug ───────────────────────────────────────────────────────

  it('setCompanySlug → PUTs /api/company-pages/:id/slug with slug value', async () => {
    mockApi.put.mockResolvedValue({ data: {} })

    await slugService.setCompanySlug('page-1', 'acme')

    expect(mockApi.put).toHaveBeenCalledWith('/api/company-pages/page-1/slug', { slug: 'acme' })
  })

  it('setCompanySlug → PUTs null to clear the company slug', async () => {
    mockApi.put.mockResolvedValue({ data: {} })

    await slugService.setCompanySlug('page-2', null)

    expect(mockApi.put).toHaveBeenCalledWith('/api/company-pages/page-2/slug', { slug: null })
  })

  it('setCompanySlug → propagates errors', async () => {
    mockApi.put.mockRejectedValue(new Error('forbidden'))

    await expect(slugService.setCompanySlug('page-1', 'taken')).rejects.toThrow('forbidden')
  })
})
