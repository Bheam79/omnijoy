import { randomUUID } from 'crypto'
import { test, expect } from '../../support/fixtures'
import { TEST_LOCATION } from '../../fixtures/seed-data'
import { fetchLatestPasswordResetCode } from '../../support/password-reset-helpers'

/**
 * Browser E2E tests for the Forgot-Password flow (OMNIJOY-226).
 *
 * UI driven through the Vue SPA at /forgot-password.  The reset code is
 * extracted from the DB via the same helper the API spec uses
 * (brute-force the SHA-256 hash — the plaintext is only logged or emailed).
 *
 * Each test registers a fresh, uuid-suffixed user so we never mutate the
 * password of a shared seed account.
 */

const RUN_ID = randomUUID().slice(0, 8)
let counter = 0

interface FreshUser { email: string; password: string; displayName: string; xff: string }

function nextXff(): string {
  // RFC 5737 TEST-NET-2 — never routed on the public internet.  Synthetic
  // XFF gives each test its own `strict` rate-limit bucket so the suite
  // doesn't drain the (default) 10/min/IP budget shared with other auth
  // specs.  The backend's `GetClientIp` honours the leftmost XFF entry
  // (see RateLimitingExtensions.cs).
  const i = ++counter
  return `198.51.100.${150 + (i % 100)}-pwr-ui-${RUN_ID}-${i}`
}

/**
 * Pin a stable XFF on every request the browser context emits so the
 * strict rate-limiter treats this test as a fresh client IP.  Without
 * this, running several specs back-to-back can cascade into 429s.
 */
async function pinXff(
  page: import('@playwright/test').Page,
  xff: string,
): Promise<void> {
  await page.context().setExtraHTTPHeaders({ 'X-Forwarded-For': xff })
}

/**
 * Register a fresh user via the API (faster than driving the registration
 * UI, and unrelated to what this spec covers).  Returns the credentials so
 * the test can drive the forgot-password flow against them.
 */
async function registerFreshUser(
  page: import('@playwright/test').Page,
  baseURL: string,
): Promise<FreshUser> {
  const id = `${RUN_ID}_${++counter}_${randomUUID().slice(0, 6)}`
  const user: FreshUser = {
    email:       `pwreset_ui_${id}@omnijoy.test`,
    password:    'OrigUiPass1!',
    displayName: `PwResetUi_${id}`,
    xff:         nextXff(),
  }
  await pinXff(page, user.xff)
  const resp = await page.request.post(`${baseURL}/api/auth/register`, {
    headers: { 'X-Forwarded-For': user.xff },
    data: {
      email:       user.email,
      displayName: user.displayName,
      authMethod:  'password',
      password:    user.password,
      gender:      'NotDisclosed',
      ...TEST_LOCATION,
    },
  })
  if (!resp.ok()) {
    throw new Error(
      `Failed to register fresh user ${user.email}: HTTP ${resp.status()} — ${await resp.text()}`,
    )
  }
  return user
}

test.describe('Forgot password — UI flow', () => {
  test.describe.configure({ mode: 'serial' })

  test('Login page exposes a "Forgot password?" link that navigates to /forgot-password', async ({ page }) => {
    await pinXff(page, nextXff())
    await page.goto('/login')

    const link = page.locator('[data-testid="forgot-password-link"]')
    await expect(link).toBeVisible()
    await link.click()

    await page.waitForURL('**/forgot-password')
    expect(page.url()).toContain('/forgot-password')

    // Step 1 UI is visible: email input + submit button.
    await expect(page.locator('[data-testid="step1-email"]')).toBeVisible()
    await expect(page.locator('[data-testid="step1-submit"]')).toBeVisible()
    // Step 2 elements are not yet rendered.
    await expect(page.locator('[data-testid="step2-code"]')).toHaveCount(0)
  })

  test('submitting an email advances to step 2 with the email locked', async ({ page, baseURL }) => {
    const u = await registerFreshUser(page, baseURL!)

    await page.goto('/forgot-password')
    await page.locator('[data-testid="step1-email"]').fill(u.email)
    await page.locator('[data-testid="step1-submit"]').click()

    // Step 2 inputs become visible.
    await expect(page.locator('[data-testid="step2-code"]')).toBeVisible({ timeout: 8_000 })
    await expect(page.locator('[data-testid="step2-new-password"]')).toBeVisible()
    await expect(page.locator('[data-testid="step2-confirm-password"]')).toBeVisible()

    // The email is locked (readonly) on step 2 and pre-filled.
    const lockedEmail = page.locator('[data-testid="step2-email"]')
    await expect(lockedEmail).toBeVisible()
    await expect(lockedEmail).toHaveValue(u.email)
    await expect(lockedEmail).toHaveAttribute('readonly', '')
  })

  test('happy path: email → code + new password → redirect to /login?reset=1', async ({ page, baseURL }) => {
    const u = await registerFreshUser(page, baseURL!)

    await page.goto('/forgot-password')
    await page.locator('[data-testid="step1-email"]').fill(u.email)
    await page.locator('[data-testid="step1-submit"]').click()

    await expect(page.locator('[data-testid="step2-code"]')).toBeVisible({ timeout: 8_000 })

    const code = fetchLatestPasswordResetCode(u.email)
    expect(code, `expected a reset code for ${u.email}`).not.toBeNull()
    expect(code).toMatch(/^\d{6}$/)

    const newPassword = 'BrandNewUi1!'
    await page.locator('[data-testid="step2-code"]').fill(code!)
    await page.locator('[data-testid="step2-new-password"]').fill(newPassword)
    await page.locator('[data-testid="step2-confirm-password"]').fill(newPassword)

    await expect(page.locator('[data-testid="step2-submit"]')).toBeEnabled()
    await page.locator('[data-testid="step2-submit"]').click()

    // The view renders a success banner before the 1.5s setTimeout redirects.
    await expect(page.locator('[data-testid="success-message"]')).toBeVisible({ timeout: 5_000 })

    // …then we land on /login?reset=1.
    await page.waitForURL(/\/login(\?.*)?$/, { timeout: 5_000 })
    expect(page.url()).toMatch(/[?&]reset=1\b/)
  })

  test('wrong code shows an inline error banner and does NOT redirect', async ({ page, baseURL }) => {
    const u = await registerFreshUser(page, baseURL!)

    await page.goto('/forgot-password')
    await page.locator('[data-testid="step1-email"]').fill(u.email)
    await page.locator('[data-testid="step1-submit"]').click()

    await expect(page.locator('[data-testid="step2-code"]')).toBeVisible({ timeout: 8_000 })

    await page.locator('[data-testid="step2-code"]').fill('000000')
    await page.locator('[data-testid="step2-new-password"]').fill('GoodEnough1!')
    await page.locator('[data-testid="step2-confirm-password"]').fill('GoodEnough1!')
    await page.locator('[data-testid="step2-submit"]').click()

    await expect(page.locator('[data-testid="error-banner"]')).toBeVisible({ timeout: 8_000 })

    // We're still on /forgot-password, not /login.
    expect(page.url()).toContain('/forgot-password')
    expect(page.url()).not.toMatch(/[?&]reset=1\b/)
  })

  test('client-side rejects new-password < 8 chars BEFORE any /password/reset request hits the network', async ({ page, baseURL }) => {
    const u = await registerFreshUser(page, baseURL!)

    await page.goto('/forgot-password')
    await page.locator('[data-testid="step1-email"]').fill(u.email)
    await page.locator('[data-testid="step1-submit"]').click()

    await expect(page.locator('[data-testid="step2-code"]')).toBeVisible({ timeout: 8_000 })

    // Spy on outgoing requests — fail the test if /password/reset is called.
    const resetCalls: string[] = []
    page.on('request', (req) => {
      if (req.url().includes('/api/auth/password/reset')) {
        resetCalls.push(req.url())
      }
    })

    // The step2 submit button is wired to remain disabled until
    // newPassword && confirmPw && newPassword === confirmPw.  With
    // matching but-too-short values it becomes enabled — and the
    // client-side `submitConfirm` guard then surfaces the error
    // without making a network call.
    await page.locator('[data-testid="step2-code"]').fill('123456')
    await page.locator('[data-testid="step2-new-password"]').fill('abc')
    await page.locator('[data-testid="step2-confirm-password"]').fill('abc')

    await expect(page.locator('[data-testid="step2-submit"]')).toBeEnabled()
    await page.locator('[data-testid="step2-submit"]').click()

    // Error banner appears with the client-side message.
    const banner = page.locator('[data-testid="error-banner"]')
    await expect(banner).toBeVisible({ timeout: 3_000 })
    await expect(banner).toContainText(/at least 8 characters/i)

    // Give the page a beat to settle, then assert the network spy.
    await page.waitForTimeout(150)
    expect(
      resetCalls,
      `expected no /api/auth/password/reset calls, got: ${JSON.stringify(resetCalls)}`,
    ).toHaveLength(0)
  })
})
