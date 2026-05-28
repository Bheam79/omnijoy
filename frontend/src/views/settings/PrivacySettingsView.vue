<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { RouterLink } from 'vue-router'
import { userService, type PrivacySettings, type UpdatePrivacyPayload } from '@/services/userService'

const privacy = ref<PrivacySettings | null>(null)
const loading = ref(true)
const saving = ref(false)
const error = ref<string | null>(null)
const successMsg = ref<string | null>(null)

onMounted(async () => {
  try {
    privacy.value = await userService.getPrivacySettings()
  } catch {
    error.value = 'Failed to load privacy settings.'
  } finally {
    loading.value = false
  }
})

async function save() {
  if (!privacy.value) return
  saving.value = true
  error.value = null
  successMsg.value = null
  try {
    const payload: UpdatePrivacyPayload = { ...privacy.value }
    privacy.value = await userService.updatePrivacySettings(payload)
    successMsg.value = 'Privacy settings saved.'
  } catch (e: unknown) {
    const ax = e as { response?: { data?: { error?: string } } }
    error.value = ax.response?.data?.error ?? 'Failed to save settings.'
  } finally {
    saving.value = false
  }
}

const privacyOptions = [
  { value: 'Everyone', label: 'Everyone' },
  { value: 'FriendsOfFriends', label: 'Friends of friends' },
  { value: 'Friends', label: 'Friends only' },
  { value: 'OnlyMe', label: 'Only me' },
]

const privacyFields: Array<{ key: keyof PrivacySettings; label: string; description: string }> = [
  { key: 'whoCanSeePosts', label: 'Who can see my posts', description: 'Default visibility for new posts you create.' },
  { key: 'whoCanSeeProfile', label: 'Who can see my profile', description: 'Controls access to your profile page.' },
  { key: 'whoCanSendMessages', label: 'Who can message me', description: 'Restricts who can start conversations with you.' },
  { key: 'whoCanSeeFriendList', label: 'Who can see my friend list', description: 'Controls visibility of your connections.' },
  { key: 'whoCanSeeEvents', label: 'Who can see my events', description: 'Events you are attending or hosting.' },
  { key: 'whoCanTagInPosts', label: 'Who can tag me in posts', description: 'Prevents unwanted mentions.' },
]
</script>

<template>
  <div>
    <!-- Back link -->
    <div class="flex items-center gap-2 mb-6">
      <RouterLink to="/settings" class="flex items-center gap-1.5 text-sm text-gray-500 hover:text-gray-700 transition-colors">
        <svg class="h-4 w-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 19l-7-7 7-7"/>
        </svg>
        Settings
      </RouterLink>
    </div>

    <h1 class="text-2xl font-bold text-gray-900 mb-2">Privacy</h1>
    <p class="text-sm text-gray-500 mb-6">Control who can see your content and interact with you.</p>

    <!-- Loading -->
    <div v-if="loading" class="flex justify-center py-12">
      <div class="h-7 w-7 border-2 border-indigo-500 border-t-transparent rounded-full animate-spin" />
    </div>

    <template v-else-if="privacy">
      <!-- Error / success banners -->
      <div v-if="error" class="mb-5 rounded-lg bg-red-50 border border-red-200 px-4 py-3 text-sm text-red-700">{{ error }}</div>
      <div v-if="successMsg" class="mb-5 rounded-lg bg-green-50 border border-green-200 px-4 py-3 text-sm text-green-700">{{ successMsg }}</div>

      <form class="space-y-4" @submit.prevent="save">
        <div
          v-for="field in privacyFields"
          :key="field.key"
          class="bg-white rounded-xl border border-gray-100 px-5 py-4"
        >
          <label class="block text-sm font-medium text-gray-900 mb-0.5">{{ field.label }}</label>
          <p class="text-xs text-gray-500 mb-3">{{ field.description }}</p>
          <select
            v-model="privacy[field.key]"
            class="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500 focus:border-transparent"
          >
            <option v-for="opt in privacyOptions" :key="opt.value" :value="opt.value">{{ opt.label }}</option>
          </select>
        </div>

        <button
          type="submit"
          :disabled="saving"
          class="w-full bg-indigo-600 hover:bg-indigo-700 disabled:opacity-60 text-white font-medium py-2.5 rounded-xl text-sm transition-colors"
        >
          <span v-if="saving">Saving…</span>
          <span v-else>Save privacy settings</span>
        </button>
      </form>
    </template>
  </div>
</template>
