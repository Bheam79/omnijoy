<script setup lang="ts">
import { ref, computed } from 'vue'
import { RouterLink } from 'vue-router'
import { useAuthStore } from '@/stores/auth'
import { accountService } from '@/services/accountService'

const auth = useAuthStore()

// ── Change email form ─────────────────────────────────────────────────────────

const emailForm = ref({
  newEmail: auth.user?.email ?? '',
  currentPassword: '',
})
const emailSaving = ref(false)
const emailError = ref<string | null>(null)
const emailSuccess = ref<string | null>(null)

const emailValid = computed(() => {
  const e = emailForm.value.newEmail.trim()
  return e.length > 0 && e.includes('@') && e.includes('.')
})

async function saveEmail() {
  emailError.value = null
  emailSuccess.value = null

  if (!emailValid.value) {
    emailError.value = 'Please enter a valid email address.'
    return
  }
  if (!emailForm.value.currentPassword) {
    emailError.value = 'Please enter your current password.'
    return
  }

  emailSaving.value = true
  try {
    await accountService.changeEmail({
      newEmail: emailForm.value.newEmail.trim(),
      currentPassword: emailForm.value.currentPassword,
    })
    // Update local user state with the new email
    if (auth.user) {
      auth.setUser({ ...auth.user, email: emailForm.value.newEmail.trim().toLowerCase() })
    }
    emailForm.value.currentPassword = ''
    emailSuccess.value = 'Email updated successfully.'
  } catch (e: unknown) {
    const ax = e as { response?: { data?: { error?: string } } }
    emailError.value = ax.response?.data?.error ?? 'Failed to update email.'
  } finally {
    emailSaving.value = false
  }
}

// ── Change password form ──────────────────────────────────────────────────────

const pwForm = ref({
  currentPassword: '',
  newPassword: '',
  confirmNewPassword: '',
})
const pwSaving = ref(false)
const pwError = ref<string | null>(null)
const pwSuccess = ref<string | null>(null)

const pwLengthOk = computed(() => pwForm.value.newPassword.length >= 8)
const pwMatch = computed(
  () => pwForm.value.newPassword === pwForm.value.confirmNewPassword && pwForm.value.newPassword.length > 0,
)

async function savePassword() {
  pwError.value = null
  pwSuccess.value = null

  if (!pwForm.value.currentPassword) {
    pwError.value = 'Please enter your current password.'
    return
  }
  if (!pwLengthOk.value) {
    pwError.value = 'New password must be at least 8 characters.'
    return
  }
  if (!pwMatch.value) {
    pwError.value = 'New password and confirmation do not match.'
    return
  }

  pwSaving.value = true
  try {
    await accountService.changePassword({
      currentPassword: pwForm.value.currentPassword,
      newPassword: pwForm.value.newPassword,
      confirmNewPassword: pwForm.value.confirmNewPassword,
    })
    pwForm.value = { currentPassword: '', newPassword: '', confirmNewPassword: '' }
    pwSuccess.value = 'Password updated successfully.'
  } catch (e: unknown) {
    const ax = e as { response?: { data?: { error?: string } } }
    pwError.value = ax.response?.data?.error ?? 'Failed to update password.'
  } finally {
    pwSaving.value = false
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

    <h1 class="text-2xl font-bold text-gray-900 mb-2">Account</h1>
    <p class="text-sm text-gray-500 mb-6">Manage your email address and password.</p>

    <!-- ── Change email ──────────────────────────────────────────────────────── -->
    <section class="bg-white rounded-xl border border-gray-100 px-5 py-5 mb-5">
      <h2 class="text-base font-semibold text-gray-900 mb-1">Email address</h2>
      <p class="text-xs text-gray-500 mb-4">Change the email used to sign in.</p>

      <div v-if="emailError" class="mb-4 rounded-lg bg-red-50 border border-red-200 px-4 py-3 text-sm text-red-700">
        {{ emailError }}
      </div>
      <div
        v-if="emailSuccess"
        class="mb-4 rounded-lg bg-green-50 border border-green-200 px-4 py-3 text-sm text-green-700"
      >
        {{ emailSuccess }}
      </div>

      <form class="space-y-3" data-testid="email-form" @submit.prevent="saveEmail">
        <div>
          <label for="new-email" class="block text-sm font-medium text-gray-700 mb-1">New email</label>
          <input
            id="new-email"
            v-model="emailForm.newEmail"
            type="email"
            autocomplete="email"
            required
            class="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500 focus:border-transparent"
            :class="{ 'border-red-400': emailForm.newEmail && !emailValid }"
            placeholder="you@example.com"
          />
          <p v-if="emailForm.newEmail && !emailValid" class="mt-1 text-xs text-red-600">
            Enter a valid email address.
          </p>
        </div>

        <div>
          <label for="email-current-password" class="block text-sm font-medium text-gray-700 mb-1">
            Current password
          </label>
          <input
            id="email-current-password"
            v-model="emailForm.currentPassword"
            type="password"
            autocomplete="current-password"
            required
            class="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500 focus:border-transparent"
            placeholder="Enter your current password"
          />
        </div>

        <button
          type="submit"
          :disabled="emailSaving"
          class="bg-indigo-600 hover:bg-indigo-700 disabled:opacity-60 text-white font-medium px-5 py-2 rounded-lg text-sm transition-colors"
        >
          <span v-if="emailSaving">Saving…</span>
          <span v-else>Save email</span>
        </button>
      </form>
    </section>

    <!-- ── Change password ───────────────────────────────────────────────────── -->
    <section class="bg-white rounded-xl border border-gray-100 px-5 py-5">
      <h2 class="text-base font-semibold text-gray-900 mb-1">Password</h2>
      <p class="text-xs text-gray-500 mb-4">Choose a strong password with at least 8 characters.</p>

      <div v-if="pwError" class="mb-4 rounded-lg bg-red-50 border border-red-200 px-4 py-3 text-sm text-red-700">
        {{ pwError }}
      </div>
      <div
        v-if="pwSuccess"
        class="mb-4 rounded-lg bg-green-50 border border-green-200 px-4 py-3 text-sm text-green-700"
      >
        {{ pwSuccess }}
      </div>

      <form class="space-y-3" data-testid="password-form" @submit.prevent="savePassword">
        <div>
          <label for="pw-current" class="block text-sm font-medium text-gray-700 mb-1">Current password</label>
          <input
            id="pw-current"
            v-model="pwForm.currentPassword"
            type="password"
            autocomplete="current-password"
            required
            class="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500 focus:border-transparent"
            placeholder="Enter your current password"
          />
        </div>

        <div>
          <label for="pw-new" class="block text-sm font-medium text-gray-700 mb-1">New password</label>
          <input
            id="pw-new"
            v-model="pwForm.newPassword"
            type="password"
            autocomplete="new-password"
            required
            class="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500 focus:border-transparent"
            :class="{ 'border-red-400': pwForm.newPassword && !pwLengthOk }"
            placeholder="Minimum 8 characters"
          />
          <p v-if="pwForm.newPassword && !pwLengthOk" class="mt-1 text-xs text-red-600">
            Password must be at least 8 characters.
          </p>
        </div>

        <div>
          <label for="pw-confirm" class="block text-sm font-medium text-gray-700 mb-1">Confirm new password</label>
          <input
            id="pw-confirm"
            v-model="pwForm.confirmNewPassword"
            type="password"
            autocomplete="new-password"
            required
            class="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500 focus:border-transparent"
            :class="{
              'border-red-400': pwForm.confirmNewPassword && !pwMatch,
              'border-green-400': pwForm.confirmNewPassword && pwMatch,
            }"
            placeholder="Re-enter new password"
          />
          <p v-if="pwForm.confirmNewPassword && !pwMatch" class="mt-1 text-xs text-red-600">
            Passwords do not match.
          </p>
          <p v-if="pwForm.confirmNewPassword && pwMatch" class="mt-1 text-xs text-green-600">
            Passwords match.
          </p>
        </div>

        <button
          type="submit"
          :disabled="pwSaving"
          class="bg-indigo-600 hover:bg-indigo-700 disabled:opacity-60 text-white font-medium px-5 py-2 rounded-lg text-sm transition-colors"
        >
          <span v-if="pwSaving">Saving…</span>
          <span v-else>Save password</span>
        </button>
      </form>
    </section>
  </div>
</template>
