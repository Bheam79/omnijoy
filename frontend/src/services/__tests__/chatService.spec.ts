import { describe, it, expect, vi, beforeEach } from 'vitest'

// ── Mock the shared axios instance ───────────────────────────────────────────
// chatService imports the named { api } export, so we expose both to satisfy
// the module mock regardless of how each service imports it.
const mockApi = vi.hoisted(() => ({
  get:    vi.fn(),
  post:   vi.fn(),
  patch:  vi.fn(),
  put:    vi.fn(),
  delete: vi.fn(),
}))

vi.mock('@/services/api', () => ({
  default: mockApi,
  api:     mockApi,
}))

// Import after mock so chatService picks up the mocked api
import { chatService } from '@/services/chatService'

describe('chatService', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  // ── getConversations ─────────────────────────────────────────────────────

  it('getConversations → GETs /api/conversations and returns array', async () => {
    const convs = [{ id: 'conv-1', otherUser: { id: 'u-2', displayName: 'Bob' } }]
    mockApi.get.mockResolvedValue({ data: convs })

    const result = await chatService.getConversations()

    expect(mockApi.get).toHaveBeenCalledWith('/api/conversations')
    expect(result).toHaveLength(1)
  })

  it('getConversations → returns empty array when no conversations', async () => {
    mockApi.get.mockResolvedValue({ data: [] })

    const result = await chatService.getConversations()

    expect(result).toEqual([])
  })

  // ── getOrCreateDirect ────────────────────────────────────────────────────

  it('getOrCreateDirect → POSTs /api/conversations/direct/:id', async () => {
    const conv = { id: 'conv-1', otherUser: { id: 'u-2', displayName: 'Bob' } }
    mockApi.post.mockResolvedValue({ data: conv })

    const result = await chatService.getOrCreateDirect('u-2')

    expect(mockApi.post).toHaveBeenCalledWith('/api/conversations/direct/u-2')
    expect(result.id).toBe('conv-1')
  })

  // ── getMessages ──────────────────────────────────────────────────────────

  it('getMessages → GETs /api/conversations/:id/messages with default limit=30', async () => {
    mockApi.get.mockResolvedValue({ data: [] })

    await chatService.getMessages('conv-1')

    expect(mockApi.get).toHaveBeenCalledWith('/api/conversations/conv-1/messages', {
      params: { limit: 30 },
    })
  })

  it('getMessages → includes before param when provided', async () => {
    mockApi.get.mockResolvedValue({ data: [] })

    await chatService.getMessages('conv-1', '2026-01-01T00:00:00Z', 30)

    expect(mockApi.get).toHaveBeenCalledWith('/api/conversations/conv-1/messages', {
      params: { limit: 30, before: '2026-01-01T00:00:00Z' },
    })
  })

  it('getMessages → omits before when undefined', async () => {
    mockApi.get.mockResolvedValue({ data: [] })

    await chatService.getMessages('conv-1', undefined, 10)

    const { params } = mockApi.get.mock.calls[0][1]
    expect(params).not.toHaveProperty('before')
    expect(params.limit).toBe(10)
  })

  it('getMessages → honours custom limit', async () => {
    mockApi.get.mockResolvedValue({ data: [] })

    await chatService.getMessages('conv-2', undefined, 50)

    const { params } = mockApi.get.mock.calls[0][1]
    expect(params.limit).toBe(50)
  })

  // ── sendMessage ──────────────────────────────────────────────────────────

  it('sendMessage → POSTs /api/messages with FormData', async () => {
    const msg = { id: 'msg-1', content: 'Hello' }
    mockApi.post.mockResolvedValue({ data: msg })

    const result = await chatService.sendMessage('conv-1', 'Hello')

    expect(mockApi.post).toHaveBeenCalledOnce()
    const [url, body] = mockApi.post.mock.calls[0]
    expect(url).toBe('/api/messages')
    expect(body).toBeInstanceOf(FormData)
    expect(result.id).toBe('msg-1')
  })

  it('sendMessage → FormData contains conversationId field', async () => {
    mockApi.post.mockResolvedValue({ data: { id: 'msg-1' } })

    await chatService.sendMessage('conv-42', 'Hi')

    const form: FormData = mockApi.post.mock.calls[0][1]
    expect(form.get('conversationId')).toBe('conv-42')
  })

  it('sendMessage → FormData contains trimmed content when provided', async () => {
    mockApi.post.mockResolvedValue({ data: { id: 'msg-1' } })

    await chatService.sendMessage('conv-1', '  trimmed  ')

    const form: FormData = mockApi.post.mock.calls[0][1]
    expect(form.get('content')).toBe('trimmed')
  })

  it('sendMessage → omits content field when content is blank/whitespace', async () => {
    mockApi.post.mockResolvedValue({ data: { id: 'msg-1' } })

    await chatService.sendMessage('conv-1', '   ')

    const form: FormData = mockApi.post.mock.calls[0][1]
    expect(form.get('content')).toBeNull()
  })

  it('sendMessage → appends file to FormData when provided', async () => {
    mockApi.post.mockResolvedValue({ data: { id: 'msg-2' } })

    const file = new File(['data'], 'photo.jpg', { type: 'image/jpeg' })
    await chatService.sendMessage('conv-1', undefined, file)

    const form: FormData = mockApi.post.mock.calls[0][1]
    expect(form.get('file')).toBe(file)
  })

  it('sendMessage → does not append file field when no file given', async () => {
    mockApi.post.mockResolvedValue({ data: { id: 'msg-1' } })

    await chatService.sendMessage('conv-1', 'text only')

    const form: FormData = mockApi.post.mock.calls[0][1]
    expect(form.get('file')).toBeNull()
  })

  it('sendMessage → sends multipart/form-data content-type header', async () => {
    mockApi.post.mockResolvedValue({ data: { id: 'msg-1' } })

    await chatService.sendMessage('conv-1', 'hello')

    const options = mockApi.post.mock.calls[0][2]
    expect(options?.headers?.['Content-Type']).toBe('multipart/form-data')
  })

  // ── deleteMessage ────────────────────────────────────────────────────────

  it('deleteMessage → DELETEs /api/messages/:id and returns message', async () => {
    const msg = { id: 'msg-1', isDeleted: true }
    mockApi.delete.mockResolvedValue({ data: msg })

    const result = await chatService.deleteMessage('msg-1')

    expect(mockApi.delete).toHaveBeenCalledWith('/api/messages/msg-1')
    expect(result.id).toBe('msg-1')
  })

  // ── markRead ─────────────────────────────────────────────────────────────

  it('markRead → PUTs /api/conversations/:id/read', async () => {
    mockApi.put.mockResolvedValue({ data: undefined })

    await chatService.markRead('conv-1')

    expect(mockApi.put).toHaveBeenCalledWith('/api/conversations/conv-1/read')
  })

  it('markRead → resolves without a return value', async () => {
    mockApi.put.mockResolvedValue({ data: undefined })

    const result = await chatService.markRead('conv-1')

    expect(result).toBeUndefined()
  })

  // ── getMetaPreview ───────────────────────────────────────────────────────

  it('getMetaPreview → GETs /api/meta-preview with url param and returns data', async () => {
    const preview = { url: 'https://example.com', title: 'Example' }
    mockApi.get.mockResolvedValue({ data: preview })

    const result = await chatService.getMetaPreview('https://example.com')

    expect(mockApi.get).toHaveBeenCalledWith('/api/meta-preview', {
      params: { url: 'https://example.com' },
    })
    expect(result?.title).toBe('Example')
  })

  it('getMetaPreview → returns null when the request throws', async () => {
    mockApi.get.mockRejectedValue(new Error('network error'))

    const result = await chatService.getMetaPreview('https://bad.example.com')

    expect(result).toBeNull()
  })

  it('getMetaPreview → returns null on HTTP error response', async () => {
    const err = Object.assign(new Error('404'), { response: { status: 404 } })
    mockApi.get.mockRejectedValue(err)

    const result = await chatService.getMetaPreview('https://missing.example.com')

    expect(result).toBeNull()
  })
})
