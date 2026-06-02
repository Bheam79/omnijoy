import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount, flushPromises, RouterLinkStub } from '@vue/test-utils'
import PublicLandingModal from '@/components/shared/PublicLandingModal.vue'

// ── Composable mock ───────────────────────────────────────────────────────────

const mockSetLocation = vi.hoisted(() => vi.fn())
const mockDetectFromBrowser = vi.hoisted(() => vi.fn())

vi.mock('@/composables/usePublicLocation', () => ({
  usePublicLocation: () => ({
    publicLocation: { value: null },
    setLocation: mockSetLocation,
    detectFromBrowser: mockDetectFromBrowser,
  }),
}))

// ── Mount helper ──────────────────────────────────────────────────────────────

function mountModal() {
  return mount(PublicLandingModal, {
    global: {
      stubs: {
        // Render Teleport content inline so wrapper.find() / .text() work
        Teleport: { template: '<div><slot /></div>' },
        RouterLink: RouterLinkStub,
      },
    },
  })
}

// ── Tests ─────────────────────────────────────────────────────────────────────

describe('PublicLandingModal', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    mockDetectFromBrowser.mockResolvedValue('Stockholm')
  })

  // ── Initial rendering ─────────────────────────────────────────────────────

  it('renders the modal with the welcome heading', () => {
    const wrapper = mountModal()
    expect(wrapper.text()).toContain('Welcome to Omnijoy')
  })

  it('renders the "Log in" action in the default choose view', () => {
    const wrapper = mountModal()
    expect(wrapper.text()).toContain('Log in')
  })

  it('renders the "Create a free account" action in the default choose view', () => {
    const wrapper = mountModal()
    expect(wrapper.text()).toContain('Create a free account')
  })

  // ── Navigation links (RouterLink to= prop) ────────────────────────────────

  it('"Log in" link navigates to /login', () => {
    const wrapper = mountModal()
    const links = wrapper.findAllComponents(RouterLinkStub)
    const loginLink = links.find((l) => l.props('to') === '/login')
    expect(loginLink).toBeTruthy()
    expect(loginLink!.text()).toContain('Log in')
  })

  it('"Create a free account" link navigates to /register', () => {
    const wrapper = mountModal()
    const links = wrapper.findAllComponents(RouterLinkStub)
    const registerLink = links.find((l) => l.props('to') === '/register')
    expect(registerLink).toBeTruthy()
    expect(registerLink!.text()).toContain('Create a free account')
  })

  // ── Location picker view ──────────────────────────────────────────────────

  it('switches to location view when "Keep browsing as a guest" is clicked', async () => {
    const wrapper = mountModal()
    const guestBtn = wrapper
      .findAll('button')
      .find((b) => b.text().includes('Keep browsing as a guest'))
    await guestBtn!.trigger('click')
    expect(wrapper.text()).toContain('Where are you?')
  })

  it('returns to choose view when Back is clicked', async () => {
    const wrapper = mountModal()
    const guestBtn = wrapper
      .findAll('button')
      .find((b) => b.text().includes('Keep browsing as a guest'))
    await guestBtn!.trigger('click')

    const backBtn = wrapper.findAll('button').find((b) => b.text().includes('Back'))
    await backBtn!.trigger('click')

    // Login / register links should be visible again
    expect(wrapper.text()).toContain('Log in')
    expect(wrapper.text()).toContain('Create a free account')
  })

  // ── Dismiss — skip location ───────────────────────────────────────────────

  it('emits "dismiss" when "Skip — show all public events" is clicked', async () => {
    const wrapper = mountModal()
    const guestBtn = wrapper
      .findAll('button')
      .find((b) => b.text().includes('Keep browsing as a guest'))
    await guestBtn!.trigger('click')

    const skipBtn = wrapper
      .findAll('button')
      .find((b) => b.text().includes('Skip'))
    await skipBtn!.trigger('click')

    expect(wrapper.emitted('dismiss')).toBeTruthy()
    expect(mockSetLocation).toHaveBeenCalledWith(null)
  })

  // ── Dismiss — manual location save ───────────────────────────────────────

  it('emits "dismiss" and calls setLocation after saving a manual location', async () => {
    const wrapper = mountModal()
    const guestBtn = wrapper
      .findAll('button')
      .find((b) => b.text().includes('Keep browsing as a guest'))
    await guestBtn!.trigger('click')

    const input = wrapper.find('input[type="text"]')
    await input.setValue('Stockholm')

    const saveBtn = wrapper
      .findAll('button')
      .find((b) => b.text().includes('Save location'))
    await saveBtn!.trigger('click')

    expect(mockSetLocation).toHaveBeenCalledWith('Stockholm')
    expect(wrapper.emitted('dismiss')).toBeTruthy()
  })

  it('shows an error and does not dismiss when Save location is clicked with empty input', async () => {
    const wrapper = mountModal()
    const guestBtn = wrapper
      .findAll('button')
      .find((b) => b.text().includes('Keep browsing as a guest'))
    await guestBtn!.trigger('click')

    // Leave input empty
    const saveBtn = wrapper
      .findAll('button')
      .find((b) => b.text().includes('Save location'))
    await saveBtn!.trigger('click')

    expect(wrapper.text()).toContain('Please type a city name or skip this step.')
    expect(wrapper.emitted('dismiss')).toBeFalsy()
  })

  // ── Dismiss — auto-detection ──────────────────────────────────────────────

  it('emits "dismiss" after successful browser location detection', async () => {
    const wrapper = mountModal()
    const guestBtn = wrapper
      .findAll('button')
      .find((b) => b.text().includes('Keep browsing as a guest'))
    await guestBtn!.trigger('click')

    const detectBtn = wrapper
      .findAll('button')
      .find((b) => b.text().includes('Use my current location'))
    await detectBtn!.trigger('click')
    await flushPromises()

    expect(mockDetectFromBrowser).toHaveBeenCalled()
    expect(wrapper.emitted('dismiss')).toBeTruthy()
  })

  it('shows an error and does not dismiss when location detection fails', async () => {
    mockDetectFromBrowser.mockRejectedValue(new Error('Location permission was denied.'))

    const wrapper = mountModal()
    const guestBtn = wrapper
      .findAll('button')
      .find((b) => b.text().includes('Keep browsing as a guest'))
    await guestBtn!.trigger('click')

    const detectBtn = wrapper
      .findAll('button')
      .find((b) => b.text().includes('Use my current location'))
    await detectBtn!.trigger('click')
    await flushPromises()

    expect(wrapper.text()).toContain('Location permission was denied.')
    expect(wrapper.emitted('dismiss')).toBeFalsy()
  })

  // ── Detecting state ───────────────────────────────────────────────────────

  it('disables the detect button while detecting is in progress', async () => {
    // Keep the promise pending to observe mid-flight state
    let resolveDetect!: (city: string) => void
    mockDetectFromBrowser.mockImplementation(
      () => new Promise<string>((r) => { resolveDetect = r }),
    )

    const wrapper = mountModal()
    const guestBtn = wrapper
      .findAll('button')
      .find((b) => b.text().includes('Keep browsing as a guest'))
    await guestBtn!.trigger('click')

    const detectBtn = wrapper
      .findAll('button')
      .find((b) => b.text().includes('Use my current location'))
    await detectBtn!.trigger('click')
    await wrapper.vm.$nextTick()

    // After clicking, Vue re-renders: button text changes to "Detecting…"
    // and the :disabled binding is applied. Re-find to get the current state.
    const detectingBtn = wrapper
      .findAll('button')
      .find((b) => b.text().includes('Detecting'))
    expect(detectingBtn).toBeDefined()
    expect((detectingBtn!.element as HTMLButtonElement).disabled).toBe(true)

    // Resolve the promise to avoid dangling async work
    resolveDetect('Stockholm')
    await flushPromises()
  })
})
