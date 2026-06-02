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

// Import after mock so companyPageService picks up the mocked api
import { companyPageService } from '@/services/companyPageService'

const makePage = (overrides = {}) => ({
  id: 'page-1',
  name: 'Acme Corp',
  createdBy: { id: 'u-1', displayName: 'Alice' },
  followerCount: 0,
  isFollowing: false,
  createdAt: '2026-01-01T00:00:00Z',
  ...overrides,
})

const makeAdmins = (overrides = {}) => ({
  admins: [{ userId: 'u-1', displayName: 'Alice', role: 'Owner' as const }],
  ...overrides,
})

describe('companyPageService', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  // ── createPage ───────────────────────────────────────────────────────────

  it('createPage → POSTs /api/company-pages with FormData', async () => {
    mockApi.post.mockResolvedValue({ data: makePage() })

    const result = await companyPageService.createPage({ name: 'Acme Corp' })

    expect(mockApi.post).toHaveBeenCalledOnce()
    const [url, body] = mockApi.post.mock.calls[0]
    expect(url).toBe('/api/company-pages')
    expect(body).toBeInstanceOf(FormData)
    expect(result.name).toBe('Acme Corp')
  })

  it('createPage → FormData contains name field', async () => {
    mockApi.post.mockResolvedValue({ data: makePage() })

    await companyPageService.createPage({ name: 'My Company' })

    const form: FormData = mockApi.post.mock.calls[0][1]
    expect(form.get('name')).toBe('My Company')
  })

  it('createPage → appends logo and cover files when provided', async () => {
    mockApi.post.mockResolvedValue({ data: makePage() })

    const logo  = new File(['logo'],  'logo.png',  { type: 'image/png' })
    const cover = new File(['cover'], 'cover.jpg', { type: 'image/jpeg' })

    await companyPageService.createPage({ name: 'Acme', logo, cover })

    const form: FormData = mockApi.post.mock.calls[0][1]
    expect(form.get('logo')).toBe(logo)
    expect(form.get('cover')).toBe(cover)
  })

  it('createPage → does not append logo or cover when not provided', async () => {
    mockApi.post.mockResolvedValue({ data: makePage() })

    await companyPageService.createPage({ name: 'Acme' })

    const form: FormData = mockApi.post.mock.calls[0][1]
    expect(form.get('logo')).toBeNull()
    expect(form.get('cover')).toBeNull()
  })

  it('createPage → sends multipart/form-data content-type header', async () => {
    mockApi.post.mockResolvedValue({ data: makePage() })

    await companyPageService.createPage({ name: 'Test' })

    const options = mockApi.post.mock.calls[0][2]
    expect(options?.headers?.['Content-Type']).toBe('multipart/form-data')
  })

  // ── getPages ─────────────────────────────────────────────────────────────

  it('getPages → GETs /api/company-pages with default params', async () => {
    const page = { items: [], page: 1, pageSize: 20, hasMore: false }
    mockApi.get.mockResolvedValue({ data: page })

    const result = await companyPageService.getPages()

    expect(mockApi.get).toHaveBeenCalledWith('/api/company-pages', {
      params: { mine: false, page: 1, pageSize: 20 },
    })
    expect(result.items).toEqual([])
  })

  it('getPages → passes mine=true when specified', async () => {
    mockApi.get.mockResolvedValue({ data: { items: [], page: 1, pageSize: 20, hasMore: false } })

    await companyPageService.getPages({ mine: true })

    const { params } = mockApi.get.mock.calls[0][1]
    expect(params.mine).toBe(true)
  })

  it('getPages → honours custom page and pageSize', async () => {
    mockApi.get.mockResolvedValue({ data: { items: [], page: 2, pageSize: 10, hasMore: false } })

    await companyPageService.getPages({ page: 2, pageSize: 10 })

    const { params } = mockApi.get.mock.calls[0][1]
    expect(params.page).toBe(2)
    expect(params.pageSize).toBe(10)
  })

  // ── getMyAdminPages ──────────────────────────────────────────────────────

  it('getMyAdminPages → GETs /api/company-pages/mine', async () => {
    mockApi.get.mockResolvedValue({ data: [makePage()] })

    const result = await companyPageService.getMyAdminPages()

    expect(mockApi.get).toHaveBeenCalledWith('/api/company-pages/mine')
    expect(result).toHaveLength(1)
  })

  it('getMyAdminPages → returns empty array when user has no admin pages', async () => {
    mockApi.get.mockResolvedValue({ data: [] })

    const result = await companyPageService.getMyAdminPages()

    expect(result).toEqual([])
  })

  // ── getPage ──────────────────────────────────────────────────────────────

  it('getPage → GETs /api/company-pages/:id', async () => {
    mockApi.get.mockResolvedValue({ data: makePage() })

    const result = await companyPageService.getPage('page-1')

    expect(mockApi.get).toHaveBeenCalledWith('/api/company-pages/page-1')
    expect(result.id).toBe('page-1')
  })

  // ── updatePage ───────────────────────────────────────────────────────────

  it('updatePage → PUTs /api/company-pages/:id with FormData', async () => {
    mockApi.put.mockResolvedValue({ data: makePage({ name: 'Updated Corp' }) })

    const result = await companyPageService.updatePage('page-1', { name: 'Updated Corp' })

    expect(mockApi.put).toHaveBeenCalledOnce()
    const [url, body] = mockApi.put.mock.calls[0]
    expect(url).toBe('/api/company-pages/page-1')
    expect(body).toBeInstanceOf(FormData)
    expect(result.name).toBe('Updated Corp')
  })

  it('updatePage → appends name when defined', async () => {
    mockApi.put.mockResolvedValue({ data: makePage() })

    await companyPageService.updatePage('page-1', { name: 'New Name' })

    const form: FormData = mockApi.put.mock.calls[0][1]
    expect(form.get('name')).toBe('New Name')
  })

  it('updatePage → appends description when defined', async () => {
    mockApi.put.mockResolvedValue({ data: makePage() })

    await companyPageService.updatePage('page-1', { description: 'About us' })

    const form: FormData = mockApi.put.mock.calls[0][1]
    expect(form.get('description')).toBe('About us')
  })

  it('updatePage → does not append name when not provided', async () => {
    mockApi.put.mockResolvedValue({ data: makePage() })

    await companyPageService.updatePage('page-1', { description: 'Only description' })

    const form: FormData = mockApi.put.mock.calls[0][1]
    expect(form.get('name')).toBeNull()
  })

  it('updatePage → appends logo file when provided', async () => {
    mockApi.put.mockResolvedValue({ data: makePage() })

    const logo = new File(['logo'], 'logo.png', { type: 'image/png' })
    await companyPageService.updatePage('page-1', { logo })

    const form: FormData = mockApi.put.mock.calls[0][1]
    expect(form.get('logo')).toBe(logo)
  })

  // ── getAdmins ────────────────────────────────────────────────────────────

  it('getAdmins → GETs /api/company-pages/:id/admins', async () => {
    mockApi.get.mockResolvedValue({ data: makeAdmins() })

    const result = await companyPageService.getAdmins('page-1')

    expect(mockApi.get).toHaveBeenCalledWith('/api/company-pages/page-1/admins')
    expect(result.admins).toHaveLength(1)
  })

  // ── addAdmin ─────────────────────────────────────────────────────────────

  it('addAdmin → POSTs /api/company-pages/:id/admins with payload', async () => {
    const admins = makeAdmins({ admins: [{ userId: 'u-2', displayName: 'Bob', role: 'Admin' }] })
    mockApi.post.mockResolvedValue({ data: admins })

    const result = await companyPageService.addAdmin('page-1', { userId: 'u-2', role: 'Admin' })

    expect(mockApi.post).toHaveBeenCalledWith('/api/company-pages/page-1/admins', {
      userId: 'u-2',
      role: 'Admin',
    })
    expect(result.admins[0].role).toBe('Admin')
  })

  it('addAdmin → works with Editor role', async () => {
    mockApi.post.mockResolvedValue({ data: makeAdmins() })

    await companyPageService.addAdmin('page-1', { userId: 'u-3', role: 'Editor' })

    expect(mockApi.post).toHaveBeenCalledWith('/api/company-pages/page-1/admins', {
      userId: 'u-3',
      role: 'Editor',
    })
  })

  // ── removeAdmin ──────────────────────────────────────────────────────────

  it('removeAdmin → DELETEs /api/company-pages/:pageId/admins/:userId', async () => {
    mockApi.delete.mockResolvedValue({ data: makeAdmins({ admins: [] }) })

    const result = await companyPageService.removeAdmin('page-1', 'u-2')

    expect(mockApi.delete).toHaveBeenCalledWith('/api/company-pages/page-1/admins/u-2')
    expect(result.admins).toHaveLength(0)
  })

  // ── follow ───────────────────────────────────────────────────────────────

  it('follow → POSTs /api/company-pages/:id/follow', async () => {
    const page = makePage({ isFollowing: true, followerCount: 1 })
    mockApi.post.mockResolvedValue({ data: page })

    const result = await companyPageService.follow('page-1')

    expect(mockApi.post).toHaveBeenCalledWith('/api/company-pages/page-1/follow')
    expect(result.isFollowing).toBe(true)
  })

  // ── unfollow ─────────────────────────────────────────────────────────────

  it('unfollow → DELETEs /api/company-pages/:id/follow', async () => {
    const page = makePage({ isFollowing: false, followerCount: 0 })
    mockApi.delete.mockResolvedValue({ data: page })

    const result = await companyPageService.unfollow('page-1')

    expect(mockApi.delete).toHaveBeenCalledWith('/api/company-pages/page-1/follow')
    expect(result.isFollowing).toBe(false)
  })
})
