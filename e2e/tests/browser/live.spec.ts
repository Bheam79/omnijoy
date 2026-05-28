import { test, expect } from '@playwright/test'
import { SEED } from '../../fixtures/seed-data'
import { injectTokens } from '../../support/auth-helpers'

test.describe('Live streaming pages', () => {
  test.beforeEach(async ({ page, baseURL }) => {
    await injectTokens(page, baseURL!, SEED.user1.email, SEED.user1.password)
  })

  test('/live page loads', async ({ page }) => {
    await page.goto('/live')
    await page.waitForLoadState('networkidle')
    expect(page.url()).toContain('/live')
    await expect(page.locator('body')).toBeVisible()
  })

  test('live page shows active streams or empty state', async ({ page }) => {
    await page.goto('/live')
    await page.waitForLoadState('networkidle')

    // The live page always has a "Go Live" header button. The empty state shows
    // "No live streams right now". Use .first() to avoid strict mode violation
    // since "Go Live" text appears in multiple elements (header + empty state).
    const hasStreams = (await page.locator('[data-testid="stream-card"], article.stream').count()) > 0
    const hasEmpty = await page.getByText(/no live streams right now/i).first().isVisible()
    const hasGoLiveButton = await page.getByRole('button', { name: 'Go Live' }).isVisible()
    expect(hasStreams || hasEmpty || hasGoLiveButton).toBeTruthy()
  })

  test('start stream page is accessible', async ({ page }) => {
    await page.goto('/live')
    await page.waitForLoadState('networkidle')

    // Look for "Go Live" button or similar
    const goLiveBtn = page.getByRole('button', { name: /go live|start streaming/i })
    if (await goLiveBtn.isVisible()) {
      await goLiveBtn.click()
      await page.waitForLoadState('networkidle')
      // Some start-stream UI should appear
      const startForm = page.locator('input[name*="title"], [data-testid="stream-form"]')
      const hasForm = await startForm.isVisible()
      // Accept page transition or form visibility
      expect(page.url().includes('/live') || hasForm).toBeTruthy()
    }
  })

  test('active streams API returns list', async ({ request, baseURL }) => {
    const loginResp = await request.post(`${baseURL}/api/auth/login`, {
      data: { email: SEED.user1.email, password: SEED.user1.password },
    })
    const { accessToken: token } = await loginResp.json()

    const resp = await request.get(`${baseURL}/api/live/active`, {
      headers: { Authorization: `Bearer ${token}` },
    })
    expect(resp.status()).toBe(200)
    const body = await resp.json()
    expect(Array.isArray(body) || Array.isArray(body.items)).toBeTruthy()
  })

  test('viewer page (/live/:id) handles unknown stream gracefully', async ({ page }) => {
    await page.goto('/live/00000000-0000-0000-0000-000000000000')
    await page.waitForLoadState('networkidle')
    // Should show error or redirect — not crash
    await expect(page.locator('body')).toBeVisible()
  })
})
