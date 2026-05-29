/**
 * Shared auth cache — JWTs minted once during `globalSetup` and reused by
 * spec files that would otherwise overrun the strict 10-req/min auth
 * rate limit.
 *
 * NOT for use by browser tests (those have their own login flows and
 * need cookie / localStorage state per page context).
 */

import { readFileSync } from 'fs'
import { join } from 'path'

export interface SharedAuthUser {
  email: string
  token: string
  userId: string
}

export interface SharedAuth {
  baseURL: string
  users: Record<string, SharedAuthUser>
}

/**
 * Location of the cache file — under `playwright-report/` so it lives
 * inside the e2e directory but doesn't pollute the source tree.
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

/** Look up a seeded user's token + userId. Throws if missing. */
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
