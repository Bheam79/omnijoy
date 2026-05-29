<script setup lang="ts">
import { onMounted, onUnmounted, ref, watch } from 'vue'
import * as signalR from '@microsoft/signalr'
import { useFeedStore } from '@/stores/feed'
import { useAuthStore } from '@/stores/auth'
import { useReactionsStore } from '@/stores/reactions'
import { useCommentsStore } from '@/stores/comments'
import type { PostDto, FeedItemDto } from '@/services/postService'
import type { ReactionCountsUpdatedEvent } from '@/services/reactionService'
import type { CommentDto } from '@/services/commentService'
import PostCard from '@/components/post/PostCard.vue'
import SharedPostCard from '@/components/post/SharedPostCard.vue'
import PostComposer from '@/components/post/PostComposer.vue'

const feed = useFeedStore()
const auth = useAuthStore()
const reactionsStore = useReactionsStore()
const commentsStore = useCommentsStore()

const composer = ref<InstanceType<typeof PostComposer> | null>(null)
const sentinelRef = ref<HTMLElement | null>(null)
let hubConnection: signalR.HubConnection | null = null
let intersectionObserver: IntersectionObserver | null = null

// Track which post IDs we've subscribed to on the hub
const subscribedPostIds = new Set<string>()

// ── Lifecycle ─────────────────────────────────────────────────────────────────

onMounted(async () => {
  await feed.loadFeed()
  connectSignalR()
  setupInfiniteScroll()
})

onUnmounted(() => {
  hubConnection?.stop()
  intersectionObserver?.disconnect()
  feed.reset()
  reactionsStore.reset()
  commentsStore.reset()
  subscribedPostIds.clear()
})

// ── SignalR ───────────────────────────────────────────────────────────────────

function connectSignalR() {
  if (!auth.accessToken) return

  hubConnection = new signalR.HubConnectionBuilder()
    .withUrl('/hubs/feed', {
      accessTokenFactory: () => auth.accessToken ?? '',
    })
    .withAutomaticReconnect()
    .configureLogging(signalR.LogLevel.Warning)
    .build()

  hubConnection.on('NewPost', (post: PostDto) => {
    feed.prependPost(post)
    subscribeToPost(post.id)
  })

  hubConnection.on('NewSharedPost', (item: FeedItemDto) => {
    if (item.sharedPost) {
      feed.prependSharedPost(item.sharedPost)
    }
  })

  hubConnection.on('ReactionCountsUpdated', (event: ReactionCountsUpdatedEvent) => {
    reactionsStore.applyUpdate(event)
  })

  hubConnection.on('NewComment', (comment: CommentDto) => {
    commentsStore.applyNewComment(comment)
  })

  hubConnection.start()
    .then(() => {
      // Subscribe to all posts currently in the feed
      subscribeToVisiblePosts()
    })
    .catch(console.error)
}

function subscribeToPost(postId: string) {
  if (subscribedPostIds.has(postId) || !hubConnection) return
  subscribedPostIds.add(postId)
  hubConnection.invoke('SubscribeToPost', postId).catch(() => {
    // Non-fatal: if it fails, we simply won't get real-time updates for this post
    subscribedPostIds.delete(postId)
  })
}

function subscribeToVisiblePosts() {
  for (const item of feed.items) {
    if (item.post) subscribeToPost(item.post.id)
  }
}

// Re-subscribe whenever the items list grows (e.g. infinite scroll loads more)
watch(() => feed.items.length, () => {
  if (hubConnection?.state === signalR.HubConnectionState.Connected) {
    subscribeToVisiblePosts()
  }
})

// ── Infinite scroll via IntersectionObserver ──────────────────────────────────

function setupInfiniteScroll() {
  if (!sentinelRef.value) return

  intersectionObserver = new IntersectionObserver(
    (entries) => {
      if (entries[0].isIntersecting && feed.hasMore && !feed.loadingMore) {
        feed.loadMore()
      }
    },
    { rootMargin: '200px' },
  )
  intersectionObserver.observe(sentinelRef.value)
}
</script>

<template>
  <div class="space-y-4">
    <!-- Post composer -->
    <PostComposer ref="composer" @created="() => {}" />

    <!-- Feed loading state -->
    <div v-if="feed.loading" class="space-y-4">
      <div
        v-for="i in 3"
        :key="i"
        class="bg-slate-800 rounded-xl shadow-sm border border-slate-700 p-4 animate-pulse"
      >
        <div class="flex items-center gap-3 mb-3">
          <div class="w-10 h-10 rounded-full bg-slate-700" />
          <div class="space-y-1.5">
            <div class="h-3.5 w-28 bg-slate-700 rounded" />
            <div class="h-3 w-20 bg-slate-700 rounded" />
          </div>
        </div>
        <div class="space-y-2">
          <div class="h-4 bg-slate-700 rounded w-full" />
          <div class="h-4 bg-slate-700 rounded w-5/6" />
        </div>
      </div>
    </div>

    <!-- Error -->
    <div
      v-else-if="feed.error"
      class="bg-red-950 border border-red-800 rounded-xl p-4 text-sm text-red-400 flex items-center gap-2"
    >
      <svg class="w-5 h-5 shrink-0" fill="currentColor" viewBox="0 0 20 20">
        <path fill-rule="evenodd"
          d="M18 10a8 8 0 11-16 0 8 8 0 0116 0zm-7 4a1 1 0 11-2 0 1 1 0 012 0zm-1-9a1 1 0 00-1 1v4a1 1 0 102 0V6a1 1 0 00-1-1z"
          clip-rule="evenodd"/>
      </svg>
      {{ feed.error }}
      <button
        class="ml-auto text-red-400 underline text-xs hover:text-red-300"
        @click="feed.loadFeed()"
      >
        Retry
      </button>
    </div>

    <!-- Empty state -->
    <div
      v-else-if="!feed.loading && feed.items.length === 0"
      class="bg-slate-800 rounded-xl shadow-sm border border-slate-700 p-10 text-center"
    >
      <p class="text-4xl mb-3">👋</p>
      <p class="font-semibold text-slate-200 text-lg">Your wall is empty</p>
      <p class="text-gray-500 text-sm mt-1">
        Create your first post or connect with friends to see their posts here.
      </p>
      <button
        class="mt-4 bg-blue-600 hover:bg-blue-700 text-white font-semibold text-sm px-5 py-2 rounded-xl transition"
        @click="composer?.open()"
      >
        Create a post
      </button>
    </div>

    <!-- Feed items (posts + shared posts) -->
    <template v-else>
      <template v-for="item in feed.items" :key="item.post?.id ?? item.sharedPost?.id">
        <PostCard
          v-if="item.itemType === 'Post' && item.post"
          :post="item.post"
        />
        <SharedPostCard
          v-else-if="item.itemType === 'SharedPost' && item.sharedPost"
          :shared-post="item.sharedPost"
        />
      </template>

      <!-- Infinite-scroll sentinel -->
      <div ref="sentinelRef" class="py-2 flex justify-center">
        <div v-if="feed.loadingMore" class="flex items-center gap-2 text-sm text-slate-500">
          <svg class="animate-spin w-4 h-4" fill="none" viewBox="0 0 24 24">
            <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"/>
            <path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z"/>
          </svg>
          Loading more…
        </div>
        <p v-else-if="!feed.hasMore && feed.items.length > 0" class="text-xs text-slate-500">
          You've reached the end
        </p>
      </div>
    </template>
  </div>
</template>
