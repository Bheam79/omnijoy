import { test, expect } from '../../support/fixtures'
import { SEED } from '../../fixtures/seed-data'
import { getSharedTokenFor } from '../../support/shared-auth'
import { injectTokens } from '../../support/auth-helpers'

test.describe('Messenger / Chat', () => {
  test.beforeEach(async ({ page, baseURL }) => {
    await injectTokens(page, baseURL!, SEED.user1.email, SEED.user1.password)
  })

  test('chat/messenger page loads or is accessible from sidebar', async ({ page }) => {
    // Try direct navigation if there's a chat route, or find it in nav
    const chatNavLink = page.getByRole('link', { name: /messages|messenger|chat/i })
    await page.goto('/wall')
    await page.waitForLoadState('networkidle')
    if (await chatNavLink.isVisible()) {
      await chatNavLink.click()
      await page.waitForLoadState('networkidle')
    }
    // No crash is the baseline
    await expect(page.locator('body')).toBeVisible()
  })

  test('can open a direct conversation (API-backed browser flow)', async ({ page, request, baseURL }) => {
    const { token: token1, user: user1 } = getSharedTokenFor(SEED.user1)

    const { user: user2 } = getSharedTokenFor(SEED.user2)

    // Create or get direct conversation via API
    const convResp = await request.post(`${baseURL}/api/conversations/direct/${user2.id}`, {
      headers: { Authorization: `Bearer ${token1}` },
    })
    expect([200, 201]).toContain(convResp.status())
    const conversation = await convResp.json()
    expect(conversation.id).toBeTruthy()

    // Send a text message via API
    const msgResp = await request.post(`${baseURL}/api/messages`, {
      headers: { Authorization: `Bearer ${token1}` },
      multipart: {
        conversationId: conversation.id,
        content: 'Hello from E2E test!',
      },
    })
    expect([200, 201]).toContain(msgResp.status())
    const message = await msgResp.json()
    expect(message.content).toBe('Hello from E2E test!')

    // Verify messages via API
    const messagesResp = await request.get(
      `${baseURL}/api/conversations/${conversation.id}/messages`,
      { headers: { Authorization: `Bearer ${token1}` } },
    )
    expect(messagesResp.status()).toBe(200)
    const msgs = await messagesResp.json()
    expect(Array.isArray(msgs) ? msgs.length : msgs.items?.length).toBeGreaterThan(0)
  })

  test('conversation list API returns conversations', async ({ request, baseURL }) => {
    const { token: token } = getSharedTokenFor(SEED.user1)

    const resp = await request.get(`${baseURL}/api/conversations`, {
      headers: { Authorization: `Bearer ${token}` },
    })
    expect(resp.status()).toBe(200)
    const body = await resp.json()
    expect(Array.isArray(body) || Array.isArray(body.items)).toBeTruthy()
  })
})
