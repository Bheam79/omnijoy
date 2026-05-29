import { Page, Locator } from '@playwright/test'

/**
 * Page Object Model for the shared navigation elements (TopNav + Sidebar).
 */
export class NavPage {
  readonly page: Page
  // TopNav
  readonly logo: Locator
  readonly notificationBell: Locator
  readonly userMenuButton: Locator
  readonly logoutButton: Locator
  // Sidebar links
  readonly wallLink: Locator
  readonly friendsLink: Locator
  readonly eventsLink: Locator
  readonly companyLink: Locator
  readonly liveLink: Locator
  readonly settingsLink: Locator
  readonly messagesLink: Locator

  constructor(page: Page) {
    this.page = page
    // TopNav
    this.logo = page.locator('a[href="/"], a[aria-label*="home" i], .logo')
    this.notificationBell = page.locator('[data-testid="notification-bell-button"]')
    this.userMenuButton = page.locator('[data-testid="user-menu-button"]')
    this.logoutButton = page.locator('[data-testid="logout-button"]')
    // Sidebar nav links — keyed by data-testid for stability.
    this.wallLink = page.locator('[data-testid="nav-wall"]')
    this.friendsLink = page.locator('[data-testid="nav-friends"]')
    this.eventsLink = page.locator('[data-testid="nav-events"]')
    this.companyLink = page.locator('[data-testid="nav-company"]')
    this.liveLink = page.locator('[data-testid="nav-live"]')
    this.settingsLink = page.locator('[data-testid="nav-settings"]')
    this.messagesLink = page.getByRole('link', { name: /messages|messenger|chat/i })
  }
}
