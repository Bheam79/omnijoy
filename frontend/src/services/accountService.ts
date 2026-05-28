import api from './api'

export interface ChangeEmailPayload {
  newEmail: string
  currentPassword: string
}

export interface ChangePasswordPayload {
  currentPassword: string
  newPassword: string
  confirmNewPassword: string
}

/**
 * Mirrors NotificationPreferencesDto on the backend. Booleans default to true
 * server-side, so a freshly registered user starts with every notification on.
 */
export interface NotificationPreferences {
  // Post activity
  likesOnMyPosts: boolean
  commentsOnMyPosts: boolean
  postShares: boolean

  // Social
  friendRequests: boolean
  newFollower: boolean
  mentions: boolean
  newPostsFromFriends: boolean
  familyRelations: boolean

  // Events
  eventInvites: boolean

  // System
  directMessages: boolean
  liveStreams: boolean
  companyPageInvites: boolean
}

export const accountService = {
  async changeEmail(payload: ChangeEmailPayload): Promise<void> {
    await api.post('/api/account/change-email', payload)
  },

  async changePassword(payload: ChangePasswordPayload): Promise<void> {
    await api.post('/api/account/change-password', payload)
  },

  async getNotificationPreferences(): Promise<NotificationPreferences> {
    const { data } = await api.get<NotificationPreferences>('/api/account/notification-preferences')
    return data
  },

  async updateNotificationPreferences(
    payload: NotificationPreferences,
  ): Promise<NotificationPreferences> {
    const { data } = await api.put<NotificationPreferences>(
      '/api/account/notification-preferences',
      payload,
    )
    return data
  },
}
