/**
 * Pinia store for post reaction state.
 *
 * - Keeps a map of { [postId]: PostReactionsDto }.
 * - PostReactionBar components fetch once on mount; subsequent updates arrive
 *   via WallView's FeedHub connection (ReactionCountsUpdated event).
 */
import { defineStore } from 'pinia'
import { ref } from 'vue'
import {
  reactionService,
  type PostReactionsDto,
  type ReactionType,
  type ReactionCountsUpdatedEvent,
} from '@/services/reactionService'

export const useReactionsStore = defineStore('reactions', () => {
  // Keyed by postId
  const byPost = ref<Record<string, PostReactionsDto>>({})

  // ── Fetch ─────────────────────────────────────────────────────────────────

  async function fetchReactions(postId: string) {
    try {
      const dto = await reactionService.getReactions(postId)
      byPost.value[postId] = dto
    } catch {
      // silently ignore — component shows empty state
    }
  }

  // ── Apply real-time update from SignalR ────────────────────────────────────

  function applyUpdate(event: ReactionCountsUpdatedEvent) {
    const existing = byPost.value[event.postId]
    byPost.value[event.postId] = {
      counts:              event.counts,
      totalCount:          event.totalCount,
      currentUserReaction: existing?.currentUserReaction ?? null,
    }
  }

  // ── Add or change reaction ─────────────────────────────────────────────────

  async function addOrUpdate(postId: string, reactionType: ReactionType) {
    // Optimistic update
    const prev = byPost.value[postId]
    if (prev) {
      optimisticallySet(postId, reactionType)
    }
    try {
      const dto = await reactionService.addOrUpdateReaction(postId, reactionType)
      byPost.value[postId] = dto
    } catch {
      // Roll back to previous state on failure
      if (prev) byPost.value[postId] = prev
    }
  }

  // ── Remove reaction ────────────────────────────────────────────────────────

  async function remove(postId: string) {
    const prev = byPost.value[postId]
    // Optimistic clear
    if (prev) {
      byPost.value[postId] = {
        ...prev,
        currentUserReaction: null,
        totalCount: Math.max(0, prev.totalCount - 1),
        counts: prev.counts
          .map(c =>
            c.reactionType === prev.currentUserReaction
              ? { ...c, count: Math.max(0, c.count - 1) }
              : c,
          )
          .filter(c => c.count > 0),
      }
    }
    try {
      const dto = await reactionService.removeReaction(postId)
      byPost.value[postId] = dto
    } catch {
      if (prev) byPost.value[postId] = prev
    }
  }

  // ── Helpers ────────────────────────────────────────────────────────────────

  function optimisticallySet(postId: string, reactionType: ReactionType) {
    const prev = byPost.value[postId]
    if (!prev) return

    const oldType = prev.currentUserReaction
    let counts = prev.counts.map(c => ({ ...c }))

    // Remove old reaction from counts
    if (oldType) {
      counts = counts
        .map(c => c.reactionType === oldType ? { ...c, count: Math.max(0, c.count - 1) } : c)
        .filter(c => c.count > 0)
    }

    // Add new reaction to counts
    const existing = counts.find(c => c.reactionType === reactionType)
    if (existing) {
      existing.count++
    } else {
      counts.push({ reactionType, count: 1 })
    }

    const totalDelta = oldType ? 0 : 1
    byPost.value[postId] = {
      counts,
      totalCount: prev.totalCount + totalDelta,
      currentUserReaction: reactionType,
    }
  }

  function reset() {
    byPost.value = {}
  }

  return {
    byPost,
    fetchReactions,
    applyUpdate,
    addOrUpdate,
    remove,
    reset,
  }
})
