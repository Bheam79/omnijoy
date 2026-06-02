import { describe, it, expect, vi, beforeEach } from 'vitest'

// ── Mock axios.create so authAxios inside authService picks up our stub ──────
// authService calls axios.create() at module scope, so the mock must be ready
// before the module is first imported.  vi.hoisted ensures mockPost is defined
// before the vi.mock factory runs, which in turn runs before any import.
const mockPost = vi.hoisted(() => vi.fn())

vi.mock('axios', () => ({
  default: {
    create: vi.fn().mockReturnValue({ post: mockPost }),
  },
}))

// Import AFTER the mock so authService picks up the mocked axios.create
import { authService } from '@/services/authService'

// ── Helpers ───────────────────────────────────────────────────────────────────

/** Minimal raw user as the server might return it (all optional fields present). */
const makeRawUser = (overrides: Record<string, unknown> = {}) => ({
  id: 'u-1',
  email: 'alice@example.com',
  displayName: 'Alice',
  avatarUrl: '/media/avatar.jpg',
  coverUrl: '/media/cover.jpg',
  bio: 'Hello!',
  gender: 'Female',
  birthDate: '1990-01-01',
  showBirthDate: true,
  role: 'User',
  createdAt: '2026-01-01T00:00:00Z',
  locationName: 'Oslo, Norway',
  locationCity: 'Oslo',
  locationCountry: 'Norway',
  ...overrides,
})

const makeAuthResult = (userOverrides: Record<string, unknown> = {}) => ({
  accessToken: 'access-tok',
  refreshToken: 'refresh-tok',
  expiresIn: 3600,
  user: makeRawUser(userOverrides),
})

describe('authService', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  // ── register ─────────────────────────────────────────────────────────────

  it('register → POSTs /api/auth/register with full payload', async () => {
    mockPost.mockResolvedValue({ data: makeAuthResult() })

    await authService.register({
      email: 'alice@example.com',
      displayName: 'Alice',
      authMethod: 'password',
      password: 'Secret123!',
    })

    expect(mockPost).toHaveBeenCalledWith('/api/auth/register', {
      email: 'alice@example.com',
      displayName: 'Alice',
      authMethod: 'password',
      password: 'Secret123!',
    })
  })

  it('register → returns AuthResult with a normalised user', async () => {
    mockPost.mockResolvedValue({ data: makeAuthResult() })

    const result = await authService.register({
      email: 'alice@example.com',
      displayName: 'Alice',
      authMethod: 'password',
    })

    expect(result.accessToken).toBe('access-tok')
    expect(result.refreshToken).toBe('refresh-tok')
    expect(result.user.id).toBe('u-1')
    expect(result.user.email).toBe('alice@example.com')
    expect(result.user.displayName).toBe('Alice')
    expect(result.user.role).toBe('User')
  })

  it('register → passes optional location fields in the payload', async () => {
    mockPost.mockResolvedValue({ data: makeAuthResult() })

    await authService.register({
      email: 'alice@example.com',
      displayName: 'Alice',
      authMethod: 'otp',
      locationCity: 'Oslo',
      locationCountry: 'Norway',
      locationLatitude: 59.9139,
      locationLongitude: 10.7522,
    })

    const calledWith = mockPost.mock.calls[0][1]
    expect(calledWith.locationCity).toBe('Oslo')
    expect(calledWith.locationLatitude).toBe(59.9139)
  })

  // ── loginPassword ─────────────────────────────────────────────────────────

  it('loginPassword → POSTs /api/auth/login with email + password', async () => {
    mockPost.mockResolvedValue({ data: makeAuthResult() })

    await authService.loginPassword('alice@example.com', 'Secret123!')

    expect(mockPost).toHaveBeenCalledWith('/api/auth/login', {
      email: 'alice@example.com',
      password: 'Secret123!',
    })
  })

  it('loginPassword → returns AuthResult with user', async () => {
    mockPost.mockResolvedValue({ data: makeAuthResult() })

    const result = await authService.loginPassword('alice@example.com', 'pw')

    expect(result.user.displayName).toBe('Alice')
    expect(result.expiresIn).toBe(3600)
  })

  // ── requestOtp ────────────────────────────────────────────────────────────

  it('requestOtp → POSTs /api/auth/otp/request with email', async () => {
    mockPost.mockResolvedValue({ data: {} })

    await authService.requestOtp('alice@example.com')

    expect(mockPost).toHaveBeenCalledWith('/api/auth/otp/request', {
      email: 'alice@example.com',
    })
  })

  it('requestOtp → resolves without a return value', async () => {
    mockPost.mockResolvedValue({ data: {} })

    const result = await authService.requestOtp('alice@example.com')

    expect(result).toBeUndefined()
  })

  // ── verifyOtp ─────────────────────────────────────────────────────────────

  it('verifyOtp → POSTs /api/auth/otp/verify with email and code', async () => {
    mockPost.mockResolvedValue({ data: makeAuthResult() })

    await authService.verifyOtp('alice@example.com', '123456')

    expect(mockPost).toHaveBeenCalledWith('/api/auth/otp/verify', {
      email: 'alice@example.com',
      code: '123456',
    })
  })

  it('verifyOtp → returns AuthResult with user', async () => {
    mockPost.mockResolvedValue({ data: makeAuthResult() })

    const result = await authService.verifyOtp('alice@example.com', '000000')

    expect(result.accessToken).toBe('access-tok')
    expect(result.user.id).toBe('u-1')
  })

  // ── loginGoogle ───────────────────────────────────────────────────────────

  it('loginGoogle → POSTs /api/auth/oauth/google with { token: idToken }', async () => {
    mockPost.mockResolvedValue({ data: makeAuthResult() })

    await authService.loginGoogle('google-id-token-xyz')

    expect(mockPost).toHaveBeenCalledWith('/api/auth/oauth/google', {
      token: 'google-id-token-xyz',
    })
  })

  it('loginGoogle → returns AuthResult with user', async () => {
    mockPost.mockResolvedValue({ data: makeAuthResult() })

    const result = await authService.loginGoogle('tok')

    expect(result.user.displayName).toBe('Alice')
  })

  // ── loginFacebook ─────────────────────────────────────────────────────────

  it('loginFacebook → POSTs /api/auth/oauth/facebook with { token: accessToken }', async () => {
    mockPost.mockResolvedValue({ data: makeAuthResult() })

    await authService.loginFacebook('fb-access-token-abc')

    expect(mockPost).toHaveBeenCalledWith('/api/auth/oauth/facebook', {
      token: 'fb-access-token-abc',
    })
  })

  it('loginFacebook → returns AuthResult with user', async () => {
    mockPost.mockResolvedValue({ data: makeAuthResult() })

    const result = await authService.loginFacebook('tok')

    expect(result.user.email).toBe('alice@example.com')
  })

  // ── refresh ───────────────────────────────────────────────────────────────

  it('refresh → POSTs /api/auth/refresh with refreshToken', async () => {
    mockPost.mockResolvedValue({
      data: { accessToken: 'new-access', refreshToken: 'new-refresh', expiresIn: 900 },
    })

    await authService.refresh('old-refresh-tok')

    expect(mockPost).toHaveBeenCalledWith('/api/auth/refresh', {
      refreshToken: 'old-refresh-tok',
    })
  })

  it('refresh → returns AuthTokens (no user)', async () => {
    const tokens = { accessToken: 'new-access', refreshToken: 'new-refresh', expiresIn: 900 }
    mockPost.mockResolvedValue({ data: tokens })

    const result = await authService.refresh('tok')

    expect(result.accessToken).toBe('new-access')
    expect(result.refreshToken).toBe('new-refresh')
    expect(result.expiresIn).toBe(900)
    expect(result).not.toHaveProperty('user')
  })

  // ── logout ────────────────────────────────────────────────────────────────

  it('logout → POSTs /api/auth/logout with refreshToken', async () => {
    mockPost.mockResolvedValue({ data: undefined })

    await authService.logout('ref-tok')

    expect(mockPost).toHaveBeenCalledWith('/api/auth/logout', { refreshToken: 'ref-tok' })
  })

  it('logout → resolves without throwing when the POST rejects (best-effort)', async () => {
    mockPost.mockRejectedValue(new Error('network error'))

    // Should NOT throw — client-side cleanup must proceed regardless
    await expect(authService.logout('ref-tok')).resolves.toBeUndefined()
  })

  it('logout → resolves without throwing on 4xx response error', async () => {
    const err = Object.assign(new Error('401'), { response: { status: 401 } })
    mockPost.mockRejectedValue(err)

    await expect(authService.logout('expired-tok')).resolves.toBeUndefined()
  })

  // ── toUser normalisation ──────────────────────────────────────────────────

  it('toUser → converts empty string avatarUrl to undefined', async () => {
    mockPost.mockResolvedValue({ data: makeAuthResult({ avatarUrl: '' }) })

    const { user } = await authService.loginPassword('a@b.com', 'pw')

    expect(user.avatarUrl).toBeUndefined()
  })

  it('toUser → converts empty string coverUrl to undefined', async () => {
    mockPost.mockResolvedValue({ data: makeAuthResult({ coverUrl: '' }) })

    const { user } = await authService.loginPassword('a@b.com', 'pw')

    expect(user.coverUrl).toBeUndefined()
  })

  it('toUser → converts empty string bio to undefined', async () => {
    mockPost.mockResolvedValue({ data: makeAuthResult({ bio: '' }) })

    const { user } = await authService.loginPassword('a@b.com', 'pw')

    expect(user.bio).toBeUndefined()
  })

  it('toUser → defaults role to "User" when missing from server response', async () => {
    const raw = makeRawUser()
    delete (raw as Record<string, unknown>).role
    mockPost.mockResolvedValue({ data: { ...makeAuthResult(), user: raw } })

    const { user } = await authService.loginPassword('a@b.com', 'pw')

    expect(user.role).toBe('User')
  })

  it('toUser → defaults showBirthDate to false when missing', async () => {
    const raw = makeRawUser()
    delete (raw as Record<string, unknown>).showBirthDate
    mockPost.mockResolvedValue({ data: { ...makeAuthResult(), user: raw } })

    const { user } = await authService.loginPassword('a@b.com', 'pw')

    expect(user.showBirthDate).toBe(false)
  })

  it('toUser → defaults gender to "NotDisclosed" when missing', async () => {
    const raw = makeRawUser()
    delete (raw as Record<string, unknown>).gender
    mockPost.mockResolvedValue({ data: { ...makeAuthResult(), user: raw } })

    const { user } = await authService.loginPassword('a@b.com', 'pw')

    expect(user.gender).toBe('NotDisclosed')
  })

  it('toUser → preserves all provided fields correctly', async () => {
    mockPost.mockResolvedValue({
      data: makeAuthResult({
        avatarUrl: '/media/av.jpg',
        coverUrl: '/media/co.jpg',
        bio: 'My bio',
        gender: 'Male',
        birthDate: '1995-06-15',
        showBirthDate: true,
        role: 'Moderator',
        locationName: 'Berlin, Germany',
        locationCity: 'Berlin',
        locationCountry: 'Germany',
      }),
    })

    const { user } = await authService.loginPassword('a@b.com', 'pw')

    expect(user.avatarUrl).toBe('/media/av.jpg')
    expect(user.coverUrl).toBe('/media/co.jpg')
    expect(user.bio).toBe('My bio')
    expect(user.gender).toBe('Male')
    expect(user.birthDate).toBe('1995-06-15')
    expect(user.showBirthDate).toBe(true)
    expect(user.role).toBe('Moderator')
    expect(user.locationName).toBe('Berlin, Germany')
    expect(user.locationCity).toBe('Berlin')
    expect(user.locationCountry).toBe('Germany')
  })

  it('toUser → converts empty-string location fields to null', async () => {
    mockPost.mockResolvedValue({
      data: makeAuthResult({ locationName: '', locationCity: '', locationCountry: '' }),
    })

    const { user } = await authService.loginPassword('a@b.com', 'pw')

    expect(user.locationName).toBeNull()
    expect(user.locationCity).toBeNull()
    expect(user.locationCountry).toBeNull()
  })
})
