import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'

// ── Events store mock ─────────────────────────────────────────────────────────

const mockEventsStore = vi.hoisted(() => ({
  createEvent: vi.fn(),
}))

vi.mock('@/stores/events', () => ({
  useEventsStore: () => mockEventsStore,
}))

// ── CompanyMode store mock ─────────────────────────────────────────────────────

const mockCompanyMode = vi.hoisted(() => ({
  activeCompany: null as null | { id: string; name: string },
  isActive:      false,
}))

vi.mock('@/stores/companyMode', () => ({
  useCompanyModeStore: () => mockCompanyMode,
}))

// ── PlacePicker stub ──────────────────────────────────────────────────────────

vi.mock('@/components/shared/PlacePicker.vue', () => ({
  default: {
    name:     'PlacePicker',
    props:    ['modelValue', 'mode', 'label', 'placeholder'],
    emits:    ['update:modelValue'],
    template: '<div data-testid="place-picker-stub" />',
  },
}))

// Import after mocks
import EventCreateModal from '../EventCreateModal.vue'
import type { EventDto } from '@/services/eventService'

// ── Fixtures ──────────────────────────────────────────────────────────────────

function makeCreatedEvent(): EventDto {
  return {
    id:            'evt-new',
    creator:       { id: 'u1', displayName: 'Alice' },
    title:         'My New Event',
    startAt:       '2027-05-01T10:00:00Z',
    privacy:       'Everyone',
    postingPolicy: 'OrganizerOnly',
    goingCount:    0,
    maybeCount:    0,
    notGoingCount: 0,
    createdAt:     '2026-01-01T00:00:00Z',
  }
}

// ── Mount helper ──────────────────────────────────────────────────────────────

function mountModal() {
  // EventCreateModal uses Teleport to="body" — attachTo so DOM is complete
  const div = document.createElement('div')
  document.body.appendChild(div)

  return mount(EventCreateModal, {
    attachTo: div,
    global: {
      plugins: [createPinia()],
      stubs:   {
        Teleport: { template: '<div><slot /></div>' },
      },
    },
  })
}

// ── Tests ─────────────────────────────────────────────────────────────────────

describe('EventCreateModal', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.clearAllMocks()
    mockCompanyMode.activeCompany = null
    mockCompanyMode.isActive      = false
    mockEventsStore.createEvent.mockResolvedValue(makeCreatedEvent())
  })

  // ── Form fields present ───────────────────────────────────────────────────

  it('renders the title input', () => {
    const wrapper = mountModal()

    const titleInput = wrapper.find('input[placeholder="Event title"]')
    expect(titleInput.exists()).toBe(true)
  })

  it('renders the description textarea', () => {
    const wrapper = mountModal()

    const desc = wrapper.find('textarea[placeholder="What\'s this event about?"]')
    expect(desc.exists()).toBe(true)
  })

  it('renders the start date/time input', () => {
    const wrapper = mountModal()

    const inputs = wrapper.findAll('input[type="datetime-local"]')
    expect(inputs.length).toBeGreaterThanOrEqual(1)
  })

  it('renders the end date/time input', () => {
    const wrapper = mountModal()

    const inputs = wrapper.findAll('input[type="datetime-local"]')
    // Should have both start and end datetime inputs
    expect(inputs.length).toBeGreaterThanOrEqual(2)
  })

  it('renders the PlacePicker for location', () => {
    const wrapper = mountModal()

    expect(wrapper.find('[data-testid="place-picker-stub"]').exists()).toBe(true)
  })

  it('renders the privacy select', () => {
    const wrapper = mountModal()

    const selects = wrapper.findAll('select')
    const privacySelect = selects.find(s =>
      s.html().includes('Everyone') && s.html().includes('Friends'),
    )
    expect(privacySelect).toBeDefined()
  })

  it('renders the ticket URL input', () => {
    const wrapper = mountModal()

    const ticketInput = wrapper.find('[data-testid="event-ticket-url-input"]')
    expect(ticketInput.exists()).toBe(true)
  })

  // ── Validation errors ─────────────────────────────────────────────────────

  it('shows a validation error when submitting without a title', async () => {
    const wrapper = mountModal()

    const createBtn = wrapper.findAll('button').find(b => b.text().includes('Create Event'))
    expect(createBtn).toBeDefined()

    await createBtn!.trigger('click')
    await flushPromises()

    expect(wrapper.text()).toContain('Title is required.')
    expect(mockEventsStore.createEvent).not.toHaveBeenCalled()
  })

  it('shows a validation error when submitting without a start date', async () => {
    const wrapper = mountModal()

    await wrapper.find('input[placeholder="Event title"]').setValue('My Event')

    const createBtn = wrapper.findAll('button').find(b => b.text().includes('Create Event'))
    await createBtn!.trigger('click')
    await flushPromises()

    expect(wrapper.text()).toContain('Start date/time is required.')
    expect(mockEventsStore.createEvent).not.toHaveBeenCalled()
  })

  it('shows an error for a ticket URL that does not start with http(s)://', async () => {
    const wrapper = mountModal()

    await wrapper.find('input[placeholder="Event title"]').setValue('My Event')
    await wrapper.find('input[type="datetime-local"]').setValue('2027-05-01T10:00')
    await wrapper.find('[data-testid="event-ticket-url-input"]').setValue('ftp://bad-url.com')

    const createBtn = wrapper.findAll('button').find(b => b.text().includes('Create Event'))
    await createBtn!.trigger('click')
    await flushPromises()

    expect(wrapper.text()).toContain('Ticket link must start with http://')
    expect(mockEventsStore.createEvent).not.toHaveBeenCalled()
  })

  // ── Successful submit ─────────────────────────────────────────────────────

  it('calls eventsStore.createEvent with the correct payload on submit', async () => {
    const wrapper = mountModal()

    await wrapper.find('input[placeholder="Event title"]').setValue('My New Event')
    // Set the first datetime-local (startAt)
    const dateInputs = wrapper.findAll('input[type="datetime-local"]')
    await dateInputs[0].setValue('2027-05-01T10:00')

    const createBtn = wrapper.findAll('button').find(b => b.text().includes('Create Event'))
    await createBtn!.trigger('click')
    await flushPromises()

    expect(mockEventsStore.createEvent).toHaveBeenCalledOnce()
    const payload = mockEventsStore.createEvent.mock.calls[0][0]
    expect(payload.title).toBe('My New Event')
    expect(payload.startAt).toBeTruthy()
    expect(payload.privacy).toBe('Everyone')
  })

  it('emits "created" with the new event after successful creation', async () => {
    const wrapper = mountModal()

    await wrapper.find('input[placeholder="Event title"]').setValue('My New Event')
    const dateInputs = wrapper.findAll('input[type="datetime-local"]')
    await dateInputs[0].setValue('2027-05-01T10:00')

    const createBtn = wrapper.findAll('button').find(b => b.text().includes('Create Event'))
    await createBtn!.trigger('click')
    await flushPromises()

    const createdEmits = wrapper.emitted('created')
    expect(createdEmits).toHaveLength(1)
    expect((createdEmits![0] as EventDto[])[0].id).toBe('evt-new')
  })

  it('emits "close" after successful creation', async () => {
    const wrapper = mountModal()

    await wrapper.find('input[placeholder="Event title"]').setValue('My New Event')
    const dateInputs = wrapper.findAll('input[type="datetime-local"]')
    await dateInputs[0].setValue('2027-05-01T10:00')

    const createBtn = wrapper.findAll('button').find(b => b.text().includes('Create Event'))
    await createBtn!.trigger('click')
    await flushPromises()

    expect(wrapper.emitted('close')).toHaveLength(1)
  })

  // ── Cancel button ─────────────────────────────────────────────────────────

  it('emits "close" when the Cancel button is clicked', async () => {
    const wrapper = mountModal()

    const cancelBtn = wrapper.findAll('button').find(b => b.text() === 'Cancel')
    expect(cancelBtn).toBeDefined()

    await cancelBtn!.trigger('click')

    expect(wrapper.emitted('close')).toHaveLength(1)
    expect(mockEventsStore.createEvent).not.toHaveBeenCalled()
  })

  it('emits "close" when the X (aria-label="Close") button is clicked', async () => {
    const wrapper = mountModal()

    const closeBtn = wrapper.find('button[aria-label="Close"]')
    expect(closeBtn.exists()).toBe(true)

    await closeBtn.trigger('click')

    expect(wrapper.emitted('close')).toHaveLength(1)
  })

  // ── Service error ─────────────────────────────────────────────────────────

  it('shows the server error message when creation fails', async () => {
    mockEventsStore.createEvent.mockRejectedValue({
      response: { data: { error: 'You are rate limited.' } },
    })

    const wrapper = mountModal()

    await wrapper.find('input[placeholder="Event title"]').setValue('My New Event')
    const dateInputs = wrapper.findAll('input[type="datetime-local"]')
    await dateInputs[0].setValue('2027-05-01T10:00')

    const createBtn = wrapper.findAll('button').find(b => b.text().includes('Create Event'))
    await createBtn!.trigger('click')
    await flushPromises()

    expect(wrapper.text()).toContain('You are rate limited.')
    expect(wrapper.emitted('close')).toBeUndefined()
  })

  it('shows fallback error text on a generic failure', async () => {
    mockEventsStore.createEvent.mockRejectedValue(new Error('Network error'))

    const wrapper = mountModal()

    await wrapper.find('input[placeholder="Event title"]').setValue('My New Event')
    const dateInputs = wrapper.findAll('input[type="datetime-local"]')
    await dateInputs[0].setValue('2027-05-01T10:00')

    const createBtn = wrapper.findAll('button').find(b => b.text().includes('Create Event'))
    await createBtn!.trigger('click')
    await flushPromises()

    expect(wrapper.text()).toContain('Network error')
  })

  // ── Company mode ──────────────────────────────────────────────────────────

  it('shows the organizer name when company mode is active', () => {
    mockCompanyMode.isActive      = true
    mockCompanyMode.activeCompany = { id: 'cp-1', name: 'Acme Corp' }

    const wrapper = mountModal()

    expect(wrapper.text()).toContain('Acme Corp')
  })

  it('includes the company page id in the payload when company mode is active', async () => {
    mockCompanyMode.isActive      = true
    mockCompanyMode.activeCompany = { id: 'cp-1', name: 'Acme Corp' }

    const wrapper = mountModal()

    await wrapper.find('input[placeholder="Event title"]').setValue('Company Event')
    const dateInputs = wrapper.findAll('input[type="datetime-local"]')
    await dateInputs[0].setValue('2027-05-01T10:00')

    const createBtn = wrapper.findAll('button').find(b => b.text().includes('Create Event'))
    await createBtn!.trigger('click')
    await flushPromises()

    const payload = mockEventsStore.createEvent.mock.calls[0][0]
    expect(payload.companyPageId).toBe('cp-1')
  })
})
