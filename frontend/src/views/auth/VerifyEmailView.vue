<script setup lang="ts">
/**
 * /verify-email?token=…
 *
 * Landing page reached from the welcome email link. We POST the token to
 * /api/auth/verify-email immediately on mount and surface a single-screen
 * success / failure state. No auth gate — the token IS the credential, and
 * users may land here either while signed in or freshly registered.
 *
 * If the user happens to already be signed in, we patch the local user
 * record so the AccountSettings banner disappears without a page reload.
 */
import { onMounted, ref } from 'vue'
import { useRoute, RouterLink } from 'vue-router'
import { authService } from '@/services/authService'
import { useAuthStore } from '@/stores/auth'

type Status = 'loading' | 'success' | 'error'

const route  = useRoute()
const auth   = useAuthStore()
const status = ref<Status>('loading')
const errorMessage = ref<string | null>(null)

onMounted(async () => {
  // The router query param can be a string or an array — coerce safely.
  const raw = route.query.token
  const token = Array.isArray(raw) ? raw[0] : raw

  if (!token) {
    status.value = 'error'
    errorMessage.value = 'No verification token in the link.'
    return
  }

  try {
    await authService.verifyEmail(token)
    status.value = 'success'
    // If the current session belongs to the same user, reflect the new
    // verified state immediately so the settings banner clears.
    if (auth.user) {
      auth.setUser({ ...auth.user, isEmailVerified: true })
    }
  } catch (e: unknown) {
    const ax = e as { response?: { status?: number; data?: { error?: string } } }
    status.value = 'error'
    errorMessage.value =
      ax?.response?.data?.error ??
      'This verification link is invalid or has already been used.'
  }
})
</script>

<template>
  <div class="min-h-screen bg-slate-950 flex flex-col justify-center items-center px-4 py-12">
    <div class="w-full max-w-md">
      <!-- Logo / heading -->
      <div class="text-center mb-8">
        <img src="/logo.png" alt="Omnijoy" class="h-20 w-20 mx-auto mb-3 rounded-2xl" />
        <h1 class="text-4xl font-bold text-slate-100 tracking-tight">Omnijoy</h1>
        <p class="text-gray-500 mt-2">Email verification</p>
      </div>

      <div class="bg-slate-800 rounded-2xl shadow-sm border border-slate-700 p-8">

        <!-- ── Loading ── -->
        <div
          v-if="status === 'loading'"
          data-testid="verify-loading"
          class="flex flex-col items-center text-center py-6"
        >
          <svg
            class="animate-spin h-8 w-8 text-indigo-400 mb-3"
            xmlns="http://www.w3.org/2000/svg"
            fill="none"
            viewBox="0 0 24 24"
          >
            <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"></circle>
            <path
              class="opacity-75"
              fill="currentColor"
              d="M4 12a8 8 0 018-8v4a4 4 0 00-4 4H4z"
            ></path>
          </svg>
          <p class="text-sm text-slate-400">Verifying your email…</p>
        </div>

        <!-- ── Success ── -->
        <div
          v-else-if="status === 'success'"
          data-testid="verify-success"
          class="text-center"
        >
          <div class="mx-auto h-12 w-12 rounded-full bg-green-950 border border-green-800 flex items-center justify-center mb-4">
            <svg class="h-6 w-6 text-green-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M5 13l4 4L19 7" />
            </svg>
          </div>
          <h2 class="text-lg font-semibold text-slate-100 mb-2">Email verified!</h2>
          <div class="mb-5 rounded-lg bg-green-950 border border-green-800 px-4 py-3 text-sm text-green-400">
            Your email address has been confirmed. Thanks for verifying!
          </div>
          <RouterLink
            :to="auth.isAuthenticated ? { name: 'wall' } : { name: 'login' }"
            data-testid="verify-success-link"
            class="inline-block bg-indigo-600 hover:bg-indigo-700 text-white font-medium px-5 py-2 rounded-lg text-sm transition-colors"
          >
            {{ auth.isAuthenticated ? 'Go to your feed' : 'Sign in' }}
          </RouterLink>
        </div>

        <!-- ── Error ── -->
        <div
          v-else
          data-testid="verify-error"
          class="text-center"
        >
          <div class="mx-auto h-12 w-12 rounded-full bg-red-950 border border-red-800 flex items-center justify-center mb-4">
            <svg class="h-6 w-6 text-red-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" />
            </svg>
          </div>
          <h2 class="text-lg font-semibold text-slate-100 mb-2">Verification failed</h2>
          <div class="mb-5 rounded-lg bg-red-950 border border-red-800 px-4 py-3 text-sm text-red-400">
            {{ errorMessage }}
          </div>
          <p class="text-sm text-slate-400 mb-4">
            The link may have expired or already been used. You can request a new one from your account settings.
          </p>
          <RouterLink
            v-if="auth.isAuthenticated"
            :to="{ name: 'settings-account' }"
            data-testid="verify-error-resend"
            class="inline-block bg-indigo-600 hover:bg-indigo-700 text-white font-medium px-5 py-2 rounded-lg text-sm transition-colors"
          >
            Resend verification email
          </RouterLink>
          <RouterLink
            v-else
            :to="{ name: 'login' }"
            data-testid="verify-error-login"
            class="inline-block bg-indigo-600 hover:bg-indigo-700 text-white font-medium px-5 py-2 rounded-lg text-sm transition-colors"
          >
            Sign in to resend
          </RouterLink>
        </div>

      </div>

      <p class="text-center text-sm text-slate-400 mt-6">
        Already signed in?
        <RouterLink :to="{ name: 'wall' }" class="text-indigo-400 font-medium hover:underline">Go to your feed</RouterLink>
      </p>
    </div>
  </div>
</template>
