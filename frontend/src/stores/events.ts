import { defineStore } from 'pinia'
import { ref } from 'vue'
import { eventService, type EventDto, type CreateEventPayload } from '@/services/eventService'

export const useEventsStore = defineStore('events', () => {
  const events = ref<EventDto[]>([])
  const page = ref(1)
  const hasMore = ref(true)
  const loading = ref(false)
  const loadingMore = ref(false)
  const error = ref<string | null>(null)
  const activeFilter = ref<'mine' | 'friends' | 'company' | null>(null)

  // ── Load first page ───────────────────────────────────────────────────────

  async function loadEvents(filter: 'mine' | 'friends' | 'company' | null = null) {
    loading.value = true
    error.value = null
    page.value = 1
    hasMore.value = true
    activeFilter.value = filter
    try {
      const result = await eventService.getEvents({ filter, page: 1 })
      events.value = result.items
      hasMore.value = result.hasMore
      page.value = 1
    } catch (e: unknown) {
      error.value = extractError(e)
    } finally {
      loading.value = false
    }
  }

  // ── Load next page ────────────────────────────────────────────────────────

  async function loadMore() {
    if (!hasMore.value || loadingMore.value) return
    loadingMore.value = true
    try {
      const nextPage = page.value + 1
      const result = await eventService.getEvents({ filter: activeFilter.value, page: nextPage })
      events.value.push(...result.items)
      hasMore.value = result.hasMore
      page.value = nextPage
    } catch (e: unknown) {
      error.value = extractError(e)
    } finally {
      loadingMore.value = false
    }
  }

  // ── Create event ──────────────────────────────────────────────────────────

  async function createEvent(payload: CreateEventPayload): Promise<EventDto> {
    const ev = await eventService.createEvent(payload)
    prependEvent(ev)
    return ev
  }

  // ── Delete event ──────────────────────────────────────────────────────────

  async function deleteEvent(id: string) {
    await eventService.deleteEvent(id)
    events.value = events.value.filter(e => e.id !== id)
  }

  // ── SignalR: push new event (deduplication guard) ─────────────────────────

  function prependEvent(ev: EventDto) {
    if (events.value.some(e => e.id === ev.id)) return
    events.value.unshift(ev)
  }

  // ── Update a single event in the list (e.g. after RSVP) ──────────────────

  function updateEventInList(ev: EventDto) {
    const idx = events.value.findIndex(e => e.id === ev.id)
    if (idx !== -1) events.value[idx] = ev
  }

  function reset() {
    events.value = []
    page.value = 1
    hasMore.value = true
    loading.value = false
    loadingMore.value = false
    error.value = null
    activeFilter.value = null
  }

  return {
    events,
    page,
    hasMore,
    loading,
    loadingMore,
    error,
    activeFilter,
    loadEvents,
    loadMore,
    createEvent,
    deleteEvent,
    prependEvent,
    updateEventInList,
    reset,
  }
})

function extractError(e: unknown): string {
  if (typeof e === 'object' && e !== null) {
    const axiosError = e as { response?: { data?: { error?: string } }; message?: string }
    return axiosError.response?.data?.error ?? axiosError.message ?? 'An unexpected error occurred.'
  }
  return 'An unexpected error occurred.'
}
