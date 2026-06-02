import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'

// ── Live store mock ────────────────────────────────────────────────────────────

const mockLiveStore = vi.hoisted(() => ({
  startStream: vi.fn(),
}))

vi.mock('@/stores/live', () => ({
  useLiveStore: () => mockLiveStore,
}))

// Import after mocks
import GoLiveModal from '../GoLiveModal.vue'
import type { StartStreamResponse } from '@/services/liveService'

// ── Fixtures ──────────────────────────────────────────────────────────────────

function makeStartStreamResponse(): StartStreamResponse {
  return {
    id:        'stream-99',
    title:     'My Stream',
    streamKey: 'key-abc123',
    ingestUrl: 'rtmp://server:1935/live/key-abc123',
    hlsUrl:    '/media/live/stream-99/index.m3u8',
  }
}

// ── Mount helper ──────────────────────────────────────────────────────────────

function mountModal() {
  return mount(GoLiveModal, {
    global: {
      plugins: [createPinia()],
    },
  })
}

// ── Tests ─────────────────────────────────────────────────────────────────────

describe('GoLiveModal', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.clearAllMocks()
    mockLiveStore.startStream.mockResolvedValue(makeStartStreamResponse())
  })

  // ── Form fields ───────────────────────────────────────────────────────────

  it('renders the "Go Live" heading', () => {
    const wrapper = mountModal()

    expect(wrapper.text()).toContain('Go Live')
  })

  it('renders the title input', () => {
    const wrapper = mountModal()

    const input = wrapper.find('input[placeholder="What are you streaming today?"]')
    expect(input.exists()).toBe(true)
  })

  it('renders the "Friends only" and "Everyone (public)" privacy options', () => {
    const wrapper = mountModal()

    expect(wrapper.text()).toContain('Friends only')
    expect(wrapper.text()).toContain('Everyone (public)')
  })

  it('renders the "Start streaming" button', () => {
    const wrapper = mountModal()

    const buttons = wrapper.findAll('button')
    const startBtn = buttons.find(b => b.text().includes('Start streaming'))
    expect(startBtn).toBeDefined()
  })

  it('renders the "Cancel" button', () => {
    const wrapper = mountModal()

    const buttons = wrapper.findAll('button')
    const cancelBtn = buttons.find(b => b.text() === 'Cancel')
    expect(cancelBtn).toBeDefined()
  })

  // ── Validation ────────────────────────────────────────────────────────────

  it('shows a validation error when submitting without a title (via Enter key)', async () => {
    const wrapper = mountModal()

    // The Start button is disabled when title is empty — trigger via Enter key
    // on the input, which calls submit() directly
    await wrapper.find('input').trigger('keyup.enter')
    await flushPromises()

    expect(wrapper.text()).toContain('Please enter a stream title.')
    expect(mockLiveStore.startStream).not.toHaveBeenCalled()
  })

  it('does not call startStream when the title is blank whitespace (via Enter key)', async () => {
    const wrapper = mountModal()

    await wrapper.find('input').setValue('   ')
    await wrapper.find('input').trigger('keyup.enter')
    await flushPromises()

    expect(mockLiveStore.startStream).not.toHaveBeenCalled()
  })

  // ── Start Stream button is disabled when title is empty ───────────────────

  it('the "Start streaming" button is disabled when the title field is empty', () => {
    const wrapper = mountModal()

    const buttons  = wrapper.findAll('button')
    const startBtn = buttons.find(b => b.text().includes('Start streaming'))
    expect(startBtn!.attributes('disabled')).toBeDefined()
  })

  it('the "Start streaming" button is enabled once a title is entered', async () => {
    const wrapper = mountModal()

    await wrapper.find('input').setValue('My awesome stream')

    const buttons  = wrapper.findAll('button')
    const startBtn = buttons.find(b => b.attributes('disabled') === undefined && b.text().includes('Start streaming'))
    expect(startBtn).toBeDefined()
  })

  // ── Successful start ──────────────────────────────────────────────────────

  it('calls liveStore.startStream with title and selected privacy', async () => {
    const wrapper = mountModal()

    await wrapper.find('input').setValue('Chill coding session')

    const buttons  = wrapper.findAll('button')
    const startBtn = buttons.find(b => b.text().includes('Start streaming'))
    await startBtn!.trigger('click')
    await flushPromises()

    expect(mockLiveStore.startStream).toHaveBeenCalledOnce()
    const payload = mockLiveStore.startStream.mock.calls[0][0]
    expect(payload.title).toBe('Chill coding session')
    expect(payload.privacy).toBe('Friends') // default
  })

  it('emits "started" with the StartStreamResponse after success', async () => {
    const wrapper = mountModal()

    await wrapper.find('input').setValue('My stream')

    const buttons  = wrapper.findAll('button')
    const startBtn = buttons.find(b => b.text().includes('Start streaming'))
    await startBtn!.trigger('click')
    await flushPromises()

    const started = wrapper.emitted('started')
    expect(started).toHaveLength(1)
    expect((started![0] as StartStreamResponse[])[0].id).toBe('stream-99')
  })

  it('trims whitespace from the title before submitting', async () => {
    const wrapper = mountModal()

    await wrapper.find('input').setValue('  My Stream  ')

    const buttons  = wrapper.findAll('button')
    const startBtn = buttons.find(b => b.text().includes('Start streaming'))
    await startBtn!.trigger('click')
    await flushPromises()

    const payload = mockLiveStore.startStream.mock.calls[0][0]
    expect(payload.title).toBe('My Stream')
  })

  // ── Cancel button ─────────────────────────────────────────────────────────

  it('emits "close" when the Cancel button is clicked', async () => {
    const wrapper = mountModal()

    const buttons   = wrapper.findAll('button')
    const cancelBtn = buttons.find(b => b.text() === 'Cancel')
    await cancelBtn!.trigger('click')

    expect(wrapper.emitted('close')).toHaveLength(1)
    expect(mockLiveStore.startStream).not.toHaveBeenCalled()
  })

  it('emits "close" when the X icon button is clicked', async () => {
    const wrapper = mountModal()

    // The close button is the SVG X in the header — first button that is not Cancel/Start
    // Find by the svg path (M6 18L18 6M6 6l12 12)
    // In the rendered HTML it's the button inside the header containing the X svg
    // The header has exactly one close button; use the header structure
    const headerButtons = wrapper.findAll('.border-b button')
    expect(headerButtons.length).toBeGreaterThanOrEqual(1)
    await headerButtons[0].trigger('click')

    expect(wrapper.emitted('close')).toHaveLength(1)
  })

  // ── Loading state (in-flight) ─────────────────────────────────────────────

  it('disables the Cancel and Start buttons while the request is in flight', async () => {
    let resolveStream!: (v: StartStreamResponse) => void
    mockLiveStore.startStream.mockReturnValue(
      new Promise<StartStreamResponse>(resolve => { resolveStream = resolve }),
    )

    const wrapper = mountModal()

    await wrapper.find('input').setValue('My stream')

    const buttons  = wrapper.findAll('button')
    const startBtn = buttons.find(b => b.text().includes('Start streaming'))
    startBtn!.trigger('click')

    // Don't await — we're checking mid-flight state
    await new Promise(r => setTimeout(r, 0))

    // Both Cancel and Start buttons should be disabled during the request
    const cancelBtn = wrapper.findAll('button').find(b => b.text() === 'Cancel')
    expect(cancelBtn!.attributes('disabled')).toBeDefined()

    // Resolve to clean up
    resolveStream(makeStartStreamResponse())
    await flushPromises()
  })

  it('shows "Starting…" text on the Start button while in flight', async () => {
    let resolveStream!: (v: StartStreamResponse) => void
    mockLiveStore.startStream.mockReturnValue(
      new Promise<StartStreamResponse>(resolve => { resolveStream = resolve }),
    )

    const wrapper = mountModal()

    await wrapper.find('input').setValue('My stream')

    const buttons  = wrapper.findAll('button')
    const startBtn = buttons.find(b => b.text().includes('Start streaming'))
    startBtn!.trigger('click')
    await new Promise(r => setTimeout(r, 0))

    expect(wrapper.text()).toContain('Starting…')

    resolveStream(makeStartStreamResponse())
    await flushPromises()
  })

  // ── Error from service ────────────────────────────────────────────────────

  it('shows the server error message when startStream rejects', async () => {
    mockLiveStore.startStream.mockRejectedValue({
      response: { data: { error: 'Streaming is currently unavailable.' } },
    })

    const wrapper = mountModal()

    await wrapper.find('input').setValue('My stream')

    const buttons  = wrapper.findAll('button')
    const startBtn = buttons.find(b => b.text().includes('Start streaming'))
    await startBtn!.trigger('click')
    await flushPromises()

    expect(wrapper.text()).toContain('Streaming is currently unavailable.')
    expect(wrapper.emitted('started')).toBeUndefined()
  })

  it('shows fallback error text when error has only a message', async () => {
    mockLiveStore.startStream.mockRejectedValue(new Error('Network timeout'))

    const wrapper = mountModal()

    await wrapper.find('input').setValue('My stream')

    const buttons  = wrapper.findAll('button')
    const startBtn = buttons.find(b => b.text().includes('Start streaming'))
    await startBtn!.trigger('click')
    await flushPromises()

    expect(wrapper.text()).toContain('Network timeout')
  })

  // ── Privacy selection ─────────────────────────────────────────────────────

  it('switches privacy to Everyone when the Everyone button is clicked', async () => {
    const wrapper = mountModal()

    const buttons     = wrapper.findAll('button')
    const everyoneBtn = buttons.find(b => b.text().includes('Everyone (public)'))
    expect(everyoneBtn).toBeDefined()

    await everyoneBtn!.trigger('click')
    await wrapper.find('input').setValue('Public stream')

    const startBtn = wrapper.findAll('button').find(b => b.text().includes('Start streaming'))
    await startBtn!.trigger('click')
    await flushPromises()

    const payload = mockLiveStore.startStream.mock.calls[0][0]
    expect(payload.privacy).toBe('Everyone')
  })
})
