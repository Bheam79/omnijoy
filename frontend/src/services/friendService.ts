import api from './api'

// ── DTOs (mirrors backend) ────────────────────────────────────────────────────

export interface FriendUserDto {
  id: string
  displayName: string
  avatarUrl?: string
}

export interface FamilyRelationDto {
  relationType: string
  subtype: string
}

export interface FriendDto {
  friendshipId: string
  user: FriendUserDto
  mutualFriendCount: number
  friendedAt: string
  familyRelation?: FamilyRelationDto
}

export interface FriendsPageResult {
  items: FriendDto[]
  page: number
  pageSize: number
  hasMore: boolean
}

export interface FriendRequestDto {
  friendshipId: string
  from: FriendUserDto
  sentAt: string
}

export interface FriendSuggestionDto {
  user: FriendUserDto
  mutualFriendCount: number
}

/** "None" | "RequestSent" | "RequestReceived" | "Accepted" | "BlockedByMe" | "BlockedByThem" | "Self" */
export type FriendStatus =
  | 'None'
  | 'RequestSent'
  | 'RequestReceived'
  | 'Accepted'
  | 'BlockedByMe'
  | 'BlockedByThem'
  | 'Self'

export interface SetFamilyRelationPayload {
  relationType?: string | null
  subtype?: string | null
}

// ── Service ───────────────────────────────────────────────────────────────────

export const friendService = {
  // ── Request lifecycle ─────────────────────────────────────────────────────

  async sendRequest(userId: string): Promise<FriendRequestDto> {
    const { data } = await api.post<FriendRequestDto>(`/api/friends/request/${userId}`)
    return data
  },

  async acceptRequest(userId: string): Promise<FriendDto> {
    const { data } = await api.post<FriendDto>(`/api/friends/accept/${userId}`)
    return data
  },

  async declineRequest(userId: string): Promise<void> {
    await api.post(`/api/friends/decline/${userId}`)
  },

  async cancelRequest(userId: string): Promise<void> {
    await api.delete(`/api/friends/request/${userId}`)
  },

  // ── Friendship management ─────────────────────────────────────────────────

  async removeFriend(userId: string): Promise<void> {
    await api.delete(`/api/friends/${userId}`)
  },

  async blockUser(userId: string): Promise<void> {
    await api.post(`/api/friends/block/${userId}`)
  },

  async unblockUser(userId: string): Promise<void> {
    await api.delete(`/api/friends/block/${userId}`)
  },

  // ── Queries ───────────────────────────────────────────────────────────────

  async getFriends(page = 1, pageSize = 20): Promise<FriendsPageResult> {
    const { data } = await api.get<FriendsPageResult>('/api/friends', { params: { page, pageSize } })
    return data
  },

  async getPendingRequests(): Promise<FriendRequestDto[]> {
    const { data } = await api.get<FriendRequestDto[]>('/api/friends/requests')
    return data
  },

  async getSentRequests(): Promise<FriendRequestDto[]> {
    const { data } = await api.get<FriendRequestDto[]>('/api/friends/sent')
    return data
  },

  async getSuggestions(limit = 10): Promise<FriendSuggestionDto[]> {
    const { data } = await api.get<FriendSuggestionDto[]>('/api/friends/suggestions', { params: { limit } })
    return data
  },

  async getStatus(userId: string): Promise<FriendStatus> {
    const { data } = await api.get<{ status: FriendStatus }>(`/api/friends/status/${userId}`)
    return data.status
  },

  async getPendingCount(): Promise<number> {
    const { data } = await api.get<{ count: number }>('/api/friends/requests/count')
    return data.count
  },

  // ── Family relation ───────────────────────────────────────────────────────

  async setFamilyRelation(userId: string, payload: SetFamilyRelationPayload): Promise<FamilyRelationDto | null> {
    const { data } = await api.put<{ relation: FamilyRelationDto | null }>(`/api/friends/${userId}/relation`, payload)
    return data.relation
  },
}
