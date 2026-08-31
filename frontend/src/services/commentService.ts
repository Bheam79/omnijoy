import api from './api'
import type { MentionDto } from '@/types/mentions'

export type { MentionDto } from '@/types/mentions'

// ── DTOs (mirrors backend CommentDtos.cs) ────────────────────────────────────

export interface CommentAuthor {
  id: string
  displayName: string
  avatarUrl?: string
}

export interface CommentDto {
  id: string
  postId: string
  author: CommentAuthor
  parentCommentId?: string | null
  content: string
  mentions: MentionDto[]
  replyCount: number
  createdAt: string
  updatedAt: string
  isDeleted: boolean
}

export interface CommentsPageResult {
  items: CommentDto[]
  page: number
  pageSize: number
  hasMore: boolean
}

export interface CreateCommentPayload {
  content: string
  parentCommentId?: string | null
}

export interface UpdateCommentPayload {
  content: string
}

// ── Service ───────────────────────────────────────────────────────────────────

export const commentService = {
  async createComment(postId: string, payload: CreateCommentPayload): Promise<CommentDto> {
    const { data } = await api.post<CommentDto>(`/api/posts/${postId}/comments`, payload)
    return data
  },

  async getComments(postId: string, page = 1, pageSize = 20): Promise<CommentsPageResult> {
    const { data } = await api.get<CommentsPageResult>(`/api/posts/${postId}/comments`, {
      params: { page, pageSize },
    })
    return data
  },

  async getReplies(commentId: string): Promise<CommentDto[]> {
    const { data } = await api.get<CommentDto[]>(`/api/comments/${commentId}/replies`)
    return data
  },

  async updateComment(commentId: string, payload: UpdateCommentPayload): Promise<CommentDto> {
    const { data } = await api.put<CommentDto>(`/api/comments/${commentId}`, payload)
    return data
  },

  async deleteComment(commentId: string): Promise<void> {
    await api.delete(`/api/comments/${commentId}`)
  },
}
