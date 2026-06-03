<script setup lang="ts">
import { ref, onUnmounted } from 'vue'
import { useRouter } from 'vue-router'
import { authService } from '@/services/authService'

const router = useRouter()

// ── State ─────────────────────────────────────────────────────────────────────
const step = ref<1 | 2>(1)

const email       = ref('')
const code        = ref('')
const newPassword = ref('')
const confirmPw   = ref('')

const error        = ref<string | null>(null)
const successMsg   = ref<string | null>(null)
const loading      = ref(false)

// Resend cooldown (seconds remaining)
const resendCooldown = ref(0)
let cooldownTimer: ReturnType<typeof setInterval> | null = null

// ── Step 1: request reset code ────────────────────────────────────────────────
async function submitRequest() {
  error.value   = null
  successMsg.value = null
  if (!email.value.trim()) {
    error.value = 'Please enter your email.'
    return
  }
  loading.value = true
  try {
    await authService.requestPasswordReset(email.value.trim())
    step.value = 2
  } catch {
    // Only true network / 5xx errors land here; unknown-email returns 200.
    error.value = 'Something went wrong. Please try again.'
  } finally {
    loading.value = false
  }
}

// ── Step 2: confirm reset ─────────────────────────────────────────────────────
async function submitConfirm() {
  error.value   = null
  successMsg.value = null

  if (newPassword.value.length < 8) {
    error.value = 'Password must be at least 8 characters.'
    return
  }
  if (newPassword.value !== confirmPw.value) {
    error.value = 'Passwords do not match.'
    return
  }

  loading.value = true
  try {
    await authService.confirmPasswordReset(
      email.value.trim(),
      code.value.trim(),
      newPassword.value,
    )
    successMsg.value = 'Password updated. Redirecting to login…'
    setTimeout(() => {
      router.push({ name: 'login', query: { reset: '1' } })
    }, 1500)
  } catch (e: unknown) {
    const ax = e as { response?: { data?: { error?: string } } }
    error.value = ax?.response?.data?.error ?? 'Invalid or expired code. Please try again.'
  } finally {
    loading.value = false
  }
}

// ── Resend code ───────────────────────────────────────────────────────────────
async function resendCode() {
  // Disable the button immediately — polite client-side rate limiting.
  startCooldown(30)
  error.value = null
  try {
    await authService.requestPasswordReset(email.value.trim())
  } catch {
    // Silently swallow — same as the forgot endpoint itself.
  }
}

function startCooldown(seconds: number) {
  resendCooldown.value = seconds
  if (cooldownTimer) clearInterval(cooldownTimer)
  cooldownTimer = setInterval(() => {
    if (resendCooldown.value > 0) {
      resendCooldown.value--
    } else {
      clearInterval(cooldownTimer!)
      cooldownTimer = null
    }
  }, 1000)
}

onUnmounted(() => { if (cooldownTimer) clearInterval(cooldownTimer) })
</script>

<template>
  <div class="min-h-screen bg-slate-950 flex flex-col justify-center items-center px-4 py-12">
    <div class="w-full max-w-md">
      <!-- Logo / Heading -->
      <div class="text-center mb-8">
        <img src="/logo.png" alt="Omnijoy" class="h-20 w-20 mx-auto mb-3 rounded-2xl" />
        <h1 class="text-4xl font-bold text-slate-100 tracking-tight">Omnijoy</h1>
        <p class="text-gray-500 mt-2">Reset your password</p>
      </div>

      <div class="bg-slate-800 rounded-2xl shadow-sm border border-slate-700 p-8">

        <!-- Error banner -->
        <div
          v-if="error"
          data-testid="error-banner"
          class="mb-4 rounded-lg bg-red-950 border border-red-800 px-4 py-3 text-sm text-red-400"
        >
          {{ error }}
        </div>

        <!-- Success banner -->
        <div
          v-if="successMsg"
          data-testid="success-message"
          class="mb-4 rounded-lg bg-green-950 border border-green-800 px-4 py-3 text-sm text-green-400"
        >
          {{ successMsg }}
        </div>

        <!-- ── Step 1: request code ── -->
        <div v-if="step === 1">
          <p class="text-sm text-slate-400 mb-5">
            Enter your email address and we'll send you a 6-digit code to reset your password.
          </p>
          <div class="space-y-4">
            <div>
              <label class="block text-sm font-medium text-slate-300 mb-1">Email</label>
              <input
                v-model="email"
                data-testid="step1-email"
                type="email"
                autocomplete="email"
                placeholder="you@example.com"
                class="w-full rounded-lg border border-slate-600 bg-slate-900 text-slate-100 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500 focus:border-transparent"
              />
            </div>
            <button
              data-testid="step1-submit"
              type="button"
              :disabled="loading"
              class="w-full bg-indigo-600 hover:bg-indigo-700 disabled:opacity-60 text-white font-medium py-2.5 rounded-lg text-sm transition-colors"
              @click="submitRequest"
            >
              <span v-if="loading">Sending…</span>
              <span v-else>Send reset code</span>
            </button>
          </div>
        </div>

        <!-- ── Step 2: enter code + new password ── -->
        <div v-else-if="step === 2 && !successMsg">
          <p class="text-sm text-slate-400 mb-5">
            If your email is registered, you'll receive a 6-digit code shortly. Enter it below.
          </p>
          <div class="space-y-4">
            <!-- Locked email -->
            <div>
              <label class="block text-sm font-medium text-slate-300 mb-1">Email</label>
              <input
                :value="email"
                data-testid="step2-email"
                type="email"
                readonly
                class="w-full rounded-lg border border-slate-600 bg-slate-900/50 text-slate-400 px-3 py-2 text-sm cursor-not-allowed"
              />
            </div>
            <!-- Code -->
            <div>
              <label class="block text-sm font-medium text-slate-300 mb-1">Reset code</label>
              <input
                v-model="code"
                data-testid="step2-code"
                type="text"
                inputmode="numeric"
                maxlength="6"
                placeholder="123456"
                class="w-full rounded-lg border border-slate-600 bg-slate-900 text-slate-100 px-3 py-2 text-sm tracking-widest font-mono focus:outline-none focus:ring-2 focus:ring-indigo-500 focus:border-transparent"
              />
            </div>
            <!-- New password -->
            <div>
              <label class="block text-sm font-medium text-slate-300 mb-1">New password</label>
              <input
                v-model="newPassword"
                data-testid="step2-new-password"
                type="password"
                autocomplete="new-password"
                placeholder="Min. 8 characters"
                class="w-full rounded-lg border border-slate-600 bg-slate-900 text-slate-100 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500 focus:border-transparent"
              />
            </div>
            <!-- Confirm password -->
            <div>
              <label class="block text-sm font-medium text-slate-300 mb-1">Confirm password</label>
              <input
                v-model="confirmPw"
                data-testid="step2-confirm-password"
                type="password"
                autocomplete="new-password"
                placeholder="Repeat password"
                class="w-full rounded-lg border border-slate-600 bg-slate-900 text-slate-100 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500 focus:border-transparent"
              />
            </div>
            <button
              data-testid="step2-submit"
              type="button"
              :disabled="!newPassword || !confirmPw || newPassword !== confirmPw || loading"
              class="w-full bg-indigo-600 hover:bg-indigo-700 disabled:opacity-60 text-white font-medium py-2.5 rounded-lg text-sm transition-colors"
              @click="submitConfirm"
            >
              <span v-if="loading">Resetting…</span>
              <span v-else>Reset password</span>
            </button>
            <!-- Resend -->
            <button
              data-testid="resend-button"
              type="button"
              :disabled="resendCooldown > 0 || loading"
              class="w-full text-sm text-indigo-400 hover:underline disabled:opacity-50 disabled:no-underline"
              @click="resendCode"
            >
              <span v-if="resendCooldown > 0">Didn't get a code? Resend in {{ resendCooldown }}s</span>
              <span v-else>Didn't get a code? Resend</span>
            </button>
          </div>
        </div>

      </div>

      <!-- Back to login -->
      <p class="text-center text-sm text-slate-400 mt-6">
        Remember your password?
        <RouterLink :to="{ name: 'login' }" class="text-indigo-400 font-medium hover:underline">Sign in</RouterLink>
      </p>
    </div>
  </div>
</template>
