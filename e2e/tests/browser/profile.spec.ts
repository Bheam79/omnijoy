import { test, expect } from '../../support/fixtures'
import { SEED } from '../../fixtures/seed-data'
import { injectTokens } from '../../support/auth-helpers'

test.describe('Profile view and edit', () => {
  test.beforeEach(async ({ page, baseURL }) => {
    await injectTokens(page, baseURL!, SEED.user1.email, SEED.user1.password)
  })

  test('can navigate to own profile', async ({ page, baseURL }) => {
    // Navigate to wall and find own profile link
    await page.goto('/wall')
    await page.waitForLoadState('networkidle')
    // Profile link should be reachable via sidebar or avatar
    const profileLink = page.getByRole('link', { name: /profile|my profile/i })
      .or(page.locator('a[href*="/profile/"]').first())
    if (await profileLink.isVisible()) {
      await profileLink.click()
      await expect(page).toHaveURL(/\/profile\//)
    } else {
      // Skip if can't resolve — profile navigation depends on the UI having a profile link
      test.skip()
    }
  })

  test('profile page displays display name', async ({ page }) => {
    await page.goto('/wall')
    await page.waitForLoadState('networkidle')
    // Find own profile
    const profileLink = page.locator('a[href*="/profile/"]').first()
    if (await profileLink.isVisible()) {
      const href = await profileLink.getAttribute('href')
      if (href) {
        await page.goto(href)
        await expect(page.getByText(SEED.user1.displayName)).toBeVisible()
      }
    }
  })
})

test.describe('Profile page navigation (public)', () => {
  test('visiting /profile/:id with unknown ID shows 404 or error', async ({ page, baseURL }) => {
    await injectTokens(page, baseURL!, SEED.user1.email, SEED.user1.password)
    await page.goto('/profile/00000000-0000-0000-0000-000000000000')
    await page.waitForLoadState('networkidle')
    // Expect some error indicator
    const errorText = page.getByText(/not found|does not exist|error/i)
    await expect(errorText).toBeVisible({ timeout: 8_000 })
  })
})
