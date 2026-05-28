import { defineStore } from 'pinia'
import { ref } from 'vue'
import { liveService, type LiveStreamDto, type StartStreamRequest, type StartStreamResponse } from '@/services/liveService'

export const useLiveStore = defineStore('live', () => {
  const streams = ref<LiveStreamDto[]>([])
  const loading = ref(false)
  const error = ref<string | null>(null)

  // Active stream that the current user is hosting (if any)
  const activeStream = ref<StartStreamResponse | null>(null)

  // ── Load active streams ───────────────────────────────────────────────────

  async function loadActiveStreams() {
    loading.value = true
    error.value = null
    try {
      streams.value = await liveService.getActiveStreams()
    } catch (e: unknown) {
      error.value = extractError(e)
    } finally {
      loading.value = false
    }
  }

  // ── Start a stream ────────────────────────────────────────────────────────

  async function startStream(payload: StartStreamRequest): Promise<StartStreamResponse> {
    const response = await liveService.startStream(payload)
    activeStream.value = response
    return response
  }

  // ── End a stream ──────────────────────────────────────────────────────────

  async function endStream(id: string) {
    await liveService.endStream(id)
    activeStream.value = null
    streams.value = streams.value.filter(s => s.id !== id)
  }

  // ── SignalR: stream started (pushed from a friend) ────────────────────────

  function addOrUpdateStream(stream: LiveStreamDto) {
    const idx = streams.value.findIndex(s => s.id === stream.id)
    if (idx !== -1) {
      streams.value[idx] = stream
    } else {
      streams.value.unshift(stream)
    }
  }

  // ── SignalR: stream ended ─────────────────────────────────────────────────

  function removeStream(streamId: string) {
    streams.value = streams.value.filter(s => s.id !== streamId)
  }

  function reset() {
    streams.value = []
    loading.value = false
    error.value = null
    activeStream.value = null
  }

  return {
    streams,
    loading,
    error,
    activeStream,
    loadActiveStreams,
    startStream,
    endStream,
    addOrUpdateStream,
    removeStream,
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
