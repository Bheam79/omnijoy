import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount, flushPromises, RouterLinkStub } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'

// ── Router mock ───────────────────────────────────────────────────────────────

const mockPush  = vi.hoisted(() => vi.fn())
const mockRoute = vi.hoisted(() => ({ query: {} as Record<string, string | undefined> }))

vi.mock('vue-router', async (importOriginal) => {
  const actual = await importOriginal<typeof import('vue-router')>()
  return {
    ...actual,
    useRouter: () => ({ push: mockPush }),
    useRoute:  () => mockRoute,
  }
})

// ── Auth store mock ───────────────────────────────────────────────────────────

const mockAuth = vi.hoisted(() => ({
  loginPassword: vi.fn(),
  requestOtp:    vi.fn(),
  verifyOtp:     vi.fn(),
  loading:       false,
  error:         null as string | null,
  clearError:    vi.fn(),
}))

vi.mock('@/stores/auth', () => ({
  useAuthStore: () => mockAuth,
}))

// Import after mocks so the component picks them up
import LoginView from '@/views/auth/LoginView.vue'

// ── Helpers ───────────────────────────────────────────────────────────────────

function mountView() {
  return mount(LoginView, {
    global: {
      plugins: [createPinia()],
      stubs:   { RouterLink: RouterLinkStub },
    },
  })
}

// ── Tests ─────────────────────────────────────────────────────────────────────

describe('LoginView', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.clearAllMocks()
    mockAuth.loading = false
    mockAuth.error   = null
    mockRoute.query  = {}
  })

  // ── Default tab ───────────────────────────────────────────────────────────

  it('renders the password tab button by default', () => {
    const wrapper = mountView()
    expect(wrapper.find('[data-testid="password-tab"]').exists()).toBe(true)
  })

  it('shows the password form fields (email + password + submit) on mount', () => {
    const wrapper = mountView()
    expect(wrapper.find('[data-testid="pw-email-input"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="pw-password-input"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="pw-submit"]').exists()).toBe(true)
  })

  it('does NOT show the OTP email input by default', () => {
    const wrapper = mountView()
    expect(wrapper.find('[data-testid="otp-email-input"]').exists()).toBe(false)
  })

  // ── Tab switching ─────────────────────────────────────────────────────────

  it('switches to OTP tab when the otp-tab button is clicked', async () => {
    const wrapper = mountView()

    await wrapper.find('[data-testid="otp-tab"]').trigger('click')

    expect(wrapper.find('[data-testid="otp-email-input"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="send-code-button"]').exists()).toBe(true)
    // Password form should be hidden
    expect(wrapper.find('[data-testid="pw-email-input"]').exists()).toBe(false)
  })

  it('shows the "Send code" button on OTP tab (step 1)', async () => {
    const wrapper = mountView()
    await wrapper.find('[data-testid="otp-tab"]').trigger('click')

    expect(wrapper.find('[data-testid="send-code-button"]').text()).toContain('Send code')
  })

  // ── Password login — validation ────────────────────────────────────────────

  it('shows error banner when email is empty on password submit', async () => {
    const wrapper = mountView()

    // leave email and password blank; trigger the form directly
    await wrapper.find('form').trigger('submit')
    await flushPromises()

    expect(wrapper.find('[data-testid="login-error-banner"]').exists()).toBe(true)
  })

  it('shows error banner when password is empty on submit', async () => {
    const wrapper = mountView()

    await wrapper.find('[data-testid="pw-email-input"]').setValue('alice@example.com')
    // leave password blank
    await wrapper.find('form').trigger('submit')
    await flushPromises()

    expect(wrapper.find('[data-testid="login-error-banner"]').exists()).toBe(true)
  })

  it('does NOT call loginPassword when fields are empty', async () => {
    const wrapper = mountView()
    await wrapper.find('form').trigger('submit')
    await flushPromises()

    expect(mockAuth.loginPassword).not.toHaveBeenCalled()
  })

  // ── Password login — success ───────────────────────────────────────────────

  it('calls auth.loginPassword() with trimmed email on valid submit', async () => {
    mockAuth.loginPassword.mockResolvedValue(undefined)
    const wrapper = mountView()

    await wrapper.find('[data-testid="pw-email-input"]').setValue('  alice@example.com  ')
    await wrapper.find('[data-testid="pw-password-input"]').setValue('Secret123!')
    await wrapper.find('form').trigger('submit')
    await flushPromises()

    expect(mockAuth.loginPassword).toHaveBeenCalledWith('alice@example.com', 'Secret123!')
  })

  it('redirects to /wall on successful password login (no redirect param)', async () => {
    mockAuth.loginPassword.mockResolvedValue(undefined)
    const wrapper = mountView()

    await wrapper.find('[data-testid="pw-email-input"]').setValue('alice@example.com')
    await wrapper.find('[data-testid="pw-password-input"]').setValue('pw')
    await wrapper.find('form').trigger('submit')
    await flushPromises()

    expect(mockPush).toHaveBeenCalledWith('/wall')
  })

  it('redirects to the redirect query param on successful login', async () => {
    mockRoute.query = { redirect: '/profile/me' }
    mockAuth.loginPassword.mockResolvedValue(undefined)
    const wrapper = mountView()

    await wrapper.find('[data-testid="pw-email-input"]').setValue('alice@example.com')
    await wrapper.find('[data-testid="pw-password-input"]').setValue('pw')
    await wrapper.find('form').trigger('submit')
    await flushPromises()

    expect(mockPush).toHaveBeenCalledWith('/profile/me')
  })

  // ── Password login — error ─────────────────────────────────────────────────

  it('shows auth.error in the banner when loginPassword rejects', async () => {
    mockAuth.loginPassword.mockRejectedValue(new Error('test'))
    mockAuth.error = 'Invalid credentials.'
    const wrapper = mountView()

    await wrapper.find('[data-testid="pw-email-input"]').setValue('alice@example.com')
    await wrapper.find('[data-testid="pw-password-input"]').setValue('wrong')
    await wrapper.find('form').trigger('submit')
    await flushPromises()

    expect(wrapper.find('[data-testid="login-error-banner"]').text()).toContain(
      'Invalid credentials.',
    )
  })

  it('shows fallback error text when loginPassword rejects and auth.error is null', async () => {
    mockAuth.loginPassword.mockRejectedValue(new Error('network'))
    // auth.error stays null — the catch block falls back to 'Login failed.'
    const wrapper = mountView()

    await wrapper.find('[data-testid="pw-email-input"]').setValue('a@b.com')
    await wrapper.find('[data-testid="pw-password-input"]').setValue('pw')
    await wrapper.find('form').trigger('submit')
    await flushPromises()

    expect(wrapper.find('[data-testid="login-error-banner"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="login-error-banner"]').text()).toContain('Login failed')
  })

  // ── OTP — step 1 (request) ─────────────────────────────────────────────────

  it('shows error when Send code is clicked with empty email', async () => {
    const wrapper = mountView()
    await wrapper.find('[data-testid="otp-tab"]').trigger('click')

    // leave email blank
    await wrapper.find('[data-testid="send-code-button"]').trigger('click')
    await flushPromises()

    expect(wrapper.find('[data-testid="login-error-banner"]').exists()).toBe(true)
    expect(mockAuth.requestOtp).not.toHaveBeenCalled()
  })

  it('calls auth.requestOtp() and shows success banner on OTP request', async () => {
    mockAuth.requestOtp.mockResolvedValue(undefined)
    const wrapper = mountView()
    await wrapper.find('[data-testid="otp-tab"]').trigger('click')

    await wrapper.find('[data-testid="otp-email-input"]').setValue('alice@example.com')
    await wrapper.find('[data-testid="send-code-button"]').trigger('click')
    await flushPromises()

    expect(mockAuth.requestOtp).toHaveBeenCalledWith('alice@example.com')
    expect(wrapper.find('[data-testid="login-success-banner"]').exists()).toBe(true)
  })

  it('transitions to code input step after OTP is sent', async () => {
    mockAuth.requestOtp.mockResolvedValue(undefined)
    const wrapper = mountView()
    await wrapper.find('[data-testid="otp-tab"]').trigger('click')

    await wrapper.find('[data-testid="otp-email-input"]').setValue('alice@example.com')
    await wrapper.find('[data-testid="send-code-button"]').trigger('click')
    await flushPromises()

    // Step 2 elements
    expect(wrapper.find('[data-testid="otp-code-input"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="verify-button"]').exists()).toBe(true)
    // Step 1 elements gone
    expect(wrapper.find('[data-testid="send-code-button"]').exists()).toBe(false)
  })

  // ── OTP — step 2 (verify) ─────────────────────────────────────────────────

  it('shows error if OTP code length is not 6 chars', async () => {
    mockAuth.requestOtp.mockResolvedValue(undefined)
    const wrapper = mountView()
    await wrapper.find('[data-testid="otp-tab"]').trigger('click')
    await wrapper.find('[data-testid="otp-email-input"]').setValue('alice@example.com')
    await wrapper.find('[data-testid="send-code-button"]').trigger('click')
    await flushPromises()

    // Enter a 4-digit code (not 6)
    await wrapper.find('[data-testid="otp-code-input"]').setValue('1234')
    await wrapper.find('[data-testid="verify-button"]').trigger('click')
    await flushPromises()

    expect(wrapper.find('[data-testid="login-error-banner"]').exists()).toBe(true)
    expect(mockAuth.verifyOtp).not.toHaveBeenCalled()
  })

  it('shows error if OTP code is empty', async () => {
    mockAuth.requestOtp.mockResolvedValue(undefined)
    const wrapper = mountView()
    await wrapper.find('[data-testid="otp-tab"]').trigger('click')
    await wrapper.find('[data-testid="otp-email-input"]').setValue('alice@example.com')
    await wrapper.find('[data-testid="send-code-button"]').trigger('click')
    await flushPromises()

    // Don't enter a code
    await wrapper.find('[data-testid="verify-button"]').trigger('click')
    await flushPromises()

    expect(wrapper.find('[data-testid="login-error-banner"]').exists()).toBe(true)
    expect(mockAuth.verifyOtp).not.toHaveBeenCalled()
  })

  it('calls auth.verifyOtp() and redirects when a valid 6-digit code is submitted', async () => {
    mockAuth.requestOtp.mockResolvedValue(undefined)
    mockAuth.verifyOtp.mockResolvedValue(undefined)
    const wrapper = mountView()
    await wrapper.find('[data-testid="otp-tab"]').trigger('click')
    await wrapper.find('[data-testid="otp-email-input"]').setValue('alice@example.com')
    await wrapper.find('[data-testid="send-code-button"]').trigger('click')
    await flushPromises()

    await wrapper.find('[data-testid="otp-code-input"]').setValue('123456')
    await wrapper.find('[data-testid="verify-button"]').trigger('click')
    await flushPromises()

    expect(mockAuth.verifyOtp).toHaveBeenCalledWith('alice@example.com', '123456')
    expect(mockPush).toHaveBeenCalledWith('/wall')
  })

  it('shows error banner when verifyOtp rejects', async () => {
    mockAuth.requestOtp.mockResolvedValue(undefined)
    mockAuth.verifyOtp.mockRejectedValue(new Error('expired'))
    mockAuth.error = 'Code expired.'
    const wrapper = mountView()
    await wrapper.find('[data-testid="otp-tab"]').trigger('click')
    await wrapper.find('[data-testid="otp-email-input"]').setValue('alice@example.com')
    await wrapper.find('[data-testid="send-code-button"]').trigger('click')
    await flushPromises()

    await wrapper.find('[data-testid="otp-code-input"]').setValue('000000')
    await wrapper.find('[data-testid="verify-button"]').trigger('click')
    await flushPromises()

    expect(wrapper.find('[data-testid="login-error-banner"]').text()).toContain('Code expired.')
  })
})
