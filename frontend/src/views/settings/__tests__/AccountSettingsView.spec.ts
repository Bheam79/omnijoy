import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { createRouter, createMemoryHistory } from 'vue-router'
import AccountSettingsView from '../AccountSettingsView.vue'

// ── Mocks ─────────────────────────────────────────────────────────────────────

const mockAccountService = vi.hoisted(() => ({
  changeEmail:    vi.fn(),
  changePassword: vi.fn(),
}))

vi.mock('@/services/accountService', () => ({
  accountService: mockAccountService,
}))

const mockSetUser = vi.fn()
const fakeUser = {
  id:            'user-1',
  email:         'alice@example.com',
  displayName:   'Alice',
  gender:        'NotDisclosed' as const,
  showBirthDate: false,
  createdAt:     '2024-01-01T00:00:00Z',
}

vi.mock('@/stores/auth', () => ({
  useAuthStore: () => ({
    user:    fakeUser,
    setUser: mockSetUser,
  }),
}))

// ── Router fixture ────────────────────────────────────────────────────────────

function makeRouter() {
  return createRouter({
    history: createMemoryHistory(),
    routes: [
      { path: '/settings/account', component: AccountSettingsView },
      { path: '/settings', component: { template: '<div>Settings</div>' } },
    ],
  })
}

async function mountView() {
  const router = makeRouter()
  await router.push('/settings/account')
  const wrapper = mount(AccountSettingsView, {
    global: {
      plugins: [createPinia(), router],
      stubs:   { RouterLink: true },
    },
  })
  await flushPromises()
  return wrapper
}

// ── Tests ─────────────────────────────────────────────────────────────────────

describe('AccountSettingsView', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.clearAllMocks()
  })

  // ── Rendering ──────────────────────────────────────────────────────────────

  it('renders the page heading', async () => {
    const wrapper = await mountView()
    expect(wrapper.text()).toContain('Account')
  })

  it('pre-fills email field with current user email', async () => {
    const wrapper = await mountView()
    const emailInput = wrapper.find('#new-email')
    expect((emailInput.element as HTMLInputElement).value).toBe('alice@example.com')
  })

  it('renders both forms', async () => {
    const wrapper = await mountView()
    expect(wrapper.find('[data-testid="email-form"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="password-form"]').exists()).toBe(true)
  })

  // ── Change email: validation ──────────────────────────────────────────────

  it('shows inline error for invalid email format', async () => {
    const wrapper = await mountView()
    const emailInput = wrapper.find('#new-email')
    await emailInput.setValue('notanemail')
    await flushPromises()
    expect(wrapper.text()).toContain('valid email')
  })

  it('does not show inline error for valid email', async () => {
    const wrapper = await mountView()
    const emailInput = wrapper.find('#new-email')
    await emailInput.setValue('valid@example.com')
    await flushPromises()
    expect(wrapper.text()).not.toContain('valid email')
  })

  it('shows error when submitting email form with empty password', async () => {
    const wrapper = await mountView()
    const form = wrapper.find('[data-testid="email-form"]')
    await form.trigger('submit')
    await flushPromises()
    expect(wrapper.text()).toContain('current password')
  })

  // ── Change email: success ─────────────────────────────────────────────────

  it('calls changeEmail with correct payload on success', async () => {
    mockAccountService.changeEmail.mockResolvedValue(undefined)
    const wrapper = await mountView()

    await wrapper.find('#new-email').setValue('new@example.com')
    await wrapper.find('#email-current-password').setValue('P@ssw0rd!')
    await wrapper.find('[data-testid="email-form"]').trigger('submit')
    await flushPromises()

    expect(mockAccountService.changeEmail).toHaveBeenCalledWith({
      newEmail:        'new@example.com',
      currentPassword: 'P@ssw0rd!',
    })
  })

  it('shows success message after email is changed', async () => {
    mockAccountService.changeEmail.mockResolvedValue(undefined)
    const wrapper = await mountView()

    await wrapper.find('#new-email').setValue('new@example.com')
    await wrapper.find('#email-current-password').setValue('P@ssw0rd!')
    await wrapper.find('[data-testid="email-form"]').trigger('submit')
    await flushPromises()

    expect(wrapper.text()).toContain('Email updated successfully')
  })

  it('calls setUser after email is changed', async () => {
    mockAccountService.changeEmail.mockResolvedValue(undefined)
    const wrapper = await mountView()

    await wrapper.find('#new-email').setValue('new@example.com')
    await wrapper.find('#email-current-password').setValue('P@ssw0rd!')
    await wrapper.find('[data-testid="email-form"]').trigger('submit')
    await flushPromises()

    expect(mockSetUser).toHaveBeenCalledWith(
      expect.objectContaining({ email: 'new@example.com' }),
    )
  })

  it('clears the password field after successful email change', async () => {
    mockAccountService.changeEmail.mockResolvedValue(undefined)
    const wrapper = await mountView()

    await wrapper.find('#new-email').setValue('new@example.com')
    await wrapper.find('#email-current-password').setValue('P@ssw0rd!')
    await wrapper.find('[data-testid="email-form"]').trigger('submit')
    await flushPromises()

    const pwField = wrapper.find('#email-current-password').element as HTMLInputElement
    expect(pwField.value).toBe('')
  })

  // ── Change email: error ───────────────────────────────────────────────────

  it('shows server error message when changeEmail fails', async () => {
    mockAccountService.changeEmail.mockRejectedValue({
      response: { data: { error: 'Current password is incorrect.' } },
    })
    const wrapper = await mountView()

    await wrapper.find('#new-email').setValue('new@example.com')
    await wrapper.find('#email-current-password').setValue('wrong')
    await wrapper.find('[data-testid="email-form"]').trigger('submit')
    await flushPromises()

    expect(wrapper.text()).toContain('Current password is incorrect.')
  })

  it('shows fallback error when changeEmail fails without server message', async () => {
    mockAccountService.changeEmail.mockRejectedValue(new Error('Network error'))
    const wrapper = await mountView()

    await wrapper.find('#new-email').setValue('new@example.com')
    await wrapper.find('#email-current-password').setValue('P@ssw0rd!')
    await wrapper.find('[data-testid="email-form"]').trigger('submit')
    await flushPromises()

    expect(wrapper.text()).toContain('Failed to update email')
  })

  // ── Change password: validation ───────────────────────────────────────────

  it('shows inline error when new password is too short', async () => {
    const wrapper = await mountView()
    await wrapper.find('#pw-new').setValue('short')
    await flushPromises()
    expect(wrapper.text()).toContain('at least 8 characters')
  })

  it('shows passwords-do-not-match error inline', async () => {
    const wrapper = await mountView()
    await wrapper.find('#pw-new').setValue('ValidP@ss1')
    await wrapper.find('#pw-confirm').setValue('DifferentP@ss1')
    await flushPromises()
    expect(wrapper.text()).toContain('do not match')
  })

  it('shows passwords-match confirmation inline', async () => {
    const wrapper = await mountView()
    await wrapper.find('#pw-new').setValue('ValidP@ss1')
    await wrapper.find('#pw-confirm').setValue('ValidP@ss1')
    await flushPromises()
    expect(wrapper.text()).toContain('Passwords match')
  })

  it('shows error when submitting password form with empty current password', async () => {
    const wrapper = await mountView()
    const form = wrapper.find('[data-testid="password-form"]')
    await form.trigger('submit')
    await flushPromises()
    expect(wrapper.text()).toContain('current password')
  })

  // ── Change password: success ──────────────────────────────────────────────

  it('calls changePassword with correct payload on success', async () => {
    mockAccountService.changePassword.mockResolvedValue(undefined)
    const wrapper = await mountView()

    await wrapper.find('#pw-current').setValue('OldP@ss1!')
    await wrapper.find('#pw-new').setValue('NewP@ss1!')
    await wrapper.find('#pw-confirm').setValue('NewP@ss1!')
    await wrapper.find('[data-testid="password-form"]').trigger('submit')
    await flushPromises()

    expect(mockAccountService.changePassword).toHaveBeenCalledWith({
      currentPassword:    'OldP@ss1!',
      newPassword:        'NewP@ss1!',
      confirmNewPassword: 'NewP@ss1!',
    })
  })

  it('shows success message after password is changed', async () => {
    mockAccountService.changePassword.mockResolvedValue(undefined)
    const wrapper = await mountView()

    await wrapper.find('#pw-current').setValue('OldP@ss1!')
    await wrapper.find('#pw-new').setValue('NewP@ss1!')
    await wrapper.find('#pw-confirm').setValue('NewP@ss1!')
    await wrapper.find('[data-testid="password-form"]').trigger('submit')
    await flushPromises()

    expect(wrapper.text()).toContain('Password updated successfully')
  })

  it('clears form fields after successful password change', async () => {
    mockAccountService.changePassword.mockResolvedValue(undefined)
    const wrapper = await mountView()

    await wrapper.find('#pw-current').setValue('OldP@ss1!')
    await wrapper.find('#pw-new').setValue('NewP@ss1!')
    await wrapper.find('#pw-confirm').setValue('NewP@ss1!')
    await wrapper.find('[data-testid="password-form"]').trigger('submit')
    await flushPromises()

    expect((wrapper.find('#pw-current').element as HTMLInputElement).value).toBe('')
    expect((wrapper.find('#pw-new').element as HTMLInputElement).value).toBe('')
    expect((wrapper.find('#pw-confirm').element as HTMLInputElement).value).toBe('')
  })

  // ── Change password: error ────────────────────────────────────────────────

  it('shows server error message when changePassword fails', async () => {
    mockAccountService.changePassword.mockRejectedValue({
      response: { data: { error: 'Current password is incorrect.' } },
    })
    const wrapper = await mountView()

    await wrapper.find('#pw-current').setValue('WrongP@ss1!')
    await wrapper.find('#pw-new').setValue('NewP@ss1!')
    await wrapper.find('#pw-confirm').setValue('NewP@ss1!')
    await wrapper.find('[data-testid="password-form"]').trigger('submit')
    await flushPromises()

    expect(wrapper.text()).toContain('Current password is incorrect.')
  })

  it('shows fallback error when changePassword fails without server message', async () => {
    mockAccountService.changePassword.mockRejectedValue(new Error('Network error'))
    const wrapper = await mountView()

    await wrapper.find('#pw-current').setValue('OldP@ss1!')
    await wrapper.find('#pw-new').setValue('NewP@ss1!')
    await wrapper.find('#pw-confirm').setValue('NewP@ss1!')
    await wrapper.find('[data-testid="password-form"]').trigger('submit')
    await flushPromises()

    expect(wrapper.text()).toContain('Failed to update password')
  })
})
