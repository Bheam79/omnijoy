import { defineStore } from 'pinia'
import { ref } from 'vue'
import { postService, type PostDto, type CreatePostPayload } from '@/services/postService'

export const useFeedStore = defineStore('feed', () => {
  const posts = ref<PostDto[]>([])
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
      posts.value = result.items
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
      posts.value.push(...result.items)
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
    // Optimistically prepend to feed; SignalR may also push it —
    // deduplication is handled by prependPost
    prependPost(post)
    return post
  }

  // ── Delete post ───────────────────────────────────────────────────────────

  async function deletePost(id: string) {
    await postService.deletePost(id)
    posts.value = posts.value.filter(p => p.id !== id)
  }

  // ── SignalR: push new post to top (deduplication guard) ───────────────────

  function prependPost(post: PostDto) {
    if (posts.value.some(p => p.id === post.id)) return
    posts.value.unshift(post)
  }

  function reset() {
    posts.value = []
    page.value = 1
    hasMore.value = true
    loading.value = false
    loadingMore.value = false
    error.value = null
  }

  return {
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
