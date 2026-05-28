import api from './api'
import type { User } from '@/types'

// ── DTOs (mirrors backend) ────────────────────────────────────────────────────

export interface UserProfile {
  id: string
  displayName: string
  avatarUrl?: string
  coverUrl?: string
  bio?: string
  gender: string
  birthDate?: string
  showBirthDate: boolean
  friendCount: number
  mutualFriendCount?: number
  isOwnProfile: boolean
  isFriend: boolean
  createdAt: string
}

export interface PrivacySettings {
  whoCanSeePosts: string
  whoCanSeeProfile: string
  whoCanSendMessages: string
  whoCanSeeFriendList: string
  whoCanSeeEvents: string
  whoCanTagInPosts: string
}

export interface UpdateProfilePayload {
  displayName?: string
  bio?: string
  gender?: string
  birthDate?: string
  showBirthDate?: boolean
}

export interface UpdatePrivacyPayload {
  whoCanSeePosts?: string
  whoCanSeeProfile?: string
  whoCanSendMessages?: string
  whoCanSeeFriendList?: string
  whoCanSeeEvents?: string
  whoCanTagInPosts?: string
}

// ── Service functions ─────────────────────────────────────────────────────────

export const userService = {
  async getProfile(userId: string): Promise<UserProfile> {
    const { data } = await api.get<UserProfile>(`/api/users/${userId}`)
    return data
  },

  async updateProfile(payload: UpdateProfilePayload): Promise<User> {
    const { data } = await api.put<User>('/api/users/me', payload)
    return data
  },

  async uploadAvatar(file: File): Promise<User> {
    const form = new FormData()
    form.append('file', file)
    const { data } = await api.post<User>('/api/users/me/avatar', form, {
      headers: { 'Content-Type': 'multipart/form-data' },
    })
    return data
  },

  async uploadCover(file: File): Promise<User> {
    const form = new FormData()
    form.append('file', file)
    const { data } = await api.post<User>('/api/users/me/cover', form, {
      headers: { 'Content-Type': 'multipart/form-data' },
    })
    return data
  },

  async getPrivacySettings(): Promise<PrivacySettings> {
    const { data } = await api.get<PrivacySettings>('/api/users/me/privacy')
    return data
  },

  async updatePrivacySettings(payload: UpdatePrivacyPayload): Promise<PrivacySettings> {
    const { data } = await api.put<PrivacySettings>('/api/users/me/privacy', payload)
    return data
  },
}
