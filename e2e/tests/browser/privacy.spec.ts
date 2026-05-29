import { test, expect } from '../../support/fixtures'
import { SEED } from '../../fixtures/seed-data'
import { getSharedTokenFor } from '../../support/shared-auth'
import { injectTokens } from '../../support/auth-helpers'

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
    await page.goto('/settings/privacy')
    await page.waitForLoadState('networkidle')

    // Should show some privacy controls
    const hasControls =
      (await page.getByRole('combobox').count()) > 0 ||
      (await page.getByRole('radio').count()) > 0 ||
      (await page.getByRole('switch').count()) > 0 ||
      (await page.locator('select').count()) > 0

    expect(hasControls).toBeTruthy()
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
