import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount, flushPromises, RouterLinkStub } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'

// ── Route mock ────────────────────────────────────────────────────────────────

const mockRoute = vi.hoisted(() => ({
  params: { id: 'evt-1' } as Record<string, string>,
}))

vi.mock('vue-router', async (importOriginal) => {
  const actual = await importOriginal<typeof import('vue-router')>()
  return {
    ...actual,
    useRoute:   () => mockRoute,
    RouterLink: RouterLinkStub,
  }
})

// ── Service mocks ─────────────────────────────────────────────────────────────

const mockEventService = vi.hoisted(() => ({
  getEvent:        vi.fn(),
  getAttendees:    vi.fn(),
  rsvp:            vi.fn(),
  getEventPosts:   vi.fn(),
  createEventPost: vi.fn(),
}))

vi.mock('@/services/eventService', () => ({
  eventService: mockEventService,
}))

const mockCompanyPageService = vi.hoisted(() => ({
  getPage: vi.fn(),
}))

vi.mock('@/services/companyPageService', () => ({
  companyPageService: mockCompanyPageService,
}))

// ── Auth store mock ────────────────────────────────────────────────────────────

const mockAuth = vi.hoisted(() => ({
  user:            { id: 'u1', displayName: 'Alice' } as { id: string; displayName: string } | null,
  isAuthenticated: true,
}))

vi.mock('@/stores/auth', () => ({
  useAuthStore: () => mockAuth,
}))

// ── CompanyMode store mock ─────────────────────────────────────────────────────

const mockCompanyMode = vi.hoisted(() => ({
  activeCompany:        null as null | { id: string; name: string },
  isActive:             false,
  setActiveEvent:       vi.fn(),
  canManageActiveEvent: false,
}))

vi.mock('@/stores/companyMode', () => ({
  useCompanyModeStore: () => mockCompanyMode,
}))

// Import after mocks
import EventDetailView from '../EventDetailView.vue'
import type { EventDto, EventAttendeesResult } from '@/services/eventService'

// ── Fixtures ──────────────────────────────────────────────────────────────────

function makeEvent(overrides: Partial<EventDto> = {}): EventDto {
  return {
    id:            'evt-1',
    creator:       { id: 'u1', displayName: 'Alice' },
    title:         'Vue Conf 2027',
    description:   'A great conference about Vue.',
    startAt:       '2027-06-15T10:00:00Z',
    endAt:         '2027-06-15T18:00:00Z',
    location:      'Stockholm, Sweden',
    privacy:       'Everyone',
    postingPolicy: 'Everyone',
    goingCount:    10,
    maybeCount:    5,
    notGoingCount: 2,
    createdAt:     '2026-01-01T00:00:00Z',
    ...overrides,
  }
}

const emptyAttendees: EventAttendeesResult = {
  going:     [],
  maybe:     [],
  notGoing:  [],
}

const emptyPostsPage = { items: [], page: 1, pageSize: 20, hasMore: false }

// ── Mount helper ──────────────────────────────────────────────────────────────

function mountView() {
  return mount(EventDetailView, {
    global: {
      plugins: [createPinia()],
      stubs:   { RouterLink: RouterLinkStub },
    },
  })
}

// ── Tests ─────────────────────────────────────────────────────────────────────

describe('EventDetailView', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.clearAllMocks()
    mockRoute.params = { id: 'evt-1' }
    mockAuth.user            = { id: 'u1', displayName: 'Alice' }
    mockAuth.isAuthenticated = true
    mockCompanyMode.activeCompany = null
    mockCompanyMode.isActive      = false

    mockEventService.getEvent.mockResolvedValue(makeEvent())
    mockEventService.getAttendees.mockResolvedValue({ ...emptyAttendees })
    mockEventService.getEventPosts.mockResolvedValue({ ...emptyPostsPage })
    mockCompanyPageService.getPage.mockRejectedValue(new Error('no company'))
  })

  // ── Fetch on mount ────────────────────────────────────────────────────────

  it('fetches the event using route.params.id on mount', async () => {
    mockRoute.params = { id: 'evt-42' }
    mountView()
    await flushPromises()

    expect(mockEventService.getEvent).toHaveBeenCalledWith('evt-42')
  })

  it('also fetches attendees on mount', async () => {
    mountView()
    await flushPromises()

    expect(mockEventService.getAttendees).toHaveBeenCalledWith('evt-1')
  })

  it('also fetches event posts on mount', async () => {
    mountView()
    await flushPromises()

    expect(mockEventService.getEventPosts).toHaveBeenCalledWith('evt-1', 1)
  })

  // ── Loading state ─────────────────────────────────────────────────────────

  it('shows a loading skeleton before the event resolves', () => {
    mockEventService.getEvent.mockReturnValue(new Promise(() => { /* never resolves */ }))
    const wrapper = mountView()

    expect(wrapper.find('.animate-pulse').exists()).toBe(true)
  })

  // ── Renders core details ──────────────────────────────────────────────────

  it('renders the event title', async () => {
    const wrapper = mountView()
    await flushPromises()

    const title = wrapper.find('[data-testid="event-title"]')
    expect(title.exists()).toBe(true)
    expect(title.text()).toBe('Vue Conf 2027')
  })

  it('renders the event description', async () => {
    const wrapper = mountView()
    await flushPromises()

    expect(wrapper.text()).toContain('A great conference about Vue.')
  })

  it('renders the location', async () => {
    const wrapper = mountView()
    await flushPromises()

    expect(wrapper.text()).toContain('Stockholm, Sweden')
  })

  it('renders the start date (year is visible)', async () => {
    const wrapper = mountView()
    await flushPromises()

    expect(wrapper.text()).toContain('2027')
  })

  it('renders the participant counts (goingCount + maybeCount)', async () => {
    const wrapper = mountView()
    await flushPromises()

    expect(wrapper.text()).toContain('10')
    expect(wrapper.text()).toContain('5')
  })

  // ── RSVP buttons (upcoming event) ─────────────────────────────────────────

  it('renders Going/Maybe/Not Going RSVP buttons for upcoming events', async () => {
    // startAt in the far future → isUpcoming = true
    const wrapper = mountView()
    await flushPromises()

    expect(wrapper.find('[data-testid="rsvp-going"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="rsvp-maybe"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="rsvp-not-going"]').exists()).toBe(true)
  })

  it('calls eventService.rsvp with Going when the Going button is clicked', async () => {
    const updatedEvent = makeEvent({ myRsvp: 'Going', goingCount: 11 })
    mockEventService.rsvp.mockResolvedValue(updatedEvent)
    mockEventService.getAttendees.mockResolvedValue({ ...emptyAttendees })

    const wrapper = mountView()
    await flushPromises()

    await wrapper.find('[data-testid="rsvp-going"]').trigger('click')
    await flushPromises()

    expect(mockEventService.rsvp).toHaveBeenCalledWith('evt-1', 'Going')
  })

  it('does not render RSVP buttons for past events', async () => {
    mockEventService.getEvent.mockResolvedValue(
      makeEvent({ startAt: '2020-01-01T10:00:00Z' }),
    )
    const wrapper = mountView()
    await flushPromises()

    expect(wrapper.find('[data-testid="rsvp-going"]').exists()).toBe(false)
  })

  // ── Error / not-found state ───────────────────────────────────────────────

  it('shows "Event not found." on a 404 response', async () => {
    mockEventService.getEvent.mockRejectedValue({ response: { status: 404 } })
    const wrapper = mountView()
    await flushPromises()

    expect(wrapper.text()).toContain('Event not found.')
  })

  it('shows a permission error on a 403 response', async () => {
    mockEventService.getEvent.mockRejectedValue({ response: { status: 403 } })
    const wrapper = mountView()
    await flushPromises()

    expect(wrapper.text()).toContain('You do not have permission')
  })

  it('shows a generic error message on other failures', async () => {
    mockEventService.getEvent.mockRejectedValue({
      response: { status: 500, data: { error: 'Server exploded' } },
    })
    const wrapper = mountView()
    await flushPromises()

    expect(wrapper.text()).toContain('Server exploded')
  })

  it('shows a fallback error message when no detail is provided', async () => {
    mockEventService.getEvent.mockRejectedValue(new Error('Network error'))
    const wrapper = mountView()
    await flushPromises()

    expect(wrapper.text()).toContain('Network error')
  })

  // ── Ticket URL ────────────────────────────────────────────────────────────

  it('renders the Buy tickets link when ticketUrl is present', async () => {
    mockEventService.getEvent.mockResolvedValue(
      makeEvent({ ticketUrl: 'https://tickets.example.com/vueconf' }),
    )
    const wrapper = mountView()
    await flushPromises()

    const link = wrapper.find('[data-testid="event-buy-tickets"]')
    expect(link.exists()).toBe(true)
    expect(link.attributes('href')).toBe('https://tickets.example.com/vueconf')
  })

  it('does not render the Buy tickets link when ticketUrl is absent', async () => {
    const wrapper = mountView()
    await flushPromises()

    expect(wrapper.find('[data-testid="event-buy-tickets"]').exists()).toBe(false)
  })

  // ── Attendees section ─────────────────────────────────────────────────────

  it('shows the "No RSVPs yet" message when attendees are empty', async () => {
    const wrapper = mountView()
    await flushPromises()

    expect(wrapper.text()).toContain('No RSVPs yet')
  })

  it('renders the name of a Going attendee', async () => {
    mockEventService.getAttendees.mockResolvedValue({
      going:    [{ userId: 'u2', displayName: 'Bob', rsvp: 'Going' }],
      maybe:    [],
      notGoing: [],
    })
    const wrapper = mountView()
    await flushPromises()

    expect(wrapper.text()).toContain('Bob')
  })

  // ── setActiveEvent is called ──────────────────────────────────────────────

  it('calls companyMode.setActiveEvent with the loaded event', async () => {
    mountView()
    await flushPromises()

    expect(mockCompanyMode.setActiveEvent).toHaveBeenCalled()
    const call = mockCompanyMode.setActiveEvent.mock.calls[0]
    expect(call[0]?.id).toBe('evt-1')
  })
})
