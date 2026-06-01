import api from './api'

// ── Types ─────────────────────────────────────────────────────────────────────

export type ReactionType = 'Like' | 'Love' | 'Haha' | 'Wow' | 'Sad' | 'Angry'

export const REACTION_EMOJIS: Record<ReactionType, string> = {
  Like:  '👍',
  Love:  '❤️',
  Haha:  '😂',
  Wow:   '😮',
  Sad:   '😢',
  Angry: '😠',
}

export const REACTION_LABELS: Record<ReactionType, string> = {
  Like:  'Like',
  Love:  'Love',
  Haha:  'Haha',
  Wow:   'Wow',
  Sad:   'Sad',
  Angry: 'Angry',
}

export const ALL_REACTION_TYPES: ReactionType[] = [
  'Like', 'Love', 'Haha', 'Wow', 'Sad', 'Angry',
]

export interface ReactionCountDto {
  reactionType: ReactionType
  count: number
}

export interface PostReactionsDto {
  counts: ReactionCountDto[]
  totalCount: number
  currentUserReaction: ReactionType | null
}

// SignalR event shape matching backend ReactionCountsUpdatedEvent
export interface ReactionCountsUpdatedEvent {
  postId: string
  counts: ReactionCountDto[]
  totalCount: number
}

export interface ReactionWhoUserDto {
  id: string
  displayName: string
  avatarUrl: string | null
  isFriend: boolean
  reactionType: ReactionType
}

export interface ReactionWhoDto {
  people: ReactionWhoUserDto[]
  remaining: number
}

// ── Service ───────────────────────────────────────────────────────────────────

export const reactionService = {
  async getReactions(postId: string): Promise<PostReactionsDto> {
    const { data } = await api.get<PostReactionsDto>(`/api/posts/${postId}/reactions`)
    return data
  },

  async addOrUpdateReaction(postId: string, reactionType: ReactionType): Promise<PostReactionsDto> {
    const { data } = await api.post<PostReactionsDto>(`/api/posts/${postId}/reactions`, {
      reactionType,
    })
    return data
  },

  async removeReaction(postId: string): Promise<PostReactionsDto> {
    const { data } = await api.delete<PostReactionsDto>(`/api/posts/${postId}/reactions`)
    return data
  },

  async getReactionWho(postId: string): Promise<ReactionWhoDto> {
    const { data } = await api.get<ReactionWhoDto>(`/api/posts/${postId}/reactions/who`)
    return data
  },
}
