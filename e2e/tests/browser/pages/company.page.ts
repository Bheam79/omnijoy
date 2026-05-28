import { Page, Locator } from '@playwright/test'

/**
 * Page Object Model for Company pages (/company, /company/:id).
 */
export class CompanyPage {
  readonly page: Page
  readonly createPageButton: Locator
  readonly nameInput: Locator
  readonly descriptionInput: Locator
  readonly submitButton: Locator
  readonly companyCards: Locator
  readonly addAdminButton: Locator
  readonly followButton: Locator
  readonly unfollowButton: Locator

  constructor(page: Page) {
    this.page = page
    this.createPageButton = page.getByRole('button', { name: /create page|new page/i })
    this.nameInput = page.locator('input[name*="name"], input[placeholder*="name" i]')
    this.descriptionInput = page.locator('textarea[name*="desc"], textarea[placeholder*="desc" i]')
    this.submitButton = page.getByRole('button', { name: /create|save/i })
    this.companyCards = page.locator('[data-testid="company-card"], article.company')
    this.addAdminButton = page.getByRole('button', { name: /add admin/i })
    this.followButton = page.getByRole('button', { name: /follow/i })
    this.unfollowButton = page.getByRole('button', { name: /unfollow/i })
  }

  async goto() {
    await this.page.goto('/company')
  }

  async gotoCompany(id: string) {
    await this.page.goto(`/company/${id}`)
  }
}
