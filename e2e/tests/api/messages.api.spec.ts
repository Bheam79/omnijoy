import { test, expect, type APIRequestContext } from '../../support/fixtures'
import { SEED } from '../../fixtures/seed-data'

/**
 * API E2E tests for MessagesController endpoints:
 *   POST   /api/messages       (send a message)
 *   DELETE /api/messages/{id}  (delete own message)
 *
 * Note: the core happy-path flows (text message, file attachment, missing
 * conversationId, delete own message, 403 for non-sender) are covered in
 * conversations.api.spec.ts as part of the full conversation workflow.
 * This file adds focused edge-case tests for the MessagesController in
 * isolation and ensures the controller surface is explicitly audited.
 */

async function loginAs(request: APIRequestContext, baseURL: string | undefined, user: typeof SEED.user1) {
  const resp = await request.post(`${baseURL}/api/auth/login`, {
    data: { email: user.email, password: user.password },
  })
  const body = await resp.json()
  return { token: body.accessToken as string, userId: body.user.id as string }
}

/** Opens or reuses the direct conversation between user1 and user2. */
async function getDirectConversation(
  request: APIRequestContext,
  baseURL: string | undefined,
  token1: string,
  userId2: string,
) {
  const resp = await request.post(`${baseURL}/api/conversations/direct/${userId2}`, {
    headers: { Authorization: `Bearer ${token1}` },
  })
  const body = await resp.json()
  return body.id as string
}

// ── POST /api/messages ────────────────────────────────────────────────────────

test.describe('POST /api/messages', () => {
  test('sends a text message to a direct conversation', async ({ request, baseURL }) => {
    const { token: token1 } = await loginAs(request, baseURL, SEED.user1)
    const { userId: userId2 } = await loginAs(request, baseURL, SEED.user2)
    const convId = await getDirectConversation(request, baseURL, token1, userId2)

    const resp = await request.post(`${baseURL}/api/messages`, {
      headers: { Authorization: `Bearer ${token1}` },
      multipart: { conversationId: convId, content: 'Edge case message test' },
    })
    expect([200, 201]).toContain(resp.status())
    const body = await resp.json()
    expect(body.id).toBeTruthy()
    expect(body.content).toBe('Edge case message test')
  })

  test('returns 400 when conversationId is missing', async ({ request, baseURL }) => {
    const { token } = await loginAs(request, baseURL, SEED.user1)
    const resp = await request.post(`${baseURL}/api/messages`, {
      headers: { Authorization: `Bearer ${token}` },
      multipart: { content: 'No conv' },
    })
    expect(resp.status()).toBe(400)
  })

  test('returns 400 when conversationId is not a valid GUID', async ({ request, baseURL }) => {
    const { token } = await loginAs(request, baseURL, SEED.user1)
    const resp = await request.post(`${baseURL}/api/messages`, {
      headers: { Authorization: `Bearer ${token}` },
      multipart: { conversationId: 'not-a-guid', content: 'Bad conv' },
    })
    expect(resp.status()).toBe(400)
  })

  test('returns 403 when the sender is not a participant of the conversation', async ({
    request,
    baseURL,
  }) => {
    // Create a conversation between user1 and user2
    const { token: token1 } = await loginAs(request, baseURL, SEED.user1)
    const { userId: userId2 } = await loginAs(request, baseURL, SEED.user2)
    const convId = await getDirectConversation(request, baseURL, token1, userId2)

    // User3 tries to send a message to that conversation
    const { token: token3 } = await loginAs(request, baseURL, SEED.user3)
    const resp = await request.post(`${baseURL}/api/messages`, {
      headers: { Authorization: `Bearer ${token3}` },
      multipart: { conversationId: convId, content: 'Intruder message' },
    })
    expect([403, 404]).toContain(resp.status())
  })

  test('returns 401 without auth', async ({ request, baseURL }) => {
    const resp = await request.post(`${baseURL}/api/messages`, {
      multipart: {
        conversationId: '00000000-0000-0000-0000-000000000000',
        content: 'Unauth',
      },
    })
    expect(resp.status()).toBe(401)
  })
})

// ── DELETE /api/messages/{id} ─────────────────────────────────────────────────

test.describe('DELETE /api/messages/:id', () => {
  test('sender can soft-delete their own message', async ({ request, baseURL }) => {
    const { token: token1 } = await loginAs(request, baseURL, SEED.user1)
    const { userId: userId2 } = await loginAs(request, baseURL, SEED.user2)
    const convId = await getDirectConversation(request, baseURL, token1, userId2)

    const sendResp = await request.post(`${baseURL}/api/messages`, {
      headers: { Authorization: `Bearer ${token1}` },
      multipart: { conversationId: convId, content: 'Message to delete' },
    })
    const msg = await sendResp.json()

    const deleteResp = await request.delete(`${baseURL}/api/messages/${msg.id}`, {
      headers: { Authorization: `Bearer ${token1}` },
    })
    expect([200, 204]).toContain(deleteResp.status())
  })

  test('returns 404 for a non-existent message ID', async ({ request, baseURL }) => {
    const { token } = await loginAs(request, baseURL, SEED.user1)
    const resp = await request.delete(
      `${baseURL}/api/messages/00000000-0000-0000-0000-000000000000`,
      { headers: { Authorization: `Bearer ${token}` } },
    )
    expect(resp.status()).toBe(404)
  })

  test('returns 403 when a non-sender tries to delete the message', async ({
    request,
    baseURL,
  }) => {
    const { token: token1 } = await loginAs(request, baseURL, SEED.user1)
    const { token: token2, userId: userId2 } = await loginAs(request, baseURL, SEED.user2)
    const convId = await getDirectConversation(request, baseURL, token1, userId2)

    const sendResp = await request.post(`${baseURL}/api/messages`, {
      headers: { Authorization: `Bearer ${token1}` },
      multipart: { conversationId: convId, content: 'Protected message' },
    })
    const msg = await sendResp.json()

    const deleteResp = await request.delete(`${baseURL}/api/messages/${msg.id}`, {
      headers: { Authorization: `Bearer ${token2}` },
    })
    expect([403, 404]).toContain(deleteResp.status())
  })

  test('returns 401 without auth', async ({ request, baseURL }) => {
    const resp = await request.delete(
      `${baseURL}/api/messages/00000000-0000-0000-0000-000000000000`,
    )
    expect(resp.status()).toBe(401)
  })
})
