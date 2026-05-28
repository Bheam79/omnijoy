<script setup lang="ts">
import { computed } from 'vue'
import type { PostDto } from '@/services/postService'
import { useAuthStore } from '@/stores/auth'
import { useFeedStore } from '@/stores/feed'

const props = defineProps<{ post: PostDto }>()
const emit = defineEmits<{ deleted: [id: string] }>()

const auth = useAuthStore()
const feed = useFeedStore()

const isOwn = computed(() => auth.user?.id === props.post.author.id)

const privacyLabel: Record<string, string> = {
  Everyone: 'Public',
  Friends: 'Friends',
  OnlyMe: 'Only me',
}

const privacyIcon: Record<string, string> = {
  Everyone: '🌐',
  Friends: '👥',
  OnlyMe: '🔒',
}

function formatDate(iso: string) {
  return new Date(iso).toLocaleString(undefined, {
    year: 'numeric', month: 'short', day: 'numeric',
    hour: '2-digit', minute: '2-digit',
  })
}

async function handleDelete() {
  if (!confirm('Delete this post?')) return
  await feed.deletePost(props.post.id)
  emit('deleted', props.post.id)
}

// Text-on-background style
const tobStyle = computed(() => {
  if (props.post.postType !== 'TextOnBackground') return {}
  const bg = props.post.backgroundImageUrl ?? '#1877f2'
  // If bg looks like a URL, use it as a background image; otherwise treat as color
  if (bg.startsWith('http') || bg.startsWith('/')) {
    return { backgroundImage: `url(${bg})`, backgroundSize: 'cover', backgroundPosition: 'center' }
  }
  return { background: bg }
})
</script>

<template>
  <article class="bg-white rounded-xl shadow-sm border border-gray-100 overflow-hidden">
    <!-- Header -->
    <div class="flex items-center justify-between px-4 pt-4 pb-2">
      <div class="flex items-center gap-3">
        <!-- Avatar -->
        <RouterLink :to="`/profile/${post.author.id}`">
          <img
            v-if="post.author.avatarUrl"
            :src="post.author.avatarUrl"
            :alt="post.author.displayName"
            class="w-10 h-10 rounded-full object-cover"
          />
          <div
            v-else
            class="w-10 h-10 rounded-full bg-blue-500 flex items-center justify-center text-white font-semibold text-sm"
          >
            {{ post.author.displayName.charAt(0).toUpperCase() }}
          </div>
        </RouterLink>

        <!-- Author name + timestamp + privacy -->
        <div>
          <RouterLink
            :to="`/profile/${post.author.id}`"
            class="font-semibold text-gray-900 hover:underline text-sm"
          >
            {{ post.author.displayName }}
          </RouterLink>
          <div class="flex items-center gap-1.5 text-xs text-gray-500 mt-0.5">
            <span>{{ formatDate(post.createdAt) }}</span>
            <span>·</span>
            <span :title="privacyLabel[post.privacy]">{{ privacyIcon[post.privacy] }}</span>
            <!-- Privacy badge only for own posts -->
            <span v-if="isOwn" class="text-gray-400 italic">{{ privacyLabel[post.privacy] }}</span>
          </div>
        </div>
      </div>

      <!-- Options (own posts) -->
      <div v-if="isOwn" class="relative group">
        <button
          class="text-gray-400 hover:text-gray-600 p-1 rounded-full hover:bg-gray-100 transition"
          aria-label="Post options"
        >
          <svg class="w-5 h-5" fill="currentColor" viewBox="0 0 20 20">
            <path d="M10 6a2 2 0 110-4 2 2 0 010 4zm0 6a2 2 0 110-4 2 2 0 010 4zm0 6a2 2 0 110-4 2 2 0 010 4z"/>
          </svg>
        </button>
        <div class="absolute right-0 top-7 z-10 hidden group-focus-within:block bg-white border border-gray-200 rounded-lg shadow-lg w-36 py-1">
          <button
            class="w-full text-left px-4 py-2 text-sm text-red-600 hover:bg-red-50 transition"
            @click="handleDelete"
          >
            Delete post
          </button>
        </div>
      </div>
    </div>

    <!-- Content: Text -->
    <div
      v-if="post.postType === 'Text' && post.content"
      class="px-4 pb-3 text-gray-800 leading-relaxed whitespace-pre-wrap"
    >
      {{ post.content }}
    </div>

    <!-- Content: TextOnBackground -->
    <div
      v-else-if="post.postType === 'TextOnBackground'"
      :style="tobStyle"
      class="mx-4 mb-3 rounded-xl flex items-center justify-center min-h-52 p-6"
    >
      <p class="text-white text-xl font-bold text-center drop-shadow leading-relaxed">
        {{ post.content }}
      </p>
    </div>

    <!-- Content: Image(s) -->
    <template v-else-if="post.postType === 'Image'">
      <div class="px-4 pb-2 text-gray-800 leading-relaxed whitespace-pre-wrap" v-if="post.content">
        {{ post.content }}
      </div>
      <div
        class="grid gap-1"
        :class="{
          'grid-cols-1': post.media.length === 1,
          'grid-cols-2': post.media.length >= 2,
        }"
      >
        <img
          v-for="(m, i) in post.media.slice(0, 4)"
          :key="m.id"
          :src="m.url"
          :alt="`Image ${i + 1}`"
          class="w-full object-cover max-h-96"
          :class="{ 'col-span-2': post.media.length === 1 }"
        />
      </div>
      <p v-if="post.media.length > 4" class="px-4 py-1 text-xs text-gray-500">
        +{{ post.media.length - 4 }} more
      </p>
    </template>

    <!-- Content: Video -->
    <template v-else-if="post.postType === 'Video'">
      <div class="px-4 pb-2 text-gray-800 leading-relaxed whitespace-pre-wrap" v-if="post.content">
        {{ post.content }}
      </div>
      <div v-for="m in post.media" :key="m.id" class="relative bg-black">
        <video
          :src="m.url"
          :poster="m.thumbnailUrl"
          controls
          class="w-full max-h-96 object-contain"
          preload="metadata"
        />
      </div>
    </template>

    <!-- Actions bar -->
    <div class="flex items-center gap-1 px-4 py-2 border-t border-gray-100 mt-1">
      <button
        class="flex items-center gap-1.5 text-gray-500 hover:text-blue-600 text-sm font-medium px-3 py-1.5 rounded-lg hover:bg-blue-50 transition flex-1 justify-center"
        disabled
        title="Like (coming soon)"
      >
        <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
            d="M14 10h4.764a2 2 0 011.789 2.894l-3.5 7A2 2 0 0115.263 21h-4.017c-.163 0-.326-.02-.485-.06L7 20m7-10V5a2 2 0 00-2-2h-.095c-.5 0-.905.405-.905.905 0 .714-.211 1.412-.608 2.006L7 11v9m7-10h-2M7 20H5a2 2 0 01-2-2v-6a2 2 0 012-2h2.5"/>
        </svg>
        Like
      </button>
      <button
        class="flex items-center gap-1.5 text-gray-500 hover:text-blue-600 text-sm font-medium px-3 py-1.5 rounded-lg hover:bg-blue-50 transition flex-1 justify-center"
        disabled
        title="Comment (coming soon)"
      >
        <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
            d="M8 12h.01M12 12h.01M16 12h.01M21 12c0 4.418-4.03 8-9 8a9.863 9.863 0 01-4.255-.949L3 20l1.395-3.72C3.512 15.042 3 13.574 3 12c0-4.418 4.03-8 9-8s9 3.582 9 8z"/>
        </svg>
        Comment
      </button>
      <button
        class="flex items-center gap-1.5 text-gray-500 hover:text-blue-600 text-sm font-medium px-3 py-1.5 rounded-lg hover:bg-blue-50 transition flex-1 justify-center"
        disabled
        title="Share (coming soon)"
      >
        <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
            d="M8.684 13.342C8.886 12.938 9 12.482 9 12c0-.482-.114-.938-.316-1.342m0 2.684a3 3 0 110-2.684m0 2.684l6.632 3.316m-6.632-6l6.632-3.316m0 0a3 3 0 105.367-2.684 3 3 0 00-5.367 2.684zm0 9.316a3 3 0 105.368 2.684 3 3 0 00-5.368-2.684z"/>
        </svg>
        Share
      </button>
    </div>
  </article>
</template>
