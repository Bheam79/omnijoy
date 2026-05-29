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
    this.postComposerInput = page.locator('[data-testid="post-textarea"]').or(page.locator('[data-testid="post-composer"]'))
    this.postButton = page.locator('[data-testid="post-submit-button"]')
    this.feedItems = page.locator('[data-testid="post-card"]')
    this.notificationBell = page.locator('[data-testid="notification-bell-button"]')
  }

  async goto() {
    await this.page.goto('/wall')
  }

  async createTextPost(content: string) {
    // Click the trigger div to open the modal, then fill the textarea
    const trigger = this.page.locator('[data-testid="post-composer"]')
    const textarea = this.page.locator('[data-testid="post-textarea"]')
    if (await trigger.isVisible()) {
      await trigger.click()
    }
    await textarea.waitFor({ state: 'visible' })
    await textarea.fill(content)
    await this.postButton.click()
  }
}
