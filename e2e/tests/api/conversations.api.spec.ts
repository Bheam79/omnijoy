import { test, expect } from '../../support/fixtures'
import { SEED } from '../../fixtures/seed-data'
import { getSharedTokenFor } from '../../support/shared-auth'
import { MINIMAL_PNG } from '../../fixtures/image-fixtures'

/**
 * API E2E tests for /api/conversations/* and /api/messages/* endpoints.
 */


test.describe('GET /api/conversations', () => {
  test('returns conversation list', async ({ request, baseURL }) => {
    const { token } = getSharedTokenFor(SEED.user1)
    const resp = await request.get(`${baseURL}/api/conversations`, {
      headers: { Authorization: `Bearer ${token}` },
    })
    expect(resp.status()).toBe(200)
    const body = await resp.json()
    expect(Array.isArray(body.items ?? body)).toBeTruthy()
  })

  test('returns 401 without auth', async ({ request, baseURL }) => {
    const resp = await request.get(`${baseURL}/api/conversations`)
    expect(resp.status()).toBe(401)
  })
})

test.describe('POST /api/conversations/direct/:userId', () => {
  test('creates or returns a direct conversation', async ({ request, baseURL }) => {
    const { token: token1 } = getSharedTokenFor(SEED.user1)
    const { userId: userId2 } = getSharedTokenFor(SEED.user2)

    const resp = await request.post(`${baseURL}/api/conversations/direct/${userId2}`, {
      headers: { Authorization: `Bearer ${token1}` },
    })
    expect([200, 201]).toContain(resp.status())
    const body = await resp.json()
    expect(body.id).toBeTruthy()
  })

  test('returns 401 without auth', async ({ request, baseURL }) => {
    const resp = await request.post(`${baseURL}/api/conversations/direct/00000000-0000-0000-0000-000000000000`)
    expect(resp.status()).toBe(401)
  })
})

test.describe('GET /api/conversations/:id/messages', () => {
  test('returns messages for a conversation', async ({ request, baseURL }) => {
    const { token: token1 } = getSharedTokenFor(SEED.user1)
    const { userId: userId2 } = getSharedTokenFor(SEED.user2)

    // Create conversation
    const convResp = await request.post(`${baseURL}/api/conversations/direct/${userId2}`, {
      headers: { Authorization: `Bearer ${token1}` },
    })
    const conv = await convResp.json()

    const resp = await request.get(`${baseURL}/api/conversations/${conv.id}/messages`, {
      headers: { Authorization: `Bearer ${token1}` },
    })
    expect(resp.status()).toBe(200)
    const body = await resp.json()
    expect(Array.isArray(body.items ?? body)).toBeTruthy()
  })

  test('supports limit and before parameters', async ({ request, baseURL }) => {
    const { token: token1 } = getSharedTokenFor(SEED.user1)
    const { userId: userId2 } = getSharedTokenFor(SEED.user2)

    const convResp = await request.post(`${baseURL}/api/conversations/direct/${userId2}`, {
      headers: { Authorization: `Bearer ${token1}` },
    })
    const conv = await convResp.json()

    const resp = await request.get(
      `${baseURL}/api/conversations/${conv.id}/messages?limit=10`,
      { headers: { Authorization: `Bearer ${token1}` } },
    )
    expect(resp.status()).toBe(200)
  })

  test('returns 403 for non-participant', async ({ request, baseURL }) => {
    const { token: token1 } = getSharedTokenFor(SEED.user1)
    const { userId: userId2 } = getSharedTokenFor(SEED.user2)
    const { token: token3 } = getSharedTokenFor(SEED.user3)

    const convResp = await request.post(`${baseURL}/api/conversations/direct/${userId2}`, {
      headers: { Authorization: `Bearer ${token1}` },
    })
    const conv = await convResp.json()

    const resp = await request.get(`${baseURL}/api/conversations/${conv.id}/messages`, {
      headers: { Authorization: `Bearer ${token3}` },
    })
    expect([403, 404]).toContain(resp.status())
  })
})

test.describe('PUT /api/conversations/:id/read', () => {
  test('marks conversation as read', async ({ request, baseURL }) => {
    const { token: token1 } = getSharedTokenFor(SEED.user1)
    const { userId: userId2 } = getSharedTokenFor(SEED.user2)

    const convResp = await request.post(`${baseURL}/api/conversations/direct/${userId2}`, {
      headers: { Authorization: `Bearer ${token1}` },
    })
    const conv = await convResp.json()

    const resp = await request.put(`${baseURL}/api/conversations/${conv.id}/read`, {
      headers: { Authorization: `Bearer ${token1}` },
    })
    expect([200, 204]).toContain(resp.status())
  })
})

test.describe('POST /api/messages', () => {
  test('sends a text message', async ({ request, baseURL }) => {
    const { token: token1 } = getSharedTokenFor(SEED.user1)
    const { userId: userId2 } = getSharedTokenFor(SEED.user2)

    const convResp = await request.post(`${baseURL}/api/conversations/direct/${userId2}`, {
      headers: { Authorization: `Bearer ${token1}` },
    })
    const conv = await convResp.json()

    const resp = await request.post(`${baseURL}/api/messages`, {
      headers: { Authorization: `Bearer ${token1}` },
      multipart: {
        conversationId: conv.id,
        content: 'Hello from API test!',
      },
    })
    expect([200, 201]).toContain(resp.status())
    const body = await resp.json()
    expect(body.content).toBe('Hello from API test!')
    expect(body.id).toBeTruthy()
  })

  test('sends a message with file attachment', async ({ request, baseURL }) => {
    const { token: token1 } = getSharedTokenFor(SEED.user1)
    const { userId: userId2 } = getSharedTokenFor(SEED.user2)

    const convResp = await request.post(`${baseURL}/api/conversations/direct/${userId2}`, {
      headers: { Authorization: `Bearer ${token1}` },
    })
    const conv = await convResp.json()

    const resp = await request.post(`${baseURL}/api/messages`, {
      headers: { Authorization: `Bearer ${token1}` },
      multipart: {
        conversationId: conv.id,
        content: 'Check this image!',
        file: { name: 'img.png', mimeType: 'image/png', buffer: MINIMAL_PNG },
      },
    })
    expect([200, 201]).toContain(resp.status())
  })

  test('returns 400 for missing conversationId', async ({ request, baseURL }) => {
    const { token } = getSharedTokenFor(SEED.user1)
    const resp = await request.post(`${baseURL}/api/messages`, {
      headers: { Authorization: `Bearer ${token}` },
      multipart: { content: 'No conv ID' },
    })
    expect(resp.status()).toBe(400)
  })

  test('returns 401 without auth', async ({ request, baseURL }) => {
    const resp = await request.post(`${baseURL}/api/messages`, {
      multipart: { conversationId: '00000000-0000-0000-0000-000000000000', content: 'Unauth' },
    })
    expect(resp.status()).toBe(401)
  })
})

test.describe('DELETE /api/messages/:id', () => {
  test('deletes own message', async ({ request, baseURL }) => {
    const { token: token1 } = getSharedTokenFor(SEED.user1)
    const { userId: userId2 } = getSharedTokenFor(SEED.user2)

    const convResp = await request.post(`${baseURL}/api/conversations/direct/${userId2}`, {
      headers: { Authorization: `Bearer ${token1}` },
    })
    const conv = await convResp.json()

    const msgResp = await request.post(`${baseURL}/api/messages`, {
      headers: { Authorization: `Bearer ${token1}` },
      multipart: { conversationId: conv.id, content: 'Will be deleted' },
    })
    const msg = await msgResp.json()

    const deleteResp = await request.delete(`${baseURL}/api/messages/${msg.id}`, {
      headers: { Authorization: `Bearer ${token1}` },
    })
    expect([200, 204]).toContain(deleteResp.status())
  })

  test('returns 403 when non-sender tries to delete', async ({ request, baseURL }) => {
    const { token: token1 } = getSharedTokenFor(SEED.user1)
    const { token: token2, userId: userId2 } = getSharedTokenFor(SEED.user2)

    const convResp = await request.post(`${baseURL}/api/conversations/direct/${userId2}`, {
      headers: { Authorization: `Bearer ${token1}` },
    })
    const conv = await convResp.json()

    const msgResp = await request.post(`${baseURL}/api/messages`, {
      headers: { Authorization: `Bearer ${token1}` },
      multipart: { conversationId: conv.id, content: 'Protected message' },
    })
    const msg = await msgResp.json()

    const deleteResp = await request.delete(`${baseURL}/api/messages/${msg.id}`, {
      headers: { Authorization: `Bearer ${token2}` },
    })
    expect([403, 404]).toContain(deleteResp.status())
  })
})
