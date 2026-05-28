<script setup lang="ts">
import { RouterLink } from 'vue-router'
import type { LiveStreamDto } from '@/services/liveService'

defineProps<{ stream: LiveStreamDto }>()
</script>

<template>
  <RouterLink
    :to="`/live/${stream.id}`"
    class="group block bg-white rounded-2xl border border-gray-100 overflow-hidden hover:shadow-md transition-shadow"
  >
    <!-- Thumbnail placeholder -->
    <div class="relative aspect-video bg-gradient-to-br from-gray-800 to-gray-900 flex items-center justify-center">
      <!-- LIVE badge -->
      <span class="absolute top-2 left-2 flex items-center gap-1.5 px-2 py-0.5 bg-red-600 text-white text-xs font-bold rounded-full">
        <span class="w-1.5 h-1.5 rounded-full bg-white animate-pulse" />
        LIVE
      </span>

      <!-- Viewer count -->
      <span class="absolute top-2 right-2 flex items-center gap-1 px-2 py-0.5 bg-black/60 text-white text-xs rounded-full">
        <svg class="w-3 h-3" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                d="M15 12a3 3 0 11-6 0 3 3 0 016 0z"/>
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                d="M2.458 12C3.732 7.943 7.523 5 12 5c4.478 0 8.268 2.943 9.542 7-1.274 4.057-5.064 7-9.542 7-4.477 0-8.268-2.943-9.542-7z"/>
        </svg>
        {{ stream.viewerCount }}
      </span>

      <!-- Play icon -->
      <div class="w-12 h-12 rounded-full bg-white/20 flex items-center justify-center group-hover:bg-white/30 transition">
        <svg class="w-6 h-6 text-white ml-0.5" fill="currentColor" viewBox="0 0 24 24">
          <path d="M8 5v14l11-7z"/>
        </svg>
      </div>
    </div>

    <!-- Info -->
    <div class="p-3">
      <p class="font-semibold text-gray-900 text-sm line-clamp-1 mb-1">{{ stream.title }}</p>
      <div class="flex items-center gap-2">
        <!-- Avatar -->
        <img
          v-if="stream.host.avatarUrl"
          :src="stream.host.avatarUrl"
          :alt="stream.host.displayName"
          class="w-5 h-5 rounded-full object-cover"
        />
        <div
          v-else
          class="w-5 h-5 rounded-full bg-indigo-200 flex items-center justify-center text-indigo-700 text-[10px] font-bold"
        >
          {{ stream.host.displayName.charAt(0).toUpperCase() }}
        </div>
        <span class="text-xs text-gray-600 truncate">{{ stream.host.displayName }}</span>

        <!-- Privacy badge -->
        <span
          v-if="stream.privacy === 'Friends'"
          class="ml-auto text-[10px] px-1.5 py-0.5 bg-blue-50 text-blue-600 rounded-full font-medium"
        >
          Friends
        </span>
      </div>
    </div>
  </RouterLink>
</template>
