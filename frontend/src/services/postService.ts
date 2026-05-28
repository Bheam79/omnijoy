import api from './api'

// ── DTOs (mirrors backend PostDtos.cs) ───────────────────────────────────────

export interface PostAuthor {
  id: string
  displayName: string
  avatarUrl?: string
}

export interface PostMediaItem {
  id: string
  mediaType: 'Image' | 'Video'
  url: string
  thumbnailUrl?: string
  order: number
}

export interface PostDto {
  id: string
  author: PostAuthor
  companyPageId?: string
  content: string
  backgroundImageUrl?: string
  postType: 'Text' | 'Image' | 'Video' | 'TextOnBackground'
  privacy: 'Everyone' | 'Friends' | 'OnlyMe'
  media: PostMediaItem[]
  createdAt: string
  updatedAt: string
}

export interface FeedPageResult {
  items: PostDto[]
  page: number
  pageSize: number
  hasMore: boolean
}

export interface CreatePostPayload {
  content: string
  postType: 'Text' | 'Image' | 'Video' | 'TextOnBackground'
  privacy: 'Everyone' | 'Friends' | 'OnlyMe'
  background?: string
  mediaFiles?: File[]
}

export interface UpdatePostPayload {
  content?: string
  privacy?: string
}

// ── Service ───────────────────────────────────────────────────────────────────

export const postService = {
  async createPost(payload: CreatePostPayload): Promise<PostDto> {
    const form = new FormData()
    form.append('content', payload.content)
    form.append('postType', payload.postType)
    form.append('privacy', payload.privacy)
    if (payload.background) form.append('background', payload.background)
    if (payload.mediaFiles) {
      for (const file of payload.mediaFiles) {
        form.append('media', file)
      }
    }
    const { data } = await api.post<PostDto>('/api/posts', form, {
      headers: { 'Content-Type': 'multipart/form-data' },
    })
    return data
  },

  async getFeed(page = 1, pageSize = 20): Promise<FeedPageResult> {
    const { data } = await api.get<FeedPageResult>('/api/feed', {
      params: { page, pageSize },
    })
    return data
  },

  async getPost(id: string): Promise<PostDto> {
    const { data } = await api.get<PostDto>(`/api/posts/${id}`)
    return data
  },

  async updatePost(id: string, payload: UpdatePostPayload): Promise<PostDto> {
    const { data } = await api.put<PostDto>(`/api/posts/${id}`, payload)
    return data
  },

  async deletePost(id: string): Promise<void> {
    await api.delete(`/api/posts/${id}`)
  },
}
