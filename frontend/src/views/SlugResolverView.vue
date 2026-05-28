<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { slugService } from '@/services/slugService'

const route  = useRoute()
const router = useRouter()

const notFound = ref(false)

onMounted(async () => {
  const slug = route.params.slug as string

  try {
    const owner = await slugService.resolve(slug)

    if (!owner) {
      notFound.value = true
      return
    }

    if (owner.type === 'user') {
      router.replace({ name: 'profile', params: { userId: owner.id } })
    } else if (owner.type === 'company') {
      router.replace({ name: 'company', params: { id: owner.id } })
    } else {
      notFound.value = true
    }
  } catch {
    notFound.value = true
  }
})
</script>

<template>
  <!-- 404: render the not-found content inline -->
  <div v-if="notFound" class="min-h-screen bg-gray-50 flex flex-col items-center justify-center px-4 text-center">
    <div class="mb-6">
      <div class="inline-flex h-24 w-24 rounded-full bg-indigo-50 items-center justify-center">
        <svg class="h-12 w-12 text-indigo-300" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5"
            d="M9.172 16.172a4 4 0 015.656 0M9 10h.01M15 10h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z"/>
        </svg>
      </div>
    </div>
    <p class="text-7xl font-extrabold text-gray-100 leading-none select-none mb-4">404</p>
    <h1 class="text-2xl font-bold text-gray-900 mb-3">Page not found</h1>
    <p class="text-gray-500 mb-8 max-w-sm leading-relaxed">
      We couldn't find anyone with that username. It may have changed or never existed.
    </p>
    <RouterLink
      to="/wall"
      class="inline-flex items-center justify-center gap-2 bg-indigo-600 hover:bg-indigo-700 text-white font-medium px-6 py-2.5 rounded-lg text-sm transition-colors"
    >
      <svg class="h-4 w-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M10 19l-7-7m0 0l7-7m-7 7h18"/>
      </svg>
      Back to My Wall
    </RouterLink>
  </div>

  <!-- Loading spinner (shown during slug resolution) -->
  <div
    v-else
    class="min-h-screen flex items-center justify-center"
    data-testid="slug-spinner"
  >
    <div class="h-10 w-10 rounded-full border-4 border-indigo-500 border-t-transparent animate-spin" />
  </div>
</template>
