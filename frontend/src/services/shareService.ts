import api from './api'
import type { SharedPostFeedItemDto } from './postService'

// ── Request payload ───────────────────────────────────────────────────────────

export interface CreateSharePayload {
  /** "OwnWall" | "FriendWall" | "CompanyPage" */
  targetType: 'OwnWall' | 'FriendWall' | 'CompanyPage'
  /** Friend user ID (FriendWall) or company page ID (CompanyPage). Omit for OwnWall. */
  targetId?: string
  message?: string
}

// ── Service ───────────────────────────────────────────────────────────────────

export const shareService = {
  /**
   * Share post `postId` according to the given payload.
   * POST /api/posts/{id}/share
   */
  async sharePost(postId: string, payload: CreateSharePayload): Promise<SharedPostFeedItemDto> {
    const { data } = await api.post<SharedPostFeedItemDto>(
      `/api/posts/${postId}/share`,
      payload,
    )
    return data
  },
}
