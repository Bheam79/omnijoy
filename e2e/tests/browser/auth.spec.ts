import { test, expect } from '../../support/fixtures'
import { LoginPage } from './pages/login.page'
import { RegisterPage } from './pages/register.page'
import { SEED } from '../../fixtures/seed-data'

/**
 * Browser E2E tests for Registration, Login, and Logout flows.
 *
 * These tests exercise the Vue SPA pages, not just the API.
 * A unique email is generated per run so tests are idempotent.
 */

// Unique suffix so re-runs don't collide on "email already registered"
const RUN_ID = Date.now().toString(36)

test.describe('Registration flow', () => {
  test('registers with email + password and lands on /wall', async ({ page }) => {
    const reg = new RegisterPage(page)
    await reg.goto()
    await expect(page).toHaveURL('/register')

    const email = `e2e_new_${RUN_ID}@omnijoy.test`
    await reg.emailInput.fill(email)
    await reg.displayNameInput.fill(`New User ${RUN_ID}`)
    await reg.passwordInput.fill('Test@12345!')
    await reg.confirmPasswordInput.fill('Test@12345!')
    await reg.submitButton.click()

    await page.waitForURL('**/wall', { timeout: 15_000 })
    expect(page.url()).toContain('/wall')
  })

  test('shows error when passwords do not match', async ({ page }) => {
    const reg = new RegisterPage(page)
    await reg.goto()

    await reg.emailInput.fill(`mismatch_${RUN_ID}@omnijoy.test`)
    await reg.displayNameInput.fill('Mismatch User')
    await reg.passwordInput.fill('Test@12345!')
    await reg.confirmPasswordInput.fill('DifferentPassword!')
    await reg.submitButton.click()

    // Should stay on /register and show an error
    await expect(page).toHaveURL('/register')
    await expect(reg.errorBanner).toBeVisible()
  })

  test('shows error for missing required fields', async ({ page }) => {
    const reg = new RegisterPage(page)
    await reg.goto()

    // Submit empty form
    await reg.submitButton.click()
    await expect(reg.errorBanner).toBeVisible()
    await expect(page).toHaveURL('/register')
  })

  test('shows error when email is already registered', async ({ page }) => {
    const reg = new RegisterPage(page)
    await reg.goto()

    // Seed user1 should already be registered by globalSetup
    await reg.emailInput.fill(SEED.user1.email)
    await reg.displayNameInput.fill('Duplicate User')
    await reg.passwordInput.fill('Test@12345!')
    await reg.confirmPasswordInput.fill('Test@12345!')
    await reg.submitButton.click()

    // Expect an error (conflict from API)
    await expect(reg.errorBanner).toBeVisible({ timeout: 8_000 })
    await expect(page).toHaveURL('/register')
  })
})

test.describe('Login flow', () => {
  test('logs in with valid credentials and lands on /wall', async ({ page }) => {
    const login = new LoginPage(page)
    await login.loginWithPassword(SEED.user1.email, SEED.user1.password)
    await page.waitForURL('**/wall', { timeout: 15_000 })
    expect(page.url()).toContain('/wall')
  })

  test('shows error for invalid credentials', async ({ page }) => {
    const login = new LoginPage(page)
    await login.loginWithPassword(SEED.user1.email, 'WrongPassword!')
    await expect(login.errorBanner).toBeVisible({ timeout: 8_000 })
    await expect(page).toHaveURL('/login')
  })

  test('redirects authenticated user from /login to /wall', async ({ page }) => {
    // Log in first via UI (this loads the user into the Pinia auth store)
    const login = new LoginPage(page)
    await login.loginWithPassword(SEED.user1.email, SEED.user1.password)
    await page.waitForURL('**/wall')

    // Navigate client-side to /login using Vue Router so the Pinia auth state
    // (including user.value) is preserved — a full page.goto() would reinitialize
    // the Vue app and lose the in-memory auth state, causing the guard to fail.
    await page.evaluate(() => {
      const el = document.querySelector('#app') as (Element & { __vue_app__?: { config?: { globalProperties?: { $router?: { push: (path: string) => void } } } } }) | null
      el?.__vue_app__?.config?.globalProperties?.$router?.push('/login')
    })

    // Router guard should redirect authenticated users back to /wall
    await expect(page).toHaveURL(/\/wall/, { timeout: 5_000 })
  })
})

test.describe('Logout flow', () => {
  test('logs out and redirects to home/login', async ({ page }) => {
    const login = new LoginPage(page)
    await login.loginWithPassword(SEED.user1.email, SEED.user1.password)
    await page.waitForURL('**/wall')

    // The logout button is inside a profile dropdown (aria-label="Your account").
    // Open the dropdown first, then click "Log out".
    const profileTrigger = page.getByRole('button', { name: 'Your account' })
    await profileTrigger.click()

    // Logout button appears inside the dropdown
    const logoutButton = page.getByRole('button', { name: /log out/i })
    await expect(logoutButton).toBeVisible({ timeout: 5_000 })
    await logoutButton.click()

    // After logout, should be on home (/) or login (/login)
    await page.waitForURL(/\/(login)?$/, { timeout: 8_000 })
    const url = page.url()
    expect(url.endsWith('/') || url.includes('/login')).toBeTruthy()
  })
})

test.describe('OTP login flow', () => {
  test('OTP request tab is present on login page', async ({ page }) => {
    await page.goto('/login')
    // The login page has a button-based tab switcher (not ARIA tabs)
    // Look for the "One-time code" button that switches to the OTP view
    const otpOption = page.getByRole('button', { name: /one-time code|otp/i })
      .or(page.getByText(/one-time code|sign in with code/i))
    await expect(otpOption).toBeVisible()
  })

  test('OTP request returns success message for any email', async ({ page }) => {
    await page.goto('/login')
    // Switch to OTP tab — the login page uses a button switcher (not ARIA tabs)
    const otpTab = page.getByRole('button', { name: 'One-time code' })
    if (await otpTab.isVisible()) await otpTab.click()

    const emailField = page.locator('input[type="email"]').last()
    await emailField.fill('anyone@omnijoy.test')
    const sendBtn = page.getByRole('button', { name: /send code|request/i })
    await sendBtn.click()
    // API always returns 200 to prevent email enumeration — expect the green success banner.
    // The Vue template shows: "Check your email for a 6-digit code." inside a div.bg-green-50.
    // Using the specific class avoids matching other elements that contain "code" or "email".
    await expect(page.locator('div.bg-green-50')).toBeVisible({ timeout: 8_000 })
  })
})
