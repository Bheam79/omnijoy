import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import type { User } from '@/types'
import { authService, type RegisterPayload } from '@/services/authService'

function loadUserFromStorage(): User | null {
  try {
    const raw = localStorage.getItem('auth_user')
    return raw ? (JSON.parse(raw) as User) : null
  } catch {
    return null
  }
}

export const useAuthStore = defineStore('auth', () => {
  const user = ref<User | null>(loadUserFromStorage())
  const accessToken = ref<string | null>(localStorage.getItem('access_token'))
  const refreshToken = ref<string | null>(localStorage.getItem('refresh_token'))
  const loading = ref(false)
  const error = ref<string | null>(null)

  const isAuthenticated = computed(() => !!accessToken.value && !!user.value)

  /**
   * True when the current user has confirmed their email. Default to true
   * for guests and for legacy payloads that pre-date the flag, so the
   * verification banner only appears for accounts we *know* are unverified.
   */
  const isEmailVerified = computed(() => user.value?.isEmailVerified !== false)

  function setTokens(access: string, refresh: string) {
    accessToken.value = access
    refreshToken.value = refresh
    localStorage.setItem('access_token', access)
    localStorage.setItem('refresh_token', refresh)
  }

  function setUser(u: User) {
    user.value = u
    localStorage.setItem('auth_user', JSON.stringify(u))
  }

  function clearError() {
    error.value = null
  }

  async function register(payload: RegisterPayload) {
    loading.value = true
    error.value = null
    try {
      const result = await authService.register(payload)
      setTokens(result.accessToken, result.refreshToken)
      setUser(result.user)
    } catch (e: unknown) {
      error.value = extractError(e)
      throw e
    } finally {
      loading.value = false
    }
  }

  async function loginPassword(email: string, password: string) {
    loading.value = true
    error.value = null
    try {
      const result = await authService.loginPassword(email, password)
      setTokens(result.accessToken, result.refreshToken)
      setUser(result.user)
    } catch (e: unknown) {
      error.value = extractError(e)
      throw e
    } finally {
      loading.value = false
    }
  }

  async function requestOtp(email: string) {
    loading.value = true
    error.value = null
    try {
      await authService.requestOtp(email)
    } catch (e: unknown) {
      error.value = extractError(e)
      throw e
    } finally {
      loading.value = false
    }
  }

  async function verifyOtp(email: string, code: string) {
    loading.value = true
    error.value = null
    try {
      const result = await authService.verifyOtp(email, code)
      setTokens(result.accessToken, result.refreshToken)
      setUser(result.user)
    } catch (e: unknown) {
      error.value = extractError(e)
      throw e
    } finally {
      loading.value = false
    }
  }

  async function loginGoogle(idToken: string) {
    loading.value = true
    error.value = null
    try {
      const result = await authService.loginGoogle(idToken)
      setTokens(result.accessToken, result.refreshToken)
      setUser(result.user)
    } catch (e: unknown) {
      error.value = extractError(e)
      throw e
    } finally {
      loading.value = false
    }
  }

  async function loginFacebook(fbAccessToken: string) {
    loading.value = true
    error.value = null
    try {
      const result = await authService.loginFacebook(fbAccessToken)
      setTokens(result.accessToken, result.refreshToken)
      setUser(result.user)
    } catch (e: unknown) {
      error.value = extractError(e)
      throw e
    } finally {
      loading.value = false
    }
  }

  async function logout() {
    const rt = refreshToken.value
    user.value = null
    accessToken.value = null
    refreshToken.value = null
    localStorage.removeItem('access_token')
    localStorage.removeItem('refresh_token')
    localStorage.removeItem('auth_user')
    if (rt) await authService.logout(rt)
  }

  return {
    user,
    accessToken,
    refreshToken,
    loading,
    error,
    isAuthenticated,
    isEmailVerified,
    setTokens,
    setUser,
    clearError,
    register,
    loginPassword,
    requestOtp,
    verifyOtp,
    loginGoogle,
    loginFacebook,
    logout,
  }
})

// ── Helpers ───────────────────────────────────────────────────────────────────

function extractError(e: unknown): string {
  if (typeof e === 'object' && e !== null) {
    const axiosError = e as { response?: { data?: { error?: string } }; message?: string }
    return axiosError.response?.data?.error ?? axiosError.message ?? 'An unexpected error occurred.'
  }
  return 'An unexpected error occurred.'
}
