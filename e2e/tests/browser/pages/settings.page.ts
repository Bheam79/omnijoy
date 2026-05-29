import { Page, Locator } from '@playwright/test'

/**
 * Page Object Model for the Settings section (/settings/account, /settings/notifications, …).
 */
export class SettingsPage {
  readonly page: Page

  // ── Account sub-page ────────────────────────────────────────────────────────

  /** Password form container (data-testid="password-form") */
  readonly passwordForm: Locator
  readonly currentPasswordInput: Locator
  readonly newPasswordInput: Locator
  readonly confirmPasswordInput: Locator
  readonly savePasswordButton: Locator

  /** Email form container (data-testid="email-form") */
  readonly emailForm: Locator
  readonly newEmailInput: Locator
  readonly emailCurrentPasswordInput: Locator
  readonly saveEmailButton: Locator

  readonly deactivateButton: Locator
  readonly deleteButton: Locator

  // ── Privacy sub-page ────────────────────────────────────────────────────────

  readonly privacySettingsForm: Locator
  readonly whoCanSeeProfileSelect: Locator
  readonly whoCanSeePostsSelect: Locator
  readonly whoCanSeeFriendsSelect: Locator
  readonly whoCanSendRequestsSelect: Locator
  readonly savePrivacyButton: Locator

  // ── Profile sub-page ───────────────────────────────────────────────────────

  readonly profileSettingsForm: Locator

  // ── Notification prefs ─────────────────────────────────────────────────────

  /** All notification toggle checkboxes (data-testid starts with "toggle-") */
  readonly notificationToggles: Locator

  constructor(page: Page) {
    this.page = page

    // Account — password form
    this.passwordForm = page.locator('[data-testid="password-form"]')
    this.currentPasswordInput = page.locator('[data-testid="current-password"]')
    this.newPasswordInput = page.locator('[data-testid="new-password"]')
    this.confirmPasswordInput = page.locator('[data-testid="confirm-new-password"]')
    this.savePasswordButton = page.locator('[data-testid="save-password-button"]')

    // Account — email form
    this.emailForm = page.locator('[data-testid="email-form"]')
    this.newEmailInput = page.locator('[data-testid="new-email-input"]')
    this.emailCurrentPasswordInput = page.locator('[data-testid="email-current-password-input"]')
    this.saveEmailButton = page.locator('[data-testid="save-email-button"]')

    // Account — danger zone
    this.deactivateButton = page.locator('[data-testid="deactivate-btn"]')
    this.deleteButton = page.locator('[data-testid="delete-btn"]')

    // Privacy
    this.privacySettingsForm = page.locator('[data-testid="privacy-settings-form"]')
    this.whoCanSeeProfileSelect = page.locator('[data-testid="who-can-see-profile"]')
    this.whoCanSeePostsSelect = page.locator('[data-testid="who-can-see-posts"]')
    this.whoCanSeeFriendsSelect = page.locator('[data-testid="who-can-see-friends"]')
    this.whoCanSendRequestsSelect = page.locator('[data-testid="who-can-send-requests"]')
    this.savePrivacyButton = page.locator('[data-testid="save-privacy-button"]')

    // Profile
    this.profileSettingsForm = page.locator('[data-testid="profile-settings-form"]')

    // Notifications (data-testid="toggle-<fieldKey>")
    this.notificationToggles = page.locator('[data-testid^="toggle-"]')
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

  async gotoPrivacy() {
    await this.page.goto('/settings/privacy')
    await this.page.waitForLoadState('networkidle')
  }
}
