import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { mount, flushPromises, RouterLinkStub } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'

// ── Router mock ───────────────────────────────────────────────────────────────

const mockPush = vi.hoisted(() => vi.fn())

vi.mock('vue-router', async (importOriginal) => {
  const actual = await importOriginal<typeof import('vue-router')>()
  return {
    ...actual,
    useRouter: () => ({ push: mockPush }),
  }
})

// ── authService mock ──────────────────────────────────────────────────────────

const mockRequestPasswordReset = vi.hoisted(() => vi.fn())
const mockConfirmPasswordReset  = vi.hoisted(() => vi.fn())

vi.mock('@/services/authService', () => ({
  authService: {
    requestPasswordReset: mockRequestPasswordReset,
    confirmPasswordReset:  mockConfirmPasswordReset,
  },
}))

// Import AFTER mocks
import ForgotPasswordView from '@/views/auth/ForgotPasswordView.vue'

// ── Helpers ───────────────────────────────────────────────────────────────────

function mountView() {
  return mount(ForgotPasswordView, {
    global: {
      plugins: [createPinia()],
      stubs:   { RouterLink: RouterLinkStub },
    },
  })
}

/** Advances the wrapper to step 2 by submitting a valid email. */
async function advanceToStep2(wrapper: ReturnType<typeof mountView>, email = 'alice@example.com') {
  mockRequestPasswordReset.mockResolvedValue(undefined)
  await wrapper.find('[data-testid="step1-email"]').setValue(email)
  await wrapper.find('[data-testid="step1-submit"]').trigger('click')
  await flushPromises()
}

// ── Tests ─────────────────────────────────────────────────────────────────────

describe('ForgotPasswordView', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.clearAllMocks()
  })

  afterEach(() => {
    vi.useRealTimers()
  })

  // ── Step 1 rendering ───────────────────────────────────────────────────────

  it('renders step 1 by default with email input and submit button', () => {
    const wrapper = mountView()
    expect(wrapper.find('[data-testid="step1-email"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="step1-submit"]').exists()).toBe(true)
    // Step 2 elements are absent
    expect(wrapper.find('[data-testid="step2-code"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="step2-submit"]').exists()).toBe(false)
  })

  it('shows an error banner when step 1 is submitted with an empty email', async () => {
    const wrapper = mountView()
    await wrapper.find('[data-testid="step1-submit"]').trigger('click')
    await flushPromises()

    expect(wrapper.find('[data-testid="error-banner"]').exists()).toBe(true)
    expect(mockRequestPasswordReset).not.toHaveBeenCalled()
  })

  it('calls authService.requestPasswordReset with the trimmed email and advances to step 2', async () => {
    const wrapper = mountView()
    await advanceToStep2(wrapper, '  alice@example.com  ')

    expect(mockRequestPasswordReset).toHaveBeenCalledWith('alice@example.com')
    // Step 2 is now visible
    expect(wrapper.find('[data-testid="step2-code"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="step2-submit"]').exists()).toBe(true)
    // Step 1 is hidden
    expect(wrapper.find('[data-testid="step1-submit"]').exists()).toBe(false)
  })

  it('shows an error banner and does NOT advance when step 1 throws', async () => {
    mockRequestPasswordReset.mockRejectedValue(new Error('network error'))
    const wrapper = mountView()

    await wrapper.find('[data-testid="step1-email"]').setValue('alice@example.com')
    await wrapper.find('[data-testid="step1-submit"]').trigger('click')
    await flushPromises()

    expect(wrapper.find('[data-testid="error-banner"]').exists()).toBe(true)
    // Still on step 1
    expect(wrapper.find('[data-testid="step1-submit"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="step2-code"]').exists()).toBe(false)
  })

  // ── Step 2: submit-button disabled state ───────────────────────────────────

  it('step 2 submit button is disabled when both passwords are empty', async () => {
    const wrapper = mountView()
    await advanceToStep2(wrapper)

    const btn = wrapper.find('[data-testid="step2-submit"]')
    expect(btn.attributes('disabled')).toBeDefined()
  })

  it('step 2 submit button is disabled when only newPassword is set', async () => {
    const wrapper = mountView()
    await advanceToStep2(wrapper)

    await wrapper.find('[data-testid="step2-new-password"]').setValue('MyNewPass!')
    // confirmPw still empty
    expect(wrapper.find('[data-testid="step2-submit"]').attributes('disabled')).toBeDefined()
  })

  it('step 2 submit button is disabled when passwords do not match', async () => {
    const wrapper = mountView()
    await advanceToStep2(wrapper)

    await wrapper.find('[data-testid="step2-new-password"]').setValue('MyNewPass1!')
    await wrapper.find('[data-testid="step2-confirm-password"]').setValue('Different1!')
    expect(wrapper.find('[data-testid="step2-submit"]').attributes('disabled')).toBeDefined()
  })

  it('step 2 submit button is enabled when both passwords match', async () => {
    const wrapper = mountView()
    await advanceToStep2(wrapper)

    await wrapper.find('[data-testid="step2-new-password"]').setValue('MyNewPass1!')
    await wrapper.find('[data-testid="step2-confirm-password"]').setValue('MyNewPass1!')
    expect(wrapper.find('[data-testid="step2-submit"]').attributes('disabled')).toBeUndefined()
  })

  // ── Step 2: client-side password validation ────────────────────────────────

  it('rejects password shorter than 8 chars BEFORE calling the API', async () => {
    const wrapper = mountView()
    await advanceToStep2(wrapper)

    await wrapper.find('[data-testid="step2-code"]').setValue('123456')
    // Set matching passwords that are too short
    await wrapper.find('[data-testid="step2-new-password"]').setValue('short')
    await wrapper.find('[data-testid="step2-confirm-password"]').setValue('short')

    // Temporarily enable the button by bypassing the binding — call the handler directly
    // by submitting with mismatched passwords first to get past the disabled guard,
    // OR we call the internal function. Since we can't easily bypass :disabled,
    // let's use passwords that PASS the length check to test the <8 path independently.
    // The correct approach: we set a password that is exactly 7 chars.
    await wrapper.find('[data-testid="step2-new-password"]').setValue('1234567')
    await wrapper.find('[data-testid="step2-confirm-password"]').setValue('1234567')

    // At this point the button is enabled (passwords match). Click it.
    await wrapper.find('[data-testid="step2-submit"]').trigger('click')
    await flushPromises()

    expect(mockConfirmPasswordReset).not.toHaveBeenCalled()
    expect(wrapper.find('[data-testid="error-banner"]').exists()).toBe(true)
  })

  // ── Step 2: success ────────────────────────────────────────────────────────

  it('shows success message and calls router.push after 1500ms on success', async () => {
    vi.useFakeTimers()
    mockConfirmPasswordReset.mockResolvedValue(undefined)

    const wrapper = mountView()
    await advanceToStep2(wrapper)

    await wrapper.find('[data-testid="step2-code"]').setValue('654321')
    await wrapper.find('[data-testid="step2-new-password"]').setValue('NewPassword1!')
    await wrapper.find('[data-testid="step2-confirm-password"]').setValue('NewPassword1!')
    await wrapper.find('[data-testid="step2-submit"]').trigger('click')
    await flushPromises()

    // Success message is shown
    expect(wrapper.find('[data-testid="success-message"]').exists()).toBe(true)
    // Router not called yet (waiting 1500ms)
    expect(mockPush).not.toHaveBeenCalled()

    // Advance the timer
    vi.advanceTimersByTime(1500)
    await flushPromises()

    expect(mockPush).toHaveBeenCalledWith({ name: 'login', query: { reset: '1' } })
  })

  it('calls confirmPasswordReset with the correct arguments', async () => {
    mockConfirmPasswordReset.mockResolvedValue(undefined)

    const wrapper = mountView()
    await advanceToStep2(wrapper, 'bob@example.com')

    await wrapper.find('[data-testid="step2-code"]').setValue(' 987654 ')
    await wrapper.find('[data-testid="step2-new-password"]').setValue('NewPassword1!')
    await wrapper.find('[data-testid="step2-confirm-password"]').setValue('NewPassword1!')
    await wrapper.find('[data-testid="step2-submit"]').trigger('click')
    await flushPromises()

    expect(mockConfirmPasswordReset).toHaveBeenCalledWith(
      'bob@example.com',
      '987654',
      'NewPassword1!',
    )
  })

  // ── Step 2: API error ──────────────────────────────────────────────────────

  it('surfaces response.data.error in the banner on 401 and does NOT redirect', async () => {
    const axiosError = {
      response: { status: 401, data: { error: 'Invalid or expired reset code.' } },
    }
    mockConfirmPasswordReset.mockRejectedValue(axiosError)

    const wrapper = mountView()
    await advanceToStep2(wrapper)

    await wrapper.find('[data-testid="step2-code"]').setValue('000000')
    await wrapper.find('[data-testid="step2-new-password"]').setValue('NewPassword1!')
    await wrapper.find('[data-testid="step2-confirm-password"]').setValue('NewPassword1!')
    await wrapper.find('[data-testid="step2-submit"]').trigger('click')
    await flushPromises()

    expect(wrapper.find('[data-testid="error-banner"]').text()).toContain(
      'Invalid or expired reset code.',
    )
    expect(mockPush).not.toHaveBeenCalled()
    // Form is still visible (user can retry)
    expect(wrapper.find('[data-testid="step2-submit"]').exists()).toBe(true)
  })

  it('surfaces response.data.error in the banner on 400', async () => {
    const axiosError = {
      response: { status: 400, data: { error: 'Password must be at least 8 characters.' } },
    }
    mockConfirmPasswordReset.mockRejectedValue(axiosError)

    const wrapper = mountView()
    await advanceToStep2(wrapper)

    await wrapper.find('[data-testid="step2-code"]').setValue('123456')
    await wrapper.find('[data-testid="step2-new-password"]').setValue('NewPassword1!')
    await wrapper.find('[data-testid="step2-confirm-password"]').setValue('NewPassword1!')
    await wrapper.find('[data-testid="step2-submit"]').trigger('click')
    await flushPromises()

    expect(wrapper.find('[data-testid="error-banner"]').text()).toContain(
      'Password must be at least 8 characters.',
    )
  })

  it('falls back to a generic error message when response.data.error is absent', async () => {
    mockConfirmPasswordReset.mockRejectedValue(new Error('network error'))

    const wrapper = mountView()
    await advanceToStep2(wrapper)

    await wrapper.find('[data-testid="step2-new-password"]').setValue('NewPassword1!')
    await wrapper.find('[data-testid="step2-confirm-password"]').setValue('NewPassword1!')
    await wrapper.find('[data-testid="step2-submit"]').trigger('click')
    await flushPromises()

    expect(wrapper.find('[data-testid="error-banner"]').exists()).toBe(true)
  })

  // ── Resend button ──────────────────────────────────────────────────────────

  it('resend button is enabled on step 2 initially', async () => {
    const wrapper = mountView()
    await advanceToStep2(wrapper)

    expect(wrapper.find('[data-testid="resend-button"]').attributes('disabled')).toBeUndefined()
  })

  it('resend button calls requestPasswordReset and is disabled immediately after click', async () => {
    vi.useFakeTimers()
    mockRequestPasswordReset.mockResolvedValue(undefined)

    const wrapper = mountView()
    await advanceToStep2(wrapper)

    // Clear prior call count from advanceToStep2
    vi.clearAllMocks()
    mockRequestPasswordReset.mockResolvedValue(undefined)

    const resendBtn = wrapper.find('[data-testid="resend-button"]')
    await resendBtn.trigger('click')
    // The cooldown is set synchronously at the start of resendCode()
    // — after nextTick the button should be disabled.
    await flushPromises()

    expect(mockRequestPasswordReset).toHaveBeenCalledOnce()
    expect(wrapper.find('[data-testid="resend-button"]').attributes('disabled')).toBeDefined()
  })
})
