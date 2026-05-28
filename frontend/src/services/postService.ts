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

/** Open Graph / link preview attached to a post. */
export interface PostLinkPreview {
  url: string
  title?: string
  description?: string
  imageUrl?: string
  siteName?: string
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
  linkPreview?: PostLinkPreview
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
  /** Post on behalf of this company page (user must be admin). */
  companyPageId?: string
  /** Embedded link preview (fetched at compose time via /api/meta-preview). */
  linkPreview?: PostLinkPreview
}

/** Result returned by GET /api/meta-preview?url=... */
export interface OgPreviewDto {
  url: string
  title?: string
  description?: string
  imageUrl?: string
  siteName?: string
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
    if (payload.companyPageId) form.append('companyPageId', payload.companyPageId)
    if (payload.linkPreview) {
      form.append('linkUrl', payload.linkPreview.url)
      if (payload.linkPreview.title) form.append('linkTitle', payload.linkPreview.title)
      if (payload.linkPreview.description) form.append('linkDescription', payload.linkPreview.description)
      if (payload.linkPreview.imageUrl) form.append('linkImageUrl', payload.linkPreview.imageUrl)
      if (payload.linkPreview.siteName) form.append('linkSiteName', payload.linkPreview.siteName)
    }
    const { data } = await api.post<PostDto>('/api/posts', form, {
      headers: { 'Content-Type': 'multipart/form-data' },
    })
    return data
  },

  /**
   * Fetches an Open Graph preview for an external URL. The backend caches
   * results for 30 minutes; safe to call on every paste / debounce tick.
   */
  async fetchMetaPreview(url: string): Promise<OgPreviewDto> {
    const { data } = await api.get<OgPreviewDto>('/api/meta-preview', {
      params: { url },
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
