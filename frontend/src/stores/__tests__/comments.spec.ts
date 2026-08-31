import { describe, it, expect, vi, beforeEach } from 'vitest'
import { setActivePinia, createPinia } from 'pinia'
import { useCommentsStore } from '@/stores/comments'
import type { CommentDto } from '@/services/commentService'

// ── Mock commentService ───────────────────────────────────────────────────────

const mockCommentService = vi.hoisted(() => ({
  getComments:   vi.fn(),
  getReplies:    vi.fn(),
  createComment: vi.fn(),
  updateComment: vi.fn(),
  deleteComment: vi.fn(),
}))

vi.mock('@/services/commentService', () => ({
  commentService: mockCommentService,
}))

// ── Helpers ───────────────────────────────────────────────────────────────────

function makeComment(id: string, postId: string, overrides: Partial<CommentDto> = {}): CommentDto {
  return {
    id,
    postId,
    author:          { id: 'user-1', displayName: 'Alice' },
    content:         `Comment ${id}`,
    mentions:        [],
    replyCount:      0,
    createdAt:       '2024-01-01T00:00:00Z',
    updatedAt:       '2024-01-01T00:00:00Z',
    isDeleted:       false,
    parentCommentId: null,
    ...overrides,
  }
}

function makePage(items: CommentDto[], hasMore = false) {
  return { items, page: 1, pageSize: 20, hasMore }
}

// ── Tests ─────────────────────────────────────────────────────────────────────

describe('useCommentsStore', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.clearAllMocks()
  })

  // ── ensurePost ─────────────────────────────────────────────────────────────

  it('ensurePost — creates initial state for new postId', () => {
    const store = useCommentsStore()
    const state = store.ensurePost('post-1')

    expect(state.items).toEqual([])
    expect(state.page).toBe(0)
    expect(state.hasMore).toBe(true)
    expect(state.loading).toBe(false)
    expect(state.replies).toEqual({})
    expect(state.repliesLoading).toEqual({})
  })

  it('ensurePost — returns existing state for known postId', () => {
    const store = useCommentsStore()
    store.ensurePost('post-1')
    store.byPost['post-1'].page = 99

    const state = store.ensurePost('post-1')
    expect(state.page).toBe(99)
  })

  // ── loadComments ──────────────────────────────────────────────────────────

  it('loadComments — fetches page 1 and sets state', async () => {
    const comments = [makeComment('c1', 'post-1'), makeComment('c2', 'post-1')]
    mockCommentService.getComments.mockResolvedValue(makePage(comments, true))

    const store = useCommentsStore()
    await store.loadComments('post-1')

    expect(store.byPost['post-1'].items).toHaveLength(2)
    expect(store.byPost['post-1'].page).toBe(1)
    expect(store.byPost['post-1'].hasMore).toBe(true)
    expect(store.byPost['post-1'].loading).toBe(false)
  })

  it('loadComments — calls service with page 1', async () => {
    mockCommentService.getComments.mockResolvedValue(makePage([]))

    const store = useCommentsStore()
    await store.loadComments('post-1')

    expect(mockCommentService.getComments).toHaveBeenCalledWith('post-1', 1)
  })

  it('loadComments — no-ops when already loading', async () => {
    const store = useCommentsStore()
    store.ensurePost('post-1')
    store.byPost['post-1'].loading = true

    await store.loadComments('post-1')

    expect(mockCommentService.getComments).not.toHaveBeenCalled()
  })

  it('loadComments — clears loading flag on error', async () => {
    mockCommentService.getComments.mockRejectedValue(new Error('fail'))

    const store = useCommentsStore()
    await expect(store.loadComments('post-1')).rejects.toThrow('fail')

    expect(store.byPost['post-1'].loading).toBe(false)
  })

  // ── loadMore ──────────────────────────────────────────────────────────────

  it('loadMore — appends next page and deduplicates', async () => {
    const c1 = makeComment('c1', 'post-1')
    const c2 = makeComment('c2', 'post-1')
    mockCommentService.getComments
      .mockResolvedValueOnce(makePage([c1], true))
      .mockResolvedValueOnce(makePage([c1, c2], false)) // c1 is duplicate

    const store = useCommentsStore()
    await store.loadComments('post-1')
    await store.loadMore('post-1')

    expect(store.byPost['post-1'].items).toHaveLength(2)
    expect(store.byPost['post-1'].items.map((c) => c.id)).toEqual(['c1', 'c2'])
    expect(store.byPost['post-1'].page).toBe(2)
    expect(store.byPost['post-1'].hasMore).toBe(false)
  })

  it('loadMore — no-ops when hasMore is false', async () => {
    mockCommentService.getComments.mockResolvedValue(makePage([], false))

    const store = useCommentsStore()
    await store.loadComments('post-1')
    await store.loadMore('post-1')

    expect(mockCommentService.getComments).toHaveBeenCalledTimes(1)
  })

  it('loadMore — no-ops when loading is true', async () => {
    mockCommentService.getComments.mockResolvedValue(makePage([], true))

    const store = useCommentsStore()
    await store.loadComments('post-1')
    store.byPost['post-1'].loading = true

    await store.loadMore('post-1')

    expect(mockCommentService.getComments).toHaveBeenCalledTimes(1)
  })

  it('loadMore — clears loading flag on error', async () => {
    mockCommentService.getComments
      .mockResolvedValueOnce(makePage([], true))
      .mockRejectedValueOnce(new Error('fail'))

    const store = useCommentsStore()
    await store.loadComments('post-1')

    await expect(store.loadMore('post-1')).rejects.toThrow('fail')
    expect(store.byPost['post-1'].loading).toBe(false)
  })

  // ── loadReplies ───────────────────────────────────────────────────────────

  it('loadReplies — fetches and stores replies by commentId', async () => {
    const replies = [makeComment('r1', 'post-1', { parentCommentId: 'c1' })]
    mockCommentService.getReplies.mockResolvedValue(replies)

    const store = useCommentsStore()
    await store.loadReplies('post-1', 'c1')

    expect(store.byPost['post-1'].replies['c1']).toHaveLength(1)
    expect(store.byPost['post-1'].replies['c1'][0].id).toBe('r1')
    expect(store.byPost['post-1'].repliesLoading['c1']).toBe(false)
  })

  it('loadReplies — no-ops when already loading replies for that comment', async () => {
    const store = useCommentsStore()
    store.ensurePost('post-1')
    store.byPost['post-1'].repliesLoading['c1'] = true

    await store.loadReplies('post-1', 'c1')

    expect(mockCommentService.getReplies).not.toHaveBeenCalled()
  })

  it('loadReplies — clears repliesLoading flag on error', async () => {
    mockCommentService.getReplies.mockRejectedValue(new Error('fail'))

    const store = useCommentsStore()
    await expect(store.loadReplies('post-1', 'c1')).rejects.toThrow('fail')

    expect(store.byPost['post-1'].repliesLoading['c1']).toBe(false)
  })

  // ── createComment — top-level ─────────────────────────────────────────────

  it('createComment — top-level: prepends to items list', async () => {
    const newComment = makeComment('c-new', 'post-1')
    mockCommentService.createComment.mockResolvedValue(newComment)

    const store = useCommentsStore()
    store.ensurePost('post-1')
    store.byPost['post-1'].items = [makeComment('c-old', 'post-1')]

    const result = await store.createComment('post-1', { content: 'Hello' })

    expect(result.id).toBe('c-new')
    expect(store.byPost['post-1'].items[0].id).toBe('c-new')
    expect(store.byPost['post-1'].items).toHaveLength(2)
  })

  it('createComment — top-level: does not duplicate if id already present', async () => {
    const existing = makeComment('c1', 'post-1')
    mockCommentService.createComment.mockResolvedValue(existing)

    const store = useCommentsStore()
    store.ensurePost('post-1')
    store.byPost['post-1'].items = [existing]

    await store.createComment('post-1', { content: 'Hello' })

    expect(store.byPost['post-1'].items).toHaveLength(1)
  })

  // ── createComment — reply ─────────────────────────────────────────────────

  it('createComment — reply: appends to loaded replies list', async () => {
    const existingReply = makeComment('r1', 'post-1', { parentCommentId: 'c1' })
    const newReply      = makeComment('r2', 'post-1', { parentCommentId: 'c1' })
    mockCommentService.createComment.mockResolvedValue(newReply)

    const store = useCommentsStore()
    store.ensurePost('post-1')
    store.byPost['post-1'].items           = [makeComment('c1', 'post-1')]
    store.byPost['post-1'].replies['c1']   = [existingReply]

    await store.createComment('post-1', { content: 'Reply', parentCommentId: 'c1' })

    expect(store.byPost['post-1'].replies['c1']).toHaveLength(2)
    expect(store.byPost['post-1'].replies['c1'][1].id).toBe('r2')
  })

  it('createComment — reply: bumps parent replyCount', async () => {
    const newReply = makeComment('r1', 'post-1', { parentCommentId: 'c1' })
    mockCommentService.createComment.mockResolvedValue(newReply)

    const parent = makeComment('c1', 'post-1')
    parent.replyCount = 0

    const store = useCommentsStore()
    store.ensurePost('post-1')
    store.byPost['post-1'].items         = [parent]
    store.byPost['post-1'].replies['c1'] = []

    await store.createComment('post-1', { content: 'Reply', parentCommentId: 'c1' })

    expect(store.byPost['post-1'].items[0].replyCount).toBeGreaterThanOrEqual(1)
  })

  it('createComment — reply: does not crash when parent comment not in items', async () => {
    // Tests the if (parent) false branch in createComment
    const newReply = makeComment('r1', 'post-1', { parentCommentId: 'c1' })
    mockCommentService.createComment.mockResolvedValue(newReply)

    const store = useCommentsStore()
    store.ensurePost('post-1')
    // items is empty — parent 'c1' not found
    store.byPost['post-1'].replies['c1'] = []

    // Should not throw
    await store.createComment('post-1', { content: 'Reply', parentCommentId: 'c1' })

    expect(store.byPost['post-1'].replies['c1']).toHaveLength(1)
  })

  it('createComment — reply: does not append if replies list not yet loaded', async () => {
    const newReply = makeComment('r1', 'post-1', { parentCommentId: 'c1' })
    mockCommentService.createComment.mockResolvedValue(newReply)

    const store = useCommentsStore()
    store.ensurePost('post-1')
    store.byPost['post-1'].items = [makeComment('c1', 'post-1')]
    // replies['c1'] intentionally left unset

    await store.createComment('post-1', { content: 'Reply', parentCommentId: 'c1' })

    expect(store.byPost['post-1'].replies['c1']).toBeUndefined()
  })

  it('createComment — reply: does not duplicate in replies list', async () => {
    const reply = makeComment('r1', 'post-1', { parentCommentId: 'c1' })
    mockCommentService.createComment.mockResolvedValue(reply)

    const store = useCommentsStore()
    store.ensurePost('post-1')
    store.byPost['post-1'].items         = [makeComment('c1', 'post-1')]
    store.byPost['post-1'].replies['c1'] = [reply] // already present

    await store.createComment('post-1', { content: 'Reply', parentCommentId: 'c1' })

    expect(store.byPost['post-1'].replies['c1']).toHaveLength(1)
  })

  // ── updateComment ─────────────────────────────────────────────────────────

  it('updateComment — patches top-level comment content and updatedAt', async () => {
    const original = makeComment('c1', 'post-1')
    const updated  = { ...original, content: 'Edited', updatedAt: '2024-06-01T00:00:00Z' }
    mockCommentService.updateComment.mockResolvedValue(updated)

    const store = useCommentsStore()
    store.ensurePost('post-1')
    store.byPost['post-1'].items = [original]

    await store.updateComment('post-1', 'c1', 'Edited')

    expect(store.byPost['post-1'].items[0].content).toBe('Edited')
    expect(store.byPost['post-1'].items[0].updatedAt).toBe('2024-06-01T00:00:00Z')
  })

  it('updateComment — patches reply content and updatedAt', async () => {
    const reply   = makeComment('r1', 'post-1', { parentCommentId: 'c1' })
    const updated = { ...reply, content: 'Edited reply', updatedAt: '2024-06-01T00:00:00Z' }
    mockCommentService.updateComment.mockResolvedValue(updated)

    const store = useCommentsStore()
    store.ensurePost('post-1')
    store.byPost['post-1'].items         = [makeComment('c1', 'post-1')]
    store.byPost['post-1'].replies['c1'] = [reply]

    await store.updateComment('post-1', 'r1', 'Edited reply')

    expect(store.byPost['post-1'].replies['c1'][0].content).toBe('Edited reply')
    expect(store.byPost['post-1'].replies['c1'][0].updatedAt).toBe('2024-06-01T00:00:00Z')
  })

  it('updateComment — preserves sibling replies when patching one reply', async () => {
    // Tests the ternary false branch: replies not matching ridx are returned unchanged
    const r1 = makeComment('r1', 'post-1', { parentCommentId: 'c1' })
    const r2 = makeComment('r2', 'post-1', { parentCommentId: 'c1' })
    const updated = { ...r2, content: 'Edited r2', updatedAt: '2024-06-01T00:00:00Z' }
    mockCommentService.updateComment.mockResolvedValue(updated)

    const store = useCommentsStore()
    store.ensurePost('post-1')
    store.byPost['post-1'].items         = [makeComment('c1', 'post-1')]
    store.byPost['post-1'].replies['c1'] = [r1, r2]

    await store.updateComment('post-1', 'r2', 'Edited r2')

    // r1 unchanged, r2 updated
    expect(store.byPost['post-1'].replies['c1'][0].content).toBe('Comment r1')
    expect(store.byPost['post-1'].replies['c1'][1].content).toBe('Edited r2')
  })

  it('updateComment — searches across multiple reply groups to find the target', async () => {
    // Tests the ridx === -1 false branch in the for loop (first group has no match)
    const r1 = makeComment('r1', 'post-1', { parentCommentId: 'c1' })
    const r2 = makeComment('r2', 'post-1', { parentCommentId: 'c2' })
    const updated = { ...r2, content: 'Edited r2', updatedAt: '2024-06-01T00:00:00Z' }
    mockCommentService.updateComment.mockResolvedValue(updated)

    const store = useCommentsStore()
    store.ensurePost('post-1')
    store.byPost['post-1'].items         = [makeComment('c1', 'post-1'), makeComment('c2', 'post-1')]
    store.byPost['post-1'].replies['c1'] = [r1]
    store.byPost['post-1'].replies['c2'] = [r2]

    await store.updateComment('post-1', 'r2', 'Edited r2')

    // r1 (in c1) unchanged
    expect(store.byPost['post-1'].replies['c1'][0].content).toBe('Comment r1')
    // r2 (in c2) updated
    expect(store.byPost['post-1'].replies['c2'][0].content).toBe('Edited r2')
  })

  // ── deleteComment ─────────────────────────────────────────────────────────

  it('deleteComment — soft-deletes top-level comment', async () => {
    mockCommentService.deleteComment.mockResolvedValue(undefined)

    const store = useCommentsStore()
    store.ensurePost('post-1')
    store.byPost['post-1'].items = [makeComment('c1', 'post-1')]

    await store.deleteComment('post-1', 'c1')

    expect(store.byPost['post-1'].items[0].isDeleted).toBe(true)
    expect(store.byPost['post-1'].items[0].content).toBe('[deleted]')
  })

  it('deleteComment — soft-deletes a reply', async () => {
    mockCommentService.deleteComment.mockResolvedValue(undefined)

    const reply = makeComment('r1', 'post-1', { parentCommentId: 'c1' })

    const store = useCommentsStore()
    store.ensurePost('post-1')
    store.byPost['post-1'].items         = [makeComment('c1', 'post-1')]
    store.byPost['post-1'].replies['c1'] = [reply]

    await store.deleteComment('post-1', 'r1')

    expect(store.byPost['post-1'].replies['c1'][0].isDeleted).toBe(true)
    expect(store.byPost['post-1'].replies['c1'][0].content).toBe('[deleted]')
  })

  it('deleteComment — preserves sibling replies when soft-deleting one reply', async () => {
    // Tests the ternary false branch: replies not matching ridx are returned unchanged
    mockCommentService.deleteComment.mockResolvedValue(undefined)

    const r1 = makeComment('r1', 'post-1', { parentCommentId: 'c1' })
    const r2 = makeComment('r2', 'post-1', { parentCommentId: 'c1' })

    const store = useCommentsStore()
    store.ensurePost('post-1')
    store.byPost['post-1'].items         = [makeComment('c1', 'post-1')]
    store.byPost['post-1'].replies['c1'] = [r1, r2]

    await store.deleteComment('post-1', 'r2')

    // r1 unchanged, r2 deleted
    expect(store.byPost['post-1'].replies['c1'][0].isDeleted).toBe(false)
    expect(store.byPost['post-1'].replies['c1'][1].isDeleted).toBe(true)
  })

  it('deleteComment — searches across multiple reply groups to find the target', async () => {
    // Tests the ridx === -1 false branch in the for loop (first group has no match)
    mockCommentService.deleteComment.mockResolvedValue(undefined)

    const r1 = makeComment('r1', 'post-1', { parentCommentId: 'c1' })
    const r2 = makeComment('r2', 'post-1', { parentCommentId: 'c2' })

    const store = useCommentsStore()
    store.ensurePost('post-1')
    store.byPost['post-1'].items         = [makeComment('c1', 'post-1'), makeComment('c2', 'post-1')]
    store.byPost['post-1'].replies['c1'] = [r1]
    store.byPost['post-1'].replies['c2'] = [r2]

    await store.deleteComment('post-1', 'r2')

    // r1 (in c1) unchanged
    expect(store.byPost['post-1'].replies['c1'][0].isDeleted).toBe(false)
    // r2 (in c2) deleted
    expect(store.byPost['post-1'].replies['c2'][0].isDeleted).toBe(true)
  })

  // ── applyNewComment ───────────────────────────────────────────────────────

  it('applyNewComment — top-level: prepends to items list', () => {
    const comment = makeComment('c-new', 'post-1')

    const store = useCommentsStore()
    store.ensurePost('post-1')

    store.applyNewComment(comment)

    expect(store.byPost['post-1'].items[0].id).toBe('c-new')
  })

  it('applyNewComment — top-level: deduplicates by id', () => {
    const comment = makeComment('c1', 'post-1')

    const store = useCommentsStore()
    store.ensurePost('post-1')
    store.byPost['post-1'].items = [comment]

    store.applyNewComment(comment) // same id — should not duplicate

    expect(store.byPost['post-1'].items).toHaveLength(1)
  })

  it('applyNewComment — reply: appends to loaded replies and bumps parent replyCount', () => {
    const parent = makeComment('c1', 'post-1')
    parent.replyCount = 0
    const reply  = makeComment('r1', 'post-1', { parentCommentId: 'c1' })

    const store = useCommentsStore()
    store.ensurePost('post-1')
    store.byPost['post-1'].items         = [parent]
    store.byPost['post-1'].replies['c1'] = []

    store.applyNewComment(reply)

    expect(store.byPost['post-1'].replies['c1']).toHaveLength(1)
    expect(store.byPost['post-1'].items[0].replyCount).toBe(1)
  })

  it('applyNewComment — reply: skips if replies not yet loaded', () => {
    const reply = makeComment('r1', 'post-1', { parentCommentId: 'c1' })

    const store = useCommentsStore()
    store.ensurePost('post-1')
    // replies['c1'] not initialised

    store.applyNewComment(reply)

    expect(store.byPost['post-1'].replies['c1']).toBeUndefined()
  })

  it('applyNewComment — reply: does not duplicate in replies list', () => {
    const reply = makeComment('r1', 'post-1', { parentCommentId: 'c1' })

    const store = useCommentsStore()
    store.ensurePost('post-1')
    store.byPost['post-1'].items         = [makeComment('c1', 'post-1')]
    store.byPost['post-1'].replies['c1'] = [reply]

    store.applyNewComment(reply) // same id — should not duplicate

    expect(store.byPost['post-1'].replies['c1']).toHaveLength(1)
  })

  it('applyNewComment — reply: appends to replies even when parent not in items', () => {
    // Tests the if (parent) false branch: parent is not in items list
    const reply = makeComment('r1', 'post-1', { parentCommentId: 'c1' })

    const store = useCommentsStore()
    store.ensurePost('post-1')
    // items is empty — parent 'c1' won't be found
    store.byPost['post-1'].replies['c1'] = []

    store.applyNewComment(reply)

    // Reply was still appended (replies list grows)
    expect(store.byPost['post-1'].replies['c1']).toHaveLength(1)
  })

  // ── reset ─────────────────────────────────────────────────────────────────

  it('reset — clears all post state', async () => {
    mockCommentService.getComments.mockResolvedValue(makePage([makeComment('c1', 'post-1')]))

    const store = useCommentsStore()
    await store.loadComments('post-1')

    store.reset()

    expect(store.byPost).toEqual({})
  })
})
