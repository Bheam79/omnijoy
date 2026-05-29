import { Page, Locator } from '@playwright/test'

/**
 * Page Object Model for User Profile pages (/profile/:userId).
 */
export class ProfilePage {
  readonly page: Page
  readonly editProfileButton: Locator
  readonly bioInput: Locator
  readonly saveButton: Locator
  readonly avatarUploadInput: Locator
  readonly displayName: Locator
  readonly bio: Locator
  readonly addFriendButton: Locator
  readonly friendStatusText: Locator

  constructor(page: Page) {
    this.page = page
    this.editProfileButton = page.locator('[data-testid="profile-edit-button"]')
    this.bioInput = page.locator('textarea[name*="bio"], textarea[placeholder*="bio" i]')
    this.saveButton = page.getByRole('button', { name: /save/i })
    this.avatarUploadInput = page.locator('input[type="file"][accept*="image"]').first()
    this.displayName = page.locator('[data-testid="profile-display-name"]')
    this.bio = page.locator('[data-testid="profile-bio"]')
    this.addFriendButton = page.locator('[data-testid="friend-button"]')
    this.friendStatusText = page.locator('[data-testid="friend-status"]')
  }

  async goto(userId: string) {
    await this.page.goto(`/profile/${userId}`)
  }
}
