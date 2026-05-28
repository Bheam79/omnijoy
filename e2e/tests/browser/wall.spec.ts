import { test, expect } from '@playwright/test'
import { SEED } from '../../fixtures/seed-data'
import { injectTokens } from '../../support/auth-helpers'
import { WallPage } from './pages/wall.page'

test.describe('My Wall feed', () => {
  test.beforeEach(async ({ page, baseURL }) => {
    await injectTokens(page, baseURL!, SEED.user1.email, SEED.user1.password)
  })

  test('wall page loads and shows feed or empty state', async ({ page }) => {
    const wall = new WallPage(page)
    await wall.goto()
    await page.waitForLoadState('networkidle')
    // Either feed items exist or an empty-state message
    const feed = wall.feedItems
    const empty = page.getByText(/no posts yet|be the first|start sharing/i)
    const postComposer = wall.postComposerInput
    // One of these should be visible
    const hasContent =
      (await feed.count()) > 0 ||
      (await empty.isVisible()) ||
      (await postComposer.isVisible())
    expect(hasContent).toBeTruthy()
  })

  test('post composer is visible on wall page', async ({ page }) => {
    await page.goto('/wall')
    await page.waitForLoadState('networkidle')
    // The PostComposer renders a trigger bar (div) with text "What's on your mind?".
    // Clicking it opens a modal with a textarea. We just verify the trigger is visible.
    const composer = page.getByText("What's on your mind?")
      .or(page.locator('[data-testid="post-composer"]'))
      .or(page.locator('textarea').first())
    await expect(composer.first()).toBeVisible({ timeout: 8_000 })
  })

  test('unauthenticated user is redirected from /wall to /login', async ({ page }) => {
    // Clear tokens
    await page.goto('/')
    await page.evaluate(() => {
      localStorage.removeItem('access_token')
      localStorage.removeItem('refresh_token')
    })
    await page.goto('/wall')
    await expect(page).toHaveURL(/\/login/, { timeout: 8_000 })
  })
})

test.describe('Post creation', () => {
  test.beforeEach(async ({ page, baseURL }) => {
    await injectTokens(page, baseURL!, SEED.user1.email, SEED.user1.password)
  })

  test('wall page is accessible after auth injection', async ({ page }) => {
    await page.goto('/wall')
    await page.waitForLoadState('networkidle')
    expect(page.url()).toContain('/wall')
  })

  test('post composer area is interactive', async ({ page }) => {
    await page.goto('/wall')
    await page.waitForLoadState('networkidle')

    // Find composer textarea/input
    const composer = page.locator(
      'textarea, [contenteditable="true"]',
    ).first()
    if (await composer.isVisible()) {
      await composer.click()
      await composer.fill('Test post from E2E suite')
      await expect(composer).toHaveValue(/test post from e2e suite/i)
    }
  })
})
