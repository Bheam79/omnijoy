<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useRoute } from 'vue-router'
import { userService, type UserProfile } from '@/services/userService'
import { useAuthStore } from '@/stores/auth'

const route = useRoute()
const auth = useAuthStore()

const profile = ref<UserProfile | null>(null)
const loading = ref(true)
const errorMessage = ref<string | null>(null)
const errorTitle = ref<string | null>(null)

onMounted(async () => {
  const id = route.params.id as string
  try {
    profile.value = await userService.getProfile(id)
  } catch (e: unknown) {
    const axiosError = e as { response?: { status?: number; data?: { error?: string } } }
    const status = axiosError.response?.status
    if (status === 404) {
      errorTitle.value = 'User not found'
      errorMessage.value = 'This user profile does not exist.'
    } else if (status === 403 || status === 401) {
      errorTitle.value = 'This profile is not public'
      errorMessage.value = 'You may need to sign in to view this profile.'
    } else {
      errorTitle.value = 'Could not load profile'
      errorMessage.value = axiosError.response?.data?.error ?? 'Something went wrong.'
    }
  } finally {
    loading.value = false
  }
})
</script>

<template>
  <main class="min-h-screen bg-slate-700 py-8 px-4">
    <div class="max-w-xl mx-auto">
      <header class="flex items-center justify-between mb-6">
        <RouterLink to="/" class="text-2xl font-extrabold text-blue-400">Omnijoy</RouterLink>
        <RouterLink
          v-if="!auth.isAuthenticated"
          to="/login"
          class="text-sm font-semibold text-blue-400 hover:underline"
        >Sign in</RouterLink>
        <RouterLink
          v-else
          to="/wall"
          class="text-sm font-semibold text-blue-400 hover:underline"
        >Open Omnijoy</RouterLink>
      </header>

      <div v-if="loading" class="bg-slate-800 rounded-xl shadow-sm border border-slate-700 p-8 text-center text-gray-500">
        Loading…
      </div>

      <div
        v-else-if="errorMessage"
        class="bg-slate-800 rounded-xl shadow-sm border border-slate-700 p-8 text-center"
      >
        <h1 class="text-xl font-semibold text-slate-100 mb-2">{{ errorTitle }}</h1>
        <p class="text-gray-500">{{ errorMessage }}</p>
        <RouterLink
          to="/"
          class="inline-block mt-6 px-5 py-2 rounded-lg bg-blue-600 text-white font-semibold hover:bg-blue-700 transition"
        >Back to Omnijoy</RouterLink>
      </div>

      <article
        v-else-if="profile"
        class="bg-slate-800 rounded-xl shadow-sm border border-slate-700 overflow-hidden"
      >
        <!-- Cover -->
        <div
          class="h-40 bg-slate-700"
          :style="profile.coverUrl ? {
            backgroundImage: `url(${profile.coverUrl})`,
            backgroundSize: 'cover',
            backgroundPosition: 'center',
          } : undefined"
        />

        <!-- Avatar + name -->
        <div class="px-5 pb-5 -mt-12">
          <img
            v-if="profile.avatarUrl"
            :src="profile.avatarUrl"
            :alt="profile.displayName"
            class="w-24 h-24 rounded-full border-4 border-slate-700 object-cover bg-slate-800"
          />
          <div
            v-else
            class="w-24 h-24 rounded-full border-4 border-slate-700 bg-blue-500 flex items-center justify-center text-white font-bold text-3xl"
          >{{ profile.displayName.charAt(0).toUpperCase() }}</div>

          <h1 class="mt-3 text-2xl font-bold text-slate-100">{{ profile.displayName }}</h1>
          <p class="text-sm text-gray-500 mt-1">{{ profile.friendCount }} friends</p>

          <p
            v-if="profile.bio"
            class="mt-4 text-slate-300 leading-relaxed whitespace-pre-wrap"
          >{{ profile.bio }}</p>

          <div class="mt-6 flex gap-3">
            <RouterLink
              :to="auth.isAuthenticated ? `/profile/${profile.id}` : '/register'"
              class="px-5 py-2 rounded-lg bg-blue-600 text-white font-semibold hover:bg-blue-700 transition"
            >
              {{ auth.isAuthenticated ? 'View full profile' : 'Sign up to connect' }}
            </RouterLink>
          </div>
        </div>
      </article>
    </div>
  </main>
</template>
