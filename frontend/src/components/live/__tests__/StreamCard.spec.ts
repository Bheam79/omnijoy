import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { RouterLinkStub } from '@vue/test-utils'

// ── Router mock ───────────────────────────────────────────────────────────────

vi.mock('vue-router', async (importOriginal) => {
  const actual = await importOriginal<typeof import('vue-router')>()
  return {
    ...actual,
    RouterLink: RouterLinkStub,
  }
})

// Import after mocks
import StreamCard from '../StreamCard.vue'
import type { LiveStreamDto } from '@/services/liveService'

// ── Fixtures ──────────────────────────────────────────────────────────────────

function makeStream(overrides: Partial<LiveStreamDto> = {}): LiveStreamDto {
  return {
    id:          'stream-1',
    host:        { id: 'u1', displayName: 'Alice' },
    title:       'Chill Coding Session',
    status:      'Live',
    privacy:     'Everyone',
    viewerCount: 42,
    hlsUrl:      '/media/live/stream-1/index.m3u8',
    startedAt:   '2026-06-01T12:00:00Z',
    ...overrides,
  }
}

// ── Mount helper ──────────────────────────────────────────────────────────────

function mountCard(stream: LiveStreamDto = makeStream()) {
  return mount(StreamCard, {
    props: { stream },
    global: {
      plugins: [createPinia()],
      stubs:   { RouterLink: RouterLinkStub },
    },
  })
}

// ── Tests ─────────────────────────────────────────────────────────────────────

describe('StreamCard', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  // ── Basic rendering ───────────────────────────────────────────────────────

  it('renders the stream title', () => {
    const wrapper = mountCard()

    expect(wrapper.text()).toContain('Chill Coding Session')
  })

  it('renders the host display name', () => {
    const wrapper = mountCard()

    expect(wrapper.text()).toContain('Alice')
  })

  // ── Viewer count ──────────────────────────────────────────────────────────

  it('renders the viewer count', () => {
    const wrapper = mountCard()

    expect(wrapper.text()).toContain('42')
  })

  it('renders viewer count of 0', () => {
    const wrapper = mountCard(makeStream({ viewerCount: 0 }))

    expect(wrapper.text()).toContain('0')
  })

  it('renders a large viewer count', () => {
    const wrapper = mountCard(makeStream({ viewerCount: 1234 }))

    expect(wrapper.text()).toContain('1234')
  })

  // ── LIVE badge ────────────────────────────────────────────────────────────

  it('renders the LIVE badge', () => {
    const wrapper = mountCard()

    expect(wrapper.text()).toContain('LIVE')
  })

  // ── Thumbnail / placeholder ───────────────────────────────────────────────

  it('renders the thumbnail placeholder area with an aspect-video class', () => {
    const wrapper = mountCard()

    expect(wrapper.find('.aspect-video').exists()).toBe(true)
  })

  it('renders a play icon svg inside the thumbnail', () => {
    const wrapper = mountCard()

    // There should be at least one SVG inside the card
    expect(wrapper.find('svg').exists()).toBe(true)
  })

  // ── Navigation ────────────────────────────────────────────────────────────

  it('wraps the card in a RouterLink pointing to /live/:id', () => {
    const wrapper = mountCard()

    const link = wrapper.findComponent(RouterLinkStub)
    expect(link.exists()).toBe(true)
    expect(link.props('to')).toBe('/live/stream-1')
  })

  it('uses the correct stream id in the link href', () => {
    const wrapper = mountCard(makeStream({ id: 'abc-xyz-789' }))

    const link = wrapper.findComponent(RouterLinkStub)
    expect(link.props('to')).toBe('/live/abc-xyz-789')
  })

  it('has data-testid="stream-card" on the root element', () => {
    const wrapper = mountCard()

    expect(wrapper.find('[data-testid="stream-card"]').exists()).toBe(true)
  })

  // ── Host avatar ───────────────────────────────────────────────────────────

  it('renders an img when host has an avatarUrl', () => {
    const wrapper = mountCard(
      makeStream({ host: { id: 'u1', displayName: 'Alice', avatarUrl: 'https://cdn.example.com/alice.jpg' } }),
    )

    const img = wrapper.find('img[alt="Alice"]')
    expect(img.exists()).toBe(true)
    expect(img.attributes('src')).toBe('https://cdn.example.com/alice.jpg')
  })

  it('renders an initials avatar when host has no avatarUrl', () => {
    // No avatarUrl — fallback to initials div
    const wrapper = mountCard(
      makeStream({ host: { id: 'u1', displayName: 'Alice' } }),
    )

    // The img with alt="Alice" should not exist
    expect(wrapper.find('img[alt="Alice"]').exists()).toBe(false)
    // The initials text "A" should be rendered
    expect(wrapper.text()).toContain('A')
  })

  // ── Privacy badge ─────────────────────────────────────────────────────────

  it('shows the Friends privacy badge when privacy is Friends', () => {
    const wrapper = mountCard(makeStream({ privacy: 'Friends' }))

    expect(wrapper.text()).toContain('Friends')
  })

  it('does not show the Friends badge for Everyone privacy', () => {
    const wrapper = mountCard(makeStream({ privacy: 'Everyone' }))

    // The privacy badge only appears for Friends — for Everyone there's no extra badge
    // Count how many times "Friends" appears
    const friendsBadges = wrapper.findAll('.bg-blue-50')
    expect(friendsBadges).toHaveLength(0)
  })

  // ── Multiple streams ──────────────────────────────────────────────────────

  it('renders different stream data correctly for different props', () => {
    const stream1 = mountCard(makeStream({ title: 'Stream One', viewerCount: 5 }))
    const stream2 = mountCard(makeStream({ title: 'Stream Two', viewerCount: 100 }))

    expect(stream1.text()).toContain('Stream One')
    expect(stream1.text()).toContain('5')
    expect(stream2.text()).toContain('Stream Two')
    expect(stream2.text()).toContain('100')
  })
})
