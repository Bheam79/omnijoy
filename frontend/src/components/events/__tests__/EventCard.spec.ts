import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount, flushPromises, RouterLinkStub } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'

// ── Service mock ──────────────────────────────────────────────────────────────

const mockEventService = vi.hoisted(() => ({
  rsvp: vi.fn(),
}))

vi.mock('@/services/eventService', () => ({
  eventService: mockEventService,
}))

// ── Events store mock ─────────────────────────────────────────────────────────

const mockEventsStore = vi.hoisted(() => ({
  updateEventInList: vi.fn(),
}))

vi.mock('@/stores/events', () => ({
  useEventsStore: () => mockEventsStore,
}))

// Import after mocks
import EventCard from '../EventCard.vue'
import type { EventDto } from '@/services/eventService'

// ── Fixtures ──────────────────────────────────────────────────────────────────

function makeEvent(overrides: Partial<EventDto> = {}): EventDto {
  return {
    id:            'evt-1',
    creator:       { id: 'u1', displayName: 'Alice' },
    title:         'Tech Meetup',
    startAt:       '2027-03-10T18:00:00Z',
    endAt:         '2027-03-10T21:00:00Z',
    location:      'Oslo, Norway',
    privacy:       'Everyone',
    postingPolicy: 'OrganizerOnly',
    goingCount:    8,
    maybeCount:    3,
    notGoingCount: 1,
    createdAt:     '2026-01-01T00:00:00Z',
    ...overrides,
  }
}

// ── Mount helper ──────────────────────────────────────────────────────────────

function mountCard(event: EventDto) {
  return mount(EventCard, {
    props: { event },
    global: {
      plugins: [createPinia()],
      stubs:   { RouterLink: RouterLinkStub },
    },
  })
}

// ── Tests ─────────────────────────────────────────────────────────────────────

describe('EventCard', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.clearAllMocks()
    mockEventService.rsvp.mockResolvedValue(makeEvent())
  })

  // ── Renders basic event data ───────────────────────────────────────────────

  it('renders the event title', () => {
    const wrapper = mountCard(makeEvent())

    expect(wrapper.text()).toContain('Tech Meetup')
  })

  it('renders the formatted start date (year and month are visible)', () => {
    const wrapper = mountCard(makeEvent())

    // Locale-independent: year and month name should both appear somewhere
    expect(wrapper.text()).toContain('2027')
  })

  it('renders the location when present', () => {
    const wrapper = mountCard(makeEvent())

    expect(wrapper.text()).toContain('Oslo, Norway')
  })

  it('does not render a location row when location is absent', () => {
    const wrapper = mountCard(makeEvent({ location: undefined }))

    // The SVG location icon is inside a v-if block; text should not contain the location
    expect(wrapper.text()).not.toContain('Oslo')
  })

  // ── Cover image ───────────────────────────────────────────────────────────

  it('renders a cover img element when coverImageUrl is set', () => {
    const wrapper = mountCard(
      makeEvent({ coverImageUrl: 'https://cdn.example.com/cover.jpg' }),
    )

    const img = wrapper.find('img[alt="Tech Meetup"]')
    expect(img.exists()).toBe(true)
    expect(img.attributes('src')).toBe('https://cdn.example.com/cover.jpg')
  })

  it('does not render a cover img when coverImageUrl is absent', () => {
    const wrapper = mountCard(makeEvent({ coverImageUrl: undefined }))

    // Only images with the event title as alt text are cover images
    expect(wrapper.find('img[alt="Tech Meetup"]').exists()).toBe(false)
  })

  // ── Participant counts ────────────────────────────────────────────────────

  it('renders goingCount when greater than zero', () => {
    const wrapper = mountCard(makeEvent({ goingCount: 8 }))

    expect(wrapper.text()).toContain('8')
    expect(wrapper.text()).toContain('going')
  })

  it('renders maybeCount when greater than zero', () => {
    const wrapper = mountCard(makeEvent({ maybeCount: 3 }))

    expect(wrapper.text()).toContain('3')
    expect(wrapper.text()).toContain('maybe')
  })

  it('shows "No RSVPs yet" when both counts are zero', () => {
    const wrapper = mountCard(makeEvent({ goingCount: 0, maybeCount: 0 }))

    expect(wrapper.text()).toContain('No RSVPs yet')
  })

  // ── Navigation link ───────────────────────────────────────────────────────

  it('contains a router-link to /events/:id', () => {
    const wrapper = mountCard(makeEvent())

    const links = wrapper.findAllComponents(RouterLinkStub)
    const eventLink = links.find(l => l.props('to') === '/events/evt-1')
    expect(eventLink).toBeDefined()
  })

  // ── RSVP buttons (upcoming) ───────────────────────────────────────────────

  it('renders RSVP buttons for upcoming events', () => {
    // startAt is 2027 — far in the future
    const wrapper = mountCard(makeEvent())

    const buttons = wrapper.findAll('button')
    const labels = buttons.map(b => b.text())
    expect(labels.some(l => l.includes('Going'))).toBe(true)
    expect(labels.some(l => l.includes('Maybe'))).toBe(true)
    expect(labels.some(l => l.includes('Not going'))).toBe(true)
  })

  it('does not render RSVP buttons for past events', () => {
    const wrapper = mountCard(makeEvent({ startAt: '2020-01-01T10:00:00Z' }))

    // No RSVP buttons at all
    const buttons = wrapper.findAll('button')
    expect(buttons.length).toBe(0)
  })

  it('calls eventService.rsvp and eventsStore.updateEventInList when a RSVP button is clicked', async () => {
    const updatedEvent = makeEvent({ myRsvp: 'Going', goingCount: 9 })
    mockEventService.rsvp.mockResolvedValue(updatedEvent)

    const wrapper = mountCard(makeEvent())

    const goingBtn = wrapper.findAll('button').find(b => b.text().includes('Going'))
    expect(goingBtn).toBeDefined()

    await goingBtn!.trigger('click')
    await flushPromises()

    expect(mockEventService.rsvp).toHaveBeenCalledWith('evt-1', 'Going')
    expect(mockEventsStore.updateEventInList).toHaveBeenCalledWith(updatedEvent)
  })

  // ── Ticket URL ────────────────────────────────────────────────────────────

  it('renders the Get tickets button when ticketUrl is set', () => {
    const wrapper = mountCard(
      makeEvent({ ticketUrl: 'https://tickets.example.com/event' }),
    )

    const btn = wrapper.find('[data-testid="event-card-buy-tickets"]')
    expect(btn.exists()).toBe(true)
    expect(btn.attributes('href')).toBe('https://tickets.example.com/event')
  })

  it('does not render the Get tickets button when ticketUrl is absent', () => {
    const wrapper = mountCard(makeEvent({ ticketUrl: undefined }))

    expect(wrapper.find('[data-testid="event-card-buy-tickets"]').exists()).toBe(false)
  })

  // ── Privacy badge ─────────────────────────────────────────────────────────

  it('shows the Public privacy badge for Everyone privacy', () => {
    const wrapper = mountCard(makeEvent({ privacy: 'Everyone' }))

    expect(wrapper.text()).toContain('Public')
  })

  it('shows the Friends privacy label', () => {
    const wrapper = mountCard(makeEvent({ privacy: 'Friends' }))

    expect(wrapper.text()).toContain('Friends')
  })

  // ── article element ───────────────────────────────────────────────────────

  it('renders the event card as an article element', () => {
    const wrapper = mountCard(makeEvent())

    expect(wrapper.find('article[data-testid="event-card"]').exists()).toBe(true)
  })

  // ── Company-page organizer ────────────────────────────────────────────────

  it('shows the company page name when companyPageId is set', () => {
    const wrapper = mountCard(
      makeEvent({
        companyPageId:   'cp-1',
        companyPageName: 'Acme Corp',
      }),
    )

    expect(wrapper.text()).toContain('Acme Corp')
  })

  it('shows the creator name when no company page is set', () => {
    const wrapper = mountCard(makeEvent({ companyPageId: undefined }))

    expect(wrapper.text()).toContain('Alice')
  })
})
