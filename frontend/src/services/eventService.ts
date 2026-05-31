import api from './api'

// ── DTOs (mirrors backend EventDtos.cs) ──────────────────────────────────────

export interface EventCreator {
  id: string
  displayName: string
  avatarUrl?: string
}

export interface EventAttendeeDto {
  userId: string
  displayName: string
  avatarUrl?: string
  rsvp: 'Going' | 'Maybe' | 'NotGoing'
}

export interface EventDto {
  id: string
  creator: EventCreator
  companyPageId?: string
  title: string
  description?: string
  startAt: string
  endAt?: string
  location?: string
  coverImageUrl?: string
  privacy: 'Everyone' | 'FriendsOfFriends' | 'Friends' | 'OnlyMe'
  myRsvp?: 'Going' | 'Maybe' | 'NotGoing'
  goingCount: number
  maybeCount: number
  notGoingCount: number
  createdAt: string
}

export interface EventsPageResult {
  items: EventDto[]
  page: number
  pageSize: number
  hasMore: boolean
}

export interface EventAttendeesResult {
  going: EventAttendeeDto[]
  maybe: EventAttendeeDto[]
  notGoing: EventAttendeeDto[]
}

export interface CreateEventPayload {
  title: string
  description?: string
  startAt: string   // ISO date-time string
  endAt?: string
  location?: string
  privacy: 'Everyone' | 'FriendsOfFriends' | 'Friends' | 'OnlyMe'
  companyPageId?: string
  coverImage?: File
}

export interface UpdateEventPayload {
  title?: string
  description?: string
  startAt?: string
  endAt?: string
  location?: string
  privacy?: string
  coverImage?: File
}

export type RsvpStatus = 'Going' | 'Maybe' | 'NotGoing'

// ── Service ───────────────────────────────────────────────────────────────────

export const eventService = {
  async createEvent(payload: CreateEventPayload): Promise<EventDto> {
    const form = new FormData()
    form.append('title', payload.title)
    if (payload.description)   form.append('description', payload.description)
    form.append('startAt',     payload.startAt)
    if (payload.endAt)         form.append('endAt', payload.endAt)
    if (payload.location)      form.append('location', payload.location)
    form.append('privacy',     payload.privacy)
    if (payload.companyPageId) form.append('companyPageId', payload.companyPageId)
    if (payload.coverImage)    form.append('coverImage', payload.coverImage)

    const { data } = await api.post<EventDto>('/api/events', form, {
      headers: { 'Content-Type': 'multipart/form-data' },
    })
    return data
  },

  async getEvents(params: {
    filter?: 'mine' | 'friends' | 'company' | null
    page?: number
    pageSize?: number
    companyPageId?: string
  } = {}): Promise<EventsPageResult> {
    const { data } = await api.get<EventsPageResult>('/api/events', {
      params: {
        filter:        params.filter        ?? undefined,
        page:          params.page          ?? 1,
        pageSize:      params.pageSize      ?? 20,
        companyPageId: params.companyPageId ?? undefined,
      },
    })
    return data
  },

  /**
   * Public events feed (no authentication required). Optionally filtered by
   * a location substring — powers the unauthenticated front page.
   */
  async getPublicEvents(params: {
    location?: string | null
    page?: number
    pageSize?: number
  } = {}): Promise<EventsPageResult> {
    const { data } = await api.get<EventsPageResult>('/api/events/public', {
      params: {
        location: params.location && params.location.trim()
          ? params.location.trim()
          : undefined,
        page:     params.page     ?? 1,
        pageSize: params.pageSize ?? 20,
      },
    })
    return data
  },

  async getEvent(id: string): Promise<EventDto> {
    const { data } = await api.get<EventDto>(`/api/events/${id}`)
    return data
  },

  async updateEvent(id: string, payload: UpdateEventPayload): Promise<EventDto> {
    const form = new FormData()
    if (payload.title       !== undefined) form.append('title',       payload.title)
    if (payload.description !== undefined) form.append('description', payload.description ?? '')
    if (payload.startAt     !== undefined) form.append('startAt',     payload.startAt)
    if (payload.endAt       !== undefined) form.append('endAt',       payload.endAt)
    if (payload.location    !== undefined) form.append('location',    payload.location ?? '')
    if (payload.privacy     !== undefined) form.append('privacy',     payload.privacy)
    if (payload.coverImage)               form.append('coverImage',   payload.coverImage)

    const { data } = await api.put<EventDto>(`/api/events/${id}`, form, {
      headers: { 'Content-Type': 'multipart/form-data' },
    })
    return data
  },

  async deleteEvent(id: string): Promise<void> {
    await api.delete(`/api/events/${id}`)
  },

  async rsvp(id: string, status: RsvpStatus): Promise<EventDto> {
    const { data } = await api.post<EventDto>(`/api/events/${id}/rsvp`, { status })
    return data
  },

  async getAttendees(id: string): Promise<EventAttendeesResult> {
    const { data } = await api.get<EventAttendeesResult>(`/api/events/${id}/attendees`)
    return data
  },
}
