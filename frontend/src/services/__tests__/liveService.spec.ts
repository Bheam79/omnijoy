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

// Import after mock so liveService picks up the mocked api
import { liveService } from '@/services/liveService'
import type { StartStreamRequest, LiveStreamDto } from '@/services/liveService'

const makeStream = (overrides: Partial<LiveStreamDto> = {}): LiveStreamDto => ({
  id:          'stream-1',
  host:        { id: 'u-1', displayName: 'Alice', avatarUrl: undefined },
  title:       'My Live Stream',
  status:      'Live',
  privacy:     'Everyone',
  viewerCount: 0,
  hlsUrl:      'https://cdn.example.com/live/stream-1.m3u8',
  ...overrides,
})

describe('liveService', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  // ── startStream ──────────────────────────────────────────────────────────

  it('startStream → POSTs /api/live/start with payload', async () => {
    const request: StartStreamRequest = { title: 'Test Stream', privacy: 'Everyone' }
    const response = {
      id:        'stream-1',
      title:     'Test Stream',
      streamKey: 'sk-abc123',
      ingestUrl: 'rtmp://server:1935/live/sk-abc123',
      hlsUrl:    'https://cdn.example.com/live/stream-1.m3u8',
    }
    mockApi.post.mockResolvedValue({ data: response })

    const result = await liveService.startStream(request)

    expect(mockApi.post).toHaveBeenCalledWith('/api/live/start', request)
    expect(result.streamKey).toBe('sk-abc123')
    expect(result.ingestUrl).toBe('rtmp://server:1935/live/sk-abc123')
  })

  it('startStream → works with Friends privacy', async () => {
    const request: StartStreamRequest = { title: 'Private Stream', privacy: 'Friends' }
    mockApi.post.mockResolvedValue({
      data: { id: 's-2', title: 'Private Stream', streamKey: 'sk-xyz', ingestUrl: 'rtmp://s', hlsUrl: 'hls://s' },
    })

    await liveService.startStream(request)

    expect(mockApi.post).toHaveBeenCalledWith('/api/live/start', request)
  })

  it('startStream → propagates errors', async () => {
    mockApi.post.mockRejectedValue(new Error('unauthorized'))

    await expect(
      liveService.startStream({ title: 'Stream', privacy: 'Everyone' }),
    ).rejects.toThrow('unauthorized')
  })

  // ── endStream ────────────────────────────────────────────────────────────

  it('endStream → POSTs /api/live/:id/end', async () => {
    mockApi.post.mockResolvedValue({ data: {} })

    await liveService.endStream('stream-1')

    expect(mockApi.post).toHaveBeenCalledWith('/api/live/stream-1/end')
  })

  it('endStream → returns nothing (void)', async () => {
    mockApi.post.mockResolvedValue({ data: {} })

    const result = await liveService.endStream('stream-1')

    expect(result).toBeUndefined()
  })

  it('endStream → propagates errors', async () => {
    mockApi.post.mockRejectedValue(new Error('forbidden'))

    await expect(liveService.endStream('stream-1')).rejects.toThrow('forbidden')
  })

  // ── getActiveStreams ──────────────────────────────────────────────────────

  it('getActiveStreams → GETs /api/live/active and returns array', async () => {
    const streams = [makeStream(), makeStream({ id: 'stream-2', title: 'Second Stream' })]
    mockApi.get.mockResolvedValue({ data: streams })

    const result = await liveService.getActiveStreams()

    expect(mockApi.get).toHaveBeenCalledWith('/api/live/active')
    expect(result).toHaveLength(2)
    expect(result[0].id).toBe('stream-1')
    expect(result[1].title).toBe('Second Stream')
  })

  it('getActiveStreams → returns empty array when no active streams', async () => {
    mockApi.get.mockResolvedValue({ data: [] })

    const result = await liveService.getActiveStreams()

    expect(result).toEqual([])
  })

  it('getActiveStreams → propagates errors', async () => {
    mockApi.get.mockRejectedValue(new Error('server error'))

    await expect(liveService.getActiveStreams()).rejects.toThrow('server error')
  })

  // ── getStream ────────────────────────────────────────────────────────────

  it('getStream → GETs /api/live/:id and returns stream dto', async () => {
    const stream = makeStream({ viewerCount: 42 })
    mockApi.get.mockResolvedValue({ data: stream })

    const result = await liveService.getStream('stream-1')

    expect(mockApi.get).toHaveBeenCalledWith('/api/live/stream-1')
    expect(result.viewerCount).toBe(42)
    expect(result.status).toBe('Live')
  })

  it('getStream → works for Ended stream status', async () => {
    const stream = makeStream({ status: 'Ended', hlsUrl: undefined, endedAt: '2024-06-01T13:00:00Z' })
    mockApi.get.mockResolvedValue({ data: stream })

    const result = await liveService.getStream('stream-1')

    expect(result.status).toBe('Ended')
    expect(result.endedAt).toBe('2024-06-01T13:00:00Z')
  })

  it('getStream → propagates 404 errors', async () => {
    const err = Object.assign(new Error('Not Found'), { response: { status: 404 } })
    mockApi.get.mockRejectedValue(err)

    await expect(liveService.getStream('nonexistent')).rejects.toThrow('Not Found')
  })
})
