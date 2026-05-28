import { describe, it, expect, vi, beforeEach } from 'vitest'
import { setActivePinia, createPinia } from 'pinia'
import { useAuthStore } from '@/stores/auth'

// ── Mock authService ──────────────────────────────────────────────────────────

const mockAuthService = vi.hoisted(() => ({
  register:      vi.fn(),
  loginPassword: vi.fn(),
  requestOtp:    vi.fn(),
  verifyOtp:     vi.fn(),
  loginGoogle:   vi.fn(),
  loginFacebook: vi.fn(),
  logout:        vi.fn(),
}))

vi.mock('@/services/authService', () => ({
  authService: mockAuthService,
}))

// ── Helpers ───────────────────────────────────────────────────────────────────

const fakeUser = {
  id:            'user-1',
  email:         'alice@example.com',
  displayName:   'Alice',
  gender:        'NotDisclosed' as const,
  showBirthDate: false,
  createdAt:     '2024-01-01T00:00:00Z',
}

const fakeAuthResult = {
  accessToken:  'fake-access-token',
  refreshToken: 'fake-refresh-token',
  expiresIn:    3600,
  user:         fakeUser,
}

// ── Tests ─────────────────────────────────────────────────────────────────────

describe('useAuthStore', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    vi.clearAllMocks()
  })

  // ── loginPassword ──────────────────────────────────────────────────────────

  it('loginPassword — success sets user, tokens, and loading state', async () => {
    mockAuthService.loginPassword.mockResolvedValue(fakeAuthResult)

    const store = useAuthStore()
    await store.loginPassword('alice@example.com', 'P@ssw0rd!')

    expect(store.user).toEqual(fakeUser)
    expect(store.accessToken).toBe('fake-access-token')
    expect(store.refreshToken).toBe('fake-refresh-token')
    expect(store.loading).toBe(false)
    expect(store.error).toBeNull()
    expect(localStorage.getItem('access_token')).toBe('fake-access-token')
  })

  it('loginPassword — failure sets error and re-throws', async () => {
    const err = new Error('Invalid credentials')
    mockAuthService.loginPassword.mockRejectedValue(err)

    const store = useAuthStore()
    await expect(store.loginPassword('bad@example.com', 'wrong')).rejects.toThrow()

    expect(store.user).toBeNull()
    expect(store.error).toBeTruthy()
    expect(store.loading).toBe(false)
  })

  // ── register ───────────────────────────────────────────────────────────────

  it('register — success populates store', async () => {
    mockAuthService.register.mockResolvedValue(fakeAuthResult)

    const store = useAuthStore()
    await store.register({
      email:       'alice@example.com',
      displayName: 'Alice',
      authMethod:  'password',
      password:    'P@ssw0rd!',
    })

    expect(store.user?.displayName).toBe('Alice')
    expect(store.isAuthenticated).toBe(true)
  })

  it('register — failure sets error', async () => {
    mockAuthService.register.mockRejectedValue(new Error('Email exists'))

    const store = useAuthStore()
    await expect(
      store.register({ email: 'dup@example.com', displayName: 'Dup', authMethod: 'password', password: 'x' })
    ).rejects.toThrow()

    expect(store.error).toBeTruthy()
  })

  // ── logout ─────────────────────────────────────────────────────────────────

  it('logout — clears user, tokens, and localStorage', async () => {
    mockAuthService.loginPassword.mockResolvedValue(fakeAuthResult)
    mockAuthService.logout.mockResolvedValue(undefined)

    const store = useAuthStore()
    await store.loginPassword('alice@example.com', 'P@ssw0rd!')
    expect(store.isAuthenticated).toBe(true)

    await store.logout()

    expect(store.user).toBeNull()
    expect(store.accessToken).toBeNull()
    expect(store.refreshToken).toBeNull()
    expect(localStorage.getItem('access_token')).toBeNull()
    expect(mockAuthService.logout).toHaveBeenCalledWith('fake-refresh-token')
  })

  // ── isAuthenticated ────────────────────────────────────────────────────────

  it('isAuthenticated — false when no token', () => {
    const store = useAuthStore()
    expect(store.isAuthenticated).toBe(false)
  })

  it('isAuthenticated — true when token and user are set', () => {
    const store = useAuthStore()
    store.setTokens('tok', 'ref')
    store.setUser(fakeUser)
    expect(store.isAuthenticated).toBe(true)
  })

  // ── OTP ────────────────────────────────────────────────────────────────────

  it('requestOtp — calls service and clears error', async () => {
    mockAuthService.requestOtp.mockResolvedValue(undefined)

    const store = useAuthStore()
    await store.requestOtp('alice@example.com')

    expect(mockAuthService.requestOtp).toHaveBeenCalledWith('alice@example.com')
    expect(store.error).toBeNull()
  })

  it('verifyOtp — success sets user and tokens', async () => {
    mockAuthService.verifyOtp.mockResolvedValue(fakeAuthResult)

    const store = useAuthStore()
    await store.verifyOtp('alice@example.com', '123456')

    expect(store.user).toEqual(fakeUser)
    expect(store.accessToken).toBe('fake-access-token')
  })

  // ── clearError ─────────────────────────────────────────────────────────────

  it('clearError — resets error to null', async () => {
    mockAuthService.loginPassword.mockRejectedValue(new Error('fail'))

    const store = useAuthStore()
    await store.loginPassword('bad@example.com', 'bad').catch(() => {})
    expect(store.error).not.toBeNull()

    store.clearError()
    expect(store.error).toBeNull()
  })
})
