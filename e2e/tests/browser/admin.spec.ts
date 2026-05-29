import { test, expect } from '../../support/fixtures'
import { SEED } from '../../fixtures/seed-data'
import { injectTokens } from '../../support/auth-helpers'
import { AdminPage } from './pages/admin.page'

// ── Route guard — non-admin user ──────────────────────────────────────────────

test.describe('Admin panel — route guard', () => {
  test('non-admin user visiting /admin is redirected or sees forbidden', async ({
    page,
    baseURL,
  }) => {
    await injectTokens(page, baseURL!, SEED.user1.email, SEED.user1.password)
    await page.goto('/admin')
    await page.waitForLoadState('networkidle')

    // Should either redirect away or show a 403/forbidden message
    const url = page.url()
    const redirected = !url.includes('/admin') || url.includes('/login')
    const forbiddenText = page.getByText(/forbidden|not authorized|access denied|you do not have/i)
    const hasForbiddenMsg = await forbiddenText.first().isVisible()

    expect(redirected || hasForbiddenMsg).toBeTruthy()
  })

  test('unauthenticated user visiting /admin is redirected to login', async ({
    page,
  }) => {
    // Explicitly clear tokens
    await page.goto('/')
    await page.evaluate(() => {
      localStorage.removeItem('access_token')
      localStorage.removeItem('refresh_token')
      localStorage.removeItem('auth_user')
    })
    await page.goto('/admin')
    await page.waitForLoadState('networkidle')
    // Should land on /login or show an auth prompt
    expect(
      page.url().includes('/login') ||
      page.url().includes('/admin') === false,
    ).toBeTruthy()
  })
})

// ── Admin user — panel access ─────────────────────────────────────────────────

test.describe('Admin panel — admin user access', () => {
  test.beforeEach(async ({ page, baseURL }) => {
    await injectTokens(page, baseURL!, SEED.admin.email, SEED.admin.password)
  })

  test('/admin page loads for admin user', async ({ page }) => {
    const admin = new AdminPage(page)
    await admin.goto()

    // Admin shell should be visible — not redirected
    await expect(page.locator('body')).toBeVisible()
    // URL should still be under /admin
    expect(page.url()).toContain('/admin')
  })

  test('/admin/reports page loads for admin user', async ({ page }) => {
    const admin = new AdminPage(page)
    await admin.gotoReports()
    expect(page.url()).toContain('/admin/reports')
    await expect(page.locator('body')).toBeVisible()
  })

  test('/admin/users page loads for admin user', async ({ page }) => {
    const admin = new AdminPage(page)
    await admin.gotoUsers()
    expect(page.url()).toContain('/admin/users')
    await expect(page.locator('body')).toBeVisible()
  })

  test('/admin/audit-log page loads for admin user', async ({ page }) => {
    const admin = new AdminPage(page)
    await admin.gotoAuditLog()
    expect(page.url()).toContain('/admin/audit-log')
    await expect(page.locator('body')).toBeVisible()
  })
})

// ── Admin API tests ───────────────────────────────────────────────────────────

test.describe('Admin — API endpoints', () => {
  test('GET /api/admin/users returns paginated user list for admin', async ({
    request,
    baseURL,
  }) => {
    const loginResp = await request.post(`${baseURL}/api/auth/login`, {
      data: { email: SEED.admin.email, password: SEED.admin.password },
    })
    const { accessToken: token } = await loginResp.json()

    const resp = await request.get(`${baseURL}/api/admin/users`, {
      headers: { Authorization: `Bearer ${token}` },
    })
    expect(resp.status()).toBe(200)
    const body = await resp.json()
    const items =
      body.items ?? body.data ?? (Array.isArray(body) ? body : null)
    expect(items).not.toBeNull()
    expect(Array.isArray(items)).toBeTruthy()
  })

  test('GET /api/admin/users returns 403 for non-admin', async ({
    request,
    baseURL,
  }) => {
    const loginResp = await request.post(`${baseURL}/api/auth/login`, {
      data: { email: SEED.user1.email, password: SEED.user1.password },
    })
    const { accessToken: token } = await loginResp.json()

    const resp = await request.get(`${baseURL}/api/admin/users`, {
      headers: { Authorization: `Bearer ${token}` },
    })
    expect(resp.status()).toBe(403)
  })

  test('GET /api/admin/reports returns report queue for admin', async ({
    request,
    baseURL,
  }) => {
    const loginResp = await request.post(`${baseURL}/api/auth/login`, {
      data: { email: SEED.admin.email, password: SEED.admin.password },
    })
    const { accessToken: token } = await loginResp.json()

    const resp = await request.get(`${baseURL}/api/admin/reports`, {
      headers: { Authorization: `Bearer ${token}` },
    })
    expect(resp.status()).toBe(200)
    const body = await resp.json()
    const items =
      body.items ?? body.data ?? (Array.isArray(body) ? body : null)
    expect(items).not.toBeNull()
  })

  test('GET /api/admin/audit-log returns log entries for admin', async ({
    request,
    baseURL,
  }) => {
    const loginResp = await request.post(`${baseURL}/api/auth/login`, {
      data: { email: SEED.admin.email, password: SEED.admin.password },
    })
    const { accessToken: token } = await loginResp.json()

    const resp = await request.get(`${baseURL}/api/admin/audit-log`, {
      headers: { Authorization: `Bearer ${token}` },
    })
    expect(resp.status()).toBe(200)
    const body = await resp.json()
    expect(typeof body).toBe('object')
  })
})
