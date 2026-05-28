import { test, expect, type APIRequestContext } from '@playwright/test'
import { SEED } from '../../fixtures/seed-data'

/**
 * API E2E tests for /api/live/* endpoints.
 */

async function loginAs(request: APIRequestContext, baseURL: string | undefined, user: typeof SEED.user1) {
  const resp = await request.post(`${baseURL}/api/auth/login`, {
    data: { email: user.email, password: user.password },
  })
  const body = await resp.json()
  return { token: body.accessToken as string, userId: body.user.id as string }
}

test.describe('GET /api/live/active', () => {
  test('returns list of active streams', async ({ request, baseURL }) => {
    const { token } = await loginAs(request, baseURL, SEED.user1)
    const resp = await request.get(`${baseURL}/api/live/active`, {
      headers: { Authorization: `Bearer ${token}` },
    })
    expect(resp.status()).toBe(200)
    const body = await resp.json()
    expect(Array.isArray(body.items ?? body)).toBeTruthy()
  })

  test('returns 401 without auth', async ({ request, baseURL }) => {
    const resp = await request.get(`${baseURL}/api/live/active`)
    expect(resp.status()).toBe(401)
  })
})

test.describe('POST /api/live/start', () => {
  test('starts a live stream or returns service unavailable', async ({ request, baseURL }) => {
    const { token } = await loginAs(request, baseURL, SEED.user1)
    const resp = await request.post(`${baseURL}/api/live/start`, {
      headers: { Authorization: `Bearer ${token}` },
      data: {
        title: 'E2E Live Stream Test',
        description: 'Created by E2E tests',
        privacy: 'Everyone',
      },
    })
    // May succeed (200) or fail if MediaMTX is not running (503/400)
    expect([200, 201, 400, 503]).toContain(resp.status())
    if ([200, 201].includes(resp.status())) {
      const body = await resp.json()
      expect(body.id).toBeTruthy()
      expect(body.title).toBe('E2E Live Stream Test')

      // End the stream
      const endResp = await request.post(`${baseURL}/api/live/${body.id}/end`, {
        headers: { Authorization: `Bearer ${token}` },
      })
      expect([200, 204]).toContain(endResp.status())
    }
  })

  test('returns 401 without auth', async ({ request, baseURL }) => {
    const resp = await request.post(`${baseURL}/api/live/start`, {
      data: { title: 'Unauthorized stream' },
    })
    expect(resp.status()).toBe(401)
  })
})

test.describe('GET /api/live/:id', () => {
  test('returns 404 for unknown stream ID', async ({ request, baseURL }) => {
    const { token } = await loginAs(request, baseURL, SEED.user1)
    const resp = await request.get(`${baseURL}/api/live/00000000-0000-0000-0000-000000000000`, {
      headers: { Authorization: `Bearer ${token}` },
    })
    expect([404, 400]).toContain(resp.status())
  })

  test('returns 401 without auth', async ({ request, baseURL }) => {
    const resp = await request.get(`${baseURL}/api/live/00000000-0000-0000-0000-000000000000`)
    expect(resp.status()).toBe(401)
  })
})

test.describe('POST /api/live/:id/end', () => {
  test('returns 404 for unknown stream ID', async ({ request, baseURL }) => {
    const { token } = await loginAs(request, baseURL, SEED.user1)
    const resp = await request.post(`${baseURL}/api/live/00000000-0000-0000-0000-000000000000/end`, {
      headers: { Authorization: `Bearer ${token}` },
    })
    expect([404, 400]).toContain(resp.status())
  })

  test('returns 401 without auth', async ({ request, baseURL }) => {
    const resp = await request.post(`${baseURL}/api/live/00000000-0000-0000-0000-000000000000/end`)
    expect(resp.status()).toBe(401)
  })
})
