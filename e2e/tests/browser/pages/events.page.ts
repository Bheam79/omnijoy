import { Page, Locator } from '@playwright/test'

/**
 * Page Object Model for the Events pages (/events, /events/:id).
 */
export class EventsPage {
  readonly page: Page
  readonly createEventButton: Locator
  readonly eventCards: Locator
  readonly titleInput: Locator
  readonly descriptionInput: Locator
  readonly startAtInput: Locator
  readonly locationInput: Locator
  readonly submitButton: Locator
  readonly rsvpGoingButton: Locator
  readonly rsvpMaybeButton: Locator
  readonly rsvpDeclineButton: Locator

  constructor(page: Page) {
    this.page = page
    this.createEventButton = page.getByRole('button', { name: /create event|new event/i })
    this.eventCards = page.locator('[data-testid="event-card"], article.event')
    this.titleInput = page.locator('input[name*="title"], input[placeholder*="title" i]')
    this.descriptionInput = page.locator('textarea[name*="desc"], textarea[placeholder*="desc" i]')
    this.startAtInput = page.locator('input[type="datetime-local"], input[name*="start"]')
    this.locationInput = page.locator('input[name*="location"], input[placeholder*="location" i]')
    this.submitButton = page.getByRole('button', { name: /create|save/i })
    this.rsvpGoingButton = page.getByRole('button', { name: /going/i })
    this.rsvpMaybeButton = page.getByRole('button', { name: /maybe/i })
    this.rsvpDeclineButton = page.getByRole('button', { name: /not going|decline/i })
  }

  async goto() {
    await this.page.goto('/events')
  }

  async gotoEvent(id: string) {
    await this.page.goto(`/events/${id}`)
  }
}
