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
    this.notificationBell = page.locator('[data-testid="notification-bell"], [aria-label*="notification" i]')
    this.userMenuButton = page.locator('[data-testid="user-menu"], [aria-label*="profile menu" i], button:has([alt*="avatar" i])')
    this.logoutButton = page.getByRole('button', { name: /log out|logout|sign out/i })
    // Sidebar — use 'aside' scope to avoid matching TopNav duplicates.
    // The sidebar links are rendered inside an <aside> element.
    const sidebar = page.locator('aside')
    this.wallLink = sidebar.getByRole('link', { name: /wall|home|feed|my wall/i })
    this.friendsLink = sidebar.getByRole('link', { name: /friends/i })
    this.eventsLink = sidebar.getByRole('link', { name: /events/i })
    this.companyLink = sidebar.getByRole('link', { name: /company|pages/i })
    this.liveLink = sidebar.getByRole('link', { name: /live/i })
    this.settingsLink = page.getByRole('link', { name: /settings/i })
    this.messagesLink = page.getByRole('link', { name: /messages|messenger|chat/i })
  }
}
