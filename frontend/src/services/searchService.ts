import api from './api'

// ── DTOs (mirrors backend SearchDtos.cs) ─────────────────────────────────────

export interface UserSearchResult {
  id: string
  displayName: string
  avatarUrl?: string
  bio?: string
}

export interface PostSearchResult {
  id: string
  authorId: string
  authorDisplayName: string
  authorAvatarUrl?: string
  content: string
  postType: 'Text' | 'Image' | 'Video' | 'TextOnBackground'
  privacy: 'Everyone' | 'Friends' | 'OnlyMe'
  createdAt: string
}

export interface EventSearchResult {
  id: string
  title: string
  description?: string
  startAt: string
  endAt?: string
  location?: string
  coverImageUrl?: string
  creatorId: string
  creatorDisplayName: string
  companyPageId?: string
}

export interface CompanySearchResult {
  id: string
  name: string
  description?: string
  logoUrl?: string
  followerCount: number
}

/** Type discriminator accepted by the backend `/api/search` endpoint. */
export type SearchType = 'all' | 'users' | 'posts' | 'events' | 'companies'

export interface SearchResponse {
  query: string
  /** Type the result was filtered to. */
  type: SearchType
  users:     UserSearchResult[]
  posts:     PostSearchResult[]
  events:    EventSearchResult[]
  companies: CompanySearchResult[]
  page:      number
  pageSize:  number
  hasMore:   boolean
}

// ── Service ───────────────────────────────────────────────────────────────────

export const searchService = {
  /**
   * Full search query — backs the `/search` results page.
   * When `type === 'all'`, each array is filled with up to `pageSize` items.
   * Otherwise only the matching category is populated, and `hasMore` refers
   * to that single category.
   */
  async search(params: {
    q: string
    type?: SearchType
    page?: number
    pageSize?: number
  }): Promise<SearchResponse> {
    const { data } = await api.get<SearchResponse>('/api/search', {
      params: {
        q:        params.q,
        type:     params.type     ?? 'all',
        page:     params.page     ?? 1,
        pageSize: params.pageSize ?? 20,
      },
    })
    return data
  },

  /**
   * Lightweight auto-suggest used by the top-nav dropdown. Returns at most
   * 5 results per category in a single round-trip.
   */
  async suggest(q: string): Promise<SearchResponse> {
    const { data } = await api.get<SearchResponse>('/api/search/suggest', {
      params: { q },
    })
    return data
  },
}

export default searchService
