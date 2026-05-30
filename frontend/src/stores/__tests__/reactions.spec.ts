import { describe, it, expect, vi, beforeEach } from 'vitest'
import { setActivePinia, createPinia } from 'pinia'
import { useReactionsStore } from '@/stores/reactions'
import type { PostReactionsDto, ReactionType } from '@/services/reactionService'

// ── Mock reactionService ──────────────────────────────────────────────────────

const mockReactionService = vi.hoisted(() => ({
  getReactions:        vi.fn(),
  addOrUpdateReaction: vi.fn(),
  removeReaction:      vi.fn(),
}))

vi.mock('@/services/reactionService', () => ({
  reactionService:    mockReactionService,
  REACTION_EMOJIS:    {},
  REACTION_LABELS:    {},
  ALL_REACTION_TYPES: [],
}))

// ── Helpers ───────────────────────────────────────────────────────────────────

function makeReactions(
  currentUserReaction: ReactionType | null = null,
  counts: Array<{ reactionType: ReactionType; count: number }> = [],
): PostReactionsDto {
  return {
    counts,
    totalCount: counts.reduce((sum, c) => sum + c.count, 0),
    currentUserReaction,
  }
}

// ── Tests ─────────────────────────────────────────────────────────────────────

describe('useReactionsStore', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.clearAllMocks()
  })

  // ── fetchReactions ─────────────────────────────────────────────────────────

  it('fetchReactions — stores fetched reactions by postId', async () => {
    const dto = makeReactions('Like', [{ reactionType: 'Like', count: 5 }])
    mockReactionService.getReactions.mockResolvedValue(dto)

    const store = useReactionsStore()
    await store.fetchReactions('post-1')

    expect(store.byPost['post-1']).toEqual(dto)
  })

  it('fetchReactions — silently swallows errors and leaves state empty', async () => {
    mockReactionService.getReactions.mockRejectedValue(new Error('network'))

    const store = useReactionsStore()
    await expect(store.fetchReactions('post-1')).resolves.toBeUndefined()
    expect(store.byPost['post-1']).toBeUndefined()
  })

  // ── applyUpdate ────────────────────────────────────────────────────────────

  it('applyUpdate — replaces counts and totalCount from SignalR event', () => {
    const initial = makeReactions('Like', [{ reactionType: 'Like', count: 3 }])
    const store = useReactionsStore()
    store.byPost['post-1'] = { ...initial }

    store.applyUpdate({
      postId:     'post-1',
      counts:     [{ reactionType: 'Love', count: 10 }],
      totalCount: 10,
    })

    expect(store.byPost['post-1'].totalCount).toBe(10)
    expect(store.byPost['post-1'].counts[0].reactionType).toBe('Love')
  })

  it('applyUpdate — preserves currentUserReaction from existing state', () => {
    const store = useReactionsStore()
    store.byPost['post-1'] = makeReactions('Like', [{ reactionType: 'Like', count: 1 }])

    store.applyUpdate({ postId: 'post-1', counts: [], totalCount: 0 })

    expect(store.byPost['post-1'].currentUserReaction).toBe('Like')
  })

  it('applyUpdate — sets currentUserReaction to null when no prior state exists', () => {
    const store = useReactionsStore()

    store.applyUpdate({
      postId:     'post-1',
      counts:     [{ reactionType: 'Love', count: 1 }],
      totalCount: 1,
    })

    expect(store.byPost['post-1'].currentUserReaction).toBeNull()
  })

  // ── addOrUpdate ────────────────────────────────────────────────────────────

  it('addOrUpdate — optimistically adds reaction then confirms with API response', async () => {
    const initial   = makeReactions(null, [])
    const confirmed = makeReactions('Like', [{ reactionType: 'Like', count: 1 }])
    mockReactionService.addOrUpdateReaction.mockResolvedValue(confirmed)

    const store = useReactionsStore()
    store.byPost['post-1'] = { ...initial }

    await store.addOrUpdate('post-1', 'Like')

    expect(store.byPost['post-1'].currentUserReaction).toBe('Like')
    expect(store.byPost['post-1'].totalCount).toBe(1)
  })

  it('addOrUpdate — rolls back to previous state on API error', async () => {
    const initial = makeReactions(null, [{ reactionType: 'Love', count: 3 }])
    mockReactionService.addOrUpdateReaction.mockRejectedValue(new Error('fail'))

    const store = useReactionsStore()
    store.byPost['post-1'] = { ...initial }

    await store.addOrUpdate('post-1', 'Like')

    expect(store.byPost['post-1'].currentUserReaction).toBeNull()
    expect(store.byPost['post-1'].counts[0].reactionType).toBe('Love')
    expect(store.byPost['post-1'].counts[0].count).toBe(3)
  })

  it('addOrUpdate — skips optimistic step if post not yet in store', async () => {
    const confirmed = makeReactions('Like', [{ reactionType: 'Like', count: 1 }])
    mockReactionService.addOrUpdateReaction.mockResolvedValue(confirmed)

    const store = useReactionsStore()

    await store.addOrUpdate('post-1', 'Like')

    // Falls through to API confirmation
    expect(store.byPost['post-1']).toEqual(confirmed)
  })

  it('addOrUpdate — no crash when API fails and post was never fetched', async () => {
    mockReactionService.addOrUpdateReaction.mockRejectedValue(new Error('fail'))

    const store = useReactionsStore()
    // byPost['post-1'] not set — prev is undefined, rollback is a no-op

    await store.addOrUpdate('post-1', 'Like')

    expect(store.byPost['post-1']).toBeUndefined()
  })

  it('addOrUpdate — optimistic: switches reaction type, removes old count entry when count hits zero', async () => {
    const initial   = makeReactions('Like', [{ reactionType: 'Like', count: 1 }])
    const confirmed = makeReactions('Love', [{ reactionType: 'Love', count: 1 }])
    mockReactionService.addOrUpdateReaction.mockResolvedValue(confirmed)

    const store = useReactionsStore()
    store.byPost['post-1'] = { ...initial }

    await store.addOrUpdate('post-1', 'Love')

    expect(store.byPost['post-1'].currentUserReaction).toBe('Love')
    // Old Like count of 1 was decremented to 0 and filtered out in optimistic step;
    // API response confirmed the final state.
    expect(store.byPost['post-1'].counts.find((c) => c.reactionType === 'Like')).toBeUndefined()
  })

  it('addOrUpdate — optimistic: keeps other reaction counts when switching type', async () => {
    // Tests the filter's TRUE branch: remaining counts (c.count > 0) are kept
    const initial = makeReactions('Like', [
      { reactionType: 'Like', count: 3 },
      { reactionType: 'Love', count: 5 },
    ])
    const confirmed = makeReactions('Haha', [
      { reactionType: 'Like', count: 2 },
      { reactionType: 'Love', count: 5 },
      { reactionType: 'Haha', count: 1 },
    ])
    mockReactionService.addOrUpdateReaction.mockResolvedValue(confirmed)

    const store = useReactionsStore()
    store.byPost['post-1'] = { ...initial }

    await store.addOrUpdate('post-1', 'Haha')

    // After API confirmation, Like decremented from 3 to 2 (still > 0, kept)
    expect(store.byPost['post-1'].counts.find((c) => c.reactionType === 'Like')?.count).toBe(2)
    expect(store.byPost['post-1'].counts.find((c) => c.reactionType === 'Love')?.count).toBe(5)
  })

  it('addOrUpdate — optimistic: increments existing reaction type count', async () => {
    const initial   = makeReactions(null, [{ reactionType: 'Like', count: 2 }])
    const confirmed = makeReactions('Like', [{ reactionType: 'Like', count: 3 }])
    mockReactionService.addOrUpdateReaction.mockResolvedValue(confirmed)

    const store = useReactionsStore()
    store.byPost['post-1'] = { ...initial }

    await store.addOrUpdate('post-1', 'Like')

    // After API confirmation
    expect(store.byPost['post-1'].counts[0].count).toBe(3)
  })

  // ── remove ─────────────────────────────────────────────────────────────────

  it('remove — optimistically clears reaction then confirms with API', async () => {
    const initial   = makeReactions('Like', [{ reactionType: 'Like', count: 3 }])
    const confirmed = makeReactions(null,   [{ reactionType: 'Like', count: 2 }])
    mockReactionService.removeReaction.mockResolvedValue(confirmed)

    const store = useReactionsStore()
    store.byPost['post-1'] = { ...initial }

    await store.remove('post-1')

    expect(store.byPost['post-1'].currentUserReaction).toBeNull()
    expect(store.byPost['post-1'].counts[0].count).toBe(2)
  })

  it('remove — optimistic: removes count entry when it would drop to zero', async () => {
    const initial   = makeReactions('Like', [{ reactionType: 'Like', count: 1 }])
    const confirmed = makeReactions(null,   [])
    mockReactionService.removeReaction.mockResolvedValue(confirmed)

    const store = useReactionsStore()
    store.byPost['post-1'] = { ...initial }

    await store.remove('post-1')

    expect(store.byPost['post-1'].counts).toHaveLength(0)
  })

  it('remove — optimistic: decrements only the current user reaction type', async () => {
    const initial = makeReactions('Like', [
      { reactionType: 'Like', count: 2 },
      { reactionType: 'Love', count: 5 },
    ])
    const confirmed = makeReactions(null, [
      { reactionType: 'Like', count: 1 },
      { reactionType: 'Love', count: 5 },
    ])
    mockReactionService.removeReaction.mockResolvedValue(confirmed)

    const store = useReactionsStore()
    store.byPost['post-1'] = { ...initial }

    await store.remove('post-1')

    // After API confirmation
    const loveCount = store.byPost['post-1'].counts.find((c) => c.reactionType === 'Love')
    expect(loveCount?.count).toBe(5)
  })

  it('remove — rolls back to previous state on API error', async () => {
    const initial = makeReactions('Like', [{ reactionType: 'Like', count: 1 }])
    mockReactionService.removeReaction.mockRejectedValue(new Error('fail'))

    const store = useReactionsStore()
    store.byPost['post-1'] = { ...initial }

    await store.remove('post-1')

    expect(store.byPost['post-1'].currentUserReaction).toBe('Like')
    expect(store.byPost['post-1'].counts[0].count).toBe(1)
  })

  it('remove — skips optimistic step if post not yet in store', async () => {
    const confirmed = makeReactions(null, [])
    mockReactionService.removeReaction.mockResolvedValue(confirmed)

    const store = useReactionsStore()
    // byPost['post-1'] intentionally not set

    await store.remove('post-1')

    expect(store.byPost['post-1']).toEqual(confirmed)
  })

  it('remove — no crash when API fails and post was never fetched', async () => {
    mockReactionService.removeReaction.mockRejectedValue(new Error('fail'))

    const store = useReactionsStore()
    // byPost['post-1'] not set — prev is undefined, rollback is a no-op

    await store.remove('post-1')

    expect(store.byPost['post-1']).toBeUndefined()
  })

  // ── reset ──────────────────────────────────────────────────────────────────

  it('reset — clears all post reaction state', async () => {
    const dto = makeReactions('Like', [{ reactionType: 'Like', count: 1 }])
    mockReactionService.getReactions.mockResolvedValue(dto)

    const store = useReactionsStore()
    await store.fetchReactions('post-1')

    store.reset()

    expect(store.byPost).toEqual({})
  })
})
