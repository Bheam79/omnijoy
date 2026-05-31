<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { friendService, type FriendInviteAcceptDto } from '@/services/friendService'
import { useAuthStore } from '@/stores/auth'

const route = useRoute()
const router = useRouter()
const auth = useAuthStore()

const token = route.params.token as string

const info = ref<FriendInviteAcceptDto | null>(null)
const loading = ref(true)
const accepting = ref(false)
const accepted = ref(false)
const error = ref<string | null>(null)

onMounted(async () => {
  try {
    info.value = await friendService.getInviteInfo(token)
  } catch {
    error.value = 'This invite link is invalid or has expired.'
  } finally {
    loading.value = false
  }

  // If already logged in, auto-accept immediately.
  if (auth.isAuthenticated && info.value) {
    await handleAccept()
  }
})

async function handleAccept() {
  if (!auth.isAuthenticated) {
    // Redirect to register with invite token so they come back after sign-up.
    router.push({ name: 'register', query: { invite: token } })
    return
  }

  accepting.value = true
  try {
    await friendService.acceptInvite(token)
    accepted.value = true
  } catch (e: any) {
    const msg = e?.response?.data?.error ?? 'Could not accept the invite.'
    // "already accepted" or "yourself" are not real errors from the user's perspective.
    if (msg.toLowerCase().includes('already') || msg.toLowerCase().includes('yourself')) {
      accepted.value = true
    } else {
      error.value = msg
    }
  } finally {
    accepting.value = false
  }
}
</script>

<template>
  <div class="min-h-screen bg-slate-950 flex items-center justify-center p-4">
    <div class="bg-slate-900 border border-slate-700 rounded-2xl shadow-xl max-w-sm w-full p-8 text-center space-y-5">

      <!-- Loading skeleton -->
      <template v-if="loading">
        <div class="w-20 h-20 rounded-full bg-slate-800 mx-auto animate-pulse" />
        <div class="h-5 bg-slate-800 rounded w-40 mx-auto animate-pulse" />
        <div class="h-4 bg-slate-800 rounded w-56 mx-auto animate-pulse" />
        <div class="h-10 bg-slate-800 rounded-xl animate-pulse" />
      </template>

      <!-- Error -->
      <template v-else-if="error && !accepted">
        <p class="text-5xl">😕</p>
        <h2 class="text-xl font-bold text-slate-100">Invite not found</h2>
        <p class="text-sm text-gray-400">{{ error }}</p>
        <RouterLink
          :to="auth.isAuthenticated ? { name: 'wall' } : { name: 'home' }"
          class="block mt-2 text-sm text-indigo-400 hover:underline"
        >
          Go to {{ auth.isAuthenticated ? 'your feed' : 'home' }}
        </RouterLink>
      </template>

      <!-- Accepted -->
      <template v-else-if="accepted">
        <p class="text-5xl">🎉</p>
        <h2 class="text-xl font-bold text-slate-100">You're now friends!</h2>
        <p class="text-sm text-gray-400">
          You and <span class="text-indigo-400 font-medium">{{ info?.inviterDisplayName }}</span>
          are now friends on Omnijoy.
        </p>
        <RouterLink
          :to="info?.inviterId ? `/profile/${info.inviterId}` : { name: 'wall' }"
          class="block w-full py-2.5 bg-indigo-600 hover:bg-indigo-700 text-white text-sm font-semibold rounded-xl transition"
        >
          View profile
        </RouterLink>
        <RouterLink to="/friends" class="block text-sm text-gray-500 hover:text-gray-300 transition">
          Go to Friends →
        </RouterLink>
      </template>

      <!-- Invite info + CTA -->
      <template v-else-if="info">
        <!-- Avatar -->
        <div class="flex justify-center">
          <img
            v-if="info.inviterAvatarUrl"
            :src="info.inviterAvatarUrl"
            :alt="info.inviterDisplayName"
            class="w-20 h-20 rounded-full object-cover ring-4 ring-indigo-500/30"
          />
          <div
            v-else
            class="w-20 h-20 rounded-full bg-indigo-900 text-indigo-400 flex items-center justify-center text-3xl font-bold ring-4 ring-indigo-500/30"
          >
            {{ info.inviterDisplayName.charAt(0).toUpperCase() }}
          </div>
        </div>

        <div>
          <h2 class="text-xl font-bold text-slate-100">
            {{ info.inviterDisplayName }} wants to be your friend
          </h2>
          <p class="text-sm text-gray-400 mt-1">
            Join Omnijoy and connect with {{ info.inviterDisplayName }} instantly.
          </p>
        </div>

        <!-- Logged in: accept button -->
        <template v-if="auth.isAuthenticated">
          <button
            class="w-full py-2.5 bg-indigo-600 hover:bg-indigo-700 text-white text-sm font-semibold rounded-xl transition disabled:opacity-60"
            :disabled="accepting"
            @click="handleAccept"
          >
            {{ accepting ? 'Accepting…' : 'Accept & become friends' }}
          </button>
        </template>

        <!-- Not logged in: register / login -->
        <template v-else>
          <RouterLink
            :to="{ name: 'register', query: { invite: token } }"
            class="block w-full py-2.5 bg-indigo-600 hover:bg-indigo-700 text-white text-sm font-semibold rounded-xl transition text-center"
          >
            Create account &amp; accept
          </RouterLink>
          <RouterLink
            :to="{ name: 'login', query: { invite: token, redirect: `/invite/${token}` } }"
            class="block text-sm text-gray-400 hover:text-gray-200 transition"
          >
            Already have an account? Log in →
          </RouterLink>
        </template>
      </template>

    </div>
  </div>
</template>
