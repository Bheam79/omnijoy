<script setup lang="ts">
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import { useAuthStore } from '@/stores/auth'

const router = useRouter()
const auth = useAuthStore()

type AuthMethod = 'password' | 'otp'

const email = ref('')
const displayName = ref('')
const authMethod = ref<AuthMethod>('password')
const password = ref('')
const confirmPassword = ref('')
const gender = ref('NotDisclosed')
const birthDate = ref('')
const showBirthDate = ref(false)
const localError = ref<string | null>(null)

function clearError() {
  localError.value = null
  auth.clearError()
}

async function submit() {
  clearError()

  if (!email.value.trim()) { localError.value = 'Email is required.'; return }
  if (!displayName.value.trim()) { localError.value = 'Display name is required.'; return }
  if (authMethod.value === 'password') {
    if (!password.value) { localError.value = 'Password is required.'; return }
    if (password.value.length < 8) { localError.value = 'Password must be at least 8 characters.'; return }
    if (password.value !== confirmPassword.value) { localError.value = 'Passwords do not match.'; return }
  }

  try {
    await auth.register({
      email: email.value.trim(),
      displayName: displayName.value.trim(),
      authMethod: authMethod.value,
      password: authMethod.value === 'password' ? password.value : undefined,
      gender: gender.value,
      birthDate: birthDate.value || undefined,
      showBirthDate: showBirthDate.value,
    })
    // Redirect to wall — for OTP users they are logged in directly after register
    router.push('/wall')
  } catch {
    localError.value = auth.error ?? 'Registration failed.'
  }
}
</script>

<template>
  <div class="min-h-screen bg-gray-50 flex flex-col justify-center items-center px-4 py-12">
    <div class="w-full max-w-md">
      <!-- Logo / Heading -->
      <div class="text-center mb-8">
        <h1 class="text-4xl font-bold text-indigo-600 tracking-tight">Omnijoy</h1>
        <p class="text-gray-500 mt-2">Create your account</p>
      </div>

      <div class="bg-white rounded-2xl shadow-sm border border-gray-200 p-8">
        <!-- Error banner -->
        <div v-if="localError || auth.error" class="mb-4 rounded-lg bg-red-50 border border-red-200 px-4 py-3 text-sm text-red-700">
          {{ localError || auth.error }}
        </div>

        <form @submit.prevent="submit" novalidate class="space-y-4">
          <!-- Email -->
          <div>
            <label class="block text-sm font-medium text-gray-700 mb-1">Email <span class="text-red-500">*</span></label>
            <input
              v-model="email"
              type="email"
              autocomplete="email"
              placeholder="you@example.com"
              class="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500 focus:border-transparent"
            />
          </div>

          <!-- Display name -->
          <div>
            <label class="block text-sm font-medium text-gray-700 mb-1">Display name <span class="text-red-500">*</span></label>
            <input
              v-model="displayName"
              type="text"
              autocomplete="name"
              placeholder="Your name"
              class="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500 focus:border-transparent"
            />
          </div>

          <!-- Auth method -->
          <div>
            <label class="block text-sm font-medium text-gray-700 mb-2">Sign-in method <span class="text-red-500">*</span></label>
            <div class="grid grid-cols-2 gap-2">
              <button
                type="button"
                class="flex items-center justify-center gap-2 rounded-lg border py-2.5 text-sm font-medium transition-all"
                :class="authMethod === 'password'
                  ? 'border-indigo-600 bg-indigo-50 text-indigo-700'
                  : 'border-gray-300 text-gray-600 hover:border-gray-400'"
                @click="authMethod = 'password'; clearError()"
              >
                <svg class="h-4 w-4" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 15v2m-6 4h12a2 2 0 002-2v-6a2 2 0 00-2-2H6a2 2 0 00-2 2v6a2 2 0 002 2zm10-10V7a4 4 0 00-8 0v4h8z"/></svg>
                Password
              </button>
              <button
                type="button"
                class="flex items-center justify-center gap-2 rounded-lg border py-2.5 text-sm font-medium transition-all"
                :class="authMethod === 'otp'
                  ? 'border-indigo-600 bg-indigo-50 text-indigo-700'
                  : 'border-gray-300 text-gray-600 hover:border-gray-400'"
                @click="authMethod = 'otp'; clearError()"
              >
                <svg class="h-4 w-4" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M3 8l7.89 5.26a2 2 0 002.22 0L21 8M5 19h14a2 2 0 002-2V7a2 2 0 00-2-2H5a2 2 0 00-2 2v10a2 2 0 002 2z"/></svg>
                Email code
              </button>
            </div>
            <p class="mt-1.5 text-xs text-gray-500">
              <span v-if="authMethod === 'password'">Log in with your email and password.</span>
              <span v-else>Receive a one-time code by email each time you log in — no password needed.</span>
            </p>
          </div>

          <!-- Password fields (only for password method) -->
          <template v-if="authMethod === 'password'">
            <div>
              <label class="block text-sm font-medium text-gray-700 mb-1">Password <span class="text-red-500">*</span></label>
              <input
                v-model="password"
                type="password"
                autocomplete="new-password"
                placeholder="Min. 8 characters"
                class="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500 focus:border-transparent"
              />
            </div>
            <div>
              <label class="block text-sm font-medium text-gray-700 mb-1">Confirm password <span class="text-red-500">*</span></label>
              <input
                v-model="confirmPassword"
                type="password"
                autocomplete="new-password"
                placeholder="Repeat password"
                class="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500 focus:border-transparent"
              />
            </div>
          </template>

          <!-- Optional profile fields -->
          <details class="group">
            <summary class="cursor-pointer text-sm text-gray-500 hover:text-gray-700 select-none list-none flex items-center gap-1">
              <svg class="h-4 w-4 transition-transform group-open:rotate-90" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 5l7 7-7 7"/></svg>
              Optional profile info
            </summary>
            <div class="mt-3 space-y-3">
              <!-- Gender -->
              <div>
                <label class="block text-sm font-medium text-gray-700 mb-1">Gender</label>
                <select
                  v-model="gender"
                  class="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500 focus:border-transparent"
                >
                  <option value="NotDisclosed">Prefer not to say</option>
                  <option value="Male">Male</option>
                  <option value="Female">Female</option>
                </select>
              </div>

              <!-- Birth date -->
              <div>
                <label class="block text-sm font-medium text-gray-700 mb-1">Date of birth</label>
                <input
                  v-model="birthDate"
                  type="date"
                  class="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500 focus:border-transparent"
                />
              </div>

              <!-- Show birth date -->
              <div v-if="birthDate" class="flex items-center gap-2">
                <input
                  v-model="showBirthDate"
                  id="show-birth-date"
                  type="checkbox"
                  class="h-4 w-4 rounded border-gray-300 text-indigo-600"
                />
                <label for="show-birth-date" class="text-sm text-gray-600">Show birth date on my profile</label>
              </div>
            </div>
          </details>

          <!-- Submit -->
          <button
            type="submit"
            :disabled="auth.loading"
            class="w-full bg-indigo-600 hover:bg-indigo-700 disabled:opacity-60 text-white font-medium py-2.5 rounded-lg text-sm transition-colors"
          >
            <span v-if="auth.loading">Creating account…</span>
            <span v-else>Create account</span>
          </button>
        </form>
      </div>

      <!-- Login link -->
      <p class="text-center text-sm text-gray-600 mt-6">
        Already have an account?
        <RouterLink to="/login" class="text-indigo-600 font-medium hover:underline">Sign in</RouterLink>
      </p>
    </div>
  </div>
</template>
