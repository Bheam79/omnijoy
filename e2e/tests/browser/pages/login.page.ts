import { Page, Locator } from '@playwright/test'

/**
 * Page Object Model for the Login page (/login).
 *
 * All locators use data-testid as the primary selector — the template
 * in LoginView.vue guarantees these attributes exist.
 */
export class LoginPage {
  readonly page: Page
  // Password tab
  readonly passwordTabButton: Locator
  readonly emailInput: Locator
  readonly passwordInput: Locator
  readonly loginButton: Locator
  // Error / success banners
  readonly errorBanner: Locator
  readonly successBanner: Locator
  // OTP tab
  readonly otpTabButton: Locator
  readonly otpEmailInput: Locator
  readonly sendOtpButton: Locator
  readonly otpCodeInput: Locator
  readonly verifyOtpButton: Locator
  readonly resendCodeButton: Locator

  constructor(page: Page) {
    this.page = page
    // Tab switcher
    this.passwordTabButton = page.locator('[data-testid="password-tab"]')
    this.otpTabButton = page.locator('[data-testid="otp-tab"]')
    // Banners
    this.errorBanner = page.locator('[data-testid="login-error-banner"]')
    this.successBanner = page.locator('[data-testid="login-success-banner"]')
    // Password flow
    this.emailInput = page.locator('[data-testid="pw-email-input"]')
    this.passwordInput = page.locator('[data-testid="pw-password-input"]')
    this.loginButton = page.locator('[data-testid="pw-submit"]')
    // OTP flow
    this.otpEmailInput = page.locator('[data-testid="otp-email-input"]')
    this.sendOtpButton = page.locator('[data-testid="send-code-button"]')
    this.otpCodeInput = page.locator('[data-testid="otp-code-input"]')
    this.verifyOtpButton = page.locator('[data-testid="verify-button"]')
    this.resendCodeButton = page.locator('[data-testid="resend-code-button"]')
  }

  async goto() {
    await this.page.goto('/login')
  }

  async loginWithPassword(email: string, password: string) {
    await this.goto()
    await this.emailInput.fill(email)
    await this.passwordInput.fill(password)
    await this.loginButton.click()
  }
}
