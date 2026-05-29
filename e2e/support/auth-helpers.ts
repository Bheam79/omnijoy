import { Page } from '@playwright/test'
import { SEED } from '../fixtures/seed-data'
import { readSharedAuth } from './shared-auth'

/**
 * Log in via the UI (password tab) and wait for redirect to /wall.
 */
export async function loginAs(page: Page, user: typeof SEED.user1) {
  await page.goto('/login')
  await page.getByLabel(/email/i).fill(user.email)
  await page.getByLabel(/password/i).fill(user.password)
  await page.getByRole('button', { name: /log in|sign in/i }).click()
  await page.waitForURL('**/wall')
}

/**
 * Inject tokens directly into localStorage so tests can skip the UI login
 * when the login flow itself is not under test.
 *
 * Prefers the shared-auth cache written by globalSetup so it never hits the
 * rate-limited `POST /api/auth/login` endpoint.  Falls back to a live API
 * call only if the user is not present in the cache (e.g. a newly registered
 * one-off user created inside a test).
 */
export async function injectTokens(
  page: Page,
  baseUrl: string,
  email: string,
  password: string,
) {
  // Try the shared cache first (written by globalSetup, never rate-limited)
  let at: string
  let rt: string
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  let userObj: Record<string, any>

  try {
    const auth = readSharedAuth()
    const cached = Object.values(auth.users).find((u) => u.email === email)
    if (cached) {
      at = cached.token
      rt = cached.refreshToken ?? ''
      userObj = cached.user ?? { id: cached.userId, email: cached.email }
    } else {
      throw new Error('not in cache')
    }
  } catch {
    // Not in cache — fall back to API login (e.g. a disposable test-only user)
    const resp = await page.request.post(`${baseUrl}/api/auth/login`, {
      data: { email, password },
    })
    const body = await resp.json()
    at = body.accessToken
    rt = body.refreshToken
    userObj = body.user
  }

  await page.goto('/')
  await page.evaluate(({ at, rt, user }) => {
    localStorage.setItem('access_token', at)
    localStorage.setItem('refresh_token', rt)
    // Store user JSON so the auth store can restore isAuthenticated on page load
    // (auth store reads auth_user from localStorage on init to populate user.value)
    localStorage.setItem('auth_user', JSON.stringify(user))
  }, { at, rt, user: userObj })
  return { accessToken: at, refreshToken: rt, user: userObj }
}
