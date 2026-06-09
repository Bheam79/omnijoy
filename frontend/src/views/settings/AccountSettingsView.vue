<script setup lang="ts">
import { ref, computed } from 'vue'
import { RouterLink, useRouter } from 'vue-router'
import { useAuthStore } from '@/stores/auth'
import { accountService } from '@/services/accountService'

const auth = useAuthStore()
const router = useRouter()

// ── Email-verification banner ────────────────────────────────────────────────
//
// Only password users land here unverified — OTP and OAuth users are marked
// verified at sign-up. We check the explicit boolean (not the
// `isEmailVerified !== false` helper) so the banner stays hidden for legacy
// payloads that pre-date the rollout and don't carry the flag.
const showVerifyBanner = computed(() => auth.user?.isEmailVerified === false)

const resendSending = ref(false)
const resendError   = ref<string | null>(null)
const resendSuccess = ref<string | null>(null)

async function resendVerification() {
  resendError.value = null
  resendSuccess.value = null
  resendSending.value = true
  try {
    await accountService.resendEmailVerification()
    resendSuccess.value = 'Verification email sent. Check your inbox.'
  } catch (e: unknown) {
    const ax = e as { response?: { data?: { error?: string } } }
    resendError.value = ax.response?.data?.error ?? 'Failed to send verification email.'
  } finally {
    resendSending.value = false
  }
}

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

// ── Danger zone: deactivate ───────────────────────────────────────────────────

const showDeactivateModal = ref(false)
const deactivateSubmitting = ref(false)
const deactivateError = ref<string | null>(null)

function openDeactivateModal() {
  deactivateError.value = null
  showDeactivateModal.value = true
}

function closeDeactivateModal() {
  if (deactivateSubmitting.value) return
  showDeactivateModal.value = false
}

async function confirmDeactivate() {
  deactivateError.value = null
  deactivateSubmitting.value = true
  try {
    await accountService.deactivate()
    // Wipe tokens locally (server already revoked refresh tokens).
    await auth.logout()
    showDeactivateModal.value = false
    await router.push({ name: 'login' })
  } catch (e: unknown) {
    const ax = e as { response?: { data?: { error?: string } } }
    deactivateError.value = ax.response?.data?.error ?? 'Failed to deactivate account.'
  } finally {
    deactivateSubmitting.value = false
  }
}

// ── Danger zone: delete ───────────────────────────────────────────────────────

const showDeleteModal = ref(false)
const deleteConfirmEmail = ref('')
const deleteSubmitting = ref(false)
const deleteError = ref<string | null>(null)

const accountEmail = computed(() => (auth.user?.email ?? '').toLowerCase())
const deleteConfirmMatches = computed(
  () => deleteConfirmEmail.value.trim().toLowerCase() === accountEmail.value && accountEmail.value.length > 0,
)

function openDeleteModal() {
  deleteError.value = null
  deleteConfirmEmail.value = ''
  showDeleteModal.value = true
}

function closeDeleteModal() {
  if (deleteSubmitting.value) return
  showDeleteModal.value = false
}

async function confirmDelete() {
  deleteError.value = null
  if (!deleteConfirmMatches.value) {
    deleteError.value = 'Type your email address exactly to confirm.'
    return
  }
  deleteSubmitting.value = true
  try {
    await accountService.deleteAccount(deleteConfirmEmail.value.trim())
    await auth.logout()
    showDeleteModal.value = false
    await router.push({ name: 'login' })
  } catch (e: unknown) {
    const ax = e as { response?: { data?: { error?: string } } }
    deleteError.value = ax.response?.data?.error ?? 'Failed to delete account.'
  } finally {
    deleteSubmitting.value = false
  }
}
</script>

<template>
  <div>
    <!-- Back link -->
    <div class="flex items-center gap-2 mb-6">
      <RouterLink
        to="/settings"
        class="flex items-center gap-1.5 text-sm text-gray-500 hover:text-slate-300 transition-colors"
      >
        <svg class="h-4 w-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 19l-7-7 7-7" />
        </svg>
        Settings
      </RouterLink>
    </div>

    <h1 class="text-2xl font-bold text-slate-100 mb-2">Account</h1>
    <p class="text-sm text-gray-500 mb-6">Manage your email address and password.</p>

    <!-- ── Email-verification banner ─────────────────────────────────────────── -->
    <section
      v-if="showVerifyBanner"
      data-testid="email-verification-banner"
      class="mb-5 rounded-xl border border-amber-700 bg-amber-950/40 px-5 py-4"
    >
      <div class="flex flex-col sm:flex-row sm:items-start sm:justify-between gap-3">
        <div class="flex items-start gap-3">
          <svg
            class="h-5 w-5 text-amber-400 mt-0.5 flex-shrink-0"
            fill="none"
            stroke="currentColor"
            viewBox="0 0 24 24"
          >
            <path
              stroke-linecap="round"
              stroke-linejoin="round"
              stroke-width="2"
              d="M12 9v2m0 4h.01M5.07 19h13.86c1.54 0 2.5-1.67 1.73-3L13.73 4a2 2 0 00-3.46 0L3.34 16c-.77 1.33.19 3 1.73 3z"
            />
          </svg>
          <div>
            <p class="text-sm font-medium text-amber-200">
              Your email address is not yet verified.
            </p>
            <p class="text-xs text-amber-300/80 mt-0.5">
              Check your inbox for a verification link.
            </p>
          </div>
        </div>
        <button
          type="button"
          data-testid="resend-verification-btn"
          :disabled="resendSending"
          class="self-start sm:self-auto whitespace-nowrap px-4 py-2 text-sm rounded-lg border border-amber-500 text-amber-200 hover:bg-amber-900/40 disabled:opacity-60 transition-colors"
          @click="resendVerification"
        >
          <span v-if="resendSending">Sending…</span>
          <span v-else>Resend email</span>
        </button>
      </div>
      <p
        v-if="resendSuccess"
        data-testid="resend-verification-success"
        class="mt-3 rounded-lg bg-green-950 border border-green-800 px-3 py-2 text-sm text-green-400"
      >
        {{ resendSuccess }}
      </p>
      <p
        v-if="resendError"
        data-testid="resend-verification-error"
        class="mt-3 rounded-lg bg-red-950 border border-red-800 px-3 py-2 text-sm text-red-400"
      >
        {{ resendError }}
      </p>
    </section>

    <!-- ── Change email ──────────────────────────────────────────────────────── -->
    <section class="bg-slate-800 rounded-xl border border-slate-700 px-5 py-5 mb-5">
      <h2 class="text-base font-semibold text-slate-100 mb-1">Email address</h2>
      <p class="text-xs text-gray-500 mb-4">Change the email used to sign in.</p>

      <div v-if="emailError" class="mb-4 rounded-lg bg-red-950 border border-red-800 px-4 py-3 text-sm text-red-400">
        {{ emailError }}
      </div>
      <div
        v-if="emailSuccess"
        class="mb-4 rounded-lg bg-green-950 border border-green-800 px-4 py-3 text-sm text-green-400"
      >
        {{ emailSuccess }}
      </div>

      <form class="space-y-3" data-testid="email-form" @submit.prevent="saveEmail">
        <div>
          <label for="new-email" class="block text-sm font-medium text-slate-300 mb-1">New email</label>
          <input
            id="new-email"
            v-model="emailForm.newEmail"
            data-testid="new-email-input"
            type="email"
            autocomplete="email"
            required
            class="w-full rounded-lg border border-slate-600 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500 focus:border-transparent"
            :class="{ 'border-red-400': emailForm.newEmail && !emailValid }"
            placeholder="you@example.com"
          />
          <p v-if="emailForm.newEmail && !emailValid" class="mt-1 text-xs text-red-400">
            Enter a valid email address.
          </p>
        </div>

        <div>
          <label for="email-current-password" class="block text-sm font-medium text-slate-300 mb-1">
            Current password
          </label>
          <input
            id="email-current-password"
            v-model="emailForm.currentPassword"
            data-testid="email-current-password-input"
            type="password"
            autocomplete="current-password"
            required
            class="w-full rounded-lg border border-slate-600 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500 focus:border-transparent"
            placeholder="Enter your current password"
          />
        </div>

        <button
          type="submit"
          data-testid="save-email-button"
          :disabled="emailSaving"
          class="bg-indigo-600 hover:bg-indigo-700 disabled:opacity-60 text-white font-medium px-5 py-2 rounded-lg text-sm transition-colors"
        >
          <span v-if="emailSaving">Saving…</span>
          <span v-else>Save email</span>
        </button>
      </form>
    </section>

    <!-- ── Change password ───────────────────────────────────────────────────── -->
    <section class="bg-slate-800 rounded-xl border border-slate-700 px-5 py-5">
      <h2 class="text-base font-semibold text-slate-100 mb-1">Password</h2>
      <p class="text-xs text-gray-500 mb-4">Choose a strong password with at least 8 characters.</p>

      <div v-if="pwError" class="mb-4 rounded-lg bg-red-950 border border-red-800 px-4 py-3 text-sm text-red-400">
        {{ pwError }}
      </div>
      <div
        v-if="pwSuccess"
        class="mb-4 rounded-lg bg-green-950 border border-green-800 px-4 py-3 text-sm text-green-400"
      >
        {{ pwSuccess }}
      </div>

      <form class="space-y-3" data-testid="password-form" @submit.prevent="savePassword">
        <div>
          <label for="pw-current" class="block text-sm font-medium text-slate-300 mb-1">Current password</label>
          <input
            id="pw-current"
            v-model="pwForm.currentPassword"
            data-testid="current-password"
            type="password"
            autocomplete="current-password"
            required
            class="w-full rounded-lg border border-slate-600 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500 focus:border-transparent"
            placeholder="Enter your current password"
          />
        </div>

        <div>
          <label for="pw-new" class="block text-sm font-medium text-slate-300 mb-1">New password</label>
          <input
            id="pw-new"
            v-model="pwForm.newPassword"
            data-testid="new-password"
            type="password"
            autocomplete="new-password"
            required
            class="w-full rounded-lg border border-slate-600 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500 focus:border-transparent"
            :class="{ 'border-red-400': pwForm.newPassword && !pwLengthOk }"
            placeholder="Minimum 8 characters"
          />
          <p v-if="pwForm.newPassword && !pwLengthOk" class="mt-1 text-xs text-red-400">
            Password must be at least 8 characters.
          </p>
        </div>

        <div>
          <label for="pw-confirm" class="block text-sm font-medium text-slate-300 mb-1">Confirm new password</label>
          <input
            id="pw-confirm"
            v-model="pwForm.confirmNewPassword"
            data-testid="confirm-new-password"
            type="password"
            autocomplete="new-password"
            required
            class="w-full rounded-lg border border-slate-600 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500 focus:border-transparent"
            :class="{
              'border-red-400': pwForm.confirmNewPassword && !pwMatch,
              'border-green-400': pwForm.confirmNewPassword && pwMatch,
            }"
            placeholder="Re-enter new password"
          />
          <p v-if="pwForm.confirmNewPassword && !pwMatch" class="mt-1 text-xs text-red-400">
            Passwords do not match.
          </p>
          <p v-if="pwForm.confirmNewPassword && pwMatch" class="mt-1 text-xs text-green-400">
            Passwords match.
          </p>
        </div>

        <button
          type="submit"
          data-testid="save-password-button"
          :disabled="pwSaving"
          class="bg-indigo-600 hover:bg-indigo-700 disabled:opacity-60 text-white font-medium px-5 py-2 rounded-lg text-sm transition-colors"
        >
          <span v-if="pwSaving">Saving…</span>
          <span v-else>Save password</span>
        </button>
      </form>
    </section>

    <!-- ── Danger zone ────────────────────────────────────────────────────────── -->
    <section
      data-testid="danger-zone"
      class="mt-8 bg-slate-800 rounded-xl border border-red-800 px-5 py-5"
    >
      <h2 class="text-base font-semibold text-red-400 mb-1">Danger zone</h2>
      <p class="text-xs text-gray-500 mb-4">
        These actions affect your account permanently. Proceed with care.
      </p>

      <div class="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-3 py-3 border-t border-slate-700">
        <div>
          <p class="text-sm font-medium text-slate-100">Deactivate account</p>
          <p class="text-xs text-gray-500">
            Temporarily disables your account. Your profile and posts are hidden until you log in again.
          </p>
        </div>
        <button
          type="button"
          data-testid="deactivate-btn"
          class="self-start sm:self-auto px-4 py-2 text-sm rounded-lg border border-amber-300 text-amber-400 hover:bg-amber-950 transition-colors"
          @click="openDeactivateModal"
        >
          Deactivate account
        </button>
      </div>

      <div class="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-3 py-3 border-t border-slate-700">
        <div>
          <p class="text-sm font-medium text-slate-100">Delete account</p>
          <p class="text-xs text-gray-500">
            Permanently removes your data after a 30-day grace period. This cannot be undone.
          </p>
        </div>
        <button
          type="button"
          data-testid="delete-btn"
          class="self-start sm:self-auto px-4 py-2 text-sm rounded-lg bg-red-600 hover:bg-red-700 text-white transition-colors"
          @click="openDeleteModal"
        >
          Delete account
        </button>
      </div>
    </section>

    <!-- ── Deactivate confirmation modal ─────────────────────────────────────── -->
    <div
      v-if="showDeactivateModal"
      data-testid="deactivate-modal"
      class="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4"
      role="dialog"
      aria-modal="true"
      @click.self="closeDeactivateModal"
    >
      <div class="bg-slate-800 rounded-xl shadow-xl max-w-md w-full p-6">
        <h3 class="text-lg font-semibold text-slate-100">Deactivate your account?</h3>
        <p class="mt-2 text-sm text-slate-400">
          Your account will be hidden from search and feed, and active sessions will be signed out.
          You can re-activate it at any time by logging back in.
        </p>

        <div
          v-if="deactivateError"
          class="mt-4 rounded-lg bg-red-950 border border-red-800 px-3 py-2 text-sm text-red-400"
        >
          {{ deactivateError }}
        </div>

        <div class="mt-6 flex justify-end gap-2">
          <button
            type="button"
            data-testid="deactivate-cancel"
            :disabled="deactivateSubmitting"
            class="px-4 py-2 text-sm rounded-lg border border-slate-600 text-slate-300 hover:bg-slate-700 disabled:opacity-60"
            @click="closeDeactivateModal"
          >
            Cancel
          </button>
          <button
            type="button"
            data-testid="deactivate-confirm"
            :disabled="deactivateSubmitting"
            class="px-4 py-2 text-sm rounded-lg bg-amber-600 text-white hover:bg-amber-700 disabled:opacity-60"
            @click="confirmDeactivate"
          >
            <span v-if="deactivateSubmitting">Deactivating…</span>
            <span v-else>Deactivate</span>
          </button>
        </div>
      </div>
    </div>

    <!-- ── Delete confirmation modal ─────────────────────────────────────────── -->
    <div
      v-if="showDeleteModal"
      data-testid="delete-modal"
      class="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4"
      role="dialog"
      aria-modal="true"
      @click.self="closeDeleteModal"
    >
      <div class="bg-slate-800 rounded-xl shadow-xl max-w-md w-full p-6">
        <h3 class="text-lg font-semibold text-red-400">Permanently delete your account?</h3>
        <p class="mt-2 text-sm text-slate-400">
          This is irreversible. Your posts, messages, and profile will be removed after the 30-day grace period.
          To confirm, type your email address
          <span class="font-medium text-slate-100">{{ accountEmail }}</span>
          below.
        </p>

        <label for="delete-confirm-email" class="block text-sm font-medium text-slate-300 mt-4 mb-1">
          Type your email to confirm
        </label>
        <input
          id="delete-confirm-email"
          v-model="deleteConfirmEmail"
          type="email"
          autocomplete="off"
          data-testid="delete-confirm-email"
          class="w-full rounded-lg border border-slate-600 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-red-500 focus:border-transparent"
          :class="{
            'border-red-400': deleteConfirmEmail && !deleteConfirmMatches,
            'border-green-400': deleteConfirmEmail && deleteConfirmMatches,
          }"
        />

        <div
          v-if="deleteError"
          class="mt-4 rounded-lg bg-red-950 border border-red-800 px-3 py-2 text-sm text-red-400"
        >
          {{ deleteError }}
        </div>

        <div class="mt-6 flex justify-end gap-2">
          <button
            type="button"
            data-testid="delete-cancel"
            :disabled="deleteSubmitting"
            class="px-4 py-2 text-sm rounded-lg border border-slate-600 text-slate-300 hover:bg-slate-700 disabled:opacity-60"
            @click="closeDeleteModal"
          >
            Cancel
          </button>
          <button
            type="button"
            data-testid="delete-confirm"
            :disabled="deleteSubmitting || !deleteConfirmMatches"
            class="px-4 py-2 text-sm rounded-lg bg-red-600 text-white hover:bg-red-700 disabled:opacity-60"
            @click="confirmDelete"
          >
            <span v-if="deleteSubmitting">Deleting…</span>
            <span v-else>Delete account</span>
          </button>
        </div>
      </div>
    </div>
  </div>
</template>
