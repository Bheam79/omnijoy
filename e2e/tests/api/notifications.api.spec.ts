import { test, expect, type APIRequestContext } from '../../support/fixtures'
import { SEED } from '../../fixtures/seed-data'

/**
 * API E2E tests for /api/notifications/* endpoints.
 */

async function loginAs(request: APIRequestContext, baseURL: string | undefined, user: typeof SEED.user1) {
  const resp = await request.post(`${baseURL}/api/auth/login`, {
    data: { email: user.email, password: user.password },
  })
  const body = await resp.json()
  return { token: body.accessToken as string, userId: body.user.id as string }
}

test.describe('GET /api/notifications', () => {
  test('returns paginated notifications', async ({ request, baseURL }) => {
    const { token } = await loginAs(request, baseURL, SEED.user1)
    const resp = await request.get(`${baseURL}/api/notifications`, {
      headers: { Authorization: `Bearer ${token}` },
    })
    expect(resp.status()).toBe(200)
    const body = await resp.json()
    expect(Array.isArray(body.items ?? body)).toBeTruthy()
  })

  test('supports pagination parameters', async ({ request, baseURL }) => {
    const { token } = await loginAs(request, baseURL, SEED.user1)
    const resp = await request.get(`${baseURL}/api/notifications?page=1&pageSize=5`, {
      headers: { Authorization: `Bearer ${token}` },
    })
    expect(resp.status()).toBe(200)
  })

  test('returns 401 without auth', async ({ request, baseURL }) => {
    const resp = await request.get(`${baseURL}/api/notifications`)
    expect(resp.status()).toBe(401)
  })
})

test.describe('GET /api/notifications/unread/count', () => {
  test('returns unread count', async ({ request, baseURL }) => {
    const { token } = await loginAs(request, baseURL, SEED.user1)
    const resp = await request.get(`${baseURL}/api/notifications/unread/count`, {
      headers: { Authorization: `Bearer ${token}` },
    })
    expect(resp.status()).toBe(200)
    const body = await resp.json()
    expect(typeof body.count).toBe('number')
    expect(body.count).toBeGreaterThanOrEqual(0)
  })

  test('returns 401 without auth', async ({ request, baseURL }) => {
    const resp = await request.get(`${baseURL}/api/notifications/unread/count`)
    expect(resp.status()).toBe(401)
  })
})

test.describe('POST /api/notifications/read-all', () => {
  test('marks all notifications as read', async ({ request, baseURL }) => {
    const { token } = await loginAs(request, baseURL, SEED.user1)
    const resp = await request.post(`${baseURL}/api/notifications/read-all`, {
      headers: { Authorization: `Bearer ${token}` },
    })
    expect(resp.status()).toBe(204)
  })

  test('unread count is 0 after mark-all-read', async ({ request, baseURL }) => {
    const { token } = await loginAs(request, baseURL, SEED.user1)
    await request.post(`${baseURL}/api/notifications/read-all`, {
      headers: { Authorization: `Bearer ${token}` },
    })
    const countResp = await request.get(`${baseURL}/api/notifications/unread/count`, {
      headers: { Authorization: `Bearer ${token}` },
    })
    const { count } = await countResp.json()
    expect(count).toBe(0)
  })

  test('returns 401 without auth', async ({ request, baseURL }) => {
    const resp = await request.post(`${baseURL}/api/notifications/read-all`)
    expect(resp.status()).toBe(401)
  })
})

test.describe('POST /api/notifications/:id/read', () => {
  test('returns 204 for a valid notification ID belonging to the user', async ({ request, baseURL }) => {
    const { token } = await loginAs(request, baseURL, SEED.user1)

    // Get a notification to mark as read
    const notifResp = await request.get(`${baseURL}/api/notifications?page=1&pageSize=1`, {
      headers: { Authorization: `Bearer ${token}` },
    })
    const body = await notifResp.json()
    const items = body.items ?? body
    if (items.length > 0) {
      const notifId = items[0].id
      const resp = await request.post(`${baseURL}/api/notifications/${notifId}/read`, {
        headers: { Authorization: `Bearer ${token}` },
      })
      expect(resp.status()).toBe(204)
    }
  })

  test('returns 401 without auth', async ({ request, baseURL }) => {
    const resp = await request.post(`${baseURL}/api/notifications/00000000-0000-0000-0000-000000000000/read`)
    expect(resp.status()).toBe(401)
  })
})
