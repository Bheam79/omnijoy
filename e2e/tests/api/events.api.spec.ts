import { test, expect, type APIRequestContext } from '@playwright/test'
import { SEED } from '../../fixtures/seed-data'

/**
 * API E2E tests for /api/events/* endpoints.
 */

async function loginAs(request: APIRequestContext, baseURL: string | undefined, user: typeof SEED.user1) {
  const resp = await request.post(`${baseURL}/api/auth/login`, {
    data: { email: user.email, password: user.password },
  })
  const body = await resp.json()
  return { token: body.accessToken as string, userId: body.user.id as string }
}

test.describe('POST /api/events', () => {
  test('creates an event successfully', async ({ request, baseURL }) => {
    const { token } = await loginAs(request, baseURL, SEED.user1)
    const resp = await request.post(`${baseURL}/api/events`, {
      headers: { Authorization: `Bearer ${token}` },
      multipart: {
        title: 'API Test Event',
        description: 'Created by E2E API test',
        startAt: new Date(Date.now() + 86400000).toISOString(),
        privacy: 'Everyone',
        location: 'Test Arena',
      },
    })
    expect([200, 201]).toContain(resp.status())
    const body = await resp.json()
    expect(body.id).toBeTruthy()
    expect(body.title).toBe('API Test Event')
  })

  test('creates an event with cover image', async ({ request, baseURL }) => {
    const { token } = await loginAs(request, baseURL, SEED.user1)
    const pngBytes = Buffer.from(
      '89504e470d0a1a0a0000000d49484452000000010000000108020000009001' +
      '2e00000000c4944415478016360f8ff00000200014040c340000000049454e44ae426082',
      'hex',
    )
    const resp = await request.post(`${baseURL}/api/events`, {
      headers: { Authorization: `Bearer ${token}` },
      multipart: {
        title: 'Event with Cover',
        startAt: new Date(Date.now() + 172800000).toISOString(),
        privacy: 'Everyone',
        coverImage: {
          name: 'cover.png',
          mimeType: 'image/png',
          buffer: pngBytes,
        },
      },
    })
    expect([200, 201]).toContain(resp.status())
  })

  test('returns 401 without auth', async ({ request, baseURL }) => {
    const resp = await request.post(`${baseURL}/api/events`, {
      multipart: { title: 'Unauthorized', startAt: new Date().toISOString() },
    })
    expect(resp.status()).toBe(401)
  })
})

test.describe('GET /api/events', () => {
  test('returns list of events', async ({ request, baseURL }) => {
    const { token } = await loginAs(request, baseURL, SEED.user1)
    const resp = await request.get(`${baseURL}/api/events`, {
      headers: { Authorization: `Bearer ${token}` },
    })
    expect(resp.status()).toBe(200)
    const body = await resp.json()
    expect(Array.isArray(body.items ?? body)).toBeTruthy()
  })

  test('supports filter=mine parameter', async ({ request, baseURL }) => {
    const { token } = await loginAs(request, baseURL, SEED.user1)
    const resp = await request.get(`${baseURL}/api/events?filter=mine`, {
      headers: { Authorization: `Bearer ${token}` },
    })
    expect(resp.status()).toBe(200)
  })

  test('returns 401 without auth', async ({ request, baseURL }) => {
    const resp = await request.get(`${baseURL}/api/events`)
    expect(resp.status()).toBe(401)
  })
})

test.describe('GET /api/events/:id', () => {
  test('returns an event by ID', async ({ request, baseURL }) => {
    const { token } = await loginAs(request, baseURL, SEED.user1)
    const createResp = await request.post(`${baseURL}/api/events`, {
      headers: { Authorization: `Bearer ${token}` },
      multipart: {
        title: 'Get By ID Event',
        startAt: new Date(Date.now() + 86400000).toISOString(),
        privacy: 'Everyone',
      },
    })
    const event = await createResp.json()

    const resp = await request.get(`${baseURL}/api/events/${event.id}`, {
      headers: { Authorization: `Bearer ${token}` },
    })
    expect(resp.status()).toBe(200)
    const body = await resp.json()
    expect(body.title).toBe('Get By ID Event')
  })

  test('returns 404 for unknown event ID', async ({ request, baseURL }) => {
    const { token } = await loginAs(request, baseURL, SEED.user1)
    const resp = await request.get(`${baseURL}/api/events/00000000-0000-0000-0000-000000000000`, {
      headers: { Authorization: `Bearer ${token}` },
    })
    expect(resp.status()).toBe(404)
  })
})

test.describe('PUT /api/events/:id', () => {
  test('updates event title', async ({ request, baseURL }) => {
    const { token } = await loginAs(request, baseURL, SEED.user1)
    const createResp = await request.post(`${baseURL}/api/events`, {
      headers: { Authorization: `Bearer ${token}` },
      multipart: { title: 'Original Title', startAt: new Date(Date.now() + 86400000).toISOString(), privacy: 'Everyone' },
    })
    const event = await createResp.json()

    const updateResp = await request.put(`${baseURL}/api/events/${event.id}`, {
      headers: { Authorization: `Bearer ${token}` },
      multipart: { title: 'Updated Title' },
    })
    expect([200, 201]).toContain(updateResp.status())
    const updated = await updateResp.json()
    expect(updated.title).toBe('Updated Title')
  })

  test('returns 403 when non-owner tries to update', async ({ request, baseURL }) => {
    const { token: token1 } = await loginAs(request, baseURL, SEED.user1)
    const createResp = await request.post(`${baseURL}/api/events`, {
      headers: { Authorization: `Bearer ${token1}` },
      multipart: { title: 'User1 Event', startAt: new Date(Date.now() + 86400000).toISOString(), privacy: 'Everyone' },
    })
    const event = await createResp.json()

    const { token: token2 } = await loginAs(request, baseURL, SEED.user2)
    const updateResp = await request.put(`${baseURL}/api/events/${event.id}`, {
      headers: { Authorization: `Bearer ${token2}` },
      multipart: { title: 'Hacked Title' },
    })
    expect([403, 404]).toContain(updateResp.status())
  })
})

test.describe('DELETE /api/events/:id', () => {
  test('deletes own event', async ({ request, baseURL }) => {
    const { token } = await loginAs(request, baseURL, SEED.user1)
    const createResp = await request.post(`${baseURL}/api/events`, {
      headers: { Authorization: `Bearer ${token}` },
      multipart: { title: 'To Be Deleted', startAt: new Date(Date.now() + 86400000).toISOString(), privacy: 'Everyone' },
    })
    const event = await createResp.json()

    const deleteResp = await request.delete(`${baseURL}/api/events/${event.id}`, {
      headers: { Authorization: `Bearer ${token}` },
    })
    expect([200, 204]).toContain(deleteResp.status())
  })
})

test.describe('POST /api/events/:id/rsvp', () => {
  test('RSVP Going to an event', async ({ request, baseURL }) => {
    const { token: token1 } = await loginAs(request, baseURL, SEED.user1)
    const { token: token2 } = await loginAs(request, baseURL, SEED.user2)
    const createResp = await request.post(`${baseURL}/api/events`, {
      headers: { Authorization: `Bearer ${token1}` },
      multipart: { title: 'RSVP Test Event', startAt: new Date(Date.now() + 86400000).toISOString(), privacy: 'Everyone' },
    })
    const event = await createResp.json()

    const rsvpResp = await request.post(`${baseURL}/api/events/${event.id}/rsvp`, {
      headers: { Authorization: `Bearer ${token2}` },
      data: { status: 'Going' },
    })
    expect([200, 201]).toContain(rsvpResp.status())
  })

  test('RSVP Maybe to an event', async ({ request, baseURL }) => {
    const { token: token1 } = await loginAs(request, baseURL, SEED.user1)
    const { token: token2 } = await loginAs(request, baseURL, SEED.user2)
    const createResp = await request.post(`${baseURL}/api/events`, {
      headers: { Authorization: `Bearer ${token1}` },
      multipart: { title: 'Maybe Test Event', startAt: new Date(Date.now() + 86400000).toISOString(), privacy: 'Everyone' },
    })
    const event = await createResp.json()

    const rsvpResp = await request.post(`${baseURL}/api/events/${event.id}/rsvp`, {
      headers: { Authorization: `Bearer ${token2}` },
      data: { status: 'Maybe' },
    })
    expect([200, 201]).toContain(rsvpResp.status())
  })

  test('returns 400 for invalid RSVP status', async ({ request, baseURL }) => {
    const { token } = await loginAs(request, baseURL, SEED.user1)
    const createResp = await request.post(`${baseURL}/api/events`, {
      headers: { Authorization: `Bearer ${token}` },
      multipart: { title: 'Invalid RSVP Event', startAt: new Date(Date.now() + 86400000).toISOString(), privacy: 'Everyone' },
    })
    const event = await createResp.json()

    const rsvpResp = await request.post(`${baseURL}/api/events/${event.id}/rsvp`, {
      headers: { Authorization: `Bearer ${token}` },
      data: { status: 'NotAValidStatus' },
    })
    expect([400, 422]).toContain(rsvpResp.status())
  })
})

test.describe('GET /api/events/:id/attendees', () => {
  test('returns attendee list', async ({ request, baseURL }) => {
    const { token } = await loginAs(request, baseURL, SEED.user1)
    const createResp = await request.post(`${baseURL}/api/events`, {
      headers: { Authorization: `Bearer ${token}` },
      multipart: { title: 'Attendees Test', startAt: new Date(Date.now() + 86400000).toISOString(), privacy: 'Everyone' },
    })
    const event = await createResp.json()

    const resp = await request.get(`${baseURL}/api/events/${event.id}/attendees`, {
      headers: { Authorization: `Bearer ${token}` },
    })
    expect(resp.status()).toBe(200)
    const body = await resp.json()
    // Response shape: { going: [...], maybe: [...], notGoing: [...] } or an array/items
    const hasExpectedShape =
      Array.isArray(body.going) ||
      Array.isArray(body.items ?? body)
    expect(hasExpectedShape).toBeTruthy()
  })
})
