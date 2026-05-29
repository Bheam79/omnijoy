import { Page, Locator } from '@playwright/test'

/**
 * Page Object Model for the search UI (top-bar input + /search results page).
 */
export class SearchPage {
  readonly page: Page
  /** The search input in the top navigation bar. */
  readonly topBarInput: Locator
  /** The autocomplete/suggest dropdown container. */
  readonly suggestDropdown: Locator
  /** The full results page container. */
  readonly resultsContainer: Locator

  constructor(page: Page) {
    this.page = page
    this.topBarInput = page.locator('[data-testid="search-input"]')
    this.suggestDropdown = page.locator('[data-testid="search-dropdown"]')
    this.resultsContainer = page.locator(
      '[data-testid="search-results"], .search-results, main',
    ).first()
  }

  async goto(query?: string) {
    const url = query ? `/search?q=${encodeURIComponent(query)}` : '/search'
    await this.page.goto(url)
    await this.page.waitForLoadState('networkidle')
  }

  async typeInTopBar(text: string) {
    await this.topBarInput.fill(text)
  }
}
