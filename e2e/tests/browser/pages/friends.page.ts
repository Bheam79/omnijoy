import { Page, Locator } from '@playwright/test'

/**
 * Page Object Model for the Friends page (/friends).
 */
export class FriendsPage {
  readonly page: Page
  readonly pendingRequestsList: Locator
  readonly acceptButtons: Locator
  readonly declineButtons: Locator
  readonly friendCards: Locator

  constructor(page: Page) {
    this.page = page
    this.pendingRequestsList = page.locator('[data-testid="pending-requests"]')
    this.acceptButtons = page.locator('[data-testid="accept-button"]')
    this.declineButtons = page.locator('[data-testid="decline-button"]')
    this.friendCards = page.locator('[data-testid="friend-card"]')
  }

  async goto() {
    await this.page.goto('/friends')
  }
}
