<script setup lang="ts">
import { ref } from 'vue'
import { RouterLink } from 'vue-router'
import { useAuthStore } from '@/stores/auth'
import SlugPicker from '@/components/shared/SlugPicker.vue'

const auth = useAuthStore()

const currentSlug = ref<string | null>(
  (auth.user as (typeof auth.user & { urlSlug?: string | null }))?.urlSlug ?? null,
)

function onSlugUpdated(newSlug: string | null) {
  currentSlug.value = newSlug
  // Persist slug back onto the cached user so TopNav / profile links update instantly.
  if (auth.user) {
    auth.setUser({ ...auth.user, urlSlug: newSlug ?? undefined } as typeof auth.user & { urlSlug?: string })
  }
}
</script>

<template>
  <div>
    <!-- Back link -->
    <div class="flex items-center gap-2 mb-6">
      <RouterLink
        to="/settings"
        class="flex items-center gap-1.5 text-sm text-gray-500 hover:text-gray-700 transition-colors"
      >
        <svg class="h-4 w-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 19l-7-7 7-7" />
        </svg>
        Settings
      </RouterLink>
    </div>

    <h1 class="text-2xl font-bold text-gray-900 mb-2">Profile</h1>
    <p class="text-sm text-gray-500 mb-6">Manage your public profile settings.</p>

    <!-- Public URL card -->
    <SlugPicker
      v-if="auth.user"
      :initial-slug="currentSlug"
      scope="user"
      @updated="onSlugUpdated"
    />
  </div>
</template>
