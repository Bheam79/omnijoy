<script setup lang="ts">
import type { SharedPostFeedItemDto } from '@/services/postService'
import { computed } from 'vue'
import { useAuthStore } from '@/stores/auth'
import SavePostButton from './SavePostButton.vue'
import MentionText from '@/components/shared/MentionText.vue'

const props = defineProps<{ sharedPost: SharedPostFeedItemDto }>()
const auth = useAuthStore()
const isLoggedIn = computed(() => !!auth.user)

function formatDate(iso: string) {
  return new Date(iso).toLocaleString(undefined, {
    year: 'numeric', month: 'short', day: 'numeric',
    hour: '2-digit', minute: '2-digit',
    hour12: false,
  })
}

const privacyIcon: Record<string, string> = {
  Everyone: '🌐',
  Friends: '👥',
  OnlyMe: '🔒',
}
</script>

<template>
  <article data-testid="shared-post-card" class="bg-slate-800 rounded-xl shadow-sm border border-slate-700 overflow-hidden">
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
        <p class="text-sm font-semibold text-slate-100">
          <RouterLink
            :to="`/profile/${sharedPost.sharer.id}`"
            class="hover:underline"
          >{{ sharedPost.sharer.displayName }}</RouterLink>
          <span class="font-normal text-gray-500"> shared a post</span>
        </p>
        <p class="text-xs text-slate-500 mt-0.5">{{ formatDate(sharedPost.createdAt) }}</p>
      </div>
    </div>

    <!-- Optional sharer message -->
    <div
      v-if="sharedPost.message"
      class="px-4 pb-3 text-slate-200 leading-relaxed whitespace-pre-wrap text-sm"
    >
      {{ sharedPost.message }}
    </div>

    <!-- Embedded original post -->
    <div class="mx-4 mb-4 border border-slate-700 rounded-xl overflow-hidden bg-slate-950">
      <!-- Original post header -->
      <div class="flex items-center justify-between gap-3 px-3 pt-3 pb-2">
        <div class="flex items-center gap-3">
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
              class="font-semibold text-slate-100 hover:underline text-sm"
            >
              {{ sharedPost.originalPost.author.displayName }}
            </RouterLink>
            <div class="flex items-center gap-1 text-xs text-slate-500 mt-0.5">
              <span>{{ formatDate(sharedPost.originalPost.createdAt) }}</span>
              <span>·</span>
              <span>{{ privacyIcon[sharedPost.originalPost.privacy] ?? '👥' }}</span>
            </div>
          </div>
        </div>
        <div v-if="isLoggedIn" class="w-28 shrink-0">
          <SavePostButton :post="sharedPost.originalPost" />
        </div>
      </div>

      <!-- Original post content: Text -->
      <div
        v-if="sharedPost.originalPost.postType === 'Text' && sharedPost.originalPost.content"
        class="px-3 pb-3 text-slate-200 text-sm leading-relaxed whitespace-pre-wrap"
      >
        <MentionText
          :content="sharedPost.originalPost.content"
          :mentions="sharedPost.originalPost.mentions"
        />
      </div>

      <!-- Link preview -->
      <a
        v-if="sharedPost.originalPost.linkPreview"
        :href="sharedPost.originalPost.linkPreview.url"
        target="_blank"
        rel="noopener noreferrer"
        class="block mx-3 mb-3 border border-slate-700 rounded-lg overflow-hidden hover:bg-slate-800 transition text-sm"
      >
        <img
          v-if="sharedPost.originalPost.linkPreview.imageUrl"
          :src="sharedPost.originalPost.linkPreview.imageUrl"
          :alt="sharedPost.originalPost.linkPreview.title ?? ''"
          class="w-full max-h-48 object-cover bg-slate-700"
          loading="lazy"
        />
        <div class="px-3 py-2">
          <p v-if="sharedPost.originalPost.linkPreview.title" class="font-semibold text-slate-100 line-clamp-2">
            {{ sharedPost.originalPost.linkPreview.title }}
          </p>
          <p class="text-xs text-blue-400 mt-0.5 truncate">{{ sharedPost.originalPost.linkPreview.url }}</p>
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
          <MentionText
            :content="sharedPost.originalPost.content"
            :mentions="sharedPost.originalPost.mentions"
          />
        </p>
      </div>

      <!-- Images -->
      <template v-else-if="sharedPost.originalPost.postType === 'Image'">
        <div class="px-3 pb-2 text-sm text-slate-200 whitespace-pre-wrap" v-if="sharedPost.originalPost.content">
          <MentionText
            :content="sharedPost.originalPost.content"
            :mentions="sharedPost.originalPost.mentions"
          />
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
        <div class="px-3 pb-2 text-sm text-slate-200 whitespace-pre-wrap" v-if="sharedPost.originalPost.content">
          <MentionText
            :content="sharedPost.originalPost.content"
            :mentions="sharedPost.originalPost.mentions"
          />
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
