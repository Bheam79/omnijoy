import { describe, it, expect, vi, beforeEach } from 'vitest'

// ── Mock the shared axios instance ───────────────────────────────────────────
const mockApi = vi.hoisted(() => ({
  get:    vi.fn(),
  post:   vi.fn(),
  patch:  vi.fn(),
  put:    vi.fn(),
  delete: vi.fn(),
}))

vi.mock('@/services/api', () => ({
  default: mockApi,
  api:     mockApi,
}))

// Import after mock so eventService picks up the mocked api
import { eventService } from '@/services/eventService'

const makeEvent = (overrides = {}) => ({
  id: 'evt-1',
  creator: { id: 'u-1', displayName: 'Alice' },
  title: 'My Event',
  startAt: '2026-07-01T10:00:00Z',
  privacy: 'Everyone' as const,
  postingPolicy: 'OrganizerOnly' as const,
  goingCount: 0,
  maybeCount: 0,
  notGoingCount: 0,
  createdAt: '2026-01-01T00:00:00Z',
  ...overrides,
})

const makeEventsPage = (items = [makeEvent()]) => ({
  items,
  page: 1,
  pageSize: 20,
  hasMore: false,
})

describe('eventService', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  // ── createEvent ──────────────────────────────────────────────────────────

  it('createEvent → POSTs /api/events with FormData', async () => {
    mockApi.post.mockResolvedValue({ data: makeEvent() })

    const result = await eventService.createEvent({
      title: 'My Event',
      startAt: '2026-07-01T10:00:00Z',
      privacy: 'Everyone',
    })

    expect(mockApi.post).toHaveBeenCalledOnce()
    const [url, body] = mockApi.post.mock.calls[0]
    expect(url).toBe('/api/events')
    expect(body).toBeInstanceOf(FormData)
    expect(result.id).toBe('evt-1')
  })

  it('createEvent → FormData contains required fields (title, startAt, privacy)', async () => {
    mockApi.post.mockResolvedValue({ data: makeEvent() })

    await eventService.createEvent({
      title: 'Launch Party',
      startAt: '2026-08-15T18:00:00Z',
      privacy: 'Friends',
    })

    const form: FormData = mockApi.post.mock.calls[0][1]
    expect(form.get('title')).toBe('Launch Party')
    expect(form.get('startAt')).toBe('2026-08-15T18:00:00Z')
    expect(form.get('privacy')).toBe('Friends')
  })

  it('createEvent → appends optional description when provided', async () => {
    mockApi.post.mockResolvedValue({ data: makeEvent() })

    await eventService.createEvent({
      title: 'Event',
      startAt: '2026-07-01T10:00:00Z',
      privacy: 'Everyone',
      description: 'Join us!',
    })

    const form: FormData = mockApi.post.mock.calls[0][1]
    expect(form.get('description')).toBe('Join us!')
  })

  it('createEvent → does not append description when not provided', async () => {
    mockApi.post.mockResolvedValue({ data: makeEvent() })

    await eventService.createEvent({
      title: 'Event',
      startAt: '2026-07-01T10:00:00Z',
      privacy: 'Everyone',
    })

    const form: FormData = mockApi.post.mock.calls[0][1]
    expect(form.get('description')).toBeNull()
  })

  it('createEvent → appends endAt when provided', async () => {
    mockApi.post.mockResolvedValue({ data: makeEvent() })

    await eventService.createEvent({
      title: 'Event',
      startAt: '2026-07-01T10:00:00Z',
      endAt: '2026-07-01T12:00:00Z',
      privacy: 'Everyone',
    })

    const form: FormData = mockApi.post.mock.calls[0][1]
    expect(form.get('endAt')).toBe('2026-07-01T12:00:00Z')
  })

  it('createEvent → appends companyPageId when provided', async () => {
    mockApi.post.mockResolvedValue({ data: makeEvent() })

    await eventService.createEvent({
      title: 'Company Event',
      startAt: '2026-07-01T10:00:00Z',
      privacy: 'Everyone',
      companyPageId: 'page-1',
    })

    const form: FormData = mockApi.post.mock.calls[0][1]
    expect(form.get('companyPageId')).toBe('page-1')
  })

  it('createEvent → appends ticketUrl when provided', async () => {
    mockApi.post.mockResolvedValue({ data: makeEvent() })

    await eventService.createEvent({
      title: 'Paid Event',
      startAt: '2026-07-01T10:00:00Z',
      privacy: 'Everyone',
      ticketUrl: 'https://tickets.example.com',
    })

    const form: FormData = mockApi.post.mock.calls[0][1]
    expect(form.get('ticketUrl')).toBe('https://tickets.example.com')
  })

  it('createEvent → appends coverImage file when provided', async () => {
    mockApi.post.mockResolvedValue({ data: makeEvent() })

    const coverImage = new File(['img'], 'cover.jpg', { type: 'image/jpeg' })
    await eventService.createEvent({
      title: 'Event',
      startAt: '2026-07-01T10:00:00Z',
      privacy: 'Everyone',
      coverImage,
    })

    const form: FormData = mockApi.post.mock.calls[0][1]
    expect(form.get('coverImage')).toBe(coverImage)
  })

  it('createEvent → sends multipart/form-data content-type header', async () => {
    mockApi.post.mockResolvedValue({ data: makeEvent() })

    await eventService.createEvent({ title: 'E', startAt: '2026-01-01T00:00:00Z', privacy: 'OnlyMe' })

    const options = mockApi.post.mock.calls[0][2]
    expect(options?.headers?.['Content-Type']).toBe('multipart/form-data')
  })

  // ── getEvents ────────────────────────────────────────────────────────────

  it('getEvents → GETs /api/events with default params', async () => {
    mockApi.get.mockResolvedValue({ data: makeEventsPage() })

    const result = await eventService.getEvents()

    expect(mockApi.get).toHaveBeenCalledWith('/api/events', {
      params: { filter: undefined, page: 1, pageSize: 20, companyPageId: undefined },
    })
    expect(result.items).toHaveLength(1)
  })

  it('getEvents → passes filter=mine when specified', async () => {
    mockApi.get.mockResolvedValue({ data: makeEventsPage([]) })

    await eventService.getEvents({ filter: 'mine' })

    const { params } = mockApi.get.mock.calls[0][1]
    expect(params.filter).toBe('mine')
  })

  it('getEvents → passes companyPageId when specified', async () => {
    mockApi.get.mockResolvedValue({ data: makeEventsPage([]) })

    await eventService.getEvents({ companyPageId: 'page-1' })

    const { params } = mockApi.get.mock.calls[0][1]
    expect(params.companyPageId).toBe('page-1')
  })

  // ── getPublicEvents ──────────────────────────────────────────────────────

  it('getPublicEvents → GETs /api/events/public with default params', async () => {
    mockApi.get.mockResolvedValue({ data: makeEventsPage() })

    await eventService.getPublicEvents()

    expect(mockApi.get).toHaveBeenCalledWith('/api/events/public', {
      params: { location: undefined, page: 1, pageSize: 20 },
    })
  })

  it('getPublicEvents → includes trimmed location when provided', async () => {
    mockApi.get.mockResolvedValue({ data: makeEventsPage() })

    await eventService.getPublicEvents({ location: '  London  ' })

    const { params } = mockApi.get.mock.calls[0][1]
    expect(params.location).toBe('London')
  })

  it('getPublicEvents → omits location when blank string', async () => {
    mockApi.get.mockResolvedValue({ data: makeEventsPage() })

    await eventService.getPublicEvents({ location: '   ' })

    const { params } = mockApi.get.mock.calls[0][1]
    expect(params.location).toBeUndefined()
  })

  it('getPublicEvents → omits location when null', async () => {
    mockApi.get.mockResolvedValue({ data: makeEventsPage() })

    await eventService.getPublicEvents({ location: null })

    const { params } = mockApi.get.mock.calls[0][1]
    expect(params.location).toBeUndefined()
  })

  // ── getEvent ─────────────────────────────────────────────────────────────

  it('getEvent → GETs /api/events/:id', async () => {
    mockApi.get.mockResolvedValue({ data: makeEvent() })

    const result = await eventService.getEvent('evt-1')

    expect(mockApi.get).toHaveBeenCalledWith('/api/events/evt-1')
    expect(result.id).toBe('evt-1')
  })

  // ── updateEvent ──────────────────────────────────────────────────────────

  it('updateEvent → PUTs /api/events/:id with FormData', async () => {
    mockApi.put.mockResolvedValue({ data: makeEvent({ title: 'Updated' }) })

    const result = await eventService.updateEvent('evt-1', { title: 'Updated' })

    expect(mockApi.put).toHaveBeenCalledOnce()
    const [url, body] = mockApi.put.mock.calls[0]
    expect(url).toBe('/api/events/evt-1')
    expect(body).toBeInstanceOf(FormData)
    expect(result.title).toBe('Updated')
  })

  it('updateEvent → appends title when defined', async () => {
    mockApi.put.mockResolvedValue({ data: makeEvent() })

    await eventService.updateEvent('evt-1', { title: 'New Title' })

    const form: FormData = mockApi.put.mock.calls[0][1]
    expect(form.get('title')).toBe('New Title')
  })

  it('updateEvent → does not append title when undefined', async () => {
    mockApi.put.mockResolvedValue({ data: makeEvent() })

    await eventService.updateEvent('evt-1', { privacy: 'OnlyMe' })

    const form: FormData = mockApi.put.mock.calls[0][1]
    expect(form.get('title')).toBeNull()
  })

  it('updateEvent → appends coverImage when provided', async () => {
    mockApi.put.mockResolvedValue({ data: makeEvent() })

    const coverImage = new File(['img'], 'new-cover.jpg', { type: 'image/jpeg' })
    await eventService.updateEvent('evt-1', { coverImage })

    const form: FormData = mockApi.put.mock.calls[0][1]
    expect(form.get('coverImage')).toBe(coverImage)
  })

  // ── getEventPosts ────────────────────────────────────────────────────────

  it('getEventPosts → GETs /api/events/:id/posts with paging params', async () => {
    const postsPage = { items: [], page: 1, pageSize: 20, hasMore: false }
    mockApi.get.mockResolvedValue({ data: postsPage })

    const result = await eventService.getEventPosts('evt-1', 1, 20)

    expect(mockApi.get).toHaveBeenCalledWith('/api/events/evt-1/posts', {
      params: { page: 1, pageSize: 20 },
    })
    expect(result.items).toEqual([])
  })

  it('getEventPosts → uses default page=1, pageSize=20 when omitted', async () => {
    mockApi.get.mockResolvedValue({ data: { items: [], page: 1, pageSize: 20, hasMore: false } })

    await eventService.getEventPosts('evt-1')

    expect(mockApi.get).toHaveBeenCalledWith('/api/events/evt-1/posts', {
      params: { page: 1, pageSize: 20 },
    })
  })

  // ── createEventPost ──────────────────────────────────────────────────────

  it('createEventPost → POSTs /api/events/:id/posts with content body', async () => {
    const post = { id: 'post-1', content: 'Event update!' }
    mockApi.post.mockResolvedValue({ data: post })

    const result = await eventService.createEventPost('evt-1', 'Event update!')

    expect(mockApi.post).toHaveBeenCalledWith('/api/events/evt-1/posts', { content: 'Event update!' })
    expect(result.id).toBe('post-1')
  })

  // ── deleteEvent ──────────────────────────────────────────────────────────

  it('deleteEvent → DELETEs /api/events/:id', async () => {
    mockApi.delete.mockResolvedValue({ data: undefined })

    await eventService.deleteEvent('evt-1')

    expect(mockApi.delete).toHaveBeenCalledWith('/api/events/evt-1')
  })

  it('deleteEvent → resolves without a return value', async () => {
    mockApi.delete.mockResolvedValue({ data: undefined })

    const result = await eventService.deleteEvent('evt-1')

    expect(result).toBeUndefined()
  })

  // ── rsvp ─────────────────────────────────────────────────────────────────

  it('rsvp → POSTs /api/events/:id/rsvp with status body', async () => {
    const event = makeEvent({ myRsvp: 'Going', goingCount: 1 })
    mockApi.post.mockResolvedValue({ data: event })

    const result = await eventService.rsvp('evt-1', 'Going')

    expect(mockApi.post).toHaveBeenCalledWith('/api/events/evt-1/rsvp', { status: 'Going' })
    expect(result.myRsvp).toBe('Going')
  })

  it('rsvp → works with Maybe status', async () => {
    mockApi.post.mockResolvedValue({ data: makeEvent({ myRsvp: 'Maybe' }) })

    await eventService.rsvp('evt-1', 'Maybe')

    expect(mockApi.post).toHaveBeenCalledWith('/api/events/evt-1/rsvp', { status: 'Maybe' })
  })

  it('rsvp → works with NotGoing status', async () => {
    mockApi.post.mockResolvedValue({ data: makeEvent({ myRsvp: 'NotGoing' }) })

    await eventService.rsvp('evt-1', 'NotGoing')

    expect(mockApi.post).toHaveBeenCalledWith('/api/events/evt-1/rsvp', { status: 'NotGoing' })
  })

  // ── getAttendees ─────────────────────────────────────────────────────────

  it('getAttendees → GETs /api/events/:id/attendees', async () => {
    const attendees = { going: [], maybe: [], notGoing: [] }
    mockApi.get.mockResolvedValue({ data: attendees })

    const result = await eventService.getAttendees('evt-1')

    expect(mockApi.get).toHaveBeenCalledWith('/api/events/evt-1/attendees')
    expect(result.going).toEqual([])
    expect(result.maybe).toEqual([])
    expect(result.notGoing).toEqual([])
  })

  it('getAttendees → returns attendees grouped by rsvp status', async () => {
    const attendees = {
      going:    [{ userId: 'u-1', displayName: 'Alice', rsvp: 'Going' }],
      maybe:    [{ userId: 'u-2', displayName: 'Bob', rsvp: 'Maybe' }],
      notGoing: [],
    }
    mockApi.get.mockResolvedValue({ data: attendees })

    const result = await eventService.getAttendees('evt-1')

    expect(result.going).toHaveLength(1)
    expect(result.maybe).toHaveLength(1)
  })
})
