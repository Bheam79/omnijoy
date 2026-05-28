import { defineStore } from 'pinia'
import { ref } from 'vue'
import {
  postService,
  type PostDto,
  type FeedItemDto,
  type SharedPostFeedItemDto,
  type CreatePostPayload,
} from '@/services/postService'

export const useFeedStore = defineStore('feed', () => {
  const items = ref<FeedItemDto[]>([])
  const page = ref(1)
  const hasMore = ref(true)
  const loading = ref(false)
  const loadingMore = ref(false)
  const error = ref<string | null>(null)

  // ── Load first page ───────────────────────────────────────────────────────

  async function loadFeed() {
    loading.value = true
    error.value = null
    page.value = 1
    hasMore.value = true
    try {
      const result = await postService.getFeed(1)
      items.value = result.items
      hasMore.value = result.hasMore
      page.value = 1
    } catch (e: unknown) {
      error.value = extractError(e)
    } finally {
      loading.value = false
    }
  }

  // ── Load next page (infinite scroll) ─────────────────────────────────────

  async function loadMore() {
    if (!hasMore.value || loadingMore.value) return
    loadingMore.value = true
    try {
      const nextPage = page.value + 1
      const result = await postService.getFeed(nextPage)
      items.value.push(...result.items)
      hasMore.value = result.hasMore
      page.value = nextPage
    } catch (e: unknown) {
      error.value = extractError(e)
    } finally {
      loadingMore.value = false
    }
  }

  // ── Create post ───────────────────────────────────────────────────────────

  async function createPost(payload: CreatePostPayload): Promise<PostDto> {
    const post = await postService.createPost(payload)
    prependPost(post)
    return post
  }

  // ── Delete post ───────────────────────────────────────────────────────────

  async function deletePost(id: string) {
    await postService.deletePost(id)
    items.value = items.value.filter(item =>
      item.post?.id !== id && item.sharedPost?.originalPost.id !== id,
    )
  }

  // ── SignalR: push new post to top (deduplication guard) ───────────────────

  function prependPost(post: PostDto) {
    if (items.value.some(item => item.post?.id === post.id)) return
    items.value.unshift({ itemType: 'Post', post })
  }

  // ── SignalR: push new shared post to top ──────────────────────────────────

  function prependSharedPost(sharedPost: SharedPostFeedItemDto) {
    if (items.value.some(item => item.sharedPost?.id === sharedPost.id)) return
    items.value.unshift({ itemType: 'SharedPost', sharedPost })
  }

  function reset() {
    items.value = []
    page.value = 1
    hasMore.value = true
    loading.value = false
    loadingMore.value = false
    error.value = null
  }

  // Backward-compat: expose flat list of PostDtos for components that only care about posts
  // (e.g. WallView post subscription loop — it iterates all posts)
  const posts = items

  return {
    items,
    posts,
    page,
    hasMore,
    loading,
    loadingMore,
    error,
    loadFeed,
    loadMore,
    createPost,
    deletePost,
    prependPost,
    prependSharedPost,
    reset,
  }
})

function extractError(e: unknown): string {
  if (typeof e === 'object' && e !== null) {
    const axiosError = e as { response?: { data?: { error?: string } }; message?: string }
    return axiosError.response?.data?.error ?? axiosError.message ?? 'An unexpected error occurred.'
  }
  return 'An unexpected error occurred.'
}
