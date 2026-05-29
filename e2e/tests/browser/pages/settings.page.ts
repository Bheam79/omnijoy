import { Page, Locator } from '@playwright/test'

/**
 * Page Object Model for the Settings section (/settings/account, /settings/notifications, …).
 */
export class SettingsPage {
  readonly page: Page

  constructor(page: Page) {
    this.page = page
  }

  async gotoAccount() {
    await this.page.goto('/settings/account')
    await this.page.waitForLoadState('networkidle')
  }

  async gotoNotifications() {
    await this.page.goto('/settings/notifications')
    await this.page.waitForLoadState('networkidle')
  }

  async gotoProfile() {
    await this.page.goto('/settings/profile')
    await this.page.waitForLoadState('networkidle')
  }

  // ── Account sub-page helpers ────────────────────────────────────────────────

  get currentPasswordInput(): Locator {
    return this.page.getByLabel(/current password/i).or(
      this.page.locator('input[type="password"]').first(),
    )
  }

  get newPasswordInput(): Locator {
    return this.page.getByLabel(/new password/i).or(
      this.page.locator('input[type="password"]').nth(1),
    )
  }

  get confirmPasswordInput(): Locator {
    return this.page.getByLabel(/confirm.*password/i).or(
      this.page.locator('input[type="password"]').nth(2),
    )
  }

  get savePasswordButton(): Locator {
    return this.page.getByRole('button', { name: /save|change|update.*password/i })
  }

  get deactivateButton(): Locator {
    return this.page.getByRole('button', { name: /deactivate/i })
  }

  get deleteButton(): Locator {
    return this.page.getByRole('button', { name: /delete.*account/i })
  }

  // ── Notification prefs helpers ─────────────────────────────────────────────

  get notificationToggles(): Locator {
    return this.page.locator('input[type="checkbox"], [role="switch"]')
  }
}
