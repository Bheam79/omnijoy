/**
 * REST client for /api/notifications + /api/users/presence.
 *
 * Real-time delivery (the bell badge, the dropdown live updates, presence
 * dots, etc.) is handled by the SignalR connection in `stores/notifications.ts`
 * and `stores/presence.ts`. This service only handles initial fetches and
 * the explicit mark-as-read actions.
 */
import api from './api'
import type { Notification, UserPresence } from '@/types'

export interface NotificationsPage {
  items: Notification[]
  page: number
  pageSize: number
  hasMore: boolean
  unreadCount: number
}

export const notificationService = {
  /** Fetch a paginated list of notifications (newest first). */
  async list(page = 1, pageSize = 20): Promise<NotificationsPage> {
    const { data } = await api.get<NotificationsPage>('/api/notifications', {
      params: { page, pageSize },
    })
    return data
  },

  /** Just the unread count — cheap call used on mount + after navigation. */
  async getUnreadCount(): Promise<number> {
    const { data } = await api.get<{ count: number }>('/api/notifications/unread/count')
    return data.count
  },

  async markRead(notificationId: string): Promise<void> {
    await api.post(`/api/notifications/${notificationId}/read`)
  },

  async markAllRead(): Promise<void> {
    await api.post('/api/notifications/read-all')
  },

  /** Batch-fetch presence for a list of user IDs. */
  async getPresence(userIds: string[]): Promise<UserPresence[]> {
    if (userIds.length === 0) return []
    const { data } = await api.get<UserPresence[]>('/api/users/presence', {
      params: { userIds: userIds.join(',') },
    })
    return data
  },
}
