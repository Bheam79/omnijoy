import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import PlacePicker from '../PlacePicker.vue'
import type { PlaceSelection, AutocompleteResult } from '@/types/places'

// ── Mocks ─────────────────────────────────────────────────────────────────────

const mockPlacesService = vi.hoisted(() => ({
  autocomplete: vi.fn(),
  getDetails:   vi.fn(),
}))

vi.mock('@/services/placesService', () => ({
  placesService: mockPlacesService,
}))

// Mock crypto.randomUUID so we get predictable tokens in tests
const UUID = '00000000-0000-0000-0000-000000000001'
vi.stubGlobal('crypto', { randomUUID: vi.fn(() => UUID) })

// ── Fixtures ──────────────────────────────────────────────────────────────────

const suggestion1: AutocompleteResult = {
  placeId:       'ChIJPx…oslo',
  description:   'Oslo, Norway',
  mainText:      'Oslo',
  secondaryText: 'Norway',
}

const suggestion2: AutocompleteResult = {
  placeId:       'ChIJPx…bergen',
  description:   'Bergen, Norway',
  mainText:      'Bergen',
  secondaryText: 'Norway',
}

const osloSelection: PlaceSelection = {
  placeId:          'ChIJPx…oslo',
  displayName:      'Oslo, Norway',
  city:             'Oslo',
  country:          'Norway',
  countryCode:      'NO',
  latitude:         59.913868,
  longitude:        10.752245,
  formattedAddress: null,
}

// ── Helper ────────────────────────────────────────────────────────────────────

type MountProps = {
  modelValue?: PlaceSelection | null
  mode?: 'cities' | 'address'
  label?: string
  placeholder?: string
  required?: boolean
  disabled?: boolean
}

function mountPicker(props: MountProps = {}) {
  return mount(PlacePicker, {
    props: {
      modelValue:  props.modelValue ?? null,
      mode:        props.mode ?? 'cities',
      label:       props.label ?? 'Location',
      placeholder: props.placeholder,
      required:    props.required,
      disabled:    props.disabled,
    },
  })
}

// ── Tests ─────────────────────────────────────────────────────────────────────

describe('PlacePicker', () => {
  beforeEach(() => {
    vi.useFakeTimers()
    vi.clearAllMocks()
    mockPlacesService.autocomplete.mockResolvedValue([suggestion1, suggestion2])
    mockPlacesService.getDetails.mockResolvedValue(osloSelection)
  })

  afterEach(() => {
    vi.useRealTimers()
  })

  // ── Rendering ──────────────────────────────────────────────────────────────

  it('renders the label', () => {
    const wrapper = mountPicker({ label: 'City' })
    expect(wrapper.text()).toContain('City')
  })

  it('renders an asterisk when required is true', () => {
    const wrapper = mountPicker({ required: true })
    expect(wrapper.find('span.text-red-500').exists()).toBe(true)
  })

  it('does not render an asterisk when required is false', () => {
    const wrapper = mountPicker({ required: false })
    expect(wrapper.find('span.text-red-500').exists()).toBe(false)
  })

  it('renders the search input when modelValue is null', () => {
    const wrapper = mountPicker()
    expect(wrapper.find('[data-testid="place-input"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="selected-chip"]').exists()).toBe(false)
  })

  it('renders the selected chip when modelValue is set', () => {
    const wrapper = mountPicker({ modelValue: osloSelection })
    expect(wrapper.find('[data-testid="selected-chip"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="place-input"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="selected-display-name"]').text()).toBe('Oslo, Norway')
  })

  it('does not render the clear button on chip when disabled', () => {
    const wrapper = mountPicker({ modelValue: osloSelection, disabled: true })
    expect(wrapper.find('[data-testid="clear-btn"]').exists()).toBe(false)
  })

  it('renders the clear button on chip when not disabled', () => {
    const wrapper = mountPicker({ modelValue: osloSelection })
    expect(wrapper.find('[data-testid="clear-btn"]').exists()).toBe(true)
  })

  // ── Autocomplete debounce ─────────────────────────────────────────────────

  it('does not call autocomplete before 300ms debounce', async () => {
    const wrapper = mountPicker()
    const input = wrapper.find('[data-testid="place-input"]')
    await input.setValue('Oslo')
    await input.trigger('input')
    vi.advanceTimersByTime(200)
    await flushPromises()
    expect(mockPlacesService.autocomplete).not.toHaveBeenCalled()
  })

  it('calls autocomplete after 300ms debounce', async () => {
    const wrapper = mountPicker()
    const input = wrapper.find('[data-testid="place-input"]')
    await input.setValue('Oslo')
    await input.trigger('input')
    vi.advanceTimersByTime(300)
    await flushPromises()
    expect(mockPlacesService.autocomplete).toHaveBeenCalledWith('Oslo', '(cities)', UUID)
  })

  it('uses (address) types param when mode is address', async () => {
    const wrapper = mountPicker({ mode: 'address' })
    const input = wrapper.find('[data-testid="place-input"]')
    await input.setValue('Main St')
    await input.trigger('input')
    vi.advanceTimersByTime(300)
    await flushPromises()
    expect(mockPlacesService.autocomplete).toHaveBeenCalledWith('Main St', '(address)', UUID)
  })

  it('coalesces rapid inputs into a single API call', async () => {
    const wrapper = mountPicker()
    const input = wrapper.find('[data-testid="place-input"]')

    await input.setValue('O')
    await input.trigger('input')
    vi.advanceTimersByTime(100)
    await input.setValue('Os')
    await input.trigger('input')
    vi.advanceTimersByTime(100)
    await input.setValue('Oslo')
    await input.trigger('input')
    vi.advanceTimersByTime(300)
    await flushPromises()

    expect(mockPlacesService.autocomplete).toHaveBeenCalledTimes(1)
    expect(mockPlacesService.autocomplete).toHaveBeenCalledWith('Oslo', '(cities)', UUID)
  })

  it('reuses the same session token across multiple autocomplete calls', async () => {
    const wrapper = mountPicker()
    const input = wrapper.find('[data-testid="place-input"]')

    await input.setValue('Osl')
    await input.trigger('input')
    vi.advanceTimersByTime(300)
    await flushPromises()

    await input.setValue('Oslo')
    await input.trigger('input')
    vi.advanceTimersByTime(300)
    await flushPromises()

    // Both calls should use the same session token
    expect(mockPlacesService.autocomplete).toHaveBeenCalledTimes(2)
    expect(mockPlacesService.autocomplete.mock.calls[0][2]).toBe(UUID)
    expect(mockPlacesService.autocomplete.mock.calls[1][2]).toBe(UUID)
  })

  // ── Dropdown rendering ────────────────────────────────────────────────────

  it('shows the suggestions dropdown after autocomplete resolves', async () => {
    const wrapper = mountPicker()
    const input = wrapper.find('[data-testid="place-input"]')
    await input.setValue('Oslo')
    await input.trigger('input')
    vi.advanceTimersByTime(300)
    await flushPromises()

    expect(wrapper.find('[data-testid="suggestions-list"]').exists()).toBe(true)
    const items = wrapper.findAll('[data-testid="suggestion-item"]')
    expect(items).toHaveLength(2)
    expect(items[0].text()).toContain('Oslo')
    expect(items[1].text()).toContain('Bergen')
  })

  it('shows the loading spinner while autocomplete is in flight', async () => {
    let resolve!: (v: AutocompleteResult[]) => void
    mockPlacesService.autocomplete.mockImplementation(
      () => new Promise<AutocompleteResult[]>((r) => { resolve = r }),
    )

    const wrapper = mountPicker()
    const input = wrapper.find('[data-testid="place-input"]')
    await input.setValue('Oslo')
    await input.trigger('input')
    vi.advanceTimersByTime(300)
    await flushPromises()

    expect(wrapper.find('[data-testid="loading-spinner"]').exists()).toBe(true)

    resolve([suggestion1])
    await flushPromises()

    expect(wrapper.find('[data-testid="loading-spinner"]').exists()).toBe(false)
  })

  it('hides the dropdown when input is cleared', async () => {
    const wrapper = mountPicker()
    const input = wrapper.find('[data-testid="place-input"]')

    await input.setValue('Oslo')
    await input.trigger('input')
    vi.advanceTimersByTime(300)
    await flushPromises()
    expect(wrapper.find('[data-testid="suggestions-list"]').exists()).toBe(true)

    // Clear the input
    await input.setValue('')
    await input.trigger('input')
    await flushPromises()
    expect(wrapper.find('[data-testid="suggestions-list"]').exists()).toBe(false)
  })

  // ── Selection flow ────────────────────────────────────────────────────────

  it('calls getDetails when a suggestion is selected', async () => {
    const wrapper = mountPicker()
    const input = wrapper.find('[data-testid="place-input"]')
    await input.setValue('Oslo')
    await input.trigger('input')
    vi.advanceTimersByTime(300)
    await flushPromises()

    const item = wrapper.find('[data-testid="suggestion-item"]')
    await item.trigger('mousedown')
    await flushPromises()

    expect(mockPlacesService.getDetails).toHaveBeenCalledWith(suggestion1.placeId, UUID)
  })

  it('emits update:modelValue with the resolved PlaceSelection after selection', async () => {
    const wrapper = mountPicker()
    const input = wrapper.find('[data-testid="place-input"]')
    await input.setValue('Oslo')
    await input.trigger('input')
    vi.advanceTimersByTime(300)
    await flushPromises()

    await wrapper.find('[data-testid="suggestion-item"]').trigger('mousedown')
    await flushPromises()

    const emitted = wrapper.emitted('update:modelValue')
    expect(emitted).toBeTruthy()
    const emittedValue = emitted![0][0] as PlaceSelection
    expect(emittedValue.placeId).toBe(osloSelection.placeId)
    expect(emittedValue.city).toBe('Oslo')
  })

  it('overrides displayName with the suggestion description', async () => {
    const wrapper = mountPicker()
    const input = wrapper.find('[data-testid="place-input"]')
    await input.setValue('Oslo')
    await input.trigger('input')
    vi.advanceTimersByTime(300)
    await flushPromises()

    await wrapper.find('[data-testid="suggestion-item"]').trigger('mousedown')
    await flushPromises()

    const emitted = wrapper.emitted('update:modelValue')!
    const emittedValue = emitted[0][0] as PlaceSelection
    // displayName should be the suggestion's description
    expect(emittedValue.displayName).toBe('Oslo, Norway')
  })

  it('generates a new session token after a selection', async () => {
    // Track calls to randomUUID
    const uuidMock = vi.fn()
      .mockReturnValueOnce('token-1')
      .mockReturnValueOnce('token-2')
    vi.stubGlobal('crypto', { randomUUID: uuidMock })

    const wrapper = mountPicker()
    const input = wrapper.find('[data-testid="place-input"]')

    // First session
    await input.setValue('Oslo')
    await input.trigger('input')
    vi.advanceTimersByTime(300)
    await flushPromises()
    await wrapper.find('[data-testid="suggestion-item"]').trigger('mousedown')
    await flushPromises()

    // Simulate parent updating modelValue after selection
    await wrapper.setProps({ modelValue: osloSelection })

    // Clear selection to get back to input state
    await wrapper.find('[data-testid="clear-btn"]').trigger('click')
    await flushPromises()

    // Second session — should use a new token
    await input.setValue('Bergen')
    await input.trigger('input')
    vi.advanceTimersByTime(300)
    await flushPromises()

    // First autocomplete used token-1; second should use token-2
    expect(mockPlacesService.autocomplete.mock.calls[0][2]).toBe('token-1')
    expect(mockPlacesService.autocomplete.mock.calls[1][2]).toBe('token-2')

    // Restore original mock
    vi.stubGlobal('crypto', { randomUUID: vi.fn(() => UUID) })
  })

  // ── Clear ─────────────────────────────────────────────────────────────────

  it('emits update:modelValue with null when the clear button is clicked', async () => {
    const wrapper = mountPicker({ modelValue: osloSelection })
    await wrapper.find('[data-testid="clear-btn"]').trigger('click')
    const emitted = wrapper.emitted('update:modelValue')
    expect(emitted).toBeTruthy()
    expect(emitted![0][0]).toBeNull()
  })

  // ── Error state ───────────────────────────────────────────────────────────

  it('falls back to manual input when autocomplete call fails', async () => {
    mockPlacesService.autocomplete.mockRejectedValue(new Error('Network error'))
    const wrapper = mountPicker()
    const input = wrapper.find('[data-testid="place-input"]')
    await input.setValue('Oslo')
    await input.trigger('input')
    vi.advanceTimersByTime(300)
    await flushPromises()

    // Manual fallback is shown and an inline error is surfaced.
    expect(wrapper.find('[data-testid="manual-input"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="error-message"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="error-message"]').text()).toContain('unavailable')
  })

  it('shows an error message when getDetails call fails', async () => {
    mockPlacesService.autocomplete.mockResolvedValue([suggestion1])
    mockPlacesService.getDetails.mockRejectedValue(new Error('Details error'))

    const wrapper = mountPicker()
    const input = wrapper.find('[data-testid="place-input"]')
    await input.setValue('Oslo')
    await input.trigger('input')
    vi.advanceTimersByTime(300)
    await flushPromises()

    await wrapper.find('[data-testid="suggestion-item"]').trigger('mousedown')
    await flushPromises()

    expect(wrapper.find('[data-testid="error-message"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="error-message"]').text()).toContain('details')
  })

  // ── API unavailable fallback ──────────────────────────────────────────────

  it('shows api-unavailable message when autocomplete returns empty array', async () => {
    mockPlacesService.autocomplete.mockResolvedValue([])
    const wrapper = mountPicker()
    const input = wrapper.find('[data-testid="place-input"]')
    await input.setValue('Oslo')
    await input.trigger('input')
    vi.advanceTimersByTime(300)
    await flushPromises()

    expect(wrapper.find('[data-testid="api-unavailable-msg"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="manual-input"]').exists()).toBe(true)
  })

  it('emits a manual PlaceSelection when the user confirms manual entry', async () => {
    mockPlacesService.autocomplete.mockResolvedValue([])
    const wrapper = mountPicker()
    const input = wrapper.find('[data-testid="place-input"]')
    await input.setValue('Oslo')
    await input.trigger('input')
    vi.advanceTimersByTime(300)
    await flushPromises()

    // Now in fallback mode — type in manual-input
    const manualInput = wrapper.find('[data-testid="manual-input"]')
    await manualInput.setValue('Oslo, Norway')

    await wrapper.find('[data-testid="manual-confirm-btn"]').trigger('click')
    await flushPromises()

    const emitted = wrapper.emitted('update:modelValue')
    expect(emitted).toBeTruthy()
    const value = emitted![0][0] as PlaceSelection
    expect(value.placeId).toBe('manual')
    expect(value.displayName).toBe('Oslo, Norway')
    expect(value.city).toBeNull()
  })

  it('disables the manual confirm button when manual input is empty', async () => {
    mockPlacesService.autocomplete.mockResolvedValue([])
    const wrapper = mountPicker()
    const input = wrapper.find('[data-testid="place-input"]')
    await input.setValue('Oslo')
    await input.trigger('input')
    vi.advanceTimersByTime(300)
    await flushPromises()

    const btn = wrapper.find('[data-testid="manual-confirm-btn"]').element as HTMLButtonElement
    expect(btn.disabled).toBe(true)
  })

  // ── Cleanup ───────────────────────────────────────────────────────────────

  it('clears any pending timer on unmount without errors', async () => {
    const wrapper = mountPicker()
    const input = wrapper.find('[data-testid="place-input"]')
    await input.setValue('Oslo')
    await input.trigger('input')
    // Unmount before debounce fires
    wrapper.unmount()
    vi.advanceTimersByTime(300)
    await flushPromises()
    expect(mockPlacesService.autocomplete).not.toHaveBeenCalled()
  })
})
