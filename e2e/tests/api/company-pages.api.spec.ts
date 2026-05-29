import { test, expect } from '../../support/fixtures'
import { SEED } from '../../fixtures/seed-data'
import { getSharedTokenFor } from '../../support/shared-auth'

/**
 * API E2E tests for /api/company-pages/* endpoints.
 */


const RUN_ID = Date.now().toString(36)

test.describe('POST /api/company-pages', () => {
  test('creates a company page', async ({ request, baseURL }) => {
    const { token } = getSharedTokenFor(SEED.user1)
    const resp = await request.post(`${baseURL}/api/company-pages`, {
      headers: { Authorization: `Bearer ${token}` },
      multipart: {
        name: `E2E Company ${RUN_ID}`,
        description: 'Company created by E2E API tests',
      },
    })
    expect([200, 201]).toContain(resp.status())
    const body = await resp.json()
    expect(body.id).toBeTruthy()
    expect(body.name).toContain('E2E Company')
  })

  test('returns 400 for missing name', async ({ request, baseURL }) => {
    const { token } = getSharedTokenFor(SEED.user1)
    const resp = await request.post(`${baseURL}/api/company-pages`, {
      headers: { Authorization: `Bearer ${token}` },
      multipart: { description: 'No name provided' },
    })
    expect([400, 422]).toContain(resp.status())
  })

  test('returns 401 without auth', async ({ request, baseURL }) => {
    const resp = await request.post(`${baseURL}/api/company-pages`, {
      multipart: { name: 'Unauth Company' },
    })
    expect(resp.status()).toBe(401)
  })
})

test.describe('GET /api/company-pages', () => {
  test('returns paginated list of company pages', async ({ request, baseURL }) => {
    const { token } = getSharedTokenFor(SEED.user1)
    const resp = await request.get(`${baseURL}/api/company-pages`, {
      headers: { Authorization: `Bearer ${token}` },
    })
    expect(resp.status()).toBe(200)
    const body = await resp.json()
    expect(Array.isArray(body.items ?? body)).toBeTruthy()
  })

  test('supports mine=true parameter', async ({ request, baseURL }) => {
    const { token } = getSharedTokenFor(SEED.user1)
    const resp = await request.get(`${baseURL}/api/company-pages?mine=true`, {
      headers: { Authorization: `Bearer ${token}` },
    })
    expect(resp.status()).toBe(200)
  })
})

test.describe('GET /api/company-pages/mine', () => {
  test('returns pages where current user is admin', async ({ request, baseURL }) => {
    const { token } = getSharedTokenFor(SEED.user1)
    const resp = await request.get(`${baseURL}/api/company-pages/mine`, {
      headers: { Authorization: `Bearer ${token}` },
    })
    expect(resp.status()).toBe(200)
    const body = await resp.json()
    expect(Array.isArray(body.items ?? body)).toBeTruthy()
  })
})

test.describe('GET /api/company-pages/:id', () => {
  test('returns company page by ID', async ({ request, baseURL }) => {
    const { token } = getSharedTokenFor(SEED.user1)
    const createResp = await request.post(`${baseURL}/api/company-pages`, {
      headers: { Authorization: `Bearer ${token}` },
      multipart: { name: `GetById Co ${RUN_ID}` },
    })
    const page = await createResp.json()

    const resp = await request.get(`${baseURL}/api/company-pages/${page.id}`, {
      headers: { Authorization: `Bearer ${token}` },
    })
    expect(resp.status()).toBe(200)
    const body = await resp.json()
    expect(body.id).toBe(page.id)
  })

  test('returns 404 for unknown ID', async ({ request, baseURL }) => {
    const resp = await request.get(`${baseURL}/api/company-pages/00000000-0000-0000-0000-000000000000`)
    expect(resp.status()).toBe(404)
  })
})

test.describe('PUT /api/company-pages/:id', () => {
  test('updates page name and description', async ({ request, baseURL }) => {
    const { token } = getSharedTokenFor(SEED.user1)
    const createResp = await request.post(`${baseURL}/api/company-pages`, {
      headers: { Authorization: `Bearer ${token}` },
      multipart: { name: `Update Me ${RUN_ID}` },
    })
    const page = await createResp.json()

    const updateResp = await request.put(`${baseURL}/api/company-pages/${page.id}`, {
      headers: { Authorization: `Bearer ${token}` },
      multipart: { name: `Updated Co ${RUN_ID}`, description: 'New description' },
    })
    expect([200, 201]).toContain(updateResp.status())
    const updated = await updateResp.json()
    expect(updated.name).toContain('Updated Co')
  })

  test('returns 403 when non-admin tries to update', async ({ request, baseURL }) => {
    const { token: token1 } = getSharedTokenFor(SEED.user1)
    const { token: token2 } = getSharedTokenFor(SEED.user2)
    const createResp = await request.post(`${baseURL}/api/company-pages`, {
      headers: { Authorization: `Bearer ${token1}` },
      multipart: { name: `Protected Co ${RUN_ID}` },
    })
    const page = await createResp.json()

    const updateResp = await request.put(`${baseURL}/api/company-pages/${page.id}`, {
      headers: { Authorization: `Bearer ${token2}` },
      multipart: { name: 'Hacked' },
    })
    expect([403, 404]).toContain(updateResp.status())
  })
})

test.describe('Admin management', () => {
  test('get admins of a company page', async ({ request, baseURL }) => {
    const { token } = getSharedTokenFor(SEED.user1)
    const createResp = await request.post(`${baseURL}/api/company-pages`, {
      headers: { Authorization: `Bearer ${token}` },
      multipart: { name: `Admin Test Co ${RUN_ID}` },
    })
    const page = await createResp.json()

    const resp = await request.get(`${baseURL}/api/company-pages/${page.id}/admins`, {
      headers: { Authorization: `Bearer ${token}` },
    })
    expect(resp.status()).toBe(200)
    const body = await resp.json()
    // Response shape: { admins: [...] } or an array
    const list = body.admins ?? body.items ?? body
    expect(Array.isArray(list)).toBeTruthy()
  })

  test('add admin to a company page', async ({ request, baseURL }) => {
    const { token: token1 } = getSharedTokenFor(SEED.user1)
    const { userId: userId2 } = getSharedTokenFor(SEED.user2)

    const createResp = await request.post(`${baseURL}/api/company-pages`, {
      headers: { Authorization: `Bearer ${token1}` },
      multipart: { name: `Add Admin Co ${RUN_ID}` },
    })
    const page = await createResp.json()

    const addResp = await request.post(`${baseURL}/api/company-pages/${page.id}/admins`, {
      headers: { Authorization: `Bearer ${token1}` },
      data: { userId: userId2, role: 'Admin' },
    })
    expect([200, 201]).toContain(addResp.status())
  })

  test('remove admin from a company page', async ({ request, baseURL }) => {
    const { token: token1 } = getSharedTokenFor(SEED.user1)
    const { userId: userId2 } = getSharedTokenFor(SEED.user2)

    const createResp = await request.post(`${baseURL}/api/company-pages`, {
      headers: { Authorization: `Bearer ${token1}` },
      multipart: { name: `Remove Admin Co ${RUN_ID}` },
    })
    const page = await createResp.json()

    await request.post(`${baseURL}/api/company-pages/${page.id}/admins`, {
      headers: { Authorization: `Bearer ${token1}` },
      data: { userId: userId2, role: 'Admin' },
    })

    const removeResp = await request.delete(`${baseURL}/api/company-pages/${page.id}/admins/${userId2}`, {
      headers: { Authorization: `Bearer ${token1}` },
    })
    expect([200, 204]).toContain(removeResp.status())
  })
})

test.describe('Follow/Unfollow', () => {
  test('follow a company page', async ({ request, baseURL }) => {
    const { token: token1 } = getSharedTokenFor(SEED.user1)
    const { token: token2 } = getSharedTokenFor(SEED.user2)

    const createResp = await request.post(`${baseURL}/api/company-pages`, {
      headers: { Authorization: `Bearer ${token1}` },
      multipart: { name: `Follow Test Co ${RUN_ID}` },
    })
    const page = await createResp.json()

    const followResp = await request.post(`${baseURL}/api/company-pages/${page.id}/follow`, {
      headers: { Authorization: `Bearer ${token2}` },
    })
    expect([200, 201]).toContain(followResp.status())

    const unfollowResp = await request.delete(`${baseURL}/api/company-pages/${page.id}/follow`, {
      headers: { Authorization: `Bearer ${token2}` },
    })
    expect([200, 204]).toContain(unfollowResp.status())
  })
})
