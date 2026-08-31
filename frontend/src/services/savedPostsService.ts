import api from './api'
import type { PostDto } from './postService'

export interface SavedPostStateDto {
  isSaved: boolean
  changed: boolean
}

export interface SavedPostsPageResult {
  items: PostDto[]
  page: number
  pageSize: number
  hasMore: boolean
}

export const savedPostsService = {
  async getSavedPosts(page = 1, pageSize = 20): Promise<SavedPostsPageResult> {
    const { data } = await api.get<SavedPostsPageResult>('/api/users/me/saved-posts', {
      params: { page, pageSize },
    })
    return data
  },

  async savePost(postId: string): Promise<SavedPostStateDto> {
    const { data } = await api.post<SavedPostStateDto>(`/api/posts/${postId}/save`)
    return data
  },

  async unsavePost(postId: string): Promise<SavedPostStateDto> {
    const { data } = await api.delete<SavedPostStateDto>(`/api/posts/${postId}/save`)
    return data
  },
}
