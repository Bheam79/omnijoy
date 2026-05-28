/**
 * Chat / Messenger API service.
 * All endpoints require a valid JWT (Bearer token).
 */
import { api } from '@/services/api'
import type { Conversation, Message, OgPreview } from '@/types'

export const chatService = {
  // ── Conversations ────────────────────────────────────────────────────────────

  async getConversations(): Promise<Conversation[]> {
    const { data } = await api.get<Conversation[]>('/api/conversations')
    return data
  },

  /** Start or retrieve an existing 1:1 conversation with another user. */
  async getOrCreateDirect(userId: string): Promise<Conversation> {
    const { data } = await api.post<Conversation>(`/api/conversations/direct/${userId}`)
    return data
  },

  // ── Messages ─────────────────────────────────────────────────────────────────

  /**
   * Load messages for a conversation.
   * Pass `before` (ISO-8601 string) for cursor-based pagination of older messages.
   */
  async getMessages(
    conversationId: string,
    before?: string,
    limit = 30,
  ): Promise<Message[]> {
    const params: Record<string, unknown> = { limit }
    if (before) params.before = before
    const { data } = await api.get<Message[]>(
      `/api/conversations/${conversationId}/messages`,
      { params },
    )
    return data
  },

  /**
   * Send a message.  Pass a `file` to attach an image, video, or file.
   * `onUploadProgress` is called with 0–100 percentage.
   */
  async sendMessage(
    conversationId: string,
    content?: string,
    file?: File,
    onUploadProgress?: (percent: number) => void,
  ): Promise<Message> {
    const form = new FormData()
    form.append('conversationId', conversationId)
    if (content?.trim()) form.append('content', content.trim())
    if (file) form.append('file', file)

    const { data } = await api.post<Message>('/api/messages', form, {
      headers: { 'Content-Type': 'multipart/form-data' },
      onUploadProgress: onUploadProgress
        ? (e) => {
            if (e.total) onUploadProgress(Math.round((e.loaded * 100) / e.total))
          }
        : undefined,
    })
    return data
  },

  /** Soft-delete a message (only the sender can delete). */
  async deleteMessage(messageId: string): Promise<Message> {
    const { data } = await api.delete<Message>(`/api/messages/${messageId}`)
    return data
  },

  /** Mark all messages in a conversation as read (resets unread badge). */
  async markRead(conversationId: string): Promise<void> {
    await api.put(`/api/conversations/${conversationId}/read`)
  },

  // ── URL / OG preview ─────────────────────────────────────────────────────────

  /** Fetch Open Graph preview data for a URL. Returns null on network error. */
  async getMetaPreview(url: string): Promise<OgPreview | null> {
    try {
      const { data } = await api.get<OgPreview>('/api/meta-preview', {
        params: { url },
      })
      return data
    } catch {
      return null
    }
  },
}
