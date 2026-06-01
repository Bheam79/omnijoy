import axios from 'axios'
import type { AuthTokens, User, UserRole } from '@/types'

/** Raw axios (no interceptors) for auth endpoints that don't need a Bearer token */
const authAxios = axios.create({ baseURL: '/', headers: { 'Content-Type': 'application/json' } })

export interface RegisterPayload {
  email: string
  displayName: string
  authMethod: 'password' | 'otp'
  password?: string
  gender?: string
  birthDate?: string
  showBirthDate?: boolean
  locationPlaceId?: string | null
  locationName?: string | null
  locationCity?: string | null
  locationCountry?: string | null
  locationCountryCode?: string | null
  locationLatitude?: number | null
  locationLongitude?: number | null
}

export interface AuthResult {
  accessToken: string
  refreshToken: string
  expiresIn: number
  user: User
}

function toUser(raw: Record<string, unknown>): User {
  return {
    id: raw.id as string,
    email: raw.email as string,
    displayName: raw.displayName as string,
    avatarUrl: (raw.avatarUrl as string) || undefined,
    coverUrl: (raw.coverUrl as string) || undefined,
    bio: (raw.bio as string) || undefined,
    gender: (raw.gender as User['gender']) ?? 'NotDisclosed',
    birthDate: (raw.birthDate as string) || undefined,
    showBirthDate: (raw.showBirthDate as boolean) ?? false,
    role: (raw.role as UserRole) ?? 'User',
    createdAt: raw.createdAt as string,
    locationName: (raw.locationName as string) || null,
    locationCity: (raw.locationCity as string) || null,
    locationCountry: (raw.locationCountry as string) || null,
  }
}

export const authService = {
  async register(payload: RegisterPayload): Promise<AuthResult> {
    const { data } = await authAxios.post('/api/auth/register', payload)
    return { ...data, user: toUser(data.user) }
  },

  async loginPassword(email: string, password: string): Promise<AuthResult> {
    const { data } = await authAxios.post('/api/auth/login', { email, password })
    return { ...data, user: toUser(data.user) }
  },

  async requestOtp(email: string): Promise<void> {
    await authAxios.post('/api/auth/otp/request', { email })
  },

  async verifyOtp(email: string, code: string): Promise<AuthResult> {
    const { data } = await authAxios.post('/api/auth/otp/verify', { email, code })
    return { ...data, user: toUser(data.user) }
  },

  async loginGoogle(idToken: string): Promise<AuthResult> {
    const { data } = await authAxios.post('/api/auth/oauth/google', { token: idToken })
    return { ...data, user: toUser(data.user) }
  },

  async loginFacebook(accessToken: string): Promise<AuthResult> {
    const { data } = await authAxios.post('/api/auth/oauth/facebook', { token: accessToken })
    return { ...data, user: toUser(data.user) }
  },

  async refresh(refreshToken: string): Promise<AuthTokens> {
    const { data } = await authAxios.post('/api/auth/refresh', { refreshToken })
    return data as AuthTokens
  },

  async logout(refreshToken: string): Promise<void> {
    await authAxios.post('/api/auth/logout', { refreshToken }).catch(() => {
      // Best-effort — clear client-side tokens regardless
    })
  },
}
