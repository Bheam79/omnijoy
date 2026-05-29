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
    this.topBarInput = page.locator(
      '[data-testid="search-input"], input[placeholder*="search" i], input[type="search"]',
    ).first()
    this.suggestDropdown = page.locator(
      '[data-testid="search-dropdown"], [data-testid="suggest-dropdown"], .search-suggestions, [class*="search-dropdown"]',
    ).first()
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
