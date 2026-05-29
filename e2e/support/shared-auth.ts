/**
 * Shared auth cache — JWTs minted once during `globalSetup` and reused by
 * spec files that would otherwise overrun the strict 10-req/min auth
 * rate limit.
 *
 * API tests should import `getSharedTokenFor` instead of calling the auth
 * endpoint directly.  Browser tests should use `injectTokens` from
 * `auth-helpers.ts`, which also reads from this cache.
 */

import { readFileSync } from 'fs'
import { join } from 'path'
import type { SeedUser } from '../fixtures/seed-data'

export interface SharedAuthUser {
  email: string
  token: string
  userId: string
  /** Full user object returned by POST /api/auth/login — used by browser tests. */
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  user?: Record<string, any>
  refreshToken?: string
}

export interface SharedAuth {
  baseURL: string
  users: Record<string, SharedAuthUser>
}

/**
 * Location of the cache file written by globalSetup.
 */
export const SHARED_AUTH_PATH = join(__dirname, '..', '.auth', 'shared-auth.json')

let _cached: SharedAuth | null = null

/** Read the shared auth cache written by globalSetup. */
export function readSharedAuth(): SharedAuth {
  if (_cached) return _cached
  const raw = readFileSync(SHARED_AUTH_PATH, 'utf8')
  _cached = JSON.parse(raw) as SharedAuth
  return _cached
}

/** Look up a seeded user's token + userId by cache key. Throws if missing. */
export function getSharedUser(key: 'user1' | 'user2' | 'user3' | 'admin'): SharedAuthUser {
  const auth = readSharedAuth()
  const u = auth.users[key]
  if (!u) {
    throw new Error(
      `Shared auth cache has no entry for '${key}'. ` +
      `Ensure globalSetup ran and that the backend was reachable.`,
    )
  }
  return u
}

/**
 * Look up a seeded user's token + userId by their email address.
 *
 * Use this in API spec files instead of calling `POST /api/auth/login` per
 * test — it reads from the file written by globalSetup and never hits the
 * rate-limited auth endpoint.
 *
 * @example
 *   const { token, userId } = getSharedTokenFor(SEED.user1)
 */
export function getSharedTokenFor(user: SeedUser | { email: string }): {
  token: string
  userId: string
  refreshToken?: string
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  user: Record<string, any>
} {
  const auth = readSharedAuth()
  const entry = Object.values(auth.users).find((u) => u.email === user.email)
  if (!entry) {
    throw new Error(
      `Shared auth cache has no entry for email '${user.email}'. ` +
      `Ensure globalSetup ran and that the backend was reachable.`,
    )
  }
  // `user` is always populated from globalSetup. If running against an old cache
  // that pre-dates this field, synthesize a minimal object so downstream code
  // that accesses `user.id` still works.
  return {
    token: entry.token,
    userId: entry.userId,
    refreshToken: entry.refreshToken,
    user: entry.user ?? { id: entry.userId, email: entry.email },
  }
}
