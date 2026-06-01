import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import PostReactionBar from '@/components/post/PostReactionBar.vue'
import { useReactionsStore } from '@/stores/reactions'
import type { PostReactionsDto } from '@/services/reactionService'

// ── Mock reactionService so store actions don't hit the network ───────────────

vi.mock('@/services/reactionService', async () => {
  const actual = await vi.importActual<typeof import('@/services/reactionService')>(
    '@/services/reactionService',
  )
  return {
    ...actual,
    reactionService: {
      getReactions:        vi.fn().mockResolvedValue({ counts: [], totalCount: 0, currentUserReaction: null }),
      addOrUpdateReaction: vi.fn().mockResolvedValue({ counts: [], totalCount: 0, currentUserReaction: null }),
      removeReaction:      vi.fn().mockResolvedValue({ counts: [], totalCount: 0, currentUserReaction: null }),
      getReactionWho:      vi.fn().mockResolvedValue({ people: [], remaining: 0 }),
    },
  }
})

const EMPTY_REACTIONS: PostReactionsDto = { counts: [], totalCount: 0, currentUserReaction: null }

/**
 * Mount PostReactionBar with an optional pre-seeded reactions state.
 * Pre-seeding before mount avoids racing with onMounted's async fetch.
 */
function mountBar(options: {
  postId?:   string
  loggedIn?: boolean
  seed?:     PostReactionsDto
} = {}) {
  const { postId = 'post-1', loggedIn = true, seed } = options
  const pinia = createPinia()
  setActivePinia(pinia)

  const store = useReactionsStore(pinia)

  // Seed BEFORE mount so onMounted's guard skips the fetch
  if (seed) {
    store.byPost[postId] = seed
  }

  const wrapper = mount(PostReactionBar, {
    props:    { postId, loggedIn },
    global:   { plugins: [pinia] },
    attachTo: document.body,
  })

  return { wrapper, store }
}

describe('PostReactionBar', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    document.body.innerHTML = ''
  })

  // ── Rendering ─────────────────────────────────────────────────────────────

  it('renders without crashing', async () => {
    const { wrapper } = mountBar()
    await flushPromises()
    expect(wrapper.find('[data-testid="post-reaction-bar"]').exists()).toBe(true)
  })

  it('fetches reactions on mount when data is absent', async () => {
    const { reactionService } = await import('@/services/reactionService')
    mountBar({ postId: 'post-42' })
    await flushPromises()
    expect(reactionService.getReactions).toHaveBeenCalledWith('post-42')
  })

  it('skips fetch when data is already in store', async () => {
    const { reactionService } = await import('@/services/reactionService')
    mountBar({ postId: 'post-x', seed: EMPTY_REACTIONS })
    await flushPromises()
    expect(reactionService.getReactions).not.toHaveBeenCalled()
  })

  // ── Count display ─────────────────────────────────────────────────────────

  it('hides count strip when totalCount is 0', async () => {
    const { wrapper } = mountBar({ seed: EMPTY_REACTIONS })
    await flushPromises()
    expect(wrapper.find('[data-testid="reaction-count-button"]').exists()).toBe(false)
  })

  it('shows count strip with correct total when totalCount > 0', async () => {
    const { wrapper } = mountBar({
      seed: {
        counts:              [{ reactionType: 'Like', count: 5 }],
        totalCount:          5,
        currentUserReaction: null,
      },
    })
    await flushPromises()

    const countBtn = wrapper.find('[data-testid="reaction-count-button"]')
    expect(countBtn.exists()).toBe(true)
    expect(countBtn.text()).toContain('5')
  })

  it('shows top-3 reaction emojis sorted by count', async () => {
    const { wrapper } = mountBar({
      seed: {
        counts: [
          { reactionType: 'Like',  count: 10 },
          { reactionType: 'Love',  count: 5 },
          { reactionType: 'Haha',  count: 3 },
          { reactionType: 'Angry', count: 1 },
        ],
        totalCount: 19,
        currentUserReaction: null,
      },
    })
    await flushPromises()

    const strip = wrapper.find('[data-testid="reaction-count-button"]').text()
    expect(strip).toContain('👍') // Like
    expect(strip).toContain('❤️') // Love
    expect(strip).toContain('😂') // Haha
    // 4th reaction (Angry) should NOT appear (only top-3)
    expect(strip).not.toContain('😠')
  })

  it('renders the reaction count correctly in text', async () => {
    const { wrapper } = mountBar({
      seed: {
        counts:              [{ reactionType: 'Like', count: 42 }],
        totalCount:          42,
        currentUserReaction: null,
      },
    })
    await flushPromises()

    expect(wrapper.find('[data-testid="reaction-count-button"]').text()).toContain('42')
  })

  // ── Modal ─────────────────────────────────────────────────────────────────

  it('opens reactions modal when count button is clicked', async () => {
    const { wrapper } = mountBar({
      seed: {
        counts:              [{ reactionType: 'Like', count: 3 }],
        totalCount:          3,
        currentUserReaction: null,
      },
    })
    await flushPromises()

    await wrapper.find('[data-testid="reaction-count-button"]').trigger('click')
    await flushPromises()

    // Modal is teleported to body
    expect(document.body.querySelector('[data-testid="reactions-modal"]')).toBeTruthy()
  })

  it('closes reactions modal when close button is clicked', async () => {
    const { wrapper } = mountBar({
      seed: {
        counts:              [{ reactionType: 'Like', count: 1 }],
        totalCount:          1,
        currentUserReaction: null,
      },
    })
    await flushPromises()

    await wrapper.find('[data-testid="reaction-count-button"]').trigger('click')
    await flushPromises()
    expect(document.body.querySelector('[data-testid="reactions-modal"]')).toBeTruthy()

    const closeBtn = document.body.querySelector(
      '[data-testid="reactions-modal-close"]',
    ) as HTMLButtonElement
    closeBtn?.click()
    await flushPromises()

    expect(document.body.querySelector('[data-testid="reactions-modal"]')).toBeFalsy()
  })

  // ── Reaction actions ──────────────────────────────────────────────────────

  it('calls store.addOrUpdate when react event fires from picker', async () => {
    const { wrapper, store } = mountBar({ seed: EMPTY_REACTIONS })
    await flushPromises()

    const spy = vi.spyOn(store, 'addOrUpdate').mockResolvedValue(undefined as never)

    const picker = wrapper.findComponent({ name: 'ReactionPicker' })
    await picker.vm.$emit('react', 'Love')

    expect(spy).toHaveBeenCalledWith('post-1', 'Love')
  })

  it('calls store.remove when remove event fires from picker', async () => {
    const { wrapper, store } = mountBar({ seed: EMPTY_REACTIONS })
    await flushPromises()

    const spy = vi.spyOn(store, 'remove').mockResolvedValue(undefined as never)

    const picker = wrapper.findComponent({ name: 'ReactionPicker' })
    await picker.vm.$emit('remove')

    expect(spy).toHaveBeenCalledWith('post-1')
  })

  // ── ReactionPicker integration ────────────────────────────────────────────

  it('passes currentUserReaction to ReactionPicker', async () => {
    const { wrapper } = mountBar({
      seed: {
        counts:              [{ reactionType: 'Love', count: 1 }],
        totalCount:          1,
        currentUserReaction: 'Love',
      },
    })
    await flushPromises()

    const picker = wrapper.findComponent({ name: 'ReactionPicker' })
    expect(picker.props('currentReaction')).toBe('Love')
  })

  // ── Disabled state ────────────────────────────────────────────────────────

  it('passes disabled=true to ReactionPicker when not logged in', async () => {
    const { wrapper } = mountBar({ seed: EMPTY_REACTIONS, loggedIn: false })
    await flushPromises()

    const picker = wrapper.findComponent({ name: 'ReactionPicker' })
    expect(picker.props('disabled')).toBe(true)
  })

  it('passes disabled=false to ReactionPicker when logged in', async () => {
    const { wrapper } = mountBar({ seed: EMPTY_REACTIONS, loggedIn: true })
    await flushPromises()

    const picker = wrapper.findComponent({ name: 'ReactionPicker' })
    expect(picker.props('disabled')).toBe(false)
  })

  // ── Real-time update via store ────────────────────────────────────────────

  it('reflects updated counts when store is updated via applyUpdate', async () => {
    const { wrapper, store } = mountBar({ seed: EMPTY_REACTIONS })
    await flushPromises()

    expect(wrapper.find('[data-testid="reaction-count-button"]').exists()).toBe(false)

    store.applyUpdate({
      postId:     'post-1',
      counts:     [{ reactionType: 'Like', count: 7 }],
      totalCount: 7,
    })
    await flushPromises()

    expect(wrapper.find('[data-testid="reaction-count-button"]').text()).toContain('7')
  })

  // ── Who-reacted tooltip ───────────────────────────────────────────────────

  it('shows tooltip with names on mouseenter after delay', async () => {
    vi.useFakeTimers()
    const { reactionService } = await import('@/services/reactionService')
    const whoMock = vi.mocked(reactionService.getReactionWho)
    whoMock.mockResolvedValue({
      people: [
        { id: 'u1', displayName: 'Alice', avatarUrl: null, isFriend: true, reactionType: 'Like' },
        { id: 'u2', displayName: 'Bob',   avatarUrl: null, isFriend: false, reactionType: 'Love' },
      ],
      remaining: 0,
    })

    const { wrapper } = mountBar({
      seed: {
        counts: [{ reactionType: 'Like', count: 2 }],
        totalCount: 2,
        currentUserReaction: null,
      },
    })
    await flushPromises()

    await wrapper.find('.relative').trigger('mouseenter')

    // Before 200ms delay: tooltip not visible yet
    expect(wrapper.find('[data-testid="reaction-who-tooltip"]').exists()).toBe(false)

    // Advance past the 200ms debounce
    vi.advanceTimersByTime(200)
    await flushPromises()

    expect(wrapper.find('[data-testid="reaction-who-tooltip"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="reaction-who-tooltip"]').text()).toContain('Alice')
    expect(wrapper.find('[data-testid="reaction-who-tooltip"]').text()).toContain('Bob')

    vi.useRealTimers()
  })

  it('hides tooltip on mouseleave', async () => {
    vi.useFakeTimers()

    const { wrapper } = mountBar({
      seed: {
        counts: [{ reactionType: 'Like', count: 1 }],
        totalCount: 1,
        currentUserReaction: null,
      },
    })
    await flushPromises()

    await wrapper.find('.relative').trigger('mouseenter')
    vi.advanceTimersByTime(200)
    await flushPromises()

    expect(wrapper.find('[data-testid="reaction-who-tooltip"]').exists()).toBe(true)

    await wrapper.find('.relative').trigger('mouseleave')
    await flushPromises()

    expect(wrapper.find('[data-testid="reaction-who-tooltip"]').exists()).toBe(false)

    vi.useRealTimers()
  })

  it('shows "+N more…" when remaining > 0', async () => {
    vi.useFakeTimers()
    const { reactionService } = await import('@/services/reactionService')
    vi.mocked(reactionService.getReactionWho).mockResolvedValue({
      people: [
        { id: 'u1', displayName: 'Alice', avatarUrl: null, isFriend: false, reactionType: 'Like' },
      ],
      remaining: 12,
    })

    const { wrapper } = mountBar({
      seed: {
        counts: [{ reactionType: 'Like', count: 13 }],
        totalCount: 13,
        currentUserReaction: null,
      },
    })
    await flushPromises()

    await wrapper.find('.relative').trigger('mouseenter')
    vi.advanceTimersByTime(200)
    await flushPromises()

    expect(wrapper.find('[data-testid="reaction-who-tooltip"]').text()).toContain('+12 more')

    vi.useRealTimers()
  })
})
