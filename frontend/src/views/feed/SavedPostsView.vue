<script setup lang="ts">
import { onMounted, onUnmounted, ref } from 'vue'
import PostCard from '@/components/post/PostCard.vue'
import { useSavedPostsStore } from '@/stores/savedPosts'

const savedPosts = useSavedPostsStore()
const sentinelRef = ref<HTMLElement | null>(null)
let intersectionObserver: IntersectionObserver | null = null

onMounted(async () => {
  await savedPosts.loadSavedPosts()
  setupInfiniteScroll()
})

onUnmounted(() => {
  intersectionObserver?.disconnect()
})

function setupInfiniteScroll() {
  if (!sentinelRef.value) return
  intersectionObserver = new IntersectionObserver((entries) => {
    if (entries[0].isIntersecting && savedPosts.hasMore && !savedPosts.loadingMore) {
      void savedPosts.loadMore()
    }
  }, { rootMargin: '200px' })
  intersectionObserver.observe(sentinelRef.value)
}
</script>

<template>
  <section class="space-y-4" aria-labelledby="saved-posts-heading">
    <div>
      <h1 id="saved-posts-heading" class="text-2xl font-bold text-slate-100">Saved posts</h1>
      <p class="mt-1 text-sm text-slate-500">Posts you've bookmarked are private and only visible to you.</p>
    </div>

    <div v-if="savedPosts.loading" data-testid="saved-posts-loading" class="space-y-4" aria-label="Loading saved posts">
      <div v-for="i in 3" :key="i" class="animate-pulse rounded-xl border border-slate-700 bg-slate-800 p-4">
        <div class="mb-3 flex items-center gap-3">
          <div class="h-10 w-10 rounded-full bg-slate-700" />
          <div class="h-3.5 w-28 rounded bg-slate-700" />
        </div>
        <div class="h-4 w-5/6 rounded bg-slate-700" />
      </div>
    </div>

    <div
      v-else-if="savedPosts.error && savedPosts.posts.length === 0"
      data-testid="saved-posts-error"
      role="alert"
      class="flex items-center gap-2 rounded-xl border border-red-800 bg-red-950 p-4 text-sm text-red-400"
    >
      {{ savedPosts.error }}
      <button class="ml-auto text-xs underline hover:text-red-300" @click="savedPosts.loadSavedPosts()">Retry</button>
    </div>

    <div
      v-else-if="savedPosts.posts.length === 0"
      data-testid="saved-posts-empty"
      class="rounded-xl border border-slate-700 bg-slate-800 p-10 text-center"
    >
      <svg class="mx-auto mb-3 h-10 w-10 text-slate-500" fill="none" stroke="currentColor" viewBox="0 0 24 24" aria-hidden="true">
        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5" d="M5 5a2 2 0 012-2h10a2 2 0 012 2v16l-7-4-7 4V5z"/>
      </svg>
      <p class="text-lg font-semibold text-slate-200">No saved posts yet</p>
      <p class="mt-1 text-sm text-slate-500">Use the Save button on a post to find it here later.</p>
    </div>

    <template v-else>
      <PostCard v-for="post in savedPosts.posts" :key="post.id" :post="post" />

      <div ref="sentinelRef" data-testid="saved-posts-sentinel" class="flex justify-center py-2">
        <div v-if="savedPosts.loadingMore" class="flex items-center gap-2 text-sm text-slate-500">
          <svg class="h-4 w-4 animate-spin" fill="none" viewBox="0 0 24 24" aria-hidden="true">
            <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"/>
            <path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z"/>
          </svg>
          Loading more…
        </div>
        <p v-else-if="!savedPosts.hasMore" class="text-xs text-slate-500">You've reached the end</p>
      </div>

      <div v-if="savedPosts.error" role="alert" class="rounded-xl border border-red-800 bg-red-950 p-3 text-sm text-red-400">
        {{ savedPosts.error }}
        <button class="ml-2 text-xs underline" @click="savedPosts.loadMore()">Retry</button>
      </div>
    </template>
  </section>
</template>
