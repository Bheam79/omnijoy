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

// Import after mock so accountService picks up the mocked api
import { accountService } from '@/services/accountService'

describe('accountService', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  // ── changeEmail ──────────────────────────────────────────────────────────

  it('changeEmail → POSTs /api/account/change-email with payload', async () => {
    mockApi.post.mockResolvedValue({ data: undefined })

    await accountService.changeEmail({ newEmail: 'new@example.com', currentPassword: 'pass123' })

    expect(mockApi.post).toHaveBeenCalledWith('/api/account/change-email', {
      newEmail: 'new@example.com',
      currentPassword: 'pass123',
    })
  })

  it('changeEmail → resolves without a return value', async () => {
    mockApi.post.mockResolvedValue({ data: undefined })

    const result = await accountService.changeEmail({ newEmail: 'a@b.com', currentPassword: 'x' })

    expect(result).toBeUndefined()
  })

  // ── changePassword ───────────────────────────────────────────────────────

  it('changePassword → POSTs /api/account/change-password with payload', async () => {
    mockApi.post.mockResolvedValue({ data: undefined })

    await accountService.changePassword({
      currentPassword: 'old',
      newPassword: 'newP@ss',
      confirmNewPassword: 'newP@ss',
    })

    expect(mockApi.post).toHaveBeenCalledWith('/api/account/change-password', {
      currentPassword: 'old',
      newPassword: 'newP@ss',
      confirmNewPassword: 'newP@ss',
    })
  })

  // ── getNotificationPreferences ───────────────────────────────────────────

  it('getNotificationPreferences → GETs /api/account/notification-preferences', async () => {
    const prefs = {
      likesOnMyPosts: true,
      commentsOnMyPosts: true,
      postShares: false,
      friendRequests: true,
      newFollower: true,
      mentions: true,
      newPostsFromFriends: false,
      familyRelations: true,
      eventInvites: false,
      directMessages: true,
      liveStreams: false,
      companyPageInvites: true,
    }
    mockApi.get.mockResolvedValue({ data: prefs })

    const result = await accountService.getNotificationPreferences()

    expect(mockApi.get).toHaveBeenCalledWith('/api/account/notification-preferences')
    expect(result).toEqual(prefs)
  })

  // ── updateNotificationPreferences ────────────────────────────────────────

  it('updateNotificationPreferences → PUTs /api/account/notification-preferences', async () => {
    const prefs = {
      likesOnMyPosts: false,
      commentsOnMyPosts: false,
      postShares: false,
      friendRequests: false,
      newFollower: false,
      mentions: false,
      newPostsFromFriends: false,
      familyRelations: false,
      eventInvites: false,
      directMessages: false,
      liveStreams: false,
      companyPageInvites: false,
    }
    mockApi.put.mockResolvedValue({ data: prefs })

    const result = await accountService.updateNotificationPreferences(prefs)

    expect(mockApi.put).toHaveBeenCalledWith('/api/account/notification-preferences', prefs)
    expect(result).toEqual(prefs)
  })

  it('updateNotificationPreferences → returns the updated prefs from server', async () => {
    const serverPrefs = {
      likesOnMyPosts: true,
      commentsOnMyPosts: true,
      postShares: true,
      friendRequests: true,
      newFollower: true,
      mentions: true,
      newPostsFromFriends: true,
      familyRelations: true,
      eventInvites: true,
      directMessages: true,
      liveStreams: true,
      companyPageInvites: true,
    }
    mockApi.put.mockResolvedValue({ data: serverPrefs })

    const result = await accountService.updateNotificationPreferences(serverPrefs)

    expect(result.likesOnMyPosts).toBe(true)
  })

  // ── deactivate ───────────────────────────────────────────────────────────

  it('deactivate → POSTs /api/account/deactivate', async () => {
    mockApi.post.mockResolvedValue({ data: undefined })

    await accountService.deactivate()

    expect(mockApi.post).toHaveBeenCalledWith('/api/account/deactivate')
  })

  it('deactivate → resolves without a return value', async () => {
    mockApi.post.mockResolvedValue({ data: undefined })

    const result = await accountService.deactivate()

    expect(result).toBeUndefined()
  })

  // ── deleteAccount ────────────────────────────────────────────────────────

  it('deleteAccount → POSTs /api/account/delete with confirmEmail', async () => {
    mockApi.post.mockResolvedValue({ data: undefined })

    await accountService.deleteAccount('user@example.com')

    expect(mockApi.post).toHaveBeenCalledWith('/api/account/delete', {
      confirmEmail: 'user@example.com',
    })
  })

  it('deleteAccount → resolves without a return value', async () => {
    mockApi.post.mockResolvedValue({ data: undefined })

    const result = await accountService.deleteAccount('x@y.com')

    expect(result).toBeUndefined()
  })
})
