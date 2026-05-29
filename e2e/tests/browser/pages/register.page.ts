import { Page, Locator } from '@playwright/test'

/**
 * Page Object Model for the Register page (/register).
 *
 * All locators use data-testid as the primary selector — the template
 * in RegisterView.vue guarantees these attributes exist.
 */
export class RegisterPage {
  readonly page: Page
  readonly emailInput: Locator
  readonly displayNameInput: Locator
  readonly passwordInput: Locator
  readonly confirmPasswordInput: Locator
  readonly genderSelect: Locator
  readonly submitButton: Locator
  readonly errorBanner: Locator
  readonly passwordMethodButton: Locator
  readonly otpMethodButton: Locator

  constructor(page: Page) {
    this.page = page
    this.errorBanner = page.locator('[data-testid="register-error-banner"]')
    this.emailInput = page.locator('[data-testid="register-email-input"]')
    this.displayNameInput = page.locator('[data-testid="register-display-name-input"]')
    this.passwordMethodButton = page.locator('[data-testid="auth-method-password"]')
    this.otpMethodButton = page.locator('[data-testid="auth-method-otp"]')
    this.passwordInput = page.locator('[data-testid="register-password-input"]')
    this.confirmPasswordInput = page.locator('[data-testid="register-confirm-password-input"]')
    this.submitButton = page.locator('[data-testid="register-submit"]')
    // Gender select has no data-testid (optional field, not in spec) — keep structural selector
    this.genderSelect = page.locator('select').filter({ hasText: /prefer not to say|male|female/i })
  }

  async goto() {
    await this.page.goto('/register')
  }

  async registerWithPassword(opts: {
    email: string
    displayName: string
    password: string
    confirmPassword: string
  }) {
    await this.goto()
    await this.emailInput.fill(opts.email)
    await this.displayNameInput.fill(opts.displayName)
    await this.passwordInput.fill(opts.password)
    await this.confirmPasswordInput.fill(opts.confirmPassword)
    await this.submitButton.click()
  }
}
