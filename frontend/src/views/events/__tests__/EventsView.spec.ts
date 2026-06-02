import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { RouterLinkStub } from '@vue/test-utils'

// ── SignalR mock (EventsView uses HubConnectionBuilder) ───────────────────────

vi.mock('@microsoft/signalr', () => ({
  HubConnectionBuilder: vi.fn(() => ({
    withUrl:              vi.fn().mockReturnThis(),
    withAutomaticReconnect: vi.fn().mockReturnThis(),
    configureLogging:     vi.fn().mockReturnThis(),
    build: vi.fn(() => ({
      on:    vi.fn(),
      start: vi.fn().mockResolvedValue(undefined),
      stop:  vi.fn().mockResolvedValue(undefined),
    })),
  })),
  LogLevel: { Warning: 2 },
}))

// ── Auth store mock ────────────────────────────────────────────────────────────

const mockAuth = vi.hoisted(() => ({
  accessToken:     null as string | null,
  isAuthenticated: true,
}))

vi.mock('@/stores/auth', () => ({
  useAuthStore: () => mockAuth,
}))

// ── Events store mock ─────────────────────────────────────────────────────────

const mockEventsStore = vi.hoisted(() => ({
  events:      [] as import('@/services/eventService').EventDto[],
  loading:     false,
  loadingMore: false,
  hasMore:     false,
  error:       null as string | null,
  loadEvents:  vi.fn().mockResolvedValue(undefined),
  loadMore:    vi.fn().mockResolvedValue(undefined),
  prependEvent: vi.fn(),
  reset:       vi.fn(),
}))

vi.mock('@/stores/events', () => ({
  useEventsStore: () => mockEventsStore,
}))

// ── EventCard stub ────────────────────────────────────────────────────────────

vi.mock('@/components/events/EventCard.vue', () => ({
  default: {
    name: 'EventCard',
    props: ['event'],
    template: '<div data-testid="event-card-stub">{{ event.title }}</div>',
  },
}))

// ── EventCreateModal stub ─────────────────────────────────────────────────────

vi.mock('@/components/events/EventCreateModal.vue', () => ({
  default: {
    name: 'EventCreateModal',
    emits: ['close', 'created'],
    template: '<div data-testid="event-create-modal-stub" />',
  },
}))

// Import after mocks
import EventsView from '../EventsView.vue'
import type { EventDto } from '@/services/eventService'

// ── Fixtures ──────────────────────────────────────────────────────────────────

function makeEvent(id: string, title: string): EventDto {
  return {
    id,
    creator:      { id: 'u1', displayName: 'Alice' },
    title,
    startAt:      '2027-01-01T10:00:00Z',
    privacy:      'Everyone',
    postingPolicy: 'OrganizerOnly',
    goingCount:   5,
    maybeCount:   2,
    notGoingCount: 0,
    createdAt:    '2026-01-01T00:00:00Z',
  }
}

// ── Mount helper ──────────────────────────────────────────────────────────────

function mountView() {
  return mount(EventsView, {
    global: {
      plugins: [createPinia()],
      stubs:   { RouterLink: RouterLinkStub },
    },
  })
}

// ── Tests ─────────────────────────────────────────────────────────────────────

describe('EventsView', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.clearAllMocks()
    mockAuth.accessToken     = null
    mockAuth.isAuthenticated = true
    mockEventsStore.events      = []
    mockEventsStore.loading     = false
    mockEventsStore.loadingMore = false
    mockEventsStore.hasMore     = false
    mockEventsStore.error       = null
    mockEventsStore.loadEvents.mockResolvedValue(undefined)
  })

  // ── Load events on mount ──────────────────────────────────────────────────

  it('calls eventsStore.loadEvents(null) on mount', async () => {
    mountView()
    await flushPromises()

    expect(mockEventsStore.loadEvents).toHaveBeenCalledOnce()
    expect(mockEventsStore.loadEvents).toHaveBeenCalledWith(null)
  })

  // ── Loading skeleton ──────────────────────────────────────────────────────

  it('shows the loading skeleton while loading is true', () => {
    mockEventsStore.loading = true
    const wrapper = mountView()

    // Skeletons are rendered via v-if="eventsStore.loading"
    expect(wrapper.find('.animate-pulse').exists()).toBe(true)
  })

  it('hides the skeleton once loading is false', async () => {
    mockEventsStore.loading = false
    const wrapper = mountView()
    await flushPromises()

    expect(wrapper.find('.animate-pulse').exists()).toBe(false)
  })

  // ── Events grid ───────────────────────────────────────────────────────────

  it('renders an EventCard for each event in the list', async () => {
    mockEventsStore.events = [
      makeEvent('e1', 'Vue Conf'),
      makeEvent('e2', 'React Summit'),
    ]
    const wrapper = mountView()
    await flushPromises()

    const cards = wrapper.findAll('[data-testid="event-card-stub"]')
    expect(cards).toHaveLength(2)
    expect(cards[0].text()).toBe('Vue Conf')
    expect(cards[1].text()).toBe('React Summit')
  })

  // ── Empty state ───────────────────────────────────────────────────────────

  it('shows the empty state when there are no events', async () => {
    mockEventsStore.events = []
    const wrapper = mountView()
    await flushPromises()

    expect(wrapper.text()).toContain('No upcoming events')
  })

  it('does not render the events grid when events list is empty', async () => {
    mockEventsStore.events = []
    const wrapper = mountView()
    await flushPromises()

    expect(wrapper.find('[data-testid="events-list"]').exists()).toBe(false)
  })

  // ── Create event modal ────────────────────────────────────────────────────

  it('does not show EventCreateModal by default', () => {
    const wrapper = mountView()

    expect(wrapper.find('[data-testid="event-create-modal-stub"]').exists()).toBe(false)
  })

  it('opens EventCreateModal when the Create Event button is clicked', async () => {
    const wrapper = mountView()
    await flushPromises()

    const btn = wrapper.find('[data-testid="create-event-button"]')
    expect(btn.exists()).toBe(true)

    await btn.trigger('click')
    expect(wrapper.find('[data-testid="event-create-modal-stub"]').exists()).toBe(true)
  })

  it('also opens the modal from the empty-state "Create an Event" button', async () => {
    mockEventsStore.events = []
    const wrapper = mountView()
    await flushPromises()

    // Find the "Create an Event" button inside the empty state
    const buttons = wrapper.findAll('button')
    const createBtn = buttons.find(b => b.text().includes('Create an Event'))
    expect(createBtn).toBeDefined()

    await createBtn!.trigger('click')
    expect(wrapper.find('[data-testid="event-create-modal-stub"]').exists()).toBe(true)
  })

  // ── Error state ───────────────────────────────────────────────────────────

  it('renders the error message when eventsStore.error is set', async () => {
    mockEventsStore.error = 'Failed to load events.'
    const wrapper = mountView()
    await flushPromises()

    expect(wrapper.text()).toContain('Failed to load events.')
  })

  // ── Filter tabs ───────────────────────────────────────────────────────────

  it('renders All/Mine/Friends/Company filter tabs', () => {
    const wrapper = mountView()

    const text = wrapper.text()
    expect(text).toContain('All')
    expect(text).toContain('Mine')
    expect(text).toContain('Friends')
    expect(text).toContain('Company')
  })

  it('calls loadEvents with the correct filter when a tab is clicked', async () => {
    const wrapper = mountView()
    await flushPromises()
    mockEventsStore.loadEvents.mockClear()

    const buttons = wrapper.findAll('button')
    const mineBtn = buttons.find(b => b.text() === 'Mine')
    expect(mineBtn).toBeDefined()

    await mineBtn!.trigger('click')
    expect(mockEventsStore.loadEvents).toHaveBeenCalledWith('mine')
  })

  // ── Load more ─────────────────────────────────────────────────────────────

  it('shows Load more button when hasMore is true and not loading', async () => {
    mockEventsStore.events  = [makeEvent('e1', 'Vue Conf')]
    mockEventsStore.hasMore = true
    const wrapper = mountView()
    await flushPromises()

    const loadMoreBtn = wrapper.findAll('button').find(b => b.text().includes('Load more'))
    expect(loadMoreBtn).toBeDefined()
  })

  it('calls eventsStore.loadMore() when Load more is clicked', async () => {
    mockEventsStore.events  = [makeEvent('e1', 'Vue Conf')]
    mockEventsStore.hasMore = true
    const wrapper = mountView()
    await flushPromises()

    const loadMoreBtn = wrapper.findAll('button').find(b => b.text().includes('Load more'))
    await loadMoreBtn!.trigger('click')

    expect(mockEventsStore.loadMore).toHaveBeenCalledOnce()
  })
})
