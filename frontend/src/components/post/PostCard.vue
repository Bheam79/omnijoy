<script setup lang="ts">
import { computed, ref } from 'vue'
import type { PostDto } from '@/services/postService'
import { useAuthStore } from '@/stores/auth'
import { useFeedStore } from '@/stores/feed'
import { useProfileUrl } from '@/composables/useProfileUrl'
import PostReactionBar from './PostReactionBar.vue'
import CommentThread from './CommentThread.vue'
import ShareModal from './ShareModal.vue'
import ReportModal from './ReportModal.vue'

const props = defineProps<{ post: PostDto }>()
const emit = defineEmits<{ deleted: [id: string] }>()

const auth = useAuthStore()
const feed = useFeedStore()

const isLoggedIn = computed(() => !!auth.user)

const isOwn = computed(() => auth.user?.id === props.post.author.id)

const authorUrl = computed(() => useProfileUrl(props.post.author))

// ── Report ────────────────────────────────────────────────────────────────────
const reportModalOpen = ref(false)

// ── Share ─────────────────────────────────────────────────────────────────────
const shareModalOpen = ref(false)
const shareCopied = ref(false)

function openShareModal() {
  if (isLoggedIn.value) {
    shareModalOpen.value = true
  } else {
    // Not logged in: fall back to copying the public share link
    copyShareLink()
  }
}

const shareUrl = computed(() => `${window.location.origin}/share/posts/${props.post.id}`)

async function copyShareLink() {
  try {
    await navigator.clipboard.writeText(shareUrl.value)
    shareCopied.value = true
    setTimeout(() => { shareCopied.value = false }, 1800)
  } catch {
    window.prompt('Copy share link:', shareUrl.value)
  }
}

const privacyLabel: Record<string, string> = {
  Everyone: 'Public',
  Friends: 'Friends',
  OnlyMe: 'Only me',
  Followers: 'Followers',
}

const privacyIcon: Record<string, string> = {
  Everyone: '🌐',
  Friends: '👥',
  OnlyMe: '🔒',
  Followers: '📣',
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

const threadRef = ref<InstanceType<typeof CommentThread> | null>(null)
const threadExpanded = ref(false)

function handleCommentClick() {
  threadRef.value?.openThread()
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
  <article data-testid="post-card" class="bg-slate-800 rounded-xl shadow-sm border border-slate-700 overflow-hidden">
    <!-- Header -->
    <div class="flex items-center justify-between px-4 pt-4 pb-2">
      <div class="flex items-center gap-3">
        <!-- Avatar -->
        <RouterLink :to="authorUrl">
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
            :to="authorUrl"
            class="font-semibold text-slate-100 hover:underline text-sm"
          >
            {{ post.author.displayName }}
          </RouterLink>
          <div class="flex items-center gap-1.5 text-xs text-gray-500 mt-0.5">
            <span>{{ formatDate(post.createdAt) }}</span>
            <span>·</span>
            <span :title="privacyLabel[post.privacy]">{{ privacyIcon[post.privacy] }}</span>
            <!-- Privacy badge only for own posts -->
            <span v-if="isOwn" class="text-slate-500 italic">{{ privacyLabel[post.privacy] }}</span>
          </div>
        </div>
      </div>

      <!-- Options menu (own posts: delete; others: report) -->
      <div v-if="isOwn || isLoggedIn" class="relative group">
        <button
          data-testid="post-menu-button"
          class="text-slate-500 hover:text-slate-400 p-1 rounded-full hover:bg-slate-700 transition"
          aria-label="Post options"
        >
          <svg class="w-5 h-5" fill="currentColor" viewBox="0 0 20 20">
            <path d="M10 6a2 2 0 110-4 2 2 0 010 4zm0 6a2 2 0 110-4 2 2 0 010 4zm0 6a2 2 0 110-4 2 2 0 010 4z"/>
          </svg>
        </button>
        <div data-testid="post-menu-dropdown" class="absolute right-0 top-7 z-10 hidden group-focus-within:block bg-slate-800 border border-slate-700 rounded-lg shadow-lg w-36 py-1">
          <button
            v-if="isOwn"
            data-testid="post-delete-button"
            class="w-full text-left px-4 py-2 text-sm text-red-400 hover:bg-red-950 transition"
            @click="handleDelete"
          >
            Delete post
          </button>
          <button
            v-if="!isOwn"
            data-testid="post-report-button"
            class="w-full text-left px-4 py-2 text-sm text-slate-300 hover:bg-slate-700 transition"
            @click="reportModalOpen = true"
          >
            Report post
          </button>
        </div>
      </div>
    </div>

    <!-- Content: Text -->
    <div
      v-if="post.postType === 'Text' && post.content"
      class="px-4 pb-3 text-slate-200 leading-relaxed whitespace-pre-wrap"
    >
      {{ post.content }}
    </div>

    <!-- Link preview card (Open Graph) -->
    <a
      v-if="post.linkPreview"
      :href="post.linkPreview.url"
      target="_blank"
      rel="noopener noreferrer"
      class="block mx-4 mb-3 border border-slate-700 rounded-xl overflow-hidden hover:bg-slate-700 transition"
    >
      <img
        v-if="post.linkPreview.imageUrl"
        :src="post.linkPreview.imageUrl"
        :alt="post.linkPreview.title ?? ''"
        class="w-full max-h-72 object-cover bg-slate-700"
        loading="lazy"
      />
      <div class="px-3 py-2">
        <p v-if="post.linkPreview.siteName" class="text-xs uppercase tracking-wide text-slate-500">
          {{ post.linkPreview.siteName }}
        </p>
        <p
          v-if="post.linkPreview.title"
          class="font-semibold text-sm text-slate-100 line-clamp-2"
        >{{ post.linkPreview.title }}</p>
        <p
          v-if="post.linkPreview.description"
          class="text-xs text-gray-500 mt-1 line-clamp-2"
        >{{ post.linkPreview.description }}</p>
        <p class="text-xs text-blue-400 mt-1 truncate">{{ post.linkPreview.url }}</p>
      </div>
    </a>

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
      <div class="px-4 pb-2 text-slate-200 leading-relaxed whitespace-pre-wrap" v-if="post.content">
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
      <div class="px-4 pb-2 text-slate-200 leading-relaxed whitespace-pre-wrap" v-if="post.content">
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

    <!-- Reaction bar (picker + counts) -->
    <PostReactionBar :post-id="post.id" :logged-in="isLoggedIn" />

    <!-- Actions bar (Comment + Share) -->
    <div class="flex items-center gap-1 px-4 py-2 border-t border-slate-700">
      <button
        data-testid="post-comment-button"
        class="flex items-center gap-1.5 text-gray-500 hover:text-blue-400 text-sm font-medium px-3 py-1.5 rounded-lg hover:bg-blue-900/40 transition flex-1 justify-center"
        :class="{ 'text-blue-400': threadExpanded }"
        @click="handleCommentClick"
      >
        <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
            d="M8 12h.01M12 12h.01M16 12h.01M21 12c0 4.418-4.03 8-9 8a9.863 9.863 0 01-4.255-.949L3 20l1.395-3.72C3.512 15.042 3 13.574 3 12c0-4.418 4.03-8 9-8s9 3.582 9 8z"/>
        </svg>
        Comment
      </button>
      <button
        data-testid="post-share-button"
        class="flex items-center gap-1.5 text-gray-500 hover:text-blue-400 text-sm font-medium px-3 py-1.5 rounded-lg hover:bg-blue-900/40 transition flex-1 justify-center"
        :title="shareCopied ? 'Link copied!' : 'Share this post'"
        @click="openShareModal"
      >
        <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
            d="M8.684 13.342C8.886 12.938 9 12.482 9 12c0-.482-.114-.938-.316-1.342m0 2.684a3 3 0 110-2.684m0 2.684l6.632 3.316m-6.632-6l6.632-3.316m0 0a3 3 0 105.367-2.684 3 3 0 00-5.367 2.684zm0 9.316a3 3 0 105.368 2.684 3 3 0 00-5.368-2.684z"/>
        </svg>
        {{ shareCopied ? 'Copied!' : 'Share' }}
      </button>
    </div>

    <!-- Comment thread (collapsed by default, expands on Comment button click) -->
    <CommentThread
      ref="threadRef"
      :post-id="post.id"
      v-model:expanded="threadExpanded"
    />

    <!-- Share modal -->
    <ShareModal
      v-if="isLoggedIn"
      v-model="shareModalOpen"
      :post="post"
    />

    <!-- Report modal -->
    <ReportModal
      v-if="isLoggedIn && !isOwn"
      v-model="reportModalOpen"
      target-type="Post"
      :target-id="post.id"
    />
  </article>
</template>
