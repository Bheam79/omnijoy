import { test, expect, type APIRequestContext } from '../../support/fixtures'
import { SEED } from '../../fixtures/seed-data'

/**
 * API E2E tests for /share/* endpoints (OG meta tag rendering).
 * These are server-rendered HTML responses.
 */

async function loginAs(request: APIRequestContext, baseURL: string | undefined, user: typeof SEED.user1) {
  const resp = await request.post(`${baseURL}/api/auth/login`, {
    data: { email: user.email, password: user.password },
  })
  const body = await resp.json()
  return { token: body.accessToken as string, userId: body.user.id as string }
}

test.describe('GET /share/posts/:id', () => {
  test('returns 200 for an unknown post ID with "not found" content', async ({ request, baseURL }) => {
    const resp = await request.get(`${baseURL}/share/posts/00000000-0000-0000-0000-000000000000`)
    expect(resp.status()).toBe(200)
    const html = await resp.text()
    expect(html).toContain('og:title')
  })

  test('returns OG meta tags for a public post', async ({ request, baseURL }) => {
    const { token } = await loginAs(request, baseURL, SEED.user1)
    const postResp = await request.post(`${baseURL}/api/posts`, {
      headers: { Authorization: `Bearer ${token}` },
      multipart: {
        content: 'Share page E2E test post',
        postType: 'Text',
        privacy: 'Everyone',
      },
    })
    const post = await postResp.json()

    const resp = await request.get(`${baseURL}/share/posts/${post.id}`)
    expect(resp.status()).toBe(200)
    const html = await resp.text()
    expect(html).toContain('og:title')
    expect(html).toContain('og:description')
    expect(html).toContain('og:type')
    expect(html).toContain('twitter:card')
    expect(html).toContain('summary_large_image')
    // Content should reflect the post
    expect(html.toLowerCase()).toContain('share page e2e test post')
  })

  test('includes twitter:card for private post', async ({ request, baseURL }) => {
    const { token } = await loginAs(request, baseURL, SEED.user1)
    const postResp = await request.post(`${baseURL}/api/posts`, {
      headers: { Authorization: `Bearer ${token}` },
      multipart: {
        content: 'Private share test post',
        postType: 'Text',
        privacy: 'OnlyMe',
      },
    })
    const post = await postResp.json()

    const resp = await request.get(`${baseURL}/share/posts/${post.id}`)
    expect(resp.status()).toBe(200)
    const html = await resp.text()
    expect(html).toContain('og:title')
    expect(html.toLowerCase()).toContain('private')
  })
})

test.describe('GET /share/users/:id', () => {
  test('returns OG meta for a valid user', async ({ request, baseURL }) => {
    const { userId } = await loginAs(request, baseURL, SEED.user1)

    const resp = await request.get(`${baseURL}/share/users/${userId}`)
    expect(resp.status()).toBe(200)
    const html = await resp.text()
    expect(html).toContain('og:title')
  })

  test('returns 200 for unknown user ID with "not found" content', async ({ request, baseURL }) => {
    const resp = await request.get(`${baseURL}/share/users/00000000-0000-0000-0000-000000000000`)
    expect(resp.status()).toBe(200)
    const html = await resp.text()
    expect(html).toContain('og:title')
  })

  test('sets og:type to profile', async ({ request, baseURL }) => {
    const { userId } = await loginAs(request, baseURL, SEED.user1)
    const resp = await request.get(`${baseURL}/share/users/${userId}`)
    const html = await resp.text()
    expect(html).toContain('og:type')
    expect(html).toContain('profile')
  })
})

test.describe('GET /share/events/:id', () => {
  test('returns OG meta for a valid event', async ({ request, baseURL }) => {
    const { token } = await loginAs(request, baseURL, SEED.user1)
    const createResp = await request.post(`${baseURL}/api/events`, {
      headers: { Authorization: `Bearer ${token}` },
      multipart: {
        title: 'Share Page Event Test',
        startAt: new Date(Date.now() + 86400000).toISOString(),
        privacy: 'Everyone',
      },
    })
    const event = await createResp.json()

    const resp = await request.get(`${baseURL}/share/events/${event.id}`)
    expect(resp.status()).toBe(200)
    const html = await resp.text()
    expect(html).toContain('og:title')
    expect(html).toContain('Share Page Event Test')
    expect(html).toContain('og:type')
  })

  test('returns 200 for unknown event ID', async ({ request, baseURL }) => {
    const resp = await request.get(`${baseURL}/share/events/00000000-0000-0000-0000-000000000000`)
    expect(resp.status()).toBe(200)
    const html = await resp.text()
    expect(html).toContain('og:title')
  })
})
