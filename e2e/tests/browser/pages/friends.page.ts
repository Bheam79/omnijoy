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
    this.pendingRequestsList = page.locator('[data-testid="pending-requests"], .friend-requests')
    this.acceptButtons = page.getByRole('button', { name: /accept/i })
    this.declineButtons = page.getByRole('button', { name: /decline|reject/i })
    this.friendCards = page.locator('[data-testid="friend-card"], .friend-item')
  }

  async goto() {
    await this.page.goto('/friends')
  }
}
