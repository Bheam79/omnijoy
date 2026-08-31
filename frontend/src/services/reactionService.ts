import api from './api'

// ── Types ─────────────────────────────────────────────────────────────────────

export type ReactionType = 'Like' | 'Love' | 'Haha' | 'Wow' | 'Sad' | 'Angry'
export type ReactionTargetKind = 'post' | 'comment'

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

export interface CommentReactionCountsUpdatedEvent {
  commentId: string
  postId: string
  counts: ReactionCountDto[]
  total: number
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

function targetPath(targetId: string, targetKind: ReactionTargetKind): string {
  const collection = targetKind === 'post' ? 'posts' : 'comments'
  return `/api/${collection}/${targetId}/reactions`
}

export const reactionService = {
  async getReactions(targetId: string, targetKind: ReactionTargetKind = 'post'): Promise<PostReactionsDto> {
    const { data } = await api.get<PostReactionsDto>(targetPath(targetId, targetKind))
    return data
  },

  async addOrUpdateReaction(
    targetId: string,
    reactionType: ReactionType,
    targetKind: ReactionTargetKind = 'post',
  ): Promise<PostReactionsDto> {
    const { data } = await api.post<PostReactionsDto>(targetPath(targetId, targetKind), {
      reactionType,
    })
    return data
  },

  async removeReaction(targetId: string, targetKind: ReactionTargetKind = 'post'): Promise<PostReactionsDto> {
    const { data } = await api.delete<PostReactionsDto>(targetPath(targetId, targetKind))
    return data
  },

  async getReactionWho(targetId: string, targetKind: ReactionTargetKind = 'post'): Promise<ReactionWhoDto> {
    const { data } = await api.get<ReactionWhoDto>(`${targetPath(targetId, targetKind)}/who`)
    return data
  },
}
