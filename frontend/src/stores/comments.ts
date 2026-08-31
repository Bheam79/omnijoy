/**
 * Pinia store for post comments.
 *
 * - Keeps a map of { [postId]: PostCommentsState }.
 * - CommentThread components fetch/load their post's comments on first expand.
 * - Real-time new comments arrive via WallView's FeedHub "NewComment" event
 *   and are applied via applyNewComment().
 */
import { defineStore } from 'pinia'
import { ref } from 'vue'
import {
  commentService,
  type CommentDto,
  type CreateCommentPayload,
} from '@/services/commentService'
import type {
  CommentReactionCountsUpdatedEvent,
  PostReactionsDto,
  ReactionType,
} from '@/services/reactionService'

export interface PostCommentsState {
  items: CommentDto[]
  page: number
  hasMore: boolean
  loading: boolean
  /** Replies for each top-level comment, keyed by commentId. */
  replies: Record<string, CommentDto[]>
  repliesLoading: Record<string, boolean>
}

export const useCommentsStore = defineStore('comments', () => {
  // Keyed by postId
  const byPost = ref<Record<string, PostCommentsState>>({})

  // ── Internal helper ───────────────────────────────────────────────────────

  function ensurePost(postId: string): PostCommentsState {
    if (!byPost.value[postId]) {
      byPost.value[postId] = {
        items: [],
        page: 0,
        hasMore: true,
        loading: false,
        replies: {},
        repliesLoading: {},
      }
    }
    return byPost.value[postId]
  }

  function findComment(postId: string, commentId: string): CommentDto | undefined {
    const state = byPost.value[postId]
    if (!state) return undefined

    const topLevel = state.items.find(comment => comment.id === commentId)
    if (topLevel) return topLevel

    for (const replies of Object.values(state.replies)) {
      const reply = replies.find(comment => comment.id === commentId)
      if (reply) return reply
    }
  }

  function applyReactionSummary(comment: CommentDto, dto: PostReactionsDto) {
    comment.reactionsCount = dto.totalCount
    comment.topReactions = [...dto.counts]
      .sort((a, b) => b.count - a.count)
      .slice(0, 3)
    comment.myReaction = dto.currentUserReaction
  }

  // ── Load top-level comments ───────────────────────────────────────────────

  async function loadComments(postId: string) {
    const state = ensurePost(postId)
    if (state.loading) return
    state.loading = true
    state.page = 0
    state.hasMore = true
    try {
      const result = await commentService.getComments(postId, 1)
      state.items = result.items
      state.page = 1
      state.hasMore = result.hasMore
    } finally {
      state.loading = false
    }
  }

  async function loadMore(postId: string) {
    const state = ensurePost(postId)
    if (!state.hasMore || state.loading) return
    state.loading = true
    try {
      const nextPage = state.page + 1
      const result = await commentService.getComments(postId, nextPage)
      const knownIds = new Set(state.items.map((c) => c.id))
      const fresh = result.items.filter((c) => !knownIds.has(c.id))
      state.items.push(...fresh)
      state.page = nextPage
      state.hasMore = result.hasMore
    } finally {
      state.loading = false
    }
  }

  // ── Load replies ──────────────────────────────────────────────────────────

  async function loadReplies(postId: string, commentId: string) {
    const state = ensurePost(postId)
    if (state.repliesLoading[commentId]) return
    state.repliesLoading[commentId] = true
    try {
      const replies = await commentService.getReplies(commentId)
      state.replies[commentId] = replies
    } finally {
      state.repliesLoading[commentId] = false
    }
  }

  // ── Create ────────────────────────────────────────────────────────────────

  async function createComment(postId: string, payload: CreateCommentPayload): Promise<CommentDto> {
    const dto = await commentService.createComment(postId, payload)
    const state = ensurePost(postId)

    if (payload.parentCommentId) {
      // Reply: append to replies list (if it has been loaded)
      const existing = state.replies[payload.parentCommentId]
      if (existing !== undefined && !existing.some((r) => r.id === dto.id)) {
        state.replies[payload.parentCommentId] = [...existing, dto]
      }
      // Bump parent's replyCount
      const parent = state.items.find((c) => c.id === payload.parentCommentId)
      if (parent) parent.replyCount = Math.max(parent.replyCount, (existing?.length ?? 0) + 1)
    } else {
      // Top-level: prepend
      if (!state.items.some((c) => c.id === dto.id)) {
        state.items.unshift(dto)
      }
    }

    return dto
  }

  // ── Update ────────────────────────────────────────────────────────────────

  async function updateComment(postId: string, commentId: string, content: string): Promise<void> {
    const dto = await commentService.updateComment(commentId, { content })
    const state = ensurePost(postId)

    const idx = state.items.findIndex((c) => c.id === commentId)
    if (idx !== -1) {
      state.items[idx] = { ...state.items[idx], content: dto.content, updatedAt: dto.updatedAt }
      return
    }

    for (const [parentId, replies] of Object.entries(state.replies)) {
      const ridx = replies.findIndex((r) => r.id === commentId)
      if (ridx !== -1) {
        state.replies[parentId] = replies.map((r, i) =>
          i === ridx ? { ...r, content: dto.content, updatedAt: dto.updatedAt } : r,
        )
        return
      }
    }
  }

  // ── Delete (optimistic soft-delete) ──────────────────────────────────────

  async function deleteComment(postId: string, commentId: string): Promise<void> {
    await commentService.deleteComment(commentId)
    const state = ensurePost(postId)

    const idx = state.items.findIndex((c) => c.id === commentId)
    if (idx !== -1) {
      state.items[idx] = {
        ...state.items[idx],
        isDeleted: true,
        content: '[deleted]',
        reactionsCount: 0,
        topReactions: [],
        myReaction: null,
      }
      return
    }

    for (const [parentId, replies] of Object.entries(state.replies)) {
      const ridx = replies.findIndex((r) => r.id === commentId)
      if (ridx !== -1) {
        state.replies[parentId] = replies.map((r, i) =>
          i === ridx ? {
            ...r,
            isDeleted: true,
            content: '[deleted]',
            reactionsCount: 0,
            topReactions: [],
            myReaction: null,
          } : r,
        )
        return
      }
    }
  }

  // ── Real-time: called by WallView when FeedHub fires "NewComment" ─────────

  function applyNewComment(comment: CommentDto) {
    const state = ensurePost(comment.postId)

    if (comment.parentCommentId) {
      const existing = state.replies[comment.parentCommentId]
      if (existing !== undefined && !existing.some((r) => r.id === comment.id)) {
        state.replies[comment.parentCommentId] = [...existing, comment]
        // Bump parent replyCount
        const parent = state.items.find((c) => c.id === comment.parentCommentId)
        if (parent) parent.replyCount++
      }
    } else {
      if (!state.items.some((c) => c.id === comment.id)) {
        state.items.unshift(comment)
      }
    }
  }

  // ── Reactions ────────────────────────────────────────────────────────────────────────────────────

  function applyReactionUpdate(event: CommentReactionCountsUpdatedEvent) {
    const comment = findComment(event.postId, event.commentId)
    if (!comment || comment.isDeleted) return

    // Hub payloads contain absolute counts, so a local mutation response and
    // its broadcast can arrive in either order without double-incrementing.
    comment.reactionsCount = event.total
    comment.topReactions = [...event.counts]
      .sort((a, b) => b.count - a.count)
      .slice(0, 3)
  }

  async function addOrUpdateReaction(postId: string, commentId: string, reactionType: ReactionType) {
    const comment = findComment(postId, commentId)
    if (!comment || comment.isDeleted) return
    const previous = {
      reactionsCount: comment.reactionsCount,
      topReactions: comment.topReactions.map(count => ({ ...count })),
      myReaction: comment.myReaction,
    }

    const counts = comment.topReactions.map(count => ({ ...count }))
    if (comment.myReaction) {
      const old = counts.find(count => count.reactionType === comment.myReaction)
      if (old) old.count--
    }
    const selected = counts.find(count => count.reactionType === reactionType)
    if (selected) selected.count++
    else counts.push({ reactionType, count: 1 })
    comment.reactionsCount += comment.myReaction ? 0 : 1
    comment.topReactions = counts
      .filter(count => count.count > 0)
      .sort((a, b) => b.count - a.count)
      .slice(0, 3)
    comment.myReaction = reactionType

    try {
      applyReactionSummary(comment, await commentService.addOrUpdateReaction(commentId, reactionType))
    } catch {
      Object.assign(comment, previous)
    }
  }

  async function removeReaction(postId: string, commentId: string) {
    const comment = findComment(postId, commentId)
    if (!comment || comment.isDeleted || !comment.myReaction) return
    const previous = {
      reactionsCount: comment.reactionsCount,
      topReactions: comment.topReactions.map(count => ({ ...count })),
      myReaction: comment.myReaction,
    }

    const removedType = comment.myReaction
    comment.reactionsCount = Math.max(0, comment.reactionsCount - 1)
    comment.topReactions = comment.topReactions
      .map(count => count.reactionType === removedType ? { ...count, count: count.count - 1 } : count)
      .filter(count => count.count > 0)
    comment.myReaction = null

    try {
      applyReactionSummary(comment, await commentService.removeReaction(commentId))
    } catch {
      Object.assign(comment, previous)
    }
  }

  function reset() {
    byPost.value = {}
  }

  return {
    byPost,
    ensurePost,
    loadComments,
    loadMore,
    loadReplies,
    createComment,
    updateComment,
    deleteComment,
    applyNewComment,
    applyReactionUpdate,
    addOrUpdateReaction,
    removeReaction,
    reset,
  }
})
