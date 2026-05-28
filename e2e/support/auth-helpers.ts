import { Page } from '@playwright/test'
import { SEED } from '../fixtures/seed-data'

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
 */
export async function injectTokens(
  page: Page,
  baseUrl: string,
  email: string,
  password: string,
) {
  // Do a quick API login to get real tokens
  const resp = await page.request.post(`${baseUrl}/api/auth/login`, {
    data: { email, password },
  })
  const body = await resp.json()
  await page.goto('/')
  await page.evaluate(({ at, rt, user }) => {
    localStorage.setItem('access_token', at)
    localStorage.setItem('refresh_token', rt)
    // Store user JSON so the auth store can restore isAuthenticated on page load
    // (auth store reads auth_user from localStorage on init to populate user.value)
    localStorage.setItem('auth_user', JSON.stringify(user))
  }, { at: body.accessToken, rt: body.refreshToken, user: body.user })
  return body
}
