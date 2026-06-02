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

// Import after mock so userService picks up the mocked api
import { userService } from '@/services/userService'

const makeUser = (overrides = {}) => ({
  id: 'u-1',
  displayName: 'Alice',
  avatarUrl: undefined,
  coverUrl: undefined,
  ...overrides,
})

const makeProfile = (overrides = {}) => ({
  id: 'u-1',
  displayName: 'Alice',
  gender: 'Female',
  showBirthDate: false,
  friendCount: 5,
  isOwnProfile: false,
  isFriend: false,
  createdAt: '2026-01-01T00:00:00Z',
  ...overrides,
})

const makePrivacy = (overrides = {}) => ({
  whoCanSeePosts: 'Everyone',
  whoCanSeeProfile: 'Everyone',
  whoCanSendMessages: 'Friends',
  whoCanSeeFriendList: 'Friends',
  whoCanSeeEvents: 'Everyone',
  whoCanTagInPosts: 'Friends',
  ...overrides,
})

describe('userService', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  // ── getProfile ───────────────────────────────────────────────────────────

  it('getProfile → GETs /api/users/:id', async () => {
    const profile = makeProfile()
    mockApi.get.mockResolvedValue({ data: profile })

    const result = await userService.getProfile('u-1')

    expect(mockApi.get).toHaveBeenCalledWith('/api/users/u-1')
    expect(result.id).toBe('u-1')
  })

  it('getProfile → returns the profile data from the server', async () => {
    const profile = makeProfile({ displayName: 'Bob', friendCount: 10 })
    mockApi.get.mockResolvedValue({ data: profile })

    const result = await userService.getProfile('u-2')

    expect(result.displayName).toBe('Bob')
    expect(result.friendCount).toBe(10)
  })

  // ── updateProfile ────────────────────────────────────────────────────────

  it('updateProfile → PUTs /api/users/me with payload', async () => {
    const user = makeUser({ displayName: 'Updated' })
    mockApi.put.mockResolvedValue({ data: user })

    const result = await userService.updateProfile({ displayName: 'Updated' })

    expect(mockApi.put).toHaveBeenCalledWith('/api/users/me', { displayName: 'Updated' })
    expect(result.displayName).toBe('Updated')
  })

  it('updateProfile → can update bio only', async () => {
    mockApi.put.mockResolvedValue({ data: makeUser({ bio: 'Hello!' }) })

    await userService.updateProfile({ bio: 'Hello!' })

    expect(mockApi.put).toHaveBeenCalledWith('/api/users/me', { bio: 'Hello!' })
  })

  // ── uploadAvatar ─────────────────────────────────────────────────────────

  it('uploadAvatar → POSTs /api/users/me/avatar with FormData', async () => {
    const user = makeUser({ avatarUrl: '/media/avatar.jpg' })
    mockApi.post.mockResolvedValue({ data: user })

    const file = new File(['avatar'], 'avatar.jpg', { type: 'image/jpeg' })
    const result = await userService.uploadAvatar(file)

    expect(mockApi.post).toHaveBeenCalledOnce()
    const [url, body] = mockApi.post.mock.calls[0]
    expect(url).toBe('/api/users/me/avatar')
    expect(body).toBeInstanceOf(FormData)
    expect(result.avatarUrl).toBe('/media/avatar.jpg')
  })

  it('uploadAvatar → appends file under "file" key in FormData', async () => {
    mockApi.post.mockResolvedValue({ data: makeUser() })

    const file = new File(['data'], 'pic.png', { type: 'image/png' })
    await userService.uploadAvatar(file)

    const form: FormData = mockApi.post.mock.calls[0][1]
    expect(form.get('file')).toBe(file)
  })

  it('uploadAvatar → sends multipart/form-data content-type header', async () => {
    mockApi.post.mockResolvedValue({ data: makeUser() })

    await userService.uploadAvatar(new File(['x'], 'x.jpg'))

    const options = mockApi.post.mock.calls[0][2]
    expect(options?.headers?.['Content-Type']).toBe('multipart/form-data')
  })

  // ── uploadCover ──────────────────────────────────────────────────────────

  it('uploadCover → POSTs /api/users/me/cover with FormData', async () => {
    const user = makeUser({ coverUrl: '/media/cover.jpg' })
    mockApi.post.mockResolvedValue({ data: user })

    const file = new File(['cover'], 'cover.jpg', { type: 'image/jpeg' })
    const result = await userService.uploadCover(file)

    expect(mockApi.post).toHaveBeenCalledOnce()
    const [url, body] = mockApi.post.mock.calls[0]
    expect(url).toBe('/api/users/me/cover')
    expect(body).toBeInstanceOf(FormData)
    expect(result.coverUrl).toBe('/media/cover.jpg')
  })

  it('uploadCover → appends file under "file" key in FormData', async () => {
    mockApi.post.mockResolvedValue({ data: makeUser() })

    const file = new File(['data'], 'cover.png', { type: 'image/png' })
    await userService.uploadCover(file)

    const form: FormData = mockApi.post.mock.calls[0][1]
    expect(form.get('file')).toBe(file)
  })

  it('uploadCover → sends multipart/form-data content-type header', async () => {
    mockApi.post.mockResolvedValue({ data: makeUser() })

    await userService.uploadCover(new File(['x'], 'x.jpg'))

    const options = mockApi.post.mock.calls[0][2]
    expect(options?.headers?.['Content-Type']).toBe('multipart/form-data')
  })

  // ── getPrivacySettings ───────────────────────────────────────────────────

  it('getPrivacySettings → GETs /api/users/me/privacy', async () => {
    const privacy = makePrivacy()
    mockApi.get.mockResolvedValue({ data: privacy })

    const result = await userService.getPrivacySettings()

    expect(mockApi.get).toHaveBeenCalledWith('/api/users/me/privacy')
    expect(result.whoCanSeePosts).toBe('Everyone')
  })

  // ── updatePrivacySettings ────────────────────────────────────────────────

  it('updatePrivacySettings → PUTs /api/users/me/privacy with payload', async () => {
    const updated = makePrivacy({ whoCanSeePosts: 'Friends' })
    mockApi.put.mockResolvedValue({ data: updated })

    const result = await userService.updatePrivacySettings({ whoCanSeePosts: 'Friends' })

    expect(mockApi.put).toHaveBeenCalledWith('/api/users/me/privacy', {
      whoCanSeePosts: 'Friends',
    })
    expect(result.whoCanSeePosts).toBe('Friends')
  })

  it('updatePrivacySettings → returns updated settings from server', async () => {
    const updated = makePrivacy({ whoCanSendMessages: 'OnlyMe' })
    mockApi.put.mockResolvedValue({ data: updated })

    const result = await userService.updatePrivacySettings({ whoCanSendMessages: 'OnlyMe' })

    expect(result.whoCanSendMessages).toBe('OnlyMe')
  })
})
