import { Page, Locator } from '@playwright/test'

/**
 * Page Object Model for the Register page (/register).
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
  readonly otpMethodButton: Locator
  readonly passwordMethodButton: Locator

  constructor(page: Page) {
    this.page = page
    this.emailInput = page.locator('input[type="email"]')
    this.displayNameInput = page.locator('input[placeholder*="name" i], input[autocomplete="name"]')
    this.passwordInput = page.locator('input[type="password"]').first()
    this.confirmPasswordInput = page.locator('input[type="password"]').last()
    this.genderSelect = page.locator('select[id*="gender"], select[name*="gender"]')
    this.submitButton = page.getByRole('button', { name: /create account|register|sign up/i })
    this.errorBanner = page.locator('div.bg-red-50')
    this.otpMethodButton = page.getByRole('button', { name: /email code|otp|magic link/i })
    this.passwordMethodButton = page.getByRole('button', { name: /password/i })
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
