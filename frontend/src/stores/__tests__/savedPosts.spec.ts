import { beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import type { PostDto } from '@/services/postService'

const mockSavedPostsService = vi.hoisted(() => ({
  getSavedPosts: vi.fn(),
  savePost: vi.fn(),
  unsavePost: vi.fn(),
}))

vi.mock('@/services/savedPostsService', () => ({ savedPostsService: mockSavedPostsService }))

import { useSavedPostsStore } from '@/stores/savedPosts'

function makePost(id: string, isSavedByMe = false): PostDto {
  return {
    id,
    author: { id: 'author-1', displayName: 'Alice' },
    content: `Post ${id}`,
    mentions: [],
    postType: 'Text',
    privacy: 'Friends',
    media: [],
    isSavedByMe,
    createdAt: '2026-01-01T00:00:00Z',
    updatedAt: '2026-01-01T00:00:00Z',
  }
}

describe('useSavedPostsStore', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.clearAllMocks()
  })

  it('loads pages and deduplicates posts repeated by the API', async () => {
    mockSavedPostsService.getSavedPosts
      .mockResolvedValueOnce({ items: [makePost('1', true), makePost('2', true)], page: 1, pageSize: 20, hasMore: true })
      .mockResolvedValueOnce({ items: [makePost('2', true), makePost('3', true)], page: 2, pageSize: 20, hasMore: false })
    const store = useSavedPostsStore()

    await store.loadSavedPosts()
    await store.loadMore()

    expect(store.posts.map(post => post.id)).toEqual(['1', '2', '3'])
    expect(store.page).toBe(2)
    expect(store.hasMore).toBe(false)
  })

  it('optimistically saves and protects against duplicate in-flight toggles', async () => {
    let resolve!: (value: { isSaved: boolean; changed: boolean }) => void
    mockSavedPostsService.savePost.mockReturnValue(new Promise(r => { resolve = r }))
    const store = useSavedPostsStore()
    const post = makePost('1')

    const first = store.toggle(post)
    const second = store.toggle(post)

    expect(store.isSaved('1')).toBe(true)
    expect(store.isPending('1')).toBe(true)
    expect(mockSavedPostsService.savePost).toHaveBeenCalledOnce()
    resolve({ isSaved: true, changed: false })
    await expect(first).resolves.toBe(true)
    await expect(second).resolves.toBe(true)
  })

  it('rolls back an optimistic save and exposes feedback when the request fails', async () => {
    mockSavedPostsService.savePost.mockRejectedValue(new Error('Network unavailable'))
    const store = useSavedPostsStore()

    await store.toggle(makePost('1'))

    expect(store.isSaved('1')).toBe(false)
    expect(store.posts).toHaveLength(0)
    expect(store.errorFor('1')).toBe('Network unavailable')
    expect(store.isPending('1')).toBe(false)
  })

  it('rolls back an optimistic unsave at its original list position', async () => {
    mockSavedPostsService.getSavedPosts.mockResolvedValue({
      items: [makePost('1', true), makePost('2', true)], page: 1, pageSize: 20, hasMore: false,
    })
    mockSavedPostsService.unsavePost.mockRejectedValue({ response: { data: { detail: 'Try again' } } })
    const store = useSavedPostsStore()
    await store.loadSavedPosts()

    await store.toggle(store.posts[1])

    expect(store.posts.map(post => post.id)).toEqual(['1', '2'])
    expect(store.isSaved('2')).toBe(true)
    expect(store.errorFor('2')).toBe('Try again')
  })

  it('shares one saved state for duplicate renderings of the same post', () => {
    const store = useSavedPostsStore()
    store.seed(makePost('same', true))
    store.seed(makePost('same', false))

    expect(store.isSaved('same')).toBe(true)
    expect(store.savedPostIds.has('same')).toBe(true)
  })

  it('clears all private state on reset', async () => {
    mockSavedPostsService.getSavedPosts.mockResolvedValue({
      items: [makePost('1', true)], page: 1, pageSize: 20, hasMore: false,
    })
    const store = useSavedPostsStore()
    await store.loadSavedPosts()

    store.reset()

    expect(store.posts).toEqual([])
    expect(store.savedPostIds.size).toBe(0)
    expect(store.error).toBeNull()
  })

  it('reconciles known state from all pages and ignores focus-fallback errors', async () => {
    const store = useSavedPostsStore()
    store.seed(makePost('removed', true))
    mockSavedPostsService.getSavedPosts
      .mockResolvedValueOnce({ items: [makePost('kept', true)], page: 1, pageSize: 50, hasMore: true })
      .mockResolvedValueOnce({ items: [], page: 2, pageSize: 50, hasMore: false })

    await store.reconcile()

    expect(store.isSaved('removed')).toBe(false)
    expect(store.isSaved('kept')).toBe(true)
    mockSavedPostsService.getSavedPosts.mockRejectedValue(new Error('offline'))
    await expect(store.reconcile()).resolves.toBeUndefined()
    expect(store.posts.map(post => post.id)).toEqual(['kept'])
  })
})
