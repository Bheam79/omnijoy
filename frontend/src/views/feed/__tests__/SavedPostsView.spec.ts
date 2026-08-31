import { beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { nextTick } from 'vue'
import type { PostDto } from '@/services/postService'

const mockSavedPostsService = vi.hoisted(() => ({
  getSavedPosts: vi.fn(),
  savePost: vi.fn(),
  unsavePost: vi.fn(),
}))

vi.mock('@/services/savedPostsService', () => ({ savedPostsService: mockSavedPostsService }))
vi.mock('@/components/post/PostCard.vue', () => ({
  default: { props: ['post'], template: '<article data-testid="saved-post-card">{{ post.id }}</article>' },
}))

import SavedPostsView from '@/views/feed/SavedPostsView.vue'

function makePost(id: string): PostDto {
  return {
    id,
    author: { id: 'author-1', displayName: 'Alice' },
    content: id,
    postType: 'Text',
    privacy: 'Friends',
    media: [],
    isSavedByMe: true,
    createdAt: '2026-01-01T00:00:00Z',
    updatedAt: '2026-01-01T00:00:00Z',
  }
}

let observerCallback: IntersectionObserverCallback | undefined
const observe = vi.fn()
const disconnect = vi.fn()

function mountView() {
  const pinia = createPinia()
  setActivePinia(pinia)
  return mount(SavedPostsView, { global: { plugins: [pinia] } })
}

describe('SavedPostsView', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    observerCallback = undefined
    vi.stubGlobal('IntersectionObserver', class {
      constructor(callback: IntersectionObserverCallback) { observerCallback = callback }
      observe = observe
      disconnect = disconnect
    })
  })

  it('shows the loading skeleton while the first page is pending', async () => {
    let resolve!: (value: unknown) => void
    mockSavedPostsService.getSavedPosts.mockReturnValue(new Promise(r => { resolve = r }))
    const wrapper = mountView()
    await nextTick()

    expect(wrapper.find('[data-testid="saved-posts-loading"]').exists()).toBe(true)
    resolve({ items: [], page: 1, pageSize: 20, hasMore: false })
    await flushPromises()
  })

  it('shows the empty state when no posts are saved', async () => {
    mockSavedPostsService.getSavedPosts.mockResolvedValue({ items: [], page: 1, pageSize: 20, hasMore: false })
    const wrapper = mountView()
    await flushPromises()

    expect(wrapper.find('[data-testid="saved-posts-empty"]').exists()).toBe(true)
    expect(wrapper.text()).toContain('No saved posts yet')
  })

  it('shows an error and retries the first page', async () => {
    mockSavedPostsService.getSavedPosts
      .mockRejectedValueOnce(new Error('Could not load bookmarks'))
      .mockResolvedValueOnce({ items: [], page: 1, pageSize: 20, hasMore: false })
    const wrapper = mountView()
    await flushPromises()

    expect(wrapper.get('[data-testid="saved-posts-error"]').text()).toContain('Could not load bookmarks')
    await wrapper.get('[data-testid="saved-posts-error"] button').trigger('click')
    await flushPromises()
    expect(mockSavedPostsService.getSavedPosts).toHaveBeenCalledTimes(2)
    expect(wrapper.find('[data-testid="saved-posts-empty"]').exists()).toBe(true)
  })

  it('renders posts and loads the next page from the infinite-scroll sentinel', async () => {
    mockSavedPostsService.getSavedPosts
      .mockResolvedValueOnce({ items: [makePost('post-1')], page: 1, pageSize: 20, hasMore: true })
      .mockResolvedValueOnce({ items: [makePost('post-2')], page: 2, pageSize: 20, hasMore: false })
    const wrapper = mountView()
    await flushPromises()

    expect(wrapper.findAll('[data-testid="saved-post-card"]')).toHaveLength(1)
    expect(observe).toHaveBeenCalledOnce()
    observerCallback?.([{ isIntersecting: true } as IntersectionObserverEntry], {} as IntersectionObserver)
    await flushPromises()

    expect(mockSavedPostsService.getSavedPosts).toHaveBeenLastCalledWith(2)
    expect(wrapper.findAll('[data-testid="saved-post-card"]')).toHaveLength(2)
    expect(wrapper.text()).toContain("You've reached the end")
    wrapper.unmount()
    expect(disconnect).toHaveBeenCalledOnce()
  })
})
