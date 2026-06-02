import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount, flushPromises, RouterLinkStub } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import type { PlaceSelection } from '@/types/places'

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
  register:   vi.fn(),
  loading:    false,
  error:      null as string | null,
  clearError: vi.fn(),
}))

vi.mock('@/stores/auth', () => ({
  useAuthStore: () => mockAuth,
}))

// ── PlacePicker stub ──────────────────────────────────────────────────────────
// Renders a minimal div so the component tree mounts without hitting the
// Places API. Tests manipulate the location ref directly via wrapper.vm.

const PlacePickerStub = {
  name:     'PlacePicker',
  props:    ['modelValue', 'mode', 'label', 'placeholder', 'required'],
  emits:    ['update:modelValue'],
  template: '<div data-testid="place-picker-stub" />',
}

import RegisterView from '@/views/auth/RegisterView.vue'

// ── Shared fixture ────────────────────────────────────────────────────────────

const fakeLocation: PlaceSelection = {
  placeId:          'oslo-id',
  displayName:      'Oslo, Norway',
  city:             'Oslo',
  country:          'Norway',
  countryCode:      'NO',
  latitude:         59.9139,
  longitude:        10.7522,
  formattedAddress: null,
}

// ── Mount helper ──────────────────────────────────────────────────────────────

function mountView() {
  return mount(RegisterView, {
    global: {
      plugins:    [createPinia()],
      stubs:      { RouterLink: RouterLinkStub, PlacePicker: PlacePickerStub },
    },
  })
}

/** Fill in the minimum valid form fields and optionally set location. */
async function fillValidForm(
  wrapper: ReturnType<typeof mountView>,
  opts: { withLocation?: boolean; withPassword?: boolean } = {},
) {
  const { withLocation = true, withPassword = true } = opts

  await wrapper.find('[data-testid="register-email-input"]').setValue('alice@example.com')
  await wrapper.find('[data-testid="register-display-name-input"]').setValue('Alice')

  if (withLocation) {
    // Directly set the reactive location ref on the component VM
    ;(wrapper.vm as unknown as { location: PlaceSelection | null }).location = fakeLocation
  }

  if (withPassword) {
    await wrapper.find('[data-testid="register-password-input"]').setValue('Password1!')
    await wrapper.find('[data-testid="register-confirm-password-input"]').setValue('Password1!')
  }
}

// ── Tests ─────────────────────────────────────────────────────────────────────

describe('RegisterView', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.clearAllMocks()
    mockAuth.loading = false
    mockAuth.error   = null
    mockRoute.query  = {}
  })

  // ── Rendering ─────────────────────────────────────────────────────────────

  it('renders the email input field', () => {
    const wrapper = mountView()
    expect(wrapper.find('[data-testid="register-email-input"]').exists()).toBe(true)
  })

  it('renders the displayName input field', () => {
    const wrapper = mountView()
    expect(wrapper.find('[data-testid="register-display-name-input"]').exists()).toBe(true)
  })

  it('renders the submit button', () => {
    const wrapper = mountView()
    expect(wrapper.find('[data-testid="register-submit"]').exists()).toBe(true)
  })

  it('renders the PlacePicker component for location selection', () => {
    const wrapper = mountView()
    expect(wrapper.findComponent({ name: 'PlacePicker' }).exists()).toBe(true)
  })

  it('renders password fields by default (authMethod=password)', () => {
    const wrapper = mountView()
    expect(wrapper.find('[data-testid="register-password-input"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="register-confirm-password-input"]').exists()).toBe(true)
  })

  it('hides password fields when OTP auth method is selected', async () => {
    const wrapper = mountView()
    await wrapper.find('[data-testid="auth-method-otp"]').trigger('click')

    expect(wrapper.find('[data-testid="register-password-input"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="register-confirm-password-input"]').exists()).toBe(false)
  })

  // ── Validation — missing fields ────────────────────────────────────────────

  it('shows error banner when email is missing on submit', async () => {
    const wrapper = mountView()
    // Leave email blank; fill everything else
    await wrapper.find('[data-testid="register-display-name-input"]').setValue('Alice')
    ;(wrapper.vm as unknown as { location: PlaceSelection | null }).location = fakeLocation
    await wrapper.find('[data-testid="register-password-input"]').setValue('Password1!')
    await wrapper.find('[data-testid="register-confirm-password-input"]').setValue('Password1!')

    await wrapper.find('form').trigger('submit')
    await flushPromises()

    expect(wrapper.find('[data-testid="register-error-banner"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="register-error-banner"]').text()).toContain('Email')
  })

  it('shows error banner when displayName is missing on submit', async () => {
    const wrapper = mountView()
    await wrapper.find('[data-testid="register-email-input"]').setValue('alice@example.com')
    // Leave displayName blank
    ;(wrapper.vm as unknown as { location: PlaceSelection | null }).location = fakeLocation
    await wrapper.find('[data-testid="register-password-input"]').setValue('Password1!')
    await wrapper.find('[data-testid="register-confirm-password-input"]').setValue('Password1!')

    await wrapper.find('form').trigger('submit')
    await flushPromises()

    expect(wrapper.find('[data-testid="register-error-banner"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="register-error-banner"]').text()).toContain('Display name')
  })

  it('shows error banner when location is not set', async () => {
    const wrapper = mountView()
    await fillValidForm(wrapper, { withLocation: false })

    await wrapper.find('form').trigger('submit')
    await flushPromises()

    expect(wrapper.find('[data-testid="register-error-banner"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="register-error-banner"]').text()).toContain('location')
  })

  // ── Validation — password rules ────────────────────────────────────────────

  it('shows error when passwords do not match', async () => {
    const wrapper = mountView()
    await wrapper.find('[data-testid="register-email-input"]').setValue('alice@example.com')
    await wrapper.find('[data-testid="register-display-name-input"]').setValue('Alice')
    ;(wrapper.vm as unknown as { location: PlaceSelection | null }).location = fakeLocation
    await wrapper.find('[data-testid="register-password-input"]').setValue('Password1!')
    await wrapper.find('[data-testid="register-confirm-password-input"]').setValue('Different1!')

    await wrapper.find('form').trigger('submit')
    await flushPromises()

    expect(wrapper.find('[data-testid="register-error-banner"]').text()).toContain(
      'do not match',
    )
  })

  it('shows error when password is fewer than 8 characters', async () => {
    const wrapper = mountView()
    await wrapper.find('[data-testid="register-email-input"]').setValue('alice@example.com')
    await wrapper.find('[data-testid="register-display-name-input"]').setValue('Alice')
    ;(wrapper.vm as unknown as { location: PlaceSelection | null }).location = fakeLocation
    await wrapper.find('[data-testid="register-password-input"]').setValue('short')
    await wrapper.find('[data-testid="register-confirm-password-input"]').setValue('short')

    await wrapper.find('form').trigger('submit')
    await flushPromises()

    expect(wrapper.find('[data-testid="register-error-banner"]').text()).toContain(
      'at least 8 characters',
    )
  })

  it('shows error when password field is empty', async () => {
    const wrapper = mountView()
    await wrapper.find('[data-testid="register-email-input"]').setValue('alice@example.com')
    await wrapper.find('[data-testid="register-display-name-input"]').setValue('Alice')
    ;(wrapper.vm as unknown as { location: PlaceSelection | null }).location = fakeLocation
    // Leave passwords blank

    await wrapper.find('form').trigger('submit')
    await flushPromises()

    expect(wrapper.find('[data-testid="register-error-banner"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="register-error-banner"]').text()).toContain('Password')
  })

  it('does not validate password fields for OTP auth method', async () => {
    mockAuth.register.mockResolvedValue(undefined)
    const wrapper = mountView()
    await wrapper.find('[data-testid="auth-method-otp"]').trigger('click')

    await wrapper.find('[data-testid="register-email-input"]').setValue('alice@example.com')
    await wrapper.find('[data-testid="register-display-name-input"]').setValue('Alice')
    ;(wrapper.vm as unknown as { location: PlaceSelection | null }).location = fakeLocation

    await wrapper.find('form').trigger('submit')
    await flushPromises()

    // Should call register without password errors
    expect(mockAuth.register).toHaveBeenCalled()
    expect(wrapper.find('[data-testid="register-error-banner"]').exists()).toBe(false)
  })

  // ── Successful submission ──────────────────────────────────────────────────

  it('calls auth.register() with the correct payload on valid submit', async () => {
    mockAuth.register.mockResolvedValue(undefined)
    const wrapper = mountView()
    await fillValidForm(wrapper)

    await wrapper.find('form').trigger('submit')
    await flushPromises()

    expect(mockAuth.register).toHaveBeenCalledWith(
      expect.objectContaining({
        email:           'alice@example.com',
        displayName:     'Alice',
        authMethod:      'password',
        password:        'Password1!',
        locationCity:    'Oslo',
        locationCountry: 'Norway',
      }),
    )
  })

  it('redirects to /wall after successful registration (no invite token)', async () => {
    mockAuth.register.mockResolvedValue(undefined)
    const wrapper = mountView()
    await fillValidForm(wrapper)

    await wrapper.find('form').trigger('submit')
    await flushPromises()

    expect(mockPush).toHaveBeenCalledWith('/wall')
  })

  it('redirects to /invite/:token when inviteToken query param is present', async () => {
    mockRoute.query = { invite: 'tok-xyz' }
    mockAuth.register.mockResolvedValue(undefined)
    const wrapper = mountView()
    await fillValidForm(wrapper)

    await wrapper.find('form').trigger('submit')
    await flushPromises()

    expect(mockPush).toHaveBeenCalledWith('/invite/tok-xyz')
  })

  it('does NOT call auth.register() when validation fails', async () => {
    const wrapper = mountView()
    // Submit with all fields blank
    await wrapper.find('form').trigger('submit')
    await flushPromises()

    expect(mockAuth.register).not.toHaveBeenCalled()
  })

  // ── Registration error ─────────────────────────────────────────────────────

  it('shows auth.error in the banner when register rejects', async () => {
    mockAuth.register.mockRejectedValue(new Error('test'))
    mockAuth.error = 'Email already in use.'
    const wrapper = mountView()
    await fillValidForm(wrapper)

    await wrapper.find('form').trigger('submit')
    await flushPromises()

    expect(wrapper.find('[data-testid="register-error-banner"]').text()).toContain(
      'Email already in use.',
    )
  })

  it('shows fallback error text when register rejects and auth.error is null', async () => {
    mockAuth.register.mockRejectedValue(new Error('network'))
    mockAuth.error = null
    const wrapper = mountView()
    await fillValidForm(wrapper)

    await wrapper.find('form').trigger('submit')
    await flushPromises()

    expect(wrapper.find('[data-testid="register-error-banner"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="register-error-banner"]').text()).toContain(
      'Registration failed',
    )
  })

  // ── Payload details ────────────────────────────────────────────────────────

  it('passes locationPlaceId from PlaceSelection to register payload', async () => {
    mockAuth.register.mockResolvedValue(undefined)
    const wrapper = mountView()
    await fillValidForm(wrapper)

    await wrapper.find('form').trigger('submit')
    await flushPromises()

    const payload = mockAuth.register.mock.calls[0][0]
    expect(payload.locationPlaceId).toBe('oslo-id')
  })

  it('passes null for locationPlaceId when placeId is "manual"', async () => {
    mockAuth.register.mockResolvedValue(undefined)
    const wrapper = mountView()

    const manualLocation: PlaceSelection = {
      ...fakeLocation,
      placeId: 'manual',
    }
    ;(wrapper.vm as unknown as { location: PlaceSelection | null }).location = manualLocation

    await wrapper.find('[data-testid="register-email-input"]').setValue('alice@example.com')
    await wrapper.find('[data-testid="register-display-name-input"]').setValue('Alice')
    await wrapper.find('[data-testid="register-password-input"]').setValue('Password1!')
    await wrapper.find('[data-testid="register-confirm-password-input"]').setValue('Password1!')

    await wrapper.find('form').trigger('submit')
    await flushPromises()

    const payload = mockAuth.register.mock.calls[0][0]
    expect(payload.locationPlaceId).toBeNull()
  })

  it('omits password from payload for OTP auth method', async () => {
    mockAuth.register.mockResolvedValue(undefined)
    const wrapper = mountView()
    await wrapper.find('[data-testid="auth-method-otp"]').trigger('click')

    await wrapper.find('[data-testid="register-email-input"]').setValue('alice@example.com')
    await wrapper.find('[data-testid="register-display-name-input"]').setValue('Alice')
    ;(wrapper.vm as unknown as { location: PlaceSelection | null }).location = fakeLocation

    await wrapper.find('form').trigger('submit')
    await flushPromises()

    const payload = mockAuth.register.mock.calls[0][0]
    expect(payload.authMethod).toBe('otp')
    expect(payload.password).toBeUndefined()
  })
})
