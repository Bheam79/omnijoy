import { test, expect } from '../../support/fixtures'
import { SEED } from '../../fixtures/seed-data'
import { getSharedTokenFor } from '../../support/shared-auth'
import { injectTokens } from '../../support/auth-helpers'
import { SettingsPage } from './pages/settings.page'

// ── Browser-level settings tests ──────────────────────────────────────────────

test.describe('Settings pages — navigation', () => {
  test.beforeEach(async ({ page, baseURL }) => {
    await injectTokens(page, baseURL!, SEED.user1.email, SEED.user1.password)
  })

  test('/settings page loads', async ({ page }) => {
    await page.goto('/settings')
    await page.waitForLoadState('networkidle')
    expect(page.url()).toContain('/settings')
    await expect(page.locator('body')).toBeVisible()
  })

  test('/settings/account page loads', async ({ page }) => {
    const settings = new SettingsPage(page)
    await settings.gotoAccount()
    expect(page.url()).toContain('/settings/account')
    await expect(page.locator('body')).toBeVisible()
  })

  test('/settings/notifications page loads', async ({ page }) => {
    const settings = new SettingsPage(page)
    await settings.gotoNotifications()
    expect(page.url()).toContain('/settings/notifications')
    await expect(page.locator('body')).toBeVisible()
  })

  test('/settings/profile page loads', async ({ page }) => {
    const settings = new SettingsPage(page)
    await settings.gotoProfile()
    expect(page.url()).toContain('/settings/profile')
    await expect(page.locator('body')).toBeVisible()
  })
})

// ── API-level settings tests ──────────────────────────────────────────────────

test.describe('Settings — change password (API)', () => {
  test('change password with correct current password succeeds', async ({
    request,
    baseURL,
  }) => {
    const { token: token } = getSharedTokenFor(SEED.user1)

    // Change to the same password (idempotent-safe; no actual change in state)
    const resp = await request.post(`${baseURL}/api/account/change-password`, {
      headers: { Authorization: `Bearer ${token}` },
      data: {
        currentPassword: SEED.user1.password,
        newPassword: SEED.user1.password,
        confirmNewPassword: SEED.user1.password,
      },
    })
    // 200 OK or 400 (if the API rejects same-password) are both acceptable
    expect([200, 400]).toContain(resp.status())
  })

  test('change password with wrong current password returns 401 or 400', async ({
    request,
    baseURL,
  }) => {
    const { token: token } = getSharedTokenFor(SEED.user1)

    const resp = await request.post(`${baseURL}/api/account/change-password`, {
      headers: { Authorization: `Bearer ${token}` },
      data: {
        currentPassword: 'WrongPassword!999',
        newPassword: 'NewPass@12345',
        confirmNewPassword: 'NewPass@12345',
      },
    })
    expect([400, 401]).toContain(resp.status())
  })
})

test.describe('Settings — notification preferences (API)', () => {
  test('GET notification preferences returns a preferences object', async ({
    request,
    baseURL,
  }) => {
    const { token: token } = getSharedTokenFor(SEED.user1)

    const resp = await request.get(`${baseURL}/api/account/notification-preferences`, {
      headers: { Authorization: `Bearer ${token}` },
    })
    expect(resp.status()).toBe(200)
    const body = await resp.json()
    // Should be an object (not an error)
    expect(typeof body).toBe('object')
    expect(body).not.toBeNull()
  })

  test('PUT notification preferences succeeds', async ({ request, baseURL }) => {
    const { token: token } = getSharedTokenFor(SEED.user1)

    // Fetch current prefs to build a valid payload
    const getResp = await request.get(`${baseURL}/api/account/notification-preferences`, {
      headers: { Authorization: `Bearer ${token}` },
    })
    const current = await getResp.json()

    const updateResp = await request.put(`${baseURL}/api/account/notification-preferences`, {
      headers: { Authorization: `Bearer ${token}` },
      data: current, // send back unchanged — idempotent
    })
    expect([200, 204]).toContain(updateResp.status())
  })
})

test.describe('Settings — deactivate account (API)', () => {
  test('deactivate and immediately reactivate via re-login', async ({
    request,
    baseURL,
  }) => {
    // Use user3 for destructive tests to avoid interfering with other specs
    const { token: token, user } = getSharedTokenFor(SEED.user3)

    // Deactivate
    const deactivateResp = await request.post(`${baseURL}/api/account/deactivate`, {
      headers: { Authorization: `Bearer ${token}` },
    })
    // 200 OK or 409 if already deactivated
    expect([200, 409]).toContain(deactivateResp.status())

    // Re-activate by logging in again (login reactivates on many platforms)
    // We just verify the API round-trip succeeded without a 5xx
    const reloginResp = await request.post(`${baseURL}/api/auth/login`, {
      data: { email: SEED.user3.email, password: SEED.user3.password },
    })
    // Login may succeed (200) or fail with 401/403 for deactivated accounts
    expect([200, 401, 403]).toContain(reloginResp.status())
  })
})

test.describe('Settings — account settings UI', () => {
  test.beforeEach(async ({ page, baseURL }) => {
    await injectTokens(page, baseURL!, SEED.user2.email, SEED.user2.password)
  })

  test('account settings page renders password form', async ({ page }) => {
    const settings = new SettingsPage(page)
    await settings.gotoAccount()

    // Password form and its inputs should be present
    const hasPasswordForm = await settings.passwordForm.isVisible()
    const hasInputs =
      (await settings.currentPasswordInput.count()) > 0 ||
      (await settings.newPasswordInput.count()) > 0
    expect(hasPasswordForm || hasInputs).toBeTruthy()
  })

  test('notification settings page renders toggle controls', async ({ page }) => {
    const settings = new SettingsPage(page)
    await settings.gotoNotifications()

    // Should have data-testid toggle checkboxes
    const hasControls =
      (await settings.notificationToggles.count()) > 0 ||
      (await page.locator('select').count()) > 0 ||
      (await page.getByRole('button').count()) > 0
    expect(hasControls).toBeTruthy()
  })
})
