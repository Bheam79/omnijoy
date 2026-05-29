<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { chatService } from '@/services/chatService'
import type { OgPreview } from '@/types'

const props = defineProps<{ url: string }>()

const preview = ref<OgPreview | null>(null)
const loading = ref(true)

onMounted(async () => {
  try {
    preview.value = await chatService.getMetaPreview(props.url)
  } finally {
    loading.value = false
  }
})
</script>

<template>
  <!-- Loading skeleton -->
  <div
    v-if="loading"
    class="mt-1.5 h-16 rounded-lg bg-slate-700 animate-pulse"
  />

  <!-- Preview card -->
  <a
    v-else-if="preview && (preview.title || preview.description || preview.imageUrl)"
    :href="url"
    target="_blank"
    rel="noopener noreferrer"
    class="block mt-1.5 rounded-lg border border-slate-700 overflow-hidden hover:bg-slate-700 transition-colors text-left"
  >
    <img
      v-if="preview.imageUrl"
      :src="preview.imageUrl"
      :alt="preview.title ?? ''"
      class="w-full h-28 object-cover"
      loading="lazy"
    />
    <div class="px-2 py-1.5">
      <p v-if="preview.siteName" class="text-[10px] text-slate-500 uppercase tracking-wider truncate">
        {{ preview.siteName }}
      </p>
      <p v-if="preview.title" class="text-xs font-semibold text-slate-100 line-clamp-2 leading-snug">
        {{ preview.title }}
      </p>
      <p v-if="preview.description" class="text-[11px] text-gray-500 line-clamp-2 mt-0.5">
        {{ preview.description }}
      </p>
    </div>
  </a>
</template>
