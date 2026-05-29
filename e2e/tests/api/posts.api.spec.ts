import { test, expect } from '../../support/fixtures'
import { SEED } from '../../fixtures/seed-data'
import { getSharedTokenFor } from '../../support/shared-auth'
import { MINIMAL_PNG } from '../../fixtures/image-fixtures'

/**
 * API E2E tests for /api/posts/* and /api/feed endpoints.
 */


test.describe('POST /api/posts (create post)', () => {
  test('creates a text post', async ({ request, baseURL }) => {
    const { token } = getSharedTokenFor(SEED.user1)
    const resp = await request.post(`${baseURL}/api/posts`, {
      headers: { Authorization: `Bearer ${token}` },
      multipart: {
        content: 'Hello from E2E tests!',
        postType: 'Text',
        privacy: 'Everyone',
      },
    })
    expect([200, 201]).toContain(resp.status())
    const body = await resp.json()
    expect(body.id).toBeTruthy()
    expect(body.content).toBe('Hello from E2E tests!')
  })

  test('creates a TextOnBackground post', async ({ request, baseURL }) => {
    const { token } = getSharedTokenFor(SEED.user1)
    const resp = await request.post(`${baseURL}/api/posts`, {
      headers: { Authorization: `Bearer ${token}` },
      multipart: {
        content: 'Colorful post!',
        postType: 'TextOnBackground',
        privacy: 'Friends',
        background: '#3B82F6',
      },
    })
    expect([200, 201]).toContain(resp.status())
    const body = await resp.json()
    expect(body.postType).toBe('TextOnBackground')
  })

  test('creates a post with image attachment', async ({ request, baseURL }) => {
    const { token } = getSharedTokenFor(SEED.user1)
    const resp = await request.post(`${baseURL}/api/posts`, {
      headers: { Authorization: `Bearer ${token}` },
      multipart: {
        content: 'Post with image',
        postType: 'Image',
        privacy: 'Everyone',
        media: {
          name: 'test-image.png',
          mimeType: 'image/png',
          buffer: MINIMAL_PNG,
        },
      },
    })
    expect([200, 201]).toContain(resp.status())
    const body = await resp.json()
    expect(body.media).toHaveLength(1)
    expect(body.media[0].url).toMatch(/\/media\/posts\/images\//)
  })

  test('creates a post with link preview metadata', async ({ request, baseURL }) => {
    const { token } = getSharedTokenFor(SEED.user1)
    const resp = await request.post(`${baseURL}/api/posts`, {
      headers: { Authorization: `Bearer ${token}` },
      multipart: {
        content: 'Check this link!',
        postType: 'Text',
        privacy: 'Everyone',
        linkUrl: 'https://example.com',
        linkTitle: 'Example Domain',
        linkDescription: 'An example site',
        linkSiteName: 'example.com',
      },
    })
    expect([200, 201]).toContain(resp.status())
    const body = await resp.json()
    // Link preview can be nested under linkPreview.url or directly as linkUrl
    const linkUrl = body.linkUrl ?? body.linkPreview?.url
    expect(linkUrl).toBe('https://example.com')
  })

  test('returns 401 without auth', async ({ request, baseURL }) => {
    const resp = await request.post(`${baseURL}/api/posts`, {
      multipart: { content: 'Unauthorized post', postType: 'Text', privacy: 'Everyone' },
    })
    expect(resp.status()).toBe(401)
  })
})

test.describe('GET /api/posts/:id', () => {
  test('returns a post by ID', async ({ request, baseURL }) => {
    const { token } = getSharedTokenFor(SEED.user1)
    // Create a post first
    const createResp = await request.post(`${baseURL}/api/posts`, {
      headers: { Authorization: `Bearer ${token}` },
      multipart: { content: 'Get by ID test', postType: 'Text', privacy: 'Everyone' },
    })
    const post = await createResp.json()

    const resp = await request.get(`${baseURL}/api/posts/${post.id}`, {
      headers: { Authorization: `Bearer ${token}` },
    })
    expect(resp.status()).toBe(200)
    const body = await resp.json()
    expect(body.id).toBe(post.id)
    expect(body.content).toBe('Get by ID test')
  })

  test('returns 404 for unknown post ID', async ({ request, baseURL }) => {
    const { token } = getSharedTokenFor(SEED.user1)
    const resp = await request.get(`${baseURL}/api/posts/00000000-0000-0000-0000-000000000000`, {
      headers: { Authorization: `Bearer ${token}` },
    })
    expect(resp.status()).toBe(404)
  })
})

test.describe('PUT /api/posts/:id', () => {
  test('updates a post content', async ({ request, baseURL }) => {
    const { token } = getSharedTokenFor(SEED.user1)
    const createResp = await request.post(`${baseURL}/api/posts`, {
      headers: { Authorization: `Bearer ${token}` },
      multipart: { content: 'Original content', postType: 'Text', privacy: 'Friends' },
    })
    const post = await createResp.json()

    const updateResp = await request.put(`${baseURL}/api/posts/${post.id}`, {
      headers: { Authorization: `Bearer ${token}` },
      data: { content: 'Updated content', privacy: 'Everyone' },
    })
    expect([200, 201]).toContain(updateResp.status())
    const updated = await updateResp.json()
    expect(updated.content).toBe('Updated content')
  })

  test('returns 403 when non-owner tries to update', async ({ request, baseURL }) => {
    const { token: token1 } = getSharedTokenFor(SEED.user1)
    const createResp = await request.post(`${baseURL}/api/posts`, {
      headers: { Authorization: `Bearer ${token1}` },
      multipart: { content: 'User1 post', postType: 'Text', privacy: 'Everyone' },
    })
    const post = await createResp.json()

    const { token: token2 } = getSharedTokenFor(SEED.user2)
    const updateResp = await request.put(`${baseURL}/api/posts/${post.id}`, {
      headers: { Authorization: `Bearer ${token2}` },
      data: { content: 'Hacked!' },
    })
    expect([403, 404]).toContain(updateResp.status())
  })

  test('returns 401 without auth', async ({ request, baseURL }) => {
    const resp = await request.put(`${baseURL}/api/posts/00000000-0000-0000-0000-000000000000`, {
      data: { content: 'test' },
    })
    expect(resp.status()).toBe(401)
  })
})

test.describe('DELETE /api/posts/:id', () => {
  test('deletes own post successfully', async ({ request, baseURL }) => {
    const { token } = getSharedTokenFor(SEED.user1)
    const createResp = await request.post(`${baseURL}/api/posts`, {
      headers: { Authorization: `Bearer ${token}` },
      multipart: { content: 'To be deleted', postType: 'Text', privacy: 'OnlyMe' },
    })
    const post = await createResp.json()

    const deleteResp = await request.delete(`${baseURL}/api/posts/${post.id}`, {
      headers: { Authorization: `Bearer ${token}` },
    })
    expect([200, 204]).toContain(deleteResp.status())
  })

  test('returns 403 when non-owner tries to delete', async ({ request, baseURL }) => {
    const { token: token1 } = getSharedTokenFor(SEED.user1)
    const createResp = await request.post(`${baseURL}/api/posts`, {
      headers: { Authorization: `Bearer ${token1}` },
      multipart: { content: 'Protected post', postType: 'Text', privacy: 'Everyone' },
    })
    const post = await createResp.json()

    const { token: token2 } = getSharedTokenFor(SEED.user2)
    const deleteResp = await request.delete(`${baseURL}/api/posts/${post.id}`, {
      headers: { Authorization: `Bearer ${token2}` },
    })
    expect([403, 404]).toContain(deleteResp.status())
  })
})

test.describe('GET /api/feed', () => {
  test('returns paginated feed for authenticated user', async ({ request, baseURL }) => {
    const { token } = getSharedTokenFor(SEED.user1)
    const resp = await request.get(`${baseURL}/api/feed`, {
      headers: { Authorization: `Bearer ${token}` },
    })
    expect(resp.status()).toBe(200)
    const body = await resp.json()
    expect(Array.isArray(body.items ?? body)).toBeTruthy()
  })

  test('supports page/pageSize parameters', async ({ request, baseURL }) => {
    const { token } = getSharedTokenFor(SEED.user1)
    const resp = await request.get(`${baseURL}/api/feed?page=1&pageSize=5`, {
      headers: { Authorization: `Bearer ${token}` },
    })
    expect(resp.status()).toBe(200)
  })

  test('returns 401 without auth', async ({ request, baseURL }) => {
    const resp = await request.get(`${baseURL}/api/feed`)
    expect(resp.status()).toBe(401)
  })
})
