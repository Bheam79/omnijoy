import { test, expect } from '../../support/fixtures'
import { SEED } from '../../fixtures/seed-data'
import { getSharedTokenFor } from '../../support/shared-auth'
import { injectTokens } from '../../support/auth-helpers'
import { EventsPage } from './pages/events.page'

test.describe('Events page', () => {
  test.beforeEach(async ({ page, baseURL }) => {
    await injectTokens(page, baseURL!, SEED.user1.email, SEED.user1.password)
  })

  test('/events page loads', async ({ page }) => {
    const events = new EventsPage(page)
    await events.goto()
    await page.waitForLoadState('networkidle')
    expect(page.url()).toContain('/events')
    await expect(page.locator('body')).toBeVisible()
  })

  test('events page shows list or empty state', async ({ page }) => {
    const events = new EventsPage(page)
    await events.goto()
    await page.waitForLoadState('networkidle')

    const hasEvents = (await events.eventCards.count()) > 0
    const hasEmpty = await page.getByText(/no events|create an event|upcoming/i).isVisible()
    expect(hasEvents || hasEmpty).toBeTruthy()
  })

  test('create event button is visible', async ({ page }) => {
    await page.goto('/events')
    await page.waitForLoadState('networkidle')
    const btn = page.getByRole('button', { name: /create event|new event|add event|\+ event/i })
    await expect(btn).toBeVisible({ timeout: 8_000 })
  })

  test('event creation and RSVP flow (API-backed)', async ({ page, request, baseURL }) => {
    const { token: token, user } = getSharedTokenFor(SEED.user1)

    // Create an event via API
    const createResp = await request.post(`${baseURL}/api/events`, {
      headers: { Authorization: `Bearer ${token}` },
      multipart: {
        title: 'E2E Test Event',
        description: 'Created by E2E test suite',
        startAt: new Date(Date.now() + 86400000).toISOString(),
        privacy: 'Everyone',
      },
    })
    expect([200, 201]).toContain(createResp.status())
    const event = await createResp.json()
    expect(event.id).toBeTruthy()

    // Navigate to event detail page
    const events = new EventsPage(page)
    await events.gotoEvent(event.id)
    await page.waitForLoadState('networkidle')
    await expect(page.getByText('E2E Test Event')).toBeVisible({ timeout: 8_000 })

    // RSVP buttons: "✅ Going", "🤔 Maybe", "❌ Not Going".
    // "❌ Not Going" also contains "going" so we avoid that pattern.
    // Instead, check for the RSVP heading (unique text) and then click "✅ Going" exactly.
    const rsvpHeading = page.getByText('Will you attend?', { exact: true })
    if (await rsvpHeading.isVisible()) {
      const goingButton = page.getByRole('button', { name: '✅ Going' })
      if (await goingButton.isVisible()) {
        await goingButton.click()
        await page.waitForTimeout(1000)
      }
    }

    // Cleanup
    await request.delete(`${baseURL}/api/events/${event.id}`, {
      headers: { Authorization: `Bearer ${token}` },
    })
  })
})
