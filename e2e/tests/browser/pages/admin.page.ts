import { Page, Locator } from '@playwright/test'

/**
 * Page Object Model for the Admin panel (/admin).
 */
export class AdminPage {
  readonly page: Page

  constructor(page: Page) {
    this.page = page
  }

  async goto() {
    await this.page.goto('/admin')
    await this.page.waitForLoadState('networkidle')
  }

  async gotoReports() {
    await this.page.goto('/admin/reports')
    await this.page.waitForLoadState('networkidle')
  }

  async gotoUsers() {
    await this.page.goto('/admin/users')
    await this.page.waitForLoadState('networkidle')
  }

  async gotoAuditLog() {
    await this.page.goto('/admin/audit-log')
    await this.page.waitForLoadState('networkidle')
  }

  /** Report queue table rows. */
  get reportRows(): Locator {
    return this.page.locator(
      '[data-testid="report-row"], tr, [class*="report-item"]',
    )
  }

  /** User management rows. */
  get userRows(): Locator {
    return this.page.locator(
      '[data-testid="user-row"], tr, [class*="user-row"]',
    )
  }
}
