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
  /** Root element of the PlacePicker component (data-testid attr is forwarded by Vue inheritAttrs). */
  readonly locationPicker: Locator

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
    // The PlacePicker's root <div data-testid="place-picker"> is overridden by Vue
    // inheritAttrs with data-testid="register-location-picker" set in RegisterView.vue.
    this.locationPicker = page.locator('[data-testid="register-location-picker"]')
  }

  async goto() {
    await this.page.goto('/register')
  }

  /**
   * Fills the location picker.
   *
   * Handles both modes of PlacePicker:
   *   – Normal (Google Places API available): types into the autocomplete input,
   *     waits for suggestions, clicks the first result.
   *   – Manual fallback (no API key configured): types into the free-text input
   *     and clicks "Use".
   *
   * Resolves once the selected-chip is visible (location is committed).
   */
  async selectLocation(cityText: string = 'Oslo') {
    const root = this.locationPicker
    const selectedChip = root.locator('[data-testid="selected-chip"]')

    // Already has a selection — nothing to do.
    if (await selectedChip.isVisible()) return

    const placeInput = root.locator('[data-testid="place-input"]')
    const manualInput = root.locator('[data-testid="manual-input"]')
    const manualConfirmBtn = root.locator('[data-testid="manual-confirm-btn"]')
    const suggestionsList = root.locator('[data-testid="suggestions-list"]')
    const firstSuggestion = root.locator('[data-testid="suggestion-item"]').first()

    if (await placeInput.isVisible()) {
      // Type to trigger autocomplete / API-unavailable detection.
      await placeInput.fill(cityText)

      // Wait for whichever appears first: suggestions dropdown or manual fallback.
      await suggestionsList.or(manualInput).waitFor({ state: 'visible', timeout: 5_000 })
    }

    if (await suggestionsList.isVisible()) {
      // API returned suggestions — pick the first one.
      await firstSuggestion.click()
      await selectedChip.waitFor({ state: 'visible', timeout: 5_000 })
    } else {
      // Manual fallback — type location name and confirm.
      await manualInput.fill(`${cityText}, Norway`)
      await manualConfirmBtn.click()
      await selectedChip.waitFor({ state: 'visible', timeout: 3_000 })
    }
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
    await this.selectLocation()
    await this.passwordInput.fill(opts.password)
    await this.confirmPasswordInput.fill(opts.confirmPassword)
    await this.submitButton.click()
  }
}
