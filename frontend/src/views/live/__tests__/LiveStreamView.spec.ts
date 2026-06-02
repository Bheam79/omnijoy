import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { RouterLinkStub } from '@vue/test-utils'

// ── hls.js mock ───────────────────────────────────────────────────────────────

vi.mock('hls.js', () => {
  const HlsMock = vi.fn(() => ({
    loadSource:  vi.fn(),
    attachMedia: vi.fn(),
    on:          vi.fn(),
    destroy:     vi.fn(),
  }))
  ;(HlsMock as unknown as { isSupported: () => boolean; Events: Record<string, string> }).isSupported = vi.fn(() => true)
  ;(HlsMock as unknown as { isSupported: () => boolean; Events: Record<string, string> }).Events = {
    MANIFEST_PARSED: 'hlsManifestParsed',
  }
  return { default: HlsMock }
})

// ── SignalR mock ───────────────────────────────────────────────────────────────

const mockHubOn    = vi.fn()
const mockHubStart = vi.fn().mockResolvedValue(undefined)
const mockHubStop  = vi.fn().mockResolvedValue(undefined)
const mockHubInvoke = vi.fn().mockResolvedValue(undefined)

vi.mock('@microsoft/signalr', () => ({
  HubConnectionBuilder: class {
    withUrl()                { return this }
    withAutomaticReconnect() { return this }
    configureLogging()       { return this }
    build() {
      return {
        on:     mockHubOn,
        start:  mockHubStart,
        stop:   mockHubStop,
        invoke: mockHubInvoke,
      }
    }
  },
  LogLevel: { Warning: 2 },
}))

// ── Route mock ────────────────────────────────────────────────────────────────

const mockRoute = vi.hoisted(() => ({
  params: { id: 'stream-42' } as Record<string, string>,
}))

vi.mock('vue-router', async (importOriginal) => {
  const actual = await importOriginal<typeof import('vue-router')>()
  return {
    ...actual,
    useRoute:   () => mockRoute,
    useRouter:  () => ({ push: vi.fn(), back: vi.fn() }),
    RouterLink: RouterLinkStub,
  }
})

// ── Live service mock ─────────────────────────────────────────────────────────

const mockLiveService = vi.hoisted(() => ({
  getStream: vi.fn(),
}))

vi.mock('@/services/liveService', async (importOriginal) => {
  const actual = await importOriginal<typeof import('@/services/liveService')>()
  return {
    ...actual,
    liveService: mockLiveService,
  }
})

// ── Live store mock ────────────────────────────────────────────────────────────

const mockLiveStore = vi.hoisted(() => ({
  removeStream: vi.fn(),
  endStream:    vi.fn().mockResolvedValue(undefined),
}))

vi.mock('@/stores/live', () => ({
  useLiveStore: () => mockLiveStore,
}))

// ── Auth store mock ────────────────────────────────────────────────────────────

const mockAuth = vi.hoisted(() => ({
  accessToken: 'test-token' as string | null,
  user:        { id: 'u1', displayName: 'Alice' } as { id: string; displayName: string } | null,
}))

vi.mock('@/stores/auth', () => ({
  useAuthStore: () => mockAuth,
}))

// ── LiveHostPanel stub ────────────────────────────────────────────────────────

vi.mock('@/components/live/LiveHostPanel.vue', () => ({
  default: {
    name:     'LiveHostPanel',
    props:    ['streamId'],
    template: '<div data-testid="live-host-panel-stub" />',
  },
}))

// Import after mocks
import LiveStreamView from '../LiveStreamView.vue'
import type { LiveStreamDto } from '@/services/liveService'

// ── Fixtures ──────────────────────────────────────────────────────────────────

function makeLiveStream(overrides: Partial<LiveStreamDto> = {}): LiveStreamDto {
  return {
    id:          'stream-42',
    host:        { id: 'u99', displayName: 'Bob' },
    title:       'Coding Session',
    status:      'Live',
    privacy:     'Everyone',
    viewerCount: 12,
    hlsUrl:      '/media/live/stream-42/index.m3u8',
    startedAt:   '2026-06-01T12:00:00Z',
    ...overrides,
  }
}

// ── Mount helper ──────────────────────────────────────────────────────────────

function mountView() {
  return mount(LiveStreamView, {
    global: {
      plugins: [createPinia()],
      stubs:   { RouterLink: RouterLinkStub },
    },
  })
}

// ── Tests ─────────────────────────────────────────────────────────────────────

describe('LiveStreamView', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.clearAllMocks()
    mockRoute.params         = { id: 'stream-42' }
    mockAuth.accessToken     = 'test-token'
    mockAuth.user            = { id: 'u1', displayName: 'Alice' }
    mockHubStart.mockResolvedValue(undefined)
    mockLiveService.getStream.mockResolvedValue(makeLiveStream())
  })

  // ── Fetches stream on mount ───────────────────────────────────────────────

  it('calls liveService.getStream with the route param id on mount', async () => {
    mountView()
    await flushPromises()

    expect(mockLiveService.getStream).toHaveBeenCalledOnce()
    expect(mockLiveService.getStream).toHaveBeenCalledWith('stream-42')
  })

  // ── Loading state ─────────────────────────────────────────────────────────

  it('shows the loading spinner before the stream resolves', () => {
    // getStream never resolves during this test
    mockLiveService.getStream.mockReturnValue(new Promise(() => {}))
    const wrapper = mountView()

    expect(wrapper.find('.animate-spin').exists()).toBe(true)
  })

  it('hides the spinner after the stream loads', async () => {
    const wrapper = mountView()
    await flushPromises()

    expect(wrapper.find('.animate-spin').exists()).toBe(false)
  })

  // ── Error state ───────────────────────────────────────────────────────────

  it('shows an error panel when getStream rejects', async () => {
    mockLiveService.getStream.mockRejectedValue(new Error('Not found'))
    const wrapper = mountView()
    await flushPromises()

    expect(wrapper.text()).toContain('Could not load stream')
  })

  // ── Stream title + host ───────────────────────────────────────────────────

  it('renders the stream title', async () => {
    const wrapper = mountView()
    await flushPromises()

    expect(wrapper.text()).toContain('Coding Session')
  })

  it('renders the host display name', async () => {
    const wrapper = mountView()
    await flushPromises()

    expect(wrapper.text()).toContain('Bob')
  })

  // ── Viewer count ──────────────────────────────────────────────────────────

  it('renders the viewer count', async () => {
    const wrapper = mountView()
    await flushPromises()

    expect(wrapper.text()).toContain('12')
    expect(wrapper.text()).toContain('watching')
  })

  // ── Live badge ────────────────────────────────────────────────────────────

  it('shows the LIVE badge when status is Live', async () => {
    const wrapper = mountView()
    await flushPromises()

    expect(wrapper.text()).toContain('LIVE')
  })

  // ── HLS player ────────────────────────────────────────────────────────────

  it('renders a <video> element for a live stream viewed by a non-host', async () => {
    // Auth user (u1) is not the host (u99)
    const wrapper = mountView()
    await flushPromises()

    expect(wrapper.find('video').exists()).toBe(true)
  })

  // ── Stream ended overlay ──────────────────────────────────────────────────

  it('shows "Stream has ended" overlay when status is Ended', async () => {
    mockLiveService.getStream.mockResolvedValue(makeLiveStream({ status: 'Ended', hlsUrl: undefined }))
    const wrapper = mountView()
    await flushPromises()

    expect(wrapper.text()).toContain('Stream has ended')
  })

  it('shows the ENDED badge when status is Ended', async () => {
    mockLiveService.getStream.mockResolvedValue(makeLiveStream({ status: 'Ended', hlsUrl: undefined }))
    const wrapper = mountView()
    await flushPromises()

    expect(wrapper.text()).toContain('ENDED')
    expect(wrapper.text()).not.toContain('LIVE')
  })

  it('does not render <video> when stream is Ended and non-host', async () => {
    mockLiveService.getStream.mockResolvedValue(makeLiveStream({ status: 'Ended', hlsUrl: undefined }))
    const wrapper = mountView()
    await flushPromises()

    // The v-if="stream.status === 'Live'" video element should be absent
    // (only the "Stream has ended" overlay div is shown)
    const videos = wrapper.findAll('video')
    expect(videos).toHaveLength(0)
  })

  // ── Live chat ─────────────────────────────────────────────────────────────

  it('renders the "Live Chat" sidebar header', async () => {
    const wrapper = mountView()
    await flushPromises()

    expect(wrapper.text()).toContain('Live Chat')
  })

  it('shows "Be the first to say something!" when no chat messages exist', async () => {
    const wrapper = mountView()
    await flushPromises()

    expect(wrapper.text()).toContain('Be the first to say something!')
  })

  it('renders the chat input form', async () => {
    const wrapper = mountView()
    await flushPromises()

    const input = wrapper.find('input[placeholder="Say something…"]')
    expect(input.exists()).toBe(true)
  })

  // ── Host view ─────────────────────────────────────────────────────────────

  it('renders LiveHostPanel when the current user is the host', async () => {
    // auth user matches the host id
    mockAuth.user = { id: 'u99', displayName: 'Bob' }
    const wrapper = mountView()
    await flushPromises()

    expect(wrapper.find('[data-testid="live-host-panel-stub"]').exists()).toBe(true)
  })

  it('does not render LiveHostPanel when the current user is not the host', async () => {
    // auth user is u1, host is u99
    mockAuth.user = { id: 'u1', displayName: 'Alice' }
    const wrapper = mountView()
    await flushPromises()

    expect(wrapper.find('[data-testid="live-host-panel-stub"]').exists()).toBe(false)
  })

  it('renders the End Stream button when the current user is the host', async () => {
    mockAuth.user = { id: 'u99', displayName: 'Bob' }
    const wrapper = mountView()
    await flushPromises()

    const buttons = wrapper.findAll('button')
    const endBtn  = buttons.find(b => b.text().includes('End stream'))
    expect(endBtn).toBeDefined()
  })

  it('does not render the End Stream button for non-host viewers', async () => {
    // default mockAuth.user is u1, host is u99
    const wrapper = mountView()
    await flushPromises()

    const buttons = wrapper.findAll('button')
    const endBtn  = buttons.find(b => b.text().includes('End stream'))
    expect(endBtn).toBeUndefined()
  })
})
