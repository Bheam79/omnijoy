import { computed, ref } from 'vue'
import { defineStore } from 'pinia'
import { savedPostsService } from '@/services/savedPostsService'
import type { PostDto } from '@/services/postService'

export const useSavedPostsStore = defineStore('savedPosts', () => {
  const posts = ref<PostDto[]>([])
  const savedByPostId = ref<Record<string, boolean>>({})
  const pendingPostIds = ref<Set<string>>(new Set())
  const toggleErrors = ref<Record<string, string>>({})
  const page = ref(1)
  const hasMore = ref(true)
  const loading = ref(false)
  const loadingMore = ref(false)
  const error = ref<string | null>(null)
  const reconciling = ref(false)

  const savedPostIds = computed(() => new Set(
    Object.entries(savedByPostId.value)
      .filter(([, saved]) => saved)
      .map(([id]) => id),
  ))

  function seed(post: Pick<PostDto, 'id' | 'isSavedByMe'>) {
    if (!(post.id in savedByPostId.value)) {
      savedByPostId.value[post.id] = post.isSavedByMe
    }
  }

  function isSaved(postId: string, fallback = false): boolean {
    return savedByPostId.value[postId] ?? fallback
  }

  function isPending(postId: string): boolean {
    return pendingPostIds.value.has(postId)
  }

  function errorFor(postId: string): string | null {
    return toggleErrors.value[postId] ?? null
  }

  async function loadSavedPosts() {
    if (loading.value) return
    loading.value = true
    error.value = null
    page.value = 1
    hasMore.value = true
    try {
      const result = await savedPostsService.getSavedPosts(1)
      posts.value = deduplicate(result.items)
      for (const post of posts.value) savedByPostId.value[post.id] = true
      hasMore.value = result.hasMore
      page.value = result.page
    } catch (e: unknown) {
      posts.value = []
      error.value = extractError(e)
    } finally {
      loading.value = false
    }
  }

  async function loadMore() {
    if (!hasMore.value || loading.value || loadingMore.value) return
    loadingMore.value = true
    error.value = null
    try {
      const nextPage = page.value + 1
      const result = await savedPostsService.getSavedPosts(nextPage)
      posts.value = deduplicate([...posts.value, ...result.items])
      for (const post of result.items) savedByPostId.value[post.id] = true
      hasMore.value = result.hasMore
      page.value = result.page
    } catch (e: unknown) {
      error.value = extractError(e)
    } finally {
      loadingMore.value = false
    }
  }

  async function toggle(post: PostDto): Promise<boolean> {
    seed(post)
    if (pendingPostIds.value.has(post.id)) return isSaved(post.id)

    const wasSaved = isSaved(post.id)
    const originalIndex = posts.value.findIndex(item => item.id === post.id)
    toggleErrors.value[post.id] = ''
    pendingPostIds.value.add(post.id)
    applyState(post, !wasSaved)

    try {
      const result = wasSaved
        ? await savedPostsService.unsavePost(post.id)
        : await savedPostsService.savePost(post.id)
      applyState(post, result.isSaved)
      return result.isSaved
    } catch (e: unknown) {
      applyState(post, wasSaved, originalIndex)
      toggleErrors.value[post.id] = extractError(e)
      return wasSaved
    } finally {
      pendingPostIds.value.delete(post.id)
    }
  }

  function applyState(post: PostDto, saved: boolean, restoreIndex = -1) {
    savedByPostId.value[post.id] = saved
    if (!saved) {
      posts.value = posts.value.filter(item => item.id !== post.id)
      return
    }

    if (posts.value.some(item => item.id === post.id)) return
    const index = restoreIndex >= 0 ? Math.min(restoreIndex, posts.value.length) : 0
    posts.value.splice(index, 0, { ...post, isSavedByMe: true })
  }

  /**
   * Focus fallback for missed updates: fetch the complete private saved set,
   * then reconcile every post ID already known by this tab.
   */
  async function reconcile() {
    if (reconciling.value) return
    reconciling.value = true
    try {
      const all: PostDto[] = []
      let nextPage = 1
      let more = true
      while (more) {
        const result = await savedPostsService.getSavedPosts(nextPage, 50)
        all.push(...result.items)
        more = result.hasMore
        nextPage = result.page + 1
      }

      const reconciled = deduplicate(all)
      const ids = new Set(reconciled.map(post => post.id))
      for (const id of Object.keys(savedByPostId.value)) {
        savedByPostId.value[id] = ids.has(id)
      }
      for (const post of reconciled) savedByPostId.value[post.id] = true
      posts.value = reconciled.map(post => ({ ...post, isSavedByMe: true }))
      page.value = Math.max(1, nextPage - 1)
      hasMore.value = false
    } catch {
      // Focus reconciliation is best-effort. Existing UI state remains usable.
    } finally {
      reconciling.value = false
    }
  }

  function reset() {
    posts.value = []
    savedByPostId.value = {}
    pendingPostIds.value = new Set()
    toggleErrors.value = {}
    page.value = 1
    hasMore.value = true
    loading.value = false
    loadingMore.value = false
    error.value = null
    reconciling.value = false
  }

  return {
    posts,
    savedPostIds,
    page,
    hasMore,
    loading,
    loadingMore,
    error,
    reconciling,
    seed,
    isSaved,
    isPending,
    errorFor,
    loadSavedPosts,
    loadMore,
    toggle,
    reconcile,
    reset,
  }
})

function deduplicate(posts: PostDto[]): PostDto[] {
  const seen = new Set<string>()
  return posts.filter(post => {
    if (seen.has(post.id)) return false
    seen.add(post.id)
    return true
  })
}

function extractError(e: unknown): string {
  if (typeof e === 'object' && e !== null) {
    const axiosError = e as {
      response?: { data?: { error?: string; detail?: string; title?: string } }
      message?: string
    }
    return axiosError.response?.data?.error
      ?? axiosError.response?.data?.detail
      ?? axiosError.response?.data?.title
      ?? axiosError.message
      ?? 'Unable to update saved post.'
  }
  return 'Unable to update saved post.'
}
