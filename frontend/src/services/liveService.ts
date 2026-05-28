import api from './api'

// ── DTOs (mirrors backend LiveDtos.cs) ────────────────────────────────────────

export interface LiveStreamHost {
  id: string
  displayName: string
  avatarUrl?: string
}

export interface LiveStreamDto {
  id: string
  host: LiveStreamHost
  title: string
  /** "Scheduled" | "Live" | "Ended" */
  status: 'Scheduled' | 'Live' | 'Ended'
  /** "Everyone" | "Friends" */
  privacy: 'Everyone' | 'Friends'
  viewerCount: number
  /** Proxied HLS URL for the video player (only set when status is "Live"). */
  hlsUrl?: string
  startedAt?: string
  endedAt?: string
}

export interface StartStreamRequest {
  title: string
  privacy: 'Everyone' | 'Friends'
}

export interface StartStreamResponse {
  id: string
  title: string
  streamKey: string
  /** RTMP ingest URL for OBS (e.g. rtmp://server:1935/live/KEY) */
  ingestUrl: string
  /** HLS playback URL (proxied via the API) */
  hlsUrl: string
}

// ── Service ───────────────────────────────────────────────────────────────────

export const liveService = {
  /** Start a new stream; returns stream key + ingest URL for the host. */
  async startStream(payload: StartStreamRequest): Promise<StartStreamResponse> {
    const { data } = await api.post<StartStreamResponse>('/api/live/start', payload)
    return data
  },

  /** End an active stream. Only the host may call this. */
  async endStream(id: string): Promise<void> {
    await api.post(`/api/live/${id}/end`)
  },

  /** List currently active streams visible to the current user. */
  async getActiveStreams(): Promise<LiveStreamDto[]> {
    const { data } = await api.get<LiveStreamDto[]>('/api/live/active')
    return data
  },

  /** Get details for a single stream. */
  async getStream(id: string): Promise<LiveStreamDto> {
    const { data } = await api.get<LiveStreamDto>(`/api/live/${id}`)
    return data
  },
}
