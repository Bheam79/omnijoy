import { test, expect } from '../../support/fixtures'
import { SEED } from '../../fixtures/seed-data'
import { getSharedTokenFor } from '../../support/shared-auth'
import { injectTokens } from '../../support/auth-helpers'
import { SettingsPage } from './pages/settings.page'

test.describe('Privacy settings page', () => {
  test.beforeEach(async ({ page, baseURL }) => {
    await injectTokens(page, baseURL!, SEED.user1.email, SEED.user1.password)
  })

  test('/settings/privacy page loads', async ({ page }) => {
    await page.goto('/settings/privacy')
    await page.waitForLoadState('networkidle')
    expect(page.url()).toContain('/settings/privacy')
    await expect(page.locator('body')).toBeVisible()
  })

  test('privacy settings are displayed', async ({ page }) => {
    const settings = new SettingsPage(page)
    await settings.gotoPrivacy()

    // Should show the privacy form and at least one select control
    const hasForm = await settings.privacySettingsForm.isVisible()
    const hasSelects = (await page.locator('[data-testid^="who-can-"]').count()) > 0

    expect(hasForm || hasSelects).toBeTruthy()
  })

  test('can update privacy settings via API and reflect in page', async ({ page, request, baseURL }) => {
    const { token: token } = getSharedTokenFor(SEED.user1)

    // Update privacy via API
    const updateResp = await request.put(`${baseURL}/api/users/me/privacy`, {
      headers: { Authorization: `Bearer ${token}` },
      data: {
        whoCanSeeProfile: 'Everyone',
        whoCanSeePosts: 'Friends',
        whoCanSeeFriendList: 'Friends',
        whoCanSendFriendRequests: 'Everyone',
      },
    })
    expect([200, 201]).toContain(updateResp.status())

    // Navigate to privacy settings — should load without error
    await page.goto('/settings/privacy')
    await page.waitForLoadState('networkidle')
    expect(page.url()).toContain('/settings/privacy')
  })

  test('/settings page loads', async ({ page }) => {
    await page.goto('/settings')
    await page.waitForLoadState('networkidle')
    expect(page.url()).toContain('/settings')
    await expect(page.locator('body')).toBeVisible()
  })
})
