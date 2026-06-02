import { describe, it, expect, vi, beforeEach } from 'vitest'

// ── Mock the shared axios instance ───────────────────────────────────────────
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

// Import after mock so postService picks up the mocked api
import { postService } from '@/services/postService'

const makePost = (overrides = {}) => ({
  id: 'post-1',
  author: { id: 'u-1', displayName: 'Alice' },
  content: 'Hello world',
  postType: 'Text' as const,
  privacy: 'Everyone' as const,
  media: [],
  createdAt: '2026-01-01T00:00:00Z',
  updatedAt: '2026-01-01T00:00:00Z',
  ...overrides,
})

describe('postService', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  // ── createPost ───────────────────────────────────────────────────────────

  it('createPost → POSTs /api/posts with FormData', async () => {
    const post = makePost()
    mockApi.post.mockResolvedValue({ data: post })

    const result = await postService.createPost({
      content: 'Hello world',
      postType: 'Text',
      privacy: 'Everyone',
    })

    expect(mockApi.post).toHaveBeenCalledOnce()
    const [url, body] = mockApi.post.mock.calls[0]
    expect(url).toBe('/api/posts')
    expect(body).toBeInstanceOf(FormData)
    expect(result.id).toBe('post-1')
  })

  it('createPost → FormData contains content, postType, and privacy', async () => {
    mockApi.post.mockResolvedValue({ data: makePost() })

    await postService.createPost({
      content: 'My post',
      postType: 'Image',
      privacy: 'Friends',
    })

    const form: FormData = mockApi.post.mock.calls[0][1]
    expect(form.get('content')).toBe('My post')
    expect(form.get('postType')).toBe('Image')
    expect(form.get('privacy')).toBe('Friends')
  })

  it('createPost → sends multipart/form-data content-type header', async () => {
    mockApi.post.mockResolvedValue({ data: makePost() })

    await postService.createPost({ content: 'x', postType: 'Text', privacy: 'OnlyMe' })

    const options = mockApi.post.mock.calls[0][2]
    expect(options?.headers?.['Content-Type']).toBe('multipart/form-data')
  })

  it('createPost → appends media files to FormData when provided', async () => {
    mockApi.post.mockResolvedValue({ data: makePost({ postType: 'Image' }) })

    const file = new File(['img'], 'photo.png', { type: 'image/png' })
    await postService.createPost({
      content: '',
      postType: 'Image',
      privacy: 'Everyone',
      mediaFiles: [file],
    })

    const form: FormData = mockApi.post.mock.calls[0][1]
    expect(form.get('media')).toBe(file)
  })

  it('createPost → includes background when provided', async () => {
    mockApi.post.mockResolvedValue({ data: makePost({ postType: 'TextOnBackground' }) })

    await postService.createPost({
      content: 'text',
      postType: 'TextOnBackground',
      privacy: 'Everyone',
      background: '#ff0000',
    })

    const form: FormData = mockApi.post.mock.calls[0][1]
    expect(form.get('background')).toBe('#ff0000')
  })

  it('createPost → does not append background when not provided', async () => {
    mockApi.post.mockResolvedValue({ data: makePost() })

    await postService.createPost({ content: 'text', postType: 'Text', privacy: 'Everyone' })

    const form: FormData = mockApi.post.mock.calls[0][1]
    expect(form.get('background')).toBeNull()
  })

  // ── fetchMetaPreview ─────────────────────────────────────────────────────

  it('fetchMetaPreview → GETs /api/meta-preview with url param', async () => {
    const preview = { url: 'https://example.com', title: 'Example', description: 'Desc' }
    mockApi.get.mockResolvedValue({ data: preview })

    const result = await postService.fetchMetaPreview('https://example.com')

    expect(mockApi.get).toHaveBeenCalledWith('/api/meta-preview', {
      params: { url: 'https://example.com' },
    })
    expect(result.title).toBe('Example')
  })

  // ── getFeed ──────────────────────────────────────────────────────────────

  it('getFeed → GETs /api/feed with page + pageSize', async () => {
    const feedPage = { items: [], page: 1, pageSize: 20, hasMore: false }
    mockApi.get.mockResolvedValue({ data: feedPage })

    const result = await postService.getFeed(1, 20)

    expect(mockApi.get).toHaveBeenCalledWith('/api/feed', {
      params: { page: 1, pageSize: 20 },
    })
    expect(result.items).toEqual([])
  })

  it('getFeed → uses default page=1, pageSize=20 when omitted', async () => {
    const feedPage = { items: [], page: 1, pageSize: 20, hasMore: false }
    mockApi.get.mockResolvedValue({ data: feedPage })

    await postService.getFeed()

    expect(mockApi.get).toHaveBeenCalledWith('/api/feed', {
      params: { page: 1, pageSize: 20 },
    })
  })

  // ── getPost ──────────────────────────────────────────────────────────────

  it('getPost → GETs /api/posts/:id', async () => {
    const post = makePost()
    mockApi.get.mockResolvedValue({ data: post })

    const result = await postService.getPost('post-1')

    expect(mockApi.get).toHaveBeenCalledWith('/api/posts/post-1')
    expect(result.id).toBe('post-1')
  })

  // ── updatePost ───────────────────────────────────────────────────────────

  it('updatePost → PUTs /api/posts/:id with payload', async () => {
    const updated = makePost({ content: 'edited' })
    mockApi.put.mockResolvedValue({ data: updated })

    const result = await postService.updatePost('post-1', { content: 'edited' })

    expect(mockApi.put).toHaveBeenCalledWith('/api/posts/post-1', { content: 'edited' })
    expect(result.content).toBe('edited')
  })

  it('updatePost → can update privacy only', async () => {
    mockApi.put.mockResolvedValue({ data: makePost({ privacy: 'OnlyMe' }) })

    await postService.updatePost('post-1', { privacy: 'OnlyMe' })

    expect(mockApi.put).toHaveBeenCalledWith('/api/posts/post-1', { privacy: 'OnlyMe' })
  })

  // ── deletePost ───────────────────────────────────────────────────────────

  it('deletePost → DELETEs /api/posts/:id', async () => {
    mockApi.delete.mockResolvedValue({ data: undefined })

    await postService.deletePost('post-1')

    expect(mockApi.delete).toHaveBeenCalledWith('/api/posts/post-1')
  })

  it('deletePost → resolves without a return value', async () => {
    mockApi.delete.mockResolvedValue({ data: undefined })

    const result = await postService.deletePost('post-1')

    expect(result).toBeUndefined()
  })
})
