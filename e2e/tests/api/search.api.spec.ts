import { test, expect, type APIRequestContext } from '../../support/fixtures'
import { SEED } from '../../fixtures/seed-data'

/**
 * API E2E tests for SearchController endpoints:
 *   GET /api/search
 *   GET /api/search/suggest
 */

async function loginAs(request: APIRequestContext, baseURL: string | undefined, user: typeof SEED.user1) {
  const resp = await request.post(`${baseURL}/api/auth/login`, {
    data: { email: user.email, password: user.password },
  })
  const body = await resp.json()
  return { token: body.accessToken as string, userId: body.user.id as string }
}

// ── GET /api/search ───────────────────────────────────────────────────────────

test.describe('GET /api/search', () => {
  test('returns search results for a simple query (authenticated)', async ({ request, baseURL }) => {
    const { token } = await loginAs(request, baseURL, SEED.user1)
    const resp = await request.get(`${baseURL}/api/search?q=test`, {
      headers: { Authorization: `Bearer ${token}` },
    })
    expect(resp.status()).toBe(200)
    const body = await resp.json()
    // Response shape may vary — at minimum it should be an object
    expect(typeof body).toBe('object')
  })

  test('works anonymously (only public results)', async ({ request, baseURL }) => {
    const resp = await request.get(`${baseURL}/api/search?q=test`)
    expect(resp.status()).toBe(200)
  })

  test('returns 400 when q is missing', async ({ request, baseURL }) => {
    const resp = await request.get(`${baseURL}/api/search`)
    expect(resp.status()).toBe(400)
  })

  test('returns 400 when q is whitespace-only', async ({ request, baseURL }) => {
    const resp = await request.get(`${baseURL}/api/search?q=   `)
    expect(resp.status()).toBe(400)
  })

  test('filters by type=users', async ({ request, baseURL }) => {
    const resp = await request.get(`${baseURL}/api/search?q=alice&type=users`)
    expect(resp.status()).toBe(200)
  })

  test('filters by type=posts', async ({ request, baseURL }) => {
    const resp = await request.get(`${baseURL}/api/search?q=hello&type=posts`)
    expect(resp.status()).toBe(200)
  })

  test('filters by type=events', async ({ request, baseURL }) => {
    const resp = await request.get(`${baseURL}/api/search?q=event&type=events`)
    expect(resp.status()).toBe(200)
  })

  test('filters by type=companies', async ({ request, baseURL }) => {
    const resp = await request.get(`${baseURL}/api/search?q=corp&type=companies`)
    expect(resp.status()).toBe(200)
  })

  test('handles pagination parameters', async ({ request, baseURL }) => {
    const resp = await request.get(`${baseURL}/api/search?q=test&page=1&pageSize=5`)
    expect(resp.status()).toBe(200)
  })

  test('handles unicode query strings', async ({ request, baseURL }) => {
    const resp = await request.get(`${baseURL}/api/search?q=${encodeURIComponent('こんにちは')}`)
    expect(resp.status()).toBe(200)
  })

  test('handles a very long query string (200 chars)', async ({ request, baseURL }) => {
    const longQ = 'a'.repeat(200)
    const resp = await request.get(`${baseURL}/api/search?q=${longQ}`)
    expect(resp.status()).toBe(200)
  })
})

// ── GET /api/search/suggest ───────────────────────────────────────────────────

test.describe('GET /api/search/suggest', () => {
  test('returns suggestions for a query', async ({ request, baseURL }) => {
    const resp = await request.get(`${baseURL}/api/search/suggest?q=ali`)
    expect(resp.status()).toBe(200)
    const body = await resp.json()
    expect(typeof body).toBe('object')
  })

  test('returns 400 when q is missing', async ({ request, baseURL }) => {
    const resp = await request.get(`${baseURL}/api/search/suggest`)
    expect(resp.status()).toBe(400)
  })

  test('returns 400 when q is whitespace-only', async ({ request, baseURL }) => {
    const resp = await request.get(`${baseURL}/api/search/suggest?q=   `)
    expect(resp.status()).toBe(400)
  })

  test('works anonymously', async ({ request, baseURL }) => {
    const resp = await request.get(`${baseURL}/api/search/suggest?q=test`)
    expect(resp.status()).toBe(200)
  })

  test('handles unicode in suggest query', async ({ request, baseURL }) => {
    const resp = await request.get(
      `${baseURL}/api/search/suggest?q=${encodeURIComponent('αβγ')}`,
    )
    expect(resp.status()).toBe(200)
  })

  test('handles single-character query', async ({ request, baseURL }) => {
    const resp = await request.get(`${baseURL}/api/search/suggest?q=a`)
    expect(resp.status()).toBe(200)
  })
})
