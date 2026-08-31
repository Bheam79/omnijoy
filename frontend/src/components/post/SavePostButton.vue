<script setup lang="ts">
import { computed } from 'vue'
import type { PostDto } from '@/services/postService'
import { useSavedPostsStore } from '@/stores/savedPosts'

const props = defineProps<{ post: PostDto }>()
const savedPosts = useSavedPostsStore()

savedPosts.seed(props.post)

const saved = computed(() => savedPosts.isSaved(props.post.id, props.post.isSavedByMe))
const pending = computed(() => savedPosts.isPending(props.post.id))
const feedback = computed(() => savedPosts.errorFor(props.post.id))
const label = computed(() => saved.value ? 'Remove from saved posts' : 'Save post')
</script>

<template>
  <div class="relative flex flex-1 justify-center">
    <button
      data-testid="post-save-button"
      type="button"
      class="flex w-full items-center justify-center gap-1.5 rounded-lg px-3 py-1.5 text-sm font-medium transition focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-indigo-400 focus-visible:ring-offset-2 focus-visible:ring-offset-slate-800 disabled:cursor-wait disabled:opacity-60"
      :class="saved ? 'text-indigo-300 hover:bg-indigo-900/40' : 'text-gray-500 hover:bg-slate-700 hover:text-indigo-300'"
      :aria-label="label"
      :title="label"
      :aria-pressed="saved"
      :disabled="pending"
      @click="savedPosts.toggle(post)"
    >
      <svg class="h-5 w-5" :fill="saved ? 'currentColor' : 'none'" stroke="currentColor" viewBox="0 0 24 24" aria-hidden="true">
        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M5 5a2 2 0 012-2h10a2 2 0 012 2v16l-7-4-7 4V5z"/>
      </svg>
      <span>{{ saved ? 'Saved' : 'Save' }}</span>
    </button>
    <p
      v-if="feedback"
      role="alert"
      class="absolute right-0 top-full z-10 mt-1 max-w-56 rounded bg-red-950 px-2 py-1 text-xs text-red-300 shadow"
    >
      {{ feedback }}
    </p>
  </div>
</template>
