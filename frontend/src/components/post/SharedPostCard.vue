<script setup lang="ts">
import type { SharedPostFeedItemDto } from '@/services/postService'

const props = defineProps<{ sharedPost: SharedPostFeedItemDto }>()

function formatDate(iso: string) {
  return new Date(iso).toLocaleString(undefined, {
    year: 'numeric', month: 'short', day: 'numeric',
    hour: '2-digit', minute: '2-digit',
  })
}

const privacyIcon: Record<string, string> = {
  Everyone: '🌐',
  Friends: '👥',
  OnlyMe: '🔒',
}
</script>

<template>
  <article class="bg-white rounded-xl shadow-sm border border-gray-100 overflow-hidden">
    <!-- Share header -->
    <div class="flex items-center gap-3 px-4 pt-4 pb-2">
      <RouterLink :to="`/profile/${sharedPost.sharer.id}`">
        <img
          v-if="sharedPost.sharer.avatarUrl"
          :src="sharedPost.sharer.avatarUrl"
          :alt="sharedPost.sharer.displayName"
          class="w-10 h-10 rounded-full object-cover"
        />
        <div
          v-else
          class="w-10 h-10 rounded-full bg-green-500 flex items-center justify-center text-white font-semibold text-sm"
        >
          {{ sharedPost.sharer.displayName.charAt(0).toUpperCase() }}
        </div>
      </RouterLink>
      <div>
        <p class="text-sm font-semibold text-gray-900">
          <RouterLink
            :to="`/profile/${sharedPost.sharer.id}`"
            class="hover:underline"
          >{{ sharedPost.sharer.displayName }}</RouterLink>
          <span class="font-normal text-gray-500"> shared a post</span>
        </p>
        <p class="text-xs text-gray-400 mt-0.5">{{ formatDate(sharedPost.createdAt) }}</p>
      </div>
    </div>

    <!-- Optional sharer message -->
    <div
      v-if="sharedPost.message"
      class="px-4 pb-3 text-gray-800 leading-relaxed whitespace-pre-wrap text-sm"
    >
      {{ sharedPost.message }}
    </div>

    <!-- Embedded original post -->
    <div class="mx-4 mb-4 border border-gray-200 rounded-xl overflow-hidden bg-gray-50">
      <!-- Original post header -->
      <div class="flex items-center gap-3 px-3 pt-3 pb-2">
        <RouterLink :to="`/profile/${sharedPost.originalPost.author.id}`">
          <img
            v-if="sharedPost.originalPost.author.avatarUrl"
            :src="sharedPost.originalPost.author.avatarUrl"
            :alt="sharedPost.originalPost.author.displayName"
            class="w-8 h-8 rounded-full object-cover"
          />
          <div
            v-else
            class="w-8 h-8 rounded-full bg-blue-500 flex items-center justify-center text-white font-semibold text-xs"
          >
            {{ sharedPost.originalPost.author.displayName.charAt(0).toUpperCase() }}
          </div>
        </RouterLink>
        <div>
          <RouterLink
            :to="`/profile/${sharedPost.originalPost.author.id}`"
            class="font-semibold text-gray-900 hover:underline text-sm"
          >
            {{ sharedPost.originalPost.author.displayName }}
          </RouterLink>
          <div class="flex items-center gap-1 text-xs text-gray-400 mt-0.5">
            <span>{{ formatDate(sharedPost.originalPost.createdAt) }}</span>
            <span>·</span>
            <span>{{ privacyIcon[sharedPost.originalPost.privacy] ?? '👥' }}</span>
          </div>
        </div>
      </div>

      <!-- Original post content: Text -->
      <div
        v-if="sharedPost.originalPost.postType === 'Text' && sharedPost.originalPost.content"
        class="px-3 pb-3 text-gray-800 text-sm leading-relaxed whitespace-pre-wrap"
      >
        {{ sharedPost.originalPost.content }}
      </div>

      <!-- Link preview -->
      <a
        v-if="sharedPost.originalPost.linkPreview"
        :href="sharedPost.originalPost.linkPreview.url"
        target="_blank"
        rel="noopener noreferrer"
        class="block mx-3 mb-3 border border-gray-200 rounded-lg overflow-hidden hover:bg-white transition text-sm"
      >
        <img
          v-if="sharedPost.originalPost.linkPreview.imageUrl"
          :src="sharedPost.originalPost.linkPreview.imageUrl"
          :alt="sharedPost.originalPost.linkPreview.title ?? ''"
          class="w-full max-h-48 object-cover bg-gray-100"
          loading="lazy"
        />
        <div class="px-3 py-2">
          <p v-if="sharedPost.originalPost.linkPreview.title" class="font-semibold text-gray-900 line-clamp-2">
            {{ sharedPost.originalPost.linkPreview.title }}
          </p>
          <p class="text-xs text-blue-600 mt-0.5 truncate">{{ sharedPost.originalPost.linkPreview.url }}</p>
        </div>
      </a>

      <!-- TextOnBackground -->
      <div
        v-else-if="sharedPost.originalPost.postType === 'TextOnBackground'"
        :style="sharedPost.originalPost.backgroundImageUrl
          ? { backgroundImage: `url(${sharedPost.originalPost.backgroundImageUrl})`, backgroundSize: 'cover' }
          : { background: sharedPost.originalPost.backgroundImageUrl ?? '#1877f2' }"
        class="mx-3 mb-3 rounded-lg flex items-center justify-center min-h-32 p-4"
      >
        <p class="text-white text-lg font-bold text-center drop-shadow">
          {{ sharedPost.originalPost.content }}
        </p>
      </div>

      <!-- Images -->
      <template v-else-if="sharedPost.originalPost.postType === 'Image'">
        <div class="px-3 pb-2 text-sm text-gray-800 whitespace-pre-wrap" v-if="sharedPost.originalPost.content">
          {{ sharedPost.originalPost.content }}
        </div>
        <div
          class="grid gap-0.5"
          :class="{
            'grid-cols-1': sharedPost.originalPost.media.length === 1,
            'grid-cols-2': sharedPost.originalPost.media.length >= 2,
          }"
        >
          <img
            v-for="(m, i) in sharedPost.originalPost.media.slice(0, 4)"
            :key="m.id"
            :src="m.url"
            :alt="`Image ${i + 1}`"
            class="w-full object-cover max-h-64"
          />
        </div>
      </template>

      <!-- Video -->
      <template v-else-if="sharedPost.originalPost.postType === 'Video'">
        <div class="px-3 pb-2 text-sm text-gray-800 whitespace-pre-wrap" v-if="sharedPost.originalPost.content">
          {{ sharedPost.originalPost.content }}
        </div>
        <div v-for="m in sharedPost.originalPost.media" :key="m.id" class="bg-black">
          <video
            :src="m.url"
            :poster="m.thumbnailUrl"
            controls
            class="w-full max-h-64 object-contain"
            preload="metadata"
          />
        </div>
      </template>
    </div>
  </article>
</template>
