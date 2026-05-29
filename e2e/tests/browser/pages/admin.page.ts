import { Page, Locator } from '@playwright/test'

/**
 * Page Object Model for the Admin panel (/admin).
 */
export class AdminPage {
  readonly page: Page
  readonly reportRows: Locator
  readonly userRows: Locator
  readonly reportsTable: Locator
  readonly usersTable: Locator
  readonly reportStatusFilter: Locator
  readonly reportTypeFilter: Locator
  readonly userSearchInput: Locator
  readonly userSearchButton: Locator
  readonly navReports: Locator
  readonly navUsers: Locator
  readonly navAudit: Locator

  constructor(page: Page) {
    this.page = page
    this.reportRows = page.locator('[data-testid="report-row"]')
    this.userRows = page.locator('[data-testid="user-row"]')
    this.reportsTable = page.locator('[data-testid="reports-table"]')
    this.usersTable = page.locator('[data-testid="users-table"]')
    this.reportStatusFilter = page.locator('[data-testid="report-status-filter"]')
    this.reportTypeFilter = page.locator('[data-testid="report-type-filter"]')
    this.userSearchInput = page.locator('[data-testid="user-search-input"]')
    this.userSearchButton = page.locator('[data-testid="user-search-button"]')
    this.navReports = page.locator('[data-testid="admin-nav-reports"]')
    this.navUsers = page.locator('[data-testid="admin-nav-users"]')
    this.navAudit = page.locator('[data-testid="admin-nav-audit"]')
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
}
