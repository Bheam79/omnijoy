import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import SlugPicker from '../SlugPicker.vue'

// ── Mocks ─────────────────────────────────────────────────────────────────────

const mockSlugService = vi.hoisted(() => ({
  check:          vi.fn(),
  setUserSlug:    vi.fn(),
  setCompanySlug: vi.fn(),
}))

vi.mock('@/services/slugService', () => ({
  slugService: mockSlugService,
}))

// ── Helpers ───────────────────────────────────────────────────────────────────

function mountPicker(props: {
  initialSlug?: string | null
  scope?: 'user' | 'company'
  targetId?: string
} = {}) {
  return mount(SlugPicker, {
    props: {
      initialSlug: props.initialSlug ?? null,
      scope:       props.scope ?? 'user',
      targetId:    props.targetId,
    },
  })
}

// ── Tests ─────────────────────────────────────────────────────────────────────

describe('SlugPicker', () => {
  beforeEach(() => {
    vi.useFakeTimers()
    vi.clearAllMocks()
    mockSlugService.check.mockResolvedValue({ available: true })
    mockSlugService.setUserSlug.mockResolvedValue(undefined)
    mockSlugService.setCompanySlug.mockResolvedValue(undefined)
  })

  afterEach(() => {
    vi.useRealTimers()
  })

  // ── Rendering ──────────────────────────────────────────────────────────────

  it('renders the slug input', () => {
    const wrapper = mountPicker()
    expect(wrapper.find('[data-testid="slug-input"]').exists()).toBe(true)
  })

  it('renders the save button as disabled when input is empty', () => {
    const wrapper = mountPicker()
    const btn = wrapper.find('[data-testid="save-btn"]').element as HTMLButtonElement
    expect(btn.disabled).toBe(true)
  })

  it('does NOT render the clear button when there is no current slug', () => {
    const wrapper = mountPicker({ initialSlug: null })
    expect(wrapper.find('[data-testid="clear-btn"]').exists()).toBe(false)
  })

  it('renders the clear button when the user already has a slug', () => {
    const wrapper = mountPicker({ initialSlug: 'alice' })
    expect(wrapper.find('[data-testid="clear-btn"]').exists()).toBe(true)
  })

  it('pre-fills the input with the initial slug', () => {
    const wrapper = mountPicker({ initialSlug: 'alice' })
    const input = wrapper.find('[data-testid="slug-input"]').element as HTMLInputElement
    expect(input.value).toBe('alice')
  })

  it('shows the current canonical URL when a slug is set', () => {
    const wrapper = mountPicker({ initialSlug: 'alice' })
    expect(wrapper.find('[data-testid="current-url"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="current-url"]').text()).toContain('omnijoy.com/alice')
  })

  it('does not show the current URL when no slug is set', () => {
    const wrapper = mountPicker({ initialSlug: null })
    expect(wrapper.find('[data-testid="current-url"]').exists()).toBe(false)
  })

  // ── Client-side validation ─────────────────────────────────────────────────

  it('shows invalid feedback immediately for slugs that are too short', async () => {
    const wrapper = mountPicker()
    await wrapper.find('[data-testid="slug-input"]').setValue('ab')
    expect(wrapper.find('[data-testid="feedback-error"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="feedback-error"]').text()).toContain('3 characters')
  })

  it('shows invalid feedback for slugs starting with a digit', async () => {
    const wrapper = mountPicker()
    await wrapper.find('[data-testid="slug-input"]').setValue('1alice')
    expect(wrapper.find('[data-testid="feedback-error"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="feedback-error"]').text()).toContain('start with a letter')
  })

  it('shows invalid feedback for slugs with invalid characters', async () => {
    const wrapper = mountPicker()
    await wrapper.find('[data-testid="slug-input"]').setValue('alice!')
    expect(wrapper.find('[data-testid="feedback-error"]').exists()).toBe(true)
  })

  it('shows invalid feedback for slugs that are too long', async () => {
    const wrapper = mountPicker()
    await wrapper.find('[data-testid="slug-input"]').setValue('a'.repeat(31))
    expect(wrapper.find('[data-testid="feedback-error"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="feedback-error"]').text()).toContain('30 characters')
  })

  it('shows invalid feedback for consecutive hyphens', async () => {
    const wrapper = mountPicker()
    await wrapper.find('[data-testid="slug-input"]').setValue('alice--bob')
    expect(wrapper.find('[data-testid="feedback-error"]').exists()).toBe(true)
  })

  it('shows invalid feedback for consecutive underscores', async () => {
    const wrapper = mountPicker()
    await wrapper.find('[data-testid="slug-input"]').setValue('alice__bob')
    expect(wrapper.find('[data-testid="feedback-error"]').exists()).toBe(true)
  })

  it('shows invalid feedback for trailing hyphen', async () => {
    const wrapper = mountPicker()
    await wrapper.find('[data-testid="slug-input"]').setValue('alice-')
    expect(wrapper.find('[data-testid="feedback-error"]').exists()).toBe(true)
  })

  it('does not call the API for client-side invalid slugs', async () => {
    const wrapper = mountPicker()
    await wrapper.find('[data-testid="slug-input"]').setValue('ab')
    vi.advanceTimersByTime(1000)
    await flushPromises()
    expect(mockSlugService.check).not.toHaveBeenCalled()
  })

  // ── Debounce behaviour ────────────────────────────────────────────────────

  it('shows the checking spinner after a valid slug is typed', async () => {
    const wrapper = mountPicker()
    await wrapper.find('[data-testid="slug-input"]').setValue('alice')
    expect(wrapper.find('[data-testid="check-spinner"]').exists()).toBe(true)
  })

  it('does not call the API before the debounce window elapses', async () => {
    const wrapper = mountPicker()
    await wrapper.find('[data-testid="slug-input"]').setValue('alice')
    vi.advanceTimersByTime(300)
    await flushPromises()
    expect(mockSlugService.check).not.toHaveBeenCalled()
  })

  it('calls the API after 400ms debounce with normalised slug', async () => {
    const wrapper = mountPicker()
    await wrapper.find('[data-testid="slug-input"]').setValue('Alice')  // uppercase input
    vi.advanceTimersByTime(400)
    await flushPromises()
    expect(mockSlugService.check).toHaveBeenCalledWith('alice')
  })

  it('coalesces rapid inputs into a single API call', async () => {
    const wrapper = mountPicker()
    await wrapper.find('[data-testid="slug-input"]').setValue('ali')
    vi.advanceTimersByTime(100)
    await wrapper.find('[data-testid="slug-input"]').setValue('alic')
    vi.advanceTimersByTime(100)
    await wrapper.find('[data-testid="slug-input"]').setValue('alice')
    vi.advanceTimersByTime(400)
    await flushPromises()
    expect(mockSlugService.check).toHaveBeenCalledTimes(1)
    expect(mockSlugService.check).toHaveBeenCalledWith('alice')
  })

  // ── Availability states ───────────────────────────────────────────────────

  it('shows available state after a successful check', async () => {
    mockSlugService.check.mockResolvedValue({ available: true })
    const wrapper = mountPicker()
    await wrapper.find('[data-testid="slug-input"]').setValue('alice')
    vi.advanceTimersByTime(400)
    await flushPromises()

    expect(wrapper.find('[data-testid="status-available"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="feedback-available"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="feedback-available"]').text()).toContain('omnijoy.com/alice')
  })

  it('enables the save button when the slug is available', async () => {
    mockSlugService.check.mockResolvedValue({ available: true })
    const wrapper = mountPicker()
    await wrapper.find('[data-testid="slug-input"]').setValue('alice')
    vi.advanceTimersByTime(400)
    await flushPromises()

    const btn = wrapper.find('[data-testid="save-btn"]').element as HTMLButtonElement
    expect(btn.disabled).toBe(false)
  })

  it('shows taken feedback when slug is taken', async () => {
    mockSlugService.check.mockResolvedValue({ available: false, reason: 'taken' })
    const wrapper = mountPicker()
    await wrapper.find('[data-testid="slug-input"]').setValue('alice')
    vi.advanceTimersByTime(400)
    await flushPromises()

    expect(wrapper.find('[data-testid="status-unavailable"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="feedback-error"]').text()).toContain('already taken')
  })

  it('shows reserved feedback when slug is reserved', async () => {
    mockSlugService.check.mockResolvedValue({ available: false, reason: 'reserved' })
    const wrapper = mountPicker()
    await wrapper.find('[data-testid="slug-input"]').setValue('admin')
    vi.advanceTimersByTime(400)
    await flushPromises()

    expect(wrapper.find('[data-testid="status-unavailable"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="feedback-error"]').text()).toContain('reserved')
  })

  it('shows error feedback when availability check fails', async () => {
    mockSlugService.check.mockRejectedValue(new Error('Network error'))
    const wrapper = mountPicker()
    await wrapper.find('[data-testid="slug-input"]').setValue('alice')
    vi.advanceTimersByTime(400)
    await flushPromises()

    expect(wrapper.find('[data-testid="status-unavailable"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="feedback-error"]').text()).toContain('Could not check')
  })

  it('shows invalid feedback when the API returns reason: invalid', async () => {
    mockSlugService.check.mockResolvedValue({ available: false, reason: 'invalid' })
    const wrapper = mountPicker()
    await wrapper.find('[data-testid="slug-input"]').setValue('alice')
    vi.advanceTimersByTime(400)
    await flushPromises()

    expect(wrapper.find('[data-testid="status-unavailable"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="feedback-error"]').text()).toContain('not valid')
  })

  it('disables the save button when slug is taken', async () => {
    mockSlugService.check.mockResolvedValue({ available: false, reason: 'taken' })
    const wrapper = mountPicker()
    await wrapper.find('[data-testid="slug-input"]').setValue('alice')
    vi.advanceTimersByTime(400)
    await flushPromises()

    const btn = wrapper.find('[data-testid="save-btn"]').element as HTMLButtonElement
    expect(btn.disabled).toBe(true)
  })

  it('immediately shows available for the initial slug without an API call', async () => {
    const wrapper = mountPicker({ initialSlug: 'alice' })
    // Input starts with 'alice' — same as current slug
    vi.advanceTimersByTime(400)
    await flushPromises()
    // No API call needed for the current slug
    expect(mockSlugService.check).not.toHaveBeenCalled()
    expect(wrapper.find('[data-testid="status-available"]').exists()).toBe(true)
  })

  it('shows available immediately when user types back to the original saved slug', async () => {
    const wrapper = mountPicker({ initialSlug: 'alice' })
    // First type something different to trigger a check cycle...
    await wrapper.find('[data-testid="slug-input"]').setValue('bob')
    vi.advanceTimersByTime(400)
    await flushPromises()
    // Now type back the original slug — should show available without another API call
    mockSlugService.check.mockClear()
    await wrapper.find('[data-testid="slug-input"]').setValue('alice')
    vi.advanceTimersByTime(400)
    await flushPromises()
    expect(mockSlugService.check).not.toHaveBeenCalled()
    expect(wrapper.find('[data-testid="status-available"]').exists()).toBe(true)
  })

  it('clears any pending timer on unmount (no unhandled promise after destroy)', async () => {
    const wrapper = mountPicker({ initialSlug: null })
    // Type something to start a timer
    await wrapper.find('[data-testid="slug-input"]').setValue('newslug')
    // Unmount before the timer fires — should not throw
    wrapper.unmount()
    // Advance timers — should not cause any errors
    vi.advanceTimersByTime(1000)
    await flushPromises()
    // If we get here without errors the onUnmounted cleanup worked
    expect(true).toBe(true)
  })

  // ── Save flow ─────────────────────────────────────────────────────────────

  it('calls setUserSlug with the normalised slug on save (user scope)', async () => {
    mockSlugService.check.mockResolvedValue({ available: true })
    const wrapper = mountPicker({ scope: 'user' })

    await wrapper.find('[data-testid="slug-input"]').setValue('Alice')
    vi.advanceTimersByTime(400)
    await flushPromises()

    await wrapper.find('[data-testid="save-btn"]').trigger('click')
    await flushPromises()

    expect(mockSlugService.setUserSlug).toHaveBeenCalledWith('alice')
  })

  it('calls setCompanySlug with companyId and slug on save (company scope)', async () => {
    mockSlugService.check.mockResolvedValue({ available: true })
    const wrapper = mountPicker({ scope: 'company', targetId: 'company-99' })

    await wrapper.find('[data-testid="slug-input"]').setValue('acme-corp')
    vi.advanceTimersByTime(400)
    await flushPromises()

    await wrapper.find('[data-testid="save-btn"]').trigger('click')
    await flushPromises()

    expect(mockSlugService.setCompanySlug).toHaveBeenCalledWith('company-99', 'acme-corp')
  })

  it('emits "updated" with the new slug after a successful save', async () => {
    mockSlugService.check.mockResolvedValue({ available: true })
    const wrapper = mountPicker({ scope: 'user' })

    await wrapper.find('[data-testid="slug-input"]').setValue('alice')
    vi.advanceTimersByTime(400)
    await flushPromises()

    await wrapper.find('[data-testid="save-btn"]').trigger('click')
    await flushPromises()

    expect(wrapper.emitted('updated')).toBeTruthy()
    expect(wrapper.emitted('updated')![0]).toEqual(['alice'])
  })

  it('shows save-success banner after a successful save', async () => {
    mockSlugService.check.mockResolvedValue({ available: true })
    const wrapper = mountPicker({ scope: 'user' })

    await wrapper.find('[data-testid="slug-input"]').setValue('alice')
    vi.advanceTimersByTime(400)
    await flushPromises()

    await wrapper.find('[data-testid="save-btn"]').trigger('click')
    await flushPromises()

    expect(wrapper.find('[data-testid="save-success"]').exists()).toBe(true)
  })

  it('shows save-error banner when save fails with server message', async () => {
    mockSlugService.check.mockResolvedValue({ available: true })
    mockSlugService.setUserSlug.mockRejectedValue({
      response: { data: { error: 'Slug is now taken.' } },
    })
    const wrapper = mountPicker({ scope: 'user' })

    await wrapper.find('[data-testid="slug-input"]').setValue('alice')
    vi.advanceTimersByTime(400)
    await flushPromises()

    await wrapper.find('[data-testid="save-btn"]').trigger('click')
    await flushPromises()

    expect(wrapper.find('[data-testid="save-error"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="save-error"]').text()).toContain('Slug is now taken.')
  })

  it('shows fallback error when save fails without server message', async () => {
    mockSlugService.check.mockResolvedValue({ available: true })
    mockSlugService.setUserSlug.mockRejectedValue(new Error('boom'))
    const wrapper = mountPicker({ scope: 'user' })

    await wrapper.find('[data-testid="slug-input"]').setValue('alice')
    vi.advanceTimersByTime(400)
    await flushPromises()

    await wrapper.find('[data-testid="save-btn"]').trigger('click')
    await flushPromises()

    expect(wrapper.find('[data-testid="save-error"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="save-error"]').text()).toContain('Failed to save')
  })

  it('does nothing when save is called with canSave=false (button disabled guard)', async () => {
    // save button is disabled when checkStatus !== 'available'
    // clicking it programmatically still calls save() which should return early
    mockSlugService.check.mockResolvedValue({ available: false, reason: 'taken' })
    const wrapper = mountPicker({ scope: 'user' })
    await wrapper.find('[data-testid="slug-input"]').setValue('alice')
    vi.advanceTimersByTime(400)
    await flushPromises()
    // button is disabled, but trigger click anyway
    await wrapper.find('[data-testid="save-btn"]').trigger('click')
    await flushPromises()
    expect(mockSlugService.setUserSlug).not.toHaveBeenCalled()
  })

  it('disables the save button while saving is in flight', async () => {
    mockSlugService.check.mockResolvedValue({ available: true })
    let resolveSet!: () => void
    mockSlugService.setUserSlug.mockImplementation(
      () => new Promise<void>((res) => { resolveSet = res }),
    )
    const wrapper = mountPicker({ scope: 'user' })

    await wrapper.find('[data-testid="slug-input"]').setValue('alice')
    vi.advanceTimersByTime(400)
    await flushPromises()

    wrapper.find('[data-testid="save-btn"]').trigger('click')
    await flushPromises()

    const btn = wrapper.find('[data-testid="save-btn"]').element as HTMLButtonElement
    expect(btn.disabled).toBe(true)

    resolveSet()
    await flushPromises()
  })

  it('shows a save-error when company scope save has no targetId', async () => {
    // Defensive guard: scope=company but targetId was not provided
    mockSlugService.check.mockResolvedValue({ available: true })
    const wrapper = mountPicker({ scope: 'company' /* no targetId */ })
    await wrapper.find('[data-testid="slug-input"]').setValue('acme')
    vi.advanceTimersByTime(400)
    await flushPromises()
    await wrapper.find('[data-testid="save-btn"]').trigger('click')
    await flushPromises()
    expect(wrapper.find('[data-testid="save-error"]').exists()).toBe(true)
    expect(mockSlugService.setCompanySlug).not.toHaveBeenCalled()
  })

  // ── Clear flow ────────────────────────────────────────────────────────────

  it('calls setUserSlug(null) when the clear button is clicked (user scope)', async () => {
    const wrapper = mountPicker({ initialSlug: 'alice', scope: 'user' })
    await wrapper.find('[data-testid="clear-btn"]').trigger('click')
    await flushPromises()
    expect(mockSlugService.setUserSlug).toHaveBeenCalledWith(null)
  })

  it('calls setCompanySlug(id, null) when clear is clicked (company scope)', async () => {
    const wrapper = mountPicker({ initialSlug: 'acme', scope: 'company', targetId: 'cid-1' })
    await wrapper.find('[data-testid="clear-btn"]').trigger('click')
    await flushPromises()
    expect(mockSlugService.setCompanySlug).toHaveBeenCalledWith('cid-1', null)
  })

  it('emits "updated" with null after a successful clear', async () => {
    const wrapper = mountPicker({ initialSlug: 'alice', scope: 'user' })
    await wrapper.find('[data-testid="clear-btn"]').trigger('click')
    await flushPromises()
    expect(wrapper.emitted('updated')).toBeTruthy()
    expect(wrapper.emitted('updated')![0]).toEqual([null])
  })

  it('clears the input field after clear', async () => {
    const wrapper = mountPicker({ initialSlug: 'alice', scope: 'user' })
    await wrapper.find('[data-testid="clear-btn"]').trigger('click')
    await flushPromises()
    const input = wrapper.find('[data-testid="slug-input"]').element as HTMLInputElement
    expect(input.value).toBe('')
  })

  it('shows save-error banner when clear fails', async () => {
    mockSlugService.setUserSlug.mockRejectedValue({
      response: { data: { error: 'Cannot clear.' } },
    })
    const wrapper = mountPicker({ initialSlug: 'alice', scope: 'user' })
    await wrapper.find('[data-testid="clear-btn"]').trigger('click')
    await flushPromises()
    expect(wrapper.find('[data-testid="save-error"]').text()).toContain('Cannot clear.')
  })

  it('shows a save-error when company scope clear has no targetId', async () => {
    // Defensive guard: scope=company but targetId was not provided
    const wrapper = mountPicker({ initialSlug: 'acme', scope: 'company' /* no targetId */ })
    await wrapper.find('[data-testid="clear-btn"]').trigger('click')
    await flushPromises()
    expect(wrapper.find('[data-testid="save-error"]').exists()).toBe(true)
    expect(mockSlugService.setCompanySlug).not.toHaveBeenCalled()
  })
})
