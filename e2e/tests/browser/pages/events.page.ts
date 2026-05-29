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
    this.createEventButton = page.locator('[data-testid="create-event-button"]')
    this.eventCards = page.locator('[data-testid="event-card"]')
    this.titleInput = page.locator('input[name*="title"], input[placeholder*="title" i]')
    this.descriptionInput = page.locator('textarea[name*="desc"], textarea[placeholder*="desc" i]')
    this.startAtInput = page.locator('input[type="datetime-local"], input[name*="start"]')
    this.locationInput = page.locator('input[name*="location"], input[placeholder*="location" i]')
    this.submitButton = page.getByRole('button', { name: /create|save/i })
    this.rsvpGoingButton = page.locator('[data-testid="rsvp-going"]')
    this.rsvpMaybeButton = page.locator('[data-testid="rsvp-maybe"]')
    this.rsvpDeclineButton = page.locator('[data-testid="rsvp-not-going"]')
  }

  async goto() {
    await this.page.goto('/events')
  }

  async gotoEvent(id: string) {
    await this.page.goto(`/events/${id}`)
  }
}
