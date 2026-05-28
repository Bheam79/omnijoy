<script setup lang="ts">
import { ref, onMounted, onUnmounted } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import { useAuthStore } from '@/stores/auth'

const router = useRouter()
const route = useRoute()
const auth = useAuthStore()

// ── Tab state ─────────────────────────────────────────────────────────────────
type LoginTab = 'password' | 'otp'
const activeTab = ref<LoginTab>('password')

// ── Password form ─────────────────────────────────────────────────────────────
const pwEmail = ref('')
const pwPassword = ref('')

// ── OTP form ──────────────────────────────────────────────────────────────────
const otpEmail = ref('')
const otpCode = ref('')
const otpSent = ref(false)
const otpCooldown = ref(0)
let cooldownTimer: ReturnType<typeof setInterval> | null = null

// ── Social login ──────────────────────────────────────────────────────────────
const googleClientId = import.meta.env.VITE_GOOGLE_CLIENT_ID as string | undefined
const facebookAppId = import.meta.env.VITE_FACEBOOK_APP_ID as string | undefined

// ── Shared state ──────────────────────────────────────────────────────────────
const localError = ref<string | null>(null)
const successMsg = ref<string | null>(null)

function clearMessages() {
  localError.value = null
  successMsg.value = null
  auth.clearError()
}

function redirectAfterLogin() {
  const redirect = route.query.redirect as string | undefined
  router.push(redirect ?? '/wall')
}

// ── Password login ────────────────────────────────────────────────────────────
async function submitPassword() {
  clearMessages()
  if (!pwEmail.value || !pwPassword.value) {
    localError.value = 'Please enter your email and password.'
    return
  }
  try {
    await auth.loginPassword(pwEmail.value.trim(), pwPassword.value)
    redirectAfterLogin()
  } catch {
    localError.value = auth.error ?? 'Login failed.'
  }
}

// ── OTP request ───────────────────────────────────────────────────────────────
async function submitOtpRequest() {
  clearMessages()
  if (!otpEmail.value) {
    localError.value = 'Please enter your email.'
    return
  }
  try {
    await auth.requestOtp(otpEmail.value.trim())
    otpSent.value = true
    successMsg.value = 'Check your email for a 6-digit code.'
    startCooldown(60)
  } catch {
    localError.value = auth.error ?? 'Could not send OTP.'
  }
}

// ── OTP verify ────────────────────────────────────────────────────────────────
async function submitOtpVerify() {
  clearMessages()
  if (!otpCode.value || otpCode.value.length !== 6) {
    localError.value = 'Please enter the 6-digit code.'
    return
  }
  try {
    await auth.verifyOtp(otpEmail.value.trim(), otpCode.value.trim())
    redirectAfterLogin()
  } catch {
    localError.value = auth.error ?? 'Invalid or expired code.'
  }
}

function startCooldown(seconds: number) {
  otpCooldown.value = seconds
  if (cooldownTimer) clearInterval(cooldownTimer)
  cooldownTimer = setInterval(() => {
    if (otpCooldown.value > 0) {
      otpCooldown.value--
    } else {
      clearInterval(cooldownTimer!)
      cooldownTimer = null
    }
  }, 1000)
}

onUnmounted(() => { if (cooldownTimer) clearInterval(cooldownTimer) })

// ── Google sign-in ────────────────────────────────────────────────────────────
// Typed accessors for dynamically-loaded SDK globals
function getGoogleSDK() {
  return (window as unknown as Record<string, unknown>).google as {
    accounts: { id: { initialize: (o: object) => void; renderButton: (el: Element, o: object) => void } }
  } | undefined
}

function getFacebookSDK() {
  return (window as unknown as Record<string, unknown>).FB as {
    init: (o: object) => void
    login: (cb: (r: { authResponse?: { accessToken: string }; status: string }) => void, o: object) => void
  } | undefined
}

function setWindowProp(key: string, value: unknown) {
  (window as unknown as Record<string, unknown>)[key] = value
}

function initGoogle() {
  if (!googleClientId) return
  const google = getGoogleSDK()
  if (!google) return
  google.accounts.id.initialize({
    client_id: googleClientId,
    callback: async (response: { credential: string }) => {
      clearMessages()
      try {
        await auth.loginGoogle(response.credential)
        redirectAfterLogin()
      } catch {
        localError.value = auth.error ?? 'Google login failed.'
      }
    },
  })
  const btn = document.getElementById('google-btn')
  if (btn) {
    google.accounts.id.renderButton(btn, { theme: 'outline', size: 'large', width: 380 })
  }
}

// ── Facebook sign-in ──────────────────────────────────────────────────────────
async function loginWithFacebook() {
  clearMessages()
  const FB = getFacebookSDK()
  if (!FB || !facebookAppId) {
    localError.value = 'Facebook login is not configured.'
    return
  }
  FB.login(async (response) => {
    if (response.authResponse?.accessToken) {
      try {
        await auth.loginFacebook(response.authResponse.accessToken)
        redirectAfterLogin()
      } catch {
        localError.value = auth.error ?? 'Facebook login failed.'
      }
    } else {
      localError.value = 'Facebook login cancelled.'
    }
  }, { scope: 'email,public_profile' })
}

// ── Load SDKs on mount ────────────────────────────────────────────────────────
onMounted(() => {
  if (googleClientId) {
    const script = document.createElement('script')
    script.src = 'https://accounts.google.com/gsi/client'
    script.async = true
    script.defer = true
    script.onload = initGoogle
    document.head.appendChild(script)
  }

  if (facebookAppId) {
    setWindowProp('fbAsyncInit', function () {
      const FB = getFacebookSDK()
      if (FB) FB.init({ appId: facebookAppId, cookie: true, xfbml: true, version: 'v18.0' })
    })
    if (!getFacebookSDK()) {
      const script = document.createElement('script')
      script.src = 'https://connect.facebook.net/en_US/sdk.js'
      script.async = true
      script.defer = true
      document.head.appendChild(script)
    }
  }
})
</script>

<template>
  <div class="min-h-screen bg-gray-50 flex flex-col justify-center items-center px-4 py-12">
    <div class="w-full max-w-md">
      <!-- Logo / Heading -->
      <div class="text-center mb-8">
        <h1 class="text-4xl font-bold text-indigo-600 tracking-tight">Omnijoy</h1>
        <p class="text-gray-500 mt-2">Sign in to your account</p>
      </div>

      <div class="bg-white rounded-2xl shadow-sm border border-gray-200 p-8">
        <!-- Tab switcher -->
        <div class="flex rounded-lg bg-gray-100 p-1 mb-6">
          <button
            class="flex-1 py-2 text-sm font-medium rounded-md transition-all"
            :class="activeTab === 'password'
              ? 'bg-white shadow text-gray-900'
              : 'text-gray-500 hover:text-gray-700'"
            @click="activeTab = 'password'; clearMessages()"
          >
            Password
          </button>
          <button
            class="flex-1 py-2 text-sm font-medium rounded-md transition-all"
            :class="activeTab === 'otp'
              ? 'bg-white shadow text-gray-900'
              : 'text-gray-500 hover:text-gray-700'"
            @click="activeTab = 'otp'; clearMessages()"
          >
            One-time code
          </button>
        </div>

        <!-- Error / success banners -->
        <div v-if="localError || auth.error" class="mb-4 rounded-lg bg-red-50 border border-red-200 px-4 py-3 text-sm text-red-700">
          {{ localError || auth.error }}
        </div>
        <div v-if="successMsg" class="mb-4 rounded-lg bg-green-50 border border-green-200 px-4 py-3 text-sm text-green-700">
          {{ successMsg }}
        </div>

        <!-- ── Password tab ── -->
        <form v-if="activeTab === 'password'" @submit.prevent="submitPassword" novalidate>
          <div class="space-y-4">
            <div>
              <label class="block text-sm font-medium text-gray-700 mb-1">Email</label>
              <input
                v-model="pwEmail"
                type="email"
                autocomplete="email"
                placeholder="you@example.com"
                class="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500 focus:border-transparent"
              />
            </div>
            <div>
              <label class="block text-sm font-medium text-gray-700 mb-1">Password</label>
              <input
                v-model="pwPassword"
                type="password"
                autocomplete="current-password"
                placeholder="••••••••"
                class="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500 focus:border-transparent"
              />
            </div>
            <button
              type="submit"
              :disabled="auth.loading"
              class="w-full bg-indigo-600 hover:bg-indigo-700 disabled:opacity-60 text-white font-medium py-2.5 rounded-lg text-sm transition-colors"
            >
              <span v-if="auth.loading">Signing in…</span>
              <span v-else>Sign in</span>
            </button>
          </div>
        </form>

        <!-- ── OTP tab ── -->
        <div v-else class="space-y-4">
          <!-- Step 1: enter email -->
          <div v-if="!otpSent">
            <div>
              <label class="block text-sm font-medium text-gray-700 mb-1">Email</label>
              <input
                v-model="otpEmail"
                type="email"
                autocomplete="email"
                placeholder="you@example.com"
                class="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500 focus:border-transparent"
              />
            </div>
            <button
              :disabled="auth.loading"
              class="w-full mt-4 bg-indigo-600 hover:bg-indigo-700 disabled:opacity-60 text-white font-medium py-2.5 rounded-lg text-sm transition-colors"
              @click="submitOtpRequest"
            >
              <span v-if="auth.loading">Sending…</span>
              <span v-else>Send code</span>
            </button>
          </div>

          <!-- Step 2: enter code -->
          <div v-else>
            <p class="text-sm text-gray-600 mb-3">
              Enter the 6-digit code sent to <strong>{{ otpEmail }}</strong>
            </p>
            <div>
              <label class="block text-sm font-medium text-gray-700 mb-1">Code</label>
              <input
                v-model="otpCode"
                type="text"
                inputmode="numeric"
                maxlength="6"
                placeholder="123456"
                class="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm tracking-widest font-mono focus:outline-none focus:ring-2 focus:ring-indigo-500 focus:border-transparent"
              />
            </div>
            <button
              :disabled="auth.loading"
              class="w-full mt-4 bg-indigo-600 hover:bg-indigo-700 disabled:opacity-60 text-white font-medium py-2.5 rounded-lg text-sm transition-colors"
              @click="submitOtpVerify"
            >
              <span v-if="auth.loading">Verifying…</span>
              <span v-else>Verify &amp; sign in</span>
            </button>
            <button
              class="w-full mt-2 text-sm text-indigo-600 hover:underline disabled:opacity-50 disabled:no-underline"
              :disabled="otpCooldown > 0 || auth.loading"
              @click="submitOtpRequest"
            >
              <span v-if="otpCooldown > 0">Resend code in {{ otpCooldown }}s</span>
              <span v-else>Resend code</span>
            </button>
          </div>
        </div>

        <!-- ── Social divider ── -->
        <div class="relative my-6">
          <div class="absolute inset-0 flex items-center">
            <div class="w-full border-t border-gray-200" />
          </div>
          <div class="relative flex justify-center text-sm">
            <span class="bg-white px-3 text-gray-500">or continue with</span>
          </div>
        </div>

        <!-- ── Social buttons ── -->
        <div class="space-y-3">
          <!-- Google button (rendered by GSI SDK) -->
          <div v-if="googleClientId" id="google-btn" class="flex justify-center" />
          <button
            v-if="!googleClientId"
            disabled
            class="w-full flex items-center justify-center gap-2 border border-gray-300 rounded-lg py-2.5 text-sm text-gray-400 cursor-not-allowed"
          >
            <svg class="h-5 w-5" viewBox="0 0 24 24"><path fill="#4285F4" d="M22.56 12.25c0-.78-.07-1.53-.2-2.25H12v4.26h5.92c-.26 1.37-1.04 2.53-2.21 3.31v2.77h3.57c2.08-1.92 3.28-4.74 3.28-8.09z"/><path fill="#34A853" d="M12 23c2.97 0 5.46-.98 7.28-2.66l-3.57-2.77c-.98.66-2.23 1.06-3.71 1.06-2.86 0-5.29-1.93-6.16-4.53H2.18v2.84C3.99 20.53 7.7 23 12 23z"/><path fill="#FBBC05" d="M5.84 14.09c-.22-.66-.35-1.36-.35-2.09s.13-1.43.35-2.09V7.07H2.18C1.43 8.55 1 10.22 1 12s.43 3.45 1.18 4.93l3.66-2.84z"/><path fill="#EA4335" d="M12 5.38c1.62 0 3.06.56 4.21 1.64l3.15-3.15C17.45 2.09 14.97 1 12 1 7.7 1 3.99 3.47 2.18 7.07l3.66 2.84c.87-2.6 3.3-4.53 6.16-4.53z"/></svg>
            Google (not configured)
          </button>

          <!-- Facebook button -->
          <button
            v-if="facebookAppId"
            :disabled="auth.loading"
            class="w-full flex items-center justify-center gap-2 bg-[#1877F2] hover:bg-[#166FE5] disabled:opacity-60 text-white font-medium py-2.5 rounded-lg text-sm transition-colors"
            @click="loginWithFacebook"
          >
            <svg class="h-5 w-5" fill="currentColor" viewBox="0 0 24 24"><path d="M24 12.073c0-6.627-5.373-12-12-12s-12 5.373-12 12c0 5.99 4.388 10.954 10.125 11.854v-8.385H7.078v-3.47h3.047V9.43c0-3.007 1.792-4.669 4.533-4.669 1.312 0 2.686.235 2.686.235v2.953H15.83c-1.491 0-1.956.925-1.956 1.874v2.25h3.328l-.532 3.47h-2.796v8.385C19.612 23.027 24 18.062 24 12.073z"/></svg>
            Continue with Facebook
          </button>
          <button
            v-if="!facebookAppId"
            disabled
            class="w-full flex items-center justify-center gap-2 border border-gray-300 rounded-lg py-2.5 text-sm text-gray-400 cursor-not-allowed"
          >
            <svg class="h-5 w-5" fill="#1877F2" viewBox="0 0 24 24"><path d="M24 12.073c0-6.627-5.373-12-12-12s-12 5.373-12 12c0 5.99 4.388 10.954 10.125 11.854v-8.385H7.078v-3.47h3.047V9.43c0-3.007 1.792-4.669 4.533-4.669 1.312 0 2.686.235 2.686.235v2.953H15.83c-1.491 0-1.956.925-1.956 1.874v2.25h3.328l-.532 3.47h-2.796v8.385C19.612 23.027 24 18.062 24 12.073z"/></svg>
            Facebook (not configured)
          </button>
        </div>
      </div>

      <!-- Register link -->
      <p class="text-center text-sm text-gray-600 mt-6">
        Don't have an account?
        <RouterLink to="/register" class="text-indigo-600 font-medium hover:underline">Create one</RouterLink>
      </p>
    </div>
  </div>
</template>
