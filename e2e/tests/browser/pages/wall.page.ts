import { Page, Locator } from '@playwright/test'

/**
 * Page Object Model for the Wall / feed page (/wall).
 */
export class WallPage {
  readonly page: Page
  readonly postComposerInput: Locator
  readonly postButton: Locator
  readonly feedItems: Locator
  readonly notificationBell: Locator

  constructor(page: Page) {
    this.page = page
    this.postComposerInput = page.locator(
      'textarea[placeholder*="mind" i], textarea[placeholder*="post" i], [data-testid="post-composer"]',
    )
    this.postButton = page.getByRole('button', { name: /post|share|publish/i })
    this.feedItems = page.locator('[data-testid="feed-item"], article, .feed-item')
    this.notificationBell = page.locator('[data-testid="notification-bell"], [aria-label*="notification" i]')
  }

  async goto() {
    await this.page.goto('/wall')
  }

  async createTextPost(content: string) {
    await this.postComposerInput.click()
    await this.postComposerInput.fill(content)
    await this.postButton.click()
  }
}
