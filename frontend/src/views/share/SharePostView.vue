<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useRoute } from 'vue-router'
import { postService, type PostDto } from '@/services/postService'
import { useAuthStore } from '@/stores/auth'

const route = useRoute()
const auth = useAuthStore()

const post = ref<PostDto | null>(null)
const loading = ref(true)
const errorMessage = ref<string | null>(null)
const errorTitle = ref<string | null>(null)

onMounted(async () => {
  const id = route.params.id as string
  try {
    post.value = await postService.getPost(id)
  } catch (e: unknown) {
    const axiosError = e as { response?: { status?: number; data?: { error?: string } } }
    const status = axiosError.response?.status
    if (status === 404) {
      errorTitle.value = 'Post not found'
      errorMessage.value = 'This post does not exist or has been deleted.'
    } else if (status === 403) {
      errorTitle.value = 'This post is not public'
      errorMessage.value = 'The author has restricted who can see this post.'
    } else if (status === 401) {
      errorTitle.value = 'This post is not public'
      errorMessage.value = 'You need to sign in to view this post.'
    } else {
      errorTitle.value = 'Could not load post'
      errorMessage.value = axiosError.response?.data?.error ?? 'Something went wrong.'
    }
  } finally {
    loading.value = false
  }
})

function formatDate(iso: string) {
  return new Date(iso).toLocaleString(undefined, {
    year: 'numeric', month: 'short', day: 'numeric',
    hour: '2-digit', minute: '2-digit',
  })
}
</script>

<template>
  <main class="min-h-screen bg-gray-100 py-8 px-4">
    <div class="max-w-xl mx-auto">
      <!-- Brand header -->
      <header class="flex items-center justify-between mb-6">
        <RouterLink to="/" class="text-2xl font-extrabold text-blue-600">Omnijoy</RouterLink>
        <RouterLink
          v-if="!auth.isAuthenticated"
          to="/login"
          class="text-sm font-semibold text-blue-600 hover:underline"
        >Sign in</RouterLink>
        <RouterLink
          v-else
          to="/wall"
          class="text-sm font-semibold text-blue-600 hover:underline"
        >Open Omnijoy</RouterLink>
      </header>

      <!-- Loading -->
      <div v-if="loading" class="bg-white rounded-xl shadow-sm border border-gray-100 p-8 text-center text-gray-500">
        Loading…
      </div>

      <!-- Error state (not public, deleted, etc.) -->
      <div
        v-else-if="errorMessage"
        class="bg-white rounded-xl shadow-sm border border-gray-100 p-8 text-center"
      >
        <h1 class="text-xl font-semibold text-gray-900 mb-2">{{ errorTitle }}</h1>
        <p class="text-gray-500">{{ errorMessage }}</p>
        <RouterLink
          to="/"
          class="inline-block mt-6 px-5 py-2 rounded-lg bg-blue-600 text-white font-semibold hover:bg-blue-700 transition"
        >Back to Omnijoy</RouterLink>
      </div>

      <!-- Post body -->
      <article
        v-else-if="post"
        class="bg-white rounded-xl shadow-sm border border-gray-100 overflow-hidden"
      >
        <!-- Author header -->
        <div class="flex items-center gap-3 px-5 pt-5 pb-3">
          <img
            v-if="post.author.avatarUrl"
            :src="post.author.avatarUrl"
            :alt="post.author.displayName"
            class="w-12 h-12 rounded-full object-cover"
          />
          <div
            v-else
            class="w-12 h-12 rounded-full bg-blue-500 flex items-center justify-center text-white font-semibold"
          >{{ post.author.displayName.charAt(0).toUpperCase() }}</div>
          <div>
            <p class="font-semibold text-gray-900">{{ post.author.displayName }}</p>
            <p class="text-xs text-gray-500">{{ formatDate(post.createdAt) }} · 🌐 Public</p>
          </div>
        </div>

        <!-- Content -->
        <div
          v-if="post.content"
          class="px-5 pb-3 text-gray-800 leading-relaxed whitespace-pre-wrap"
        >{{ post.content }}</div>

        <!-- Link preview -->
        <a
          v-if="post.linkPreview"
          :href="post.linkPreview.url"
          target="_blank"
          rel="noopener noreferrer"
          class="block mx-5 mb-3 border border-gray-200 rounded-xl overflow-hidden hover:bg-gray-50 transition"
        >
          <img
            v-if="post.linkPreview.imageUrl"
            :src="post.linkPreview.imageUrl"
            :alt="post.linkPreview.title ?? ''"
            class="w-full max-h-72 object-cover bg-gray-100"
          />
          <div class="px-3 py-2">
            <p v-if="post.linkPreview.siteName" class="text-xs uppercase tracking-wide text-gray-400">
              {{ post.linkPreview.siteName }}
            </p>
            <p v-if="post.linkPreview.title" class="font-semibold text-sm text-gray-900">
              {{ post.linkPreview.title }}
            </p>
            <p v-if="post.linkPreview.description" class="text-xs text-gray-500 mt-1">
              {{ post.linkPreview.description }}
            </p>
            <p class="text-xs text-blue-600 mt-1 truncate">{{ post.linkPreview.url }}</p>
          </div>
        </a>

        <!-- Images -->
        <div
          v-if="post.postType === 'Image' && post.media.length"
          class="grid gap-1"
          :class="{ 'grid-cols-1': post.media.length === 1, 'grid-cols-2': post.media.length >= 2 }"
        >
          <img
            v-for="m in post.media.slice(0, 4)"
            :key="m.id"
            :src="m.url"
            class="w-full object-cover max-h-96"
            :class="{ 'col-span-2': post.media.length === 1 }"
          />
        </div>

        <!-- Video -->
        <template v-else-if="post.postType === 'Video'">
          <video
            v-for="m in post.media"
            :key="m.id"
            :src="m.url"
            :poster="m.thumbnailUrl"
            controls
            class="w-full max-h-96 object-contain bg-black"
            preload="metadata"
          />
        </template>

        <!-- TextOnBackground -->
        <div
          v-else-if="post.postType === 'TextOnBackground'"
          class="mx-5 mb-5 rounded-xl flex items-center justify-center min-h-52 p-6"
          :style="post.backgroundImageUrl?.startsWith('http') || post.backgroundImageUrl?.startsWith('/')
            ? { backgroundImage: `url(${post.backgroundImageUrl})`, backgroundSize: 'cover', backgroundPosition: 'center' }
            : { background: post.backgroundImageUrl ?? '#1877f2' }"
        >
          <p class="text-white text-xl font-bold text-center drop-shadow leading-relaxed">
            {{ post.content }}
          </p>
        </div>

        <!-- CTA -->
        <div class="border-t border-gray-100 px-5 py-4 text-center bg-gray-50">
          <p class="text-sm text-gray-500 mb-2">Join the conversation on Omnijoy.</p>
          <RouterLink
            :to="auth.isAuthenticated ? '/wall' : '/register'"
            class="inline-block px-5 py-2 rounded-lg bg-blue-600 text-white font-semibold hover:bg-blue-700 transition"
          >{{ auth.isAuthenticated ? 'Open Omnijoy' : 'Create an account' }}</RouterLink>
        </div>
      </article>
    </div>
  </main>
</template>
