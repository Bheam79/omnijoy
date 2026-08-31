import { beforeEach, describe, expect, it, vi } from 'vitest'

const mockApi = vi.hoisted(() => ({
  get: vi.fn(),
  post: vi.fn(),
  delete: vi.fn(),
}))

vi.mock('@/services/api', () => ({ default: mockApi, api: mockApi }))

import { savedPostsService } from '@/services/savedPostsService'

describe('savedPostsService', () => {
  beforeEach(() => vi.clearAllMocks())

  it('loads the requested saved-post page', async () => {
    const page = { items: [], page: 2, pageSize: 10, hasMore: false }
    mockApi.get.mockResolvedValue({ data: page })

    await expect(savedPostsService.getSavedPosts(2, 10)).resolves.toEqual(page)
    expect(mockApi.get).toHaveBeenCalledWith('/api/users/me/saved-posts', {
      params: { page: 2, pageSize: 10 },
    })
  })

  it('uses the idempotent save endpoint contract', async () => {
    mockApi.post.mockResolvedValue({ data: { isSaved: true, changed: false } })

    await expect(savedPostsService.savePost('post-1')).resolves.toEqual({ isSaved: true, changed: false })
    expect(mockApi.post).toHaveBeenCalledWith('/api/posts/post-1/save')
  })

  it('uses the idempotent unsave endpoint contract', async () => {
    mockApi.delete.mockResolvedValue({ data: { isSaved: false, changed: false } })

    await expect(savedPostsService.unsavePost('post-1')).resolves.toEqual({ isSaved: false, changed: false })
    expect(mockApi.delete).toHaveBeenCalledWith('/api/posts/post-1/save')
  })
})
