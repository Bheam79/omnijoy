import { Page, Locator } from '@playwright/test'

/**
 * Page Object Model for the Login page (/login).
 */
export class LoginPage {
  readonly page: Page
  readonly emailInput: Locator
  readonly passwordInput: Locator
  readonly loginButton: Locator
  readonly errorBanner: Locator
  readonly otpTabButton: Locator
  readonly otpEmailInput: Locator
  readonly sendOtpButton: Locator
  readonly otpCodeInput: Locator
  readonly verifyOtpButton: Locator

  constructor(page: Page) {
    this.page = page
    // Password tab fields
    this.emailInput = page.locator('input[type="email"]').first()
    this.passwordInput = page.locator('input[type="password"]').first()
    this.loginButton = page.getByRole('button', { name: /log in|sign in/i })
    this.errorBanner = page.locator('div.bg-red-50')
    // OTP tab — the login page uses a button switcher (not ARIA tabs)
    this.otpTabButton = page.getByRole('button', { name: /one-time code|otp/i })
    this.otpEmailInput = page.locator('input[type="email"]').last()
    this.sendOtpButton = page.getByRole('button', { name: /send code|request otp/i })
    this.otpCodeInput = page.locator('input[placeholder*="code" i], input[name*="otp" i]')
    this.verifyOtpButton = page.getByRole('button', { name: /verify|sign in with code/i })
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
