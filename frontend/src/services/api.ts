/**
 * Axios instance with JWT Bearer headers and transparent token refresh.
 *
 * On every 401 response the interceptor tries to call POST /api/auth/refresh
 * using the stored refresh token.  If that succeeds the original request is
 * retried with the new access token.  If refresh fails the auth store is
 * cleared and the user is sent to /login.
 */
import axios, { type InternalAxiosRequestConfig } from 'axios'
import { useAuthStore } from '@/stores/auth'
import router from '@/router'

export const api = axios.create({
  baseURL: '/',
  headers: { 'Content-Type': 'application/json' },
})

// ── Request interceptor: attach Bearer token ─────────────────────────────────
api.interceptors.request.use((config: InternalAxiosRequestConfig) => {
  // Defer store access to call time so Pinia is already initialised
  const auth = useAuthStore()
  if (auth.accessToken) {
    config.headers.Authorization = `Bearer ${auth.accessToken}`
  }
  return config
})

// ── Response interceptor: handle 401 with token refresh ──────────────────────
let isRefreshing = false
let pendingQueue: Array<{ resolve: (token: string) => void; reject: (err: unknown) => void }> = []

function processPendingQueue(error: unknown, token: string | null) {
  pendingQueue.forEach((p) => {
    if (error) p.reject(error)
    else p.resolve(token!)
  })
  pendingQueue = []
}

api.interceptors.response.use(
  (response) => response,
  async (error) => {
    const originalRequest = error.config
    if (error.response?.status !== 401 || originalRequest._retry) {
      return Promise.reject(error)
    }

    const auth = useAuthStore()
    if (!auth.refreshToken) {
      auth.logout()
      router.push({ name: 'login' })
      return Promise.reject(error)
    }

    if (isRefreshing) {
      // Queue up requests while refresh is in progress
      return new Promise((resolve, reject) => {
        pendingQueue.push({ resolve, reject })
      }).then((token) => {
        originalRequest.headers.Authorization = `Bearer ${token}`
        return api(originalRequest)
      })
    }

    originalRequest._retry = true
    isRefreshing = true

    try {
      const { data } = await axios.post('/api/auth/refresh', {
        refreshToken: auth.refreshToken,
      })
      auth.setTokens(data.accessToken, data.refreshToken)
      processPendingQueue(null, data.accessToken)
      originalRequest.headers.Authorization = `Bearer ${data.accessToken}`
      return api(originalRequest)
    } catch (refreshError) {
      processPendingQueue(refreshError, null)
      auth.logout()
      router.push({ name: 'login' })
      return Promise.reject(refreshError)
    } finally {
      isRefreshing = false
    }
  },
)

export default api
