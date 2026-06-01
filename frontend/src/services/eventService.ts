import api from './api'
import type { PostDto } from './postService'

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
  companyPageName?: string
  companyPageLogoUrl?: string
  title: string
  description?: string
  startAt: string
  endAt?: string
  location?: string
  locationPlaceId?: string
  locationCity?: string
  locationCountry?: string
  locationLatitude?: number
  locationLongitude?: number
  coverImageUrl?: string
  /** Optional external URL for buying tickets to this event. */
  ticketUrl?: string
  privacy: 'Everyone' | 'FriendsOfFriends' | 'Friends' | 'OnlyMe'
  postingPolicy: 'OrganizerOnly' | 'Everyone'
  myRsvp?: 'Going' | 'Maybe' | 'NotGoing'
  goingCount: number
  maybeCount: number
  notGoingCount: number
  createdAt: string
}

export interface EventPostsPageResult {
  items: PostDto[]
  page: number
  pageSize: number
  hasMore: boolean
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
  locationPlaceId?: string
  locationCity?: string
  locationCountry?: string
  locationLatitude?: number
  locationLongitude?: number
  privacy: 'Everyone' | 'FriendsOfFriends' | 'Friends' | 'OnlyMe'
  postingPolicy?: 'OrganizerOnly' | 'Everyone'
  companyPageId?: string
  /** Optional external URL for purchasing tickets. */
  ticketUrl?: string
  coverImage?: File
}

export interface UpdateEventPayload {
  title?: string
  description?: string
  startAt?: string
  endAt?: string
  location?: string
  locationPlaceId?: string
  locationCity?: string
  locationCountry?: string
  locationLatitude?: number
  locationLongitude?: number
  privacy?: string
  postingPolicy?: string
  /**
   * Optional external URL for buying tickets. Pass an empty string to clear
   * the existing link. Pass `undefined` to leave it unchanged.
   */
  ticketUrl?: string
  coverImage?: File
}

export type RsvpStatus = 'Going' | 'Maybe' | 'NotGoing'

// ── Service ───────────────────────────────────────────────────────────────────

export const eventService = {
  async createEvent(payload: CreateEventPayload): Promise<EventDto> {
    const form = new FormData()
    form.append('title', payload.title)
    if (payload.description)         form.append('description',       payload.description)
    form.append('startAt',           payload.startAt)
    if (payload.endAt)               form.append('endAt',             payload.endAt)
    if (payload.location)            form.append('location',          payload.location)
    if (payload.locationPlaceId)     form.append('locationPlaceId',   payload.locationPlaceId)
    if (payload.locationCity)        form.append('locationCity',      payload.locationCity)
    if (payload.locationCountry)     form.append('locationCountry',   payload.locationCountry)
    if (payload.locationLatitude  != null) form.append('locationLatitude',  String(payload.locationLatitude))
    if (payload.locationLongitude != null) form.append('locationLongitude', String(payload.locationLongitude))
    form.append('privacy',           payload.privacy)
    if (payload.postingPolicy)       form.append('postingPolicy',     payload.postingPolicy)
    if (payload.companyPageId)       form.append('companyPageId',     payload.companyPageId)
    if (payload.ticketUrl)           form.append('ticketUrl',         payload.ticketUrl)
    if (payload.coverImage)          form.append('coverImage',        payload.coverImage)

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
    if (payload.title             !== undefined) form.append('title',             payload.title)
    if (payload.description       !== undefined) form.append('description',       payload.description ?? '')
    if (payload.startAt           !== undefined) form.append('startAt',           payload.startAt)
    if (payload.endAt             !== undefined) form.append('endAt',             payload.endAt)
    if (payload.location          !== undefined) form.append('location',          payload.location ?? '')
    if (payload.locationPlaceId   !== undefined) form.append('locationPlaceId',   payload.locationPlaceId ?? '')
    if (payload.locationCity      !== undefined) form.append('locationCity',      payload.locationCity ?? '')
    if (payload.locationCountry   !== undefined) form.append('locationCountry',   payload.locationCountry ?? '')
    if (payload.locationLatitude  !== undefined) form.append('locationLatitude',  payload.locationLatitude != null ? String(payload.locationLatitude) : '')
    if (payload.locationLongitude !== undefined) form.append('locationLongitude', payload.locationLongitude != null ? String(payload.locationLongitude) : '')
    if (payload.privacy           !== undefined) form.append('privacy',           payload.privacy)
    if (payload.postingPolicy     !== undefined) form.append('postingPolicy',     payload.postingPolicy)
    if (payload.ticketUrl         !== undefined) form.append('ticketUrl',         payload.ticketUrl)
    if (payload.coverImage)                      form.append('coverImage',        payload.coverImage)

    const { data } = await api.put<EventDto>(`/api/events/${id}`, form, {
      headers: { 'Content-Type': 'multipart/form-data' },
    })
    return data
  },

  async getEventPosts(id: string, page = 1, pageSize = 20): Promise<EventPostsPageResult> {
    const { data } = await api.get<EventPostsPageResult>(`/api/events/${id}/posts`, {
      params: { page, pageSize },
    })
    return data
  },

  async createEventPost(id: string, content: string): Promise<PostDto> {
    const { data } = await api.post<PostDto>(`/api/events/${id}/posts`, { content })
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
