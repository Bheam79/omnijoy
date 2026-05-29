import { test, expect, type APIRequestContext } from '../../support/fixtures'
import { SEED } from '../../fixtures/seed-data'

/**
 * API E2E tests for SlugsController endpoints:
 *   GET /api/slugs/check?slug=<value>
 *   GET /api/slugs/resolve/{slug}
 *
 * Authenticated set/clear endpoints (PUT /api/users/me/slug) live on
 * UsersController and are tested in users.api.spec.ts.
 */

async function loginAs(request: APIRequestContext, baseURL: string | undefined, user: typeof SEED.user1) {
  const resp = await request.post(`${baseURL}/api/auth/login`, {
    data: { email: user.email, password: user.password },
  })
  const body = await resp.json()
  return { token: body.accessToken as string, userId: body.user.id as string }
}

// ── GET /api/slugs/check ──────────────────────────────────────────────────────

test.describe('GET /api/slugs/check', () => {
  test('reports an available slug as available', async ({ request, baseURL }) => {
    // Use a unique value unlikely to be taken
    const slug = `available-slug-${Date.now()}`
    const resp = await request.get(`${baseURL}/api/slugs/check?slug=${slug}`)
    expect(resp.status()).toBe(200)
    const body = await resp.json()
    expect(body.available).toBe(true)
    expect(body.reason).toBeNull()
  })

  test('reports an invalid slug (too short)', async ({ request, baseURL }) => {
    const resp = await request.get(`${baseURL}/api/slugs/check?slug=ab`)
    expect(resp.status()).toBe(200)
    const body = await resp.json()
    expect(body.available).toBe(false)
    expect(body.reason).toBe('invalid')
  })

  test('reports an invalid slug (starts with digit)', async ({ request, baseURL }) => {
    const resp = await request.get(`${baseURL}/api/slugs/check?slug=1invalid`)
    expect(resp.status()).toBe(200)
    const body = await resp.json()
    expect(body.available).toBe(false)
    expect(body.reason).toBe('invalid')
  })

  test('reports an invalid slug (uppercase characters)', async ({ request, baseURL }) => {
    const resp = await request.get(`${baseURL}/api/slugs/check?slug=MySlug`)
    expect(resp.status()).toBe(200)
    const body = await resp.json()
    expect(body.available).toBe(false)
    expect(body.reason).toBe('invalid')
  })

  test('reports an invalid slug (special characters)', async ({ request, baseURL }) => {
    const resp = await request.get(`${baseURL}/api/slugs/check?slug=bad@slug`)
    expect(resp.status()).toBe(200)
    const body = await resp.json()
    expect(body.available).toBe(false)
    expect(body.reason).toBe('invalid')
  })

  test('reports a reserved slug as reserved', async ({ request, baseURL }) => {
    // "feed" is a known reserved slug (it's a top-level route)
    const resp = await request.get(`${baseURL}/api/slugs/check?slug=feed`)
    expect(resp.status()).toBe(200)
    const body = await resp.json()
    expect(body.available).toBe(false)
    expect(body.reason).toBe('reserved')
  })

  test('reports another reserved slug (settings)', async ({ request, baseURL }) => {
    const resp = await request.get(`${baseURL}/api/slugs/check?slug=settings`)
    expect(resp.status()).toBe(200)
    const body = await resp.json()
    expect(body.available).toBe(false)
    expect(body.reason).toBe('reserved')
  })

  test('reports a taken slug after assignment', async ({ request, baseURL }) => {
    const { token } = await loginAs(request, baseURL, SEED.user1)
    const slug = `taken-slug-${Date.now()}`

    // Assign the slug to user1
    const setResp = await request.put(`${baseURL}/api/users/me/slug`, {
      headers: { Authorization: `Bearer ${token}` },
      data: { slug },
    })
    // Only proceed if the set was successful
    if (setResp.status() === 200) {
      const checkResp = await request.get(`${baseURL}/api/slugs/check?slug=${slug}`)
      expect(checkResp.status()).toBe(200)
      const body = await checkResp.json()
      expect(body.available).toBe(false)
      expect(body.reason).toBe('taken')

      // Clean up — clear the slug
      await request.put(`${baseURL}/api/users/me/slug`, {
        headers: { Authorization: `Bearer ${token}` },
        data: { slug: null },
      })
    }
  })

  test('works anonymously', async ({ request, baseURL }) => {
    const resp = await request.get(`${baseURL}/api/slugs/check?slug=anon-check`)
    expect(resp.status()).toBe(200)
  })
})

// ── GET /api/slugs/resolve/{slug} ─────────────────────────────────────────────

test.describe('GET /api/slugs/resolve/:slug', () => {
  test('returns 404 for an unassigned slug', async ({ request, baseURL }) => {
    const resp = await request.get(
      `${baseURL}/api/slugs/resolve/this-slug-is-definitely-not-assigned-xyz`,
    )
    expect(resp.status()).toBe(404)
  })

  test('resolves a user slug after assignment', async ({ request, baseURL }) => {
    const { token } = await loginAs(request, baseURL, SEED.user2)
    const slug = `resolve-test-${Date.now()}`

    const setResp = await request.put(`${baseURL}/api/users/me/slug`, {
      headers: { Authorization: `Bearer ${token}` },
      data: { slug },
    })
    if (setResp.status() === 200) {
      const resolveResp = await request.get(`${baseURL}/api/slugs/resolve/${slug}`)
      expect(resolveResp.status()).toBe(200)
      const body = await resolveResp.json()
      expect(body.type).toBe('user')
      expect(body.id).toBeTruthy()

      // Clean up
      await request.put(`${baseURL}/api/users/me/slug`, {
        headers: { Authorization: `Bearer ${token}` },
        data: { slug: null },
      })
    }
  })

  test('is accessible anonymously', async ({ request, baseURL }) => {
    const resp = await request.get(
      `${baseURL}/api/slugs/resolve/definitely-not-found-slug-xyz`,
    )
    expect(resp.status()).toBe(404)
  })
})
