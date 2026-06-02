import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { RouterLinkStub } from '@vue/test-utils'

// ── SignalR mock ───────────────────────────────────────────────────────────────

vi.mock('@microsoft/signalr', () => ({
  HubConnectionBuilder: class {
    withUrl()                { return this }
    withAutomaticReconnect() { return this }
    configureLogging()       { return this }
    build() {
      return {
        on:    vi.fn(),
        start: vi.fn().mockResolvedValue(undefined),
        stop:  vi.fn().mockResolvedValue(undefined),
      }
    }
  },
  LogLevel: { Warning: 2 },
}))

// ── Auth store mock ────────────────────────────────────────────────────────────

const mockAuth = vi.hoisted(() => ({
  accessToken:     'test-token' as string | null,
  isAuthenticated: true,
  user:            { id: 'u1', displayName: 'Alice' } as { id: string; displayName: string } | null,
}))

vi.mock('@/stores/auth', () => ({
  useAuthStore: () => mockAuth,
}))

// ── Live store mock ────────────────────────────────────────────────────────────

const mockLiveStore = vi.hoisted(() => ({
  streams:          [] as import('@/services/liveService').LiveStreamDto[],
  loading:          false,
  error:            null as string | null,
  loadActiveStreams: vi.fn().mockResolvedValue(undefined),
  removeStream:     vi.fn(),
}))

vi.mock('@/stores/live', () => ({
  useLiveStore: () => mockLiveStore,
}))

// ── Router mock ───────────────────────────────────────────────────────────────

const mockRouter = vi.hoisted(() => ({
  push: vi.fn(),
}))

vi.mock('vue-router', async (importOriginal) => {
  const actual = await importOriginal<typeof import('vue-router')>()
  return {
    ...actual,
    useRouter:  () => mockRouter,
    RouterLink: RouterLinkStub,
  }
})

// ── StreamCard stub ───────────────────────────────────────────────────────────

vi.mock('@/components/live/StreamCard.vue', () => ({
  default: {
    name:     'StreamCard',
    props:    ['stream'],
    template: '<div data-testid="stream-card-stub">{{ stream.title }}</div>',
  },
}))

// ── GoLiveModal stub ──────────────────────────────────────────────────────────

vi.mock('@/components/live/GoLiveModal.vue', () => ({
  default: {
    name:     'GoLiveModal',
    emits:    ['close', 'started'],
    template: '<div data-testid="go-live-modal-stub" />',
  },
}))

// Import after mocks
import LiveView from '../LiveView.vue'
import type { LiveStreamDto } from '@/services/liveService'

// ── Fixtures ──────────────────────────────────────────────────────────────────

function makeStream(id: string, title: string): LiveStreamDto {
  return {
    id,
    host:        { id: 'u1', displayName: 'Alice' },
    title,
    status:      'Live',
    privacy:     'Everyone',
    viewerCount: 5,
    hlsUrl:      `/hubs/live/${id}/index.m3u8`,
    startedAt:   '2026-06-01T10:00:00Z',
  }
}

// ── Mount helper ──────────────────────────────────────────────────────────────

function mountView() {
  return mount(LiveView, {
    global: {
      plugins: [createPinia()],
      stubs:   { RouterLink: RouterLinkStub },
    },
  })
}

// ── Tests ─────────────────────────────────────────────────────────────────────

describe('LiveView', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.clearAllMocks()
    mockAuth.accessToken     = 'test-token'
    mockAuth.isAuthenticated = true
    mockLiveStore.streams    = []
    mockLiveStore.loading    = false
    mockLiveStore.error      = null
    mockLiveStore.loadActiveStreams.mockResolvedValue(undefined)
  })

  // ── Fetches on mount ──────────────────────────────────────────────────────

  it('calls liveStore.loadActiveStreams() on mount', async () => {
    mountView()
    await flushPromises()

    expect(mockLiveStore.loadActiveStreams).toHaveBeenCalledOnce()
  })

  // ── Loading skeleton ──────────────────────────────────────────────────────

  it('shows the loading skeleton while loading is true', () => {
    mockLiveStore.loading = true
    const wrapper = mountView()

    // The skeleton cards have a unique 'aspect-video bg-slate-700' inner div
    expect(wrapper.find('.aspect-video.bg-slate-700').exists()).toBe(true)
  })

  it('hides the skeleton once loading is false', async () => {
    mockLiveStore.loading = false
    const wrapper = mountView()
    await flushPromises()

    expect(wrapper.find('.aspect-video.bg-slate-700').exists()).toBe(false)
  })

  // ── Error state ───────────────────────────────────────────────────────────

  it('renders the error message when liveStore.error is set', async () => {
    mockLiveStore.error = 'Failed to load streams.'
    const wrapper = mountView()
    await flushPromises()

    expect(wrapper.text()).toContain('Failed to load streams.')
  })

  // ── Empty state ───────────────────────────────────────────────────────────

  it('shows the empty state when there are no streams', async () => {
    mockLiveStore.streams = []
    const wrapper = mountView()
    await flushPromises()

    expect(wrapper.text()).toContain('No live streams right now')
  })

  it('does not render StreamCard components when streams list is empty', async () => {
    mockLiveStore.streams = []
    const wrapper = mountView()
    await flushPromises()

    expect(wrapper.find('[data-testid="stream-card-stub"]').exists()).toBe(false)
  })

  // ── Stream grid ───────────────────────────────────────────────────────────

  it('renders a StreamCard for each stream in the list', async () => {
    mockLiveStore.streams = [
      makeStream('s1', 'Coding with Alice'),
      makeStream('s2', 'Gaming with Bob'),
    ]
    const wrapper = mountView()
    await flushPromises()

    const cards = wrapper.findAll('[data-testid="stream-card-stub"]')
    expect(cards).toHaveLength(2)
    expect(cards[0].text()).toBe('Coding with Alice')
    expect(cards[1].text()).toBe('Gaming with Bob')
  })

  it('does not show the empty state when there are streams', async () => {
    mockLiveStore.streams = [makeStream('s1', 'Stream 1')]
    const wrapper = mountView()
    await flushPromises()

    expect(wrapper.text()).not.toContain('No live streams right now')
  })

  // ── Go Live button ────────────────────────────────────────────────────────

  it('renders the Go Live button in the header', () => {
    const wrapper = mountView()

    const btn = wrapper.find('[data-testid="go-live-button"]')
    expect(btn.exists()).toBe(true)
    expect(btn.text()).toContain('Go Live')
  })

  it('GoLiveModal is not rendered by default', () => {
    const wrapper = mountView()

    expect(wrapper.find('[data-testid="go-live-modal-stub"]').exists()).toBe(false)
  })

  it('opens GoLiveModal when the Go Live header button is clicked', async () => {
    const wrapper = mountView()

    const btn = wrapper.find('[data-testid="go-live-button"]')
    await btn.trigger('click')

    expect(wrapper.find('[data-testid="go-live-modal-stub"]').exists()).toBe(true)
  })

  it('also opens GoLiveModal from the empty-state "Start your first stream" button', async () => {
    mockLiveStore.streams = []
    const wrapper = mountView()
    await flushPromises()

    const buttons = wrapper.findAll('button')
    const startBtn = buttons.find(b => b.text().includes('Start your first stream'))
    expect(startBtn).toBeDefined()

    await startBtn!.trigger('click')

    expect(wrapper.find('[data-testid="go-live-modal-stub"]').exists()).toBe(true)
  })

  // ── Heading ───────────────────────────────────────────────────────────────

  it('renders the "Live" page heading', () => {
    const wrapper = mountView()

    expect(wrapper.text()).toContain('Live')
  })

  it('renders the subtitle text', () => {
    const wrapper = mountView()

    expect(wrapper.text()).toContain('Watch friends stream live')
  })
})
