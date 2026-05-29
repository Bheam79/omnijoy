import { test, expect, type APIRequestContext } from '../../support/fixtures'
import { SEED } from '../../fixtures/seed-data'

/**
 * API E2E tests for CommentsController endpoints:
 *   POST   /api/posts/{postId}/comments
 *   GET    /api/posts/{postId}/comments
 *   GET    /api/comments/{commentId}/replies
 *   PUT    /api/comments/{commentId}
 *   DELETE /api/comments/{commentId}
 */

async function loginAs(request: APIRequestContext, baseURL: string | undefined, user: typeof SEED.user1) {
  const resp = await request.post(`${baseURL}/api/auth/login`, {
    data: { email: user.email, password: user.password },
  })
  const body = await resp.json()
  return { token: body.accessToken as string, userId: body.user.id as string }
}

/** Creates a public text post and returns its id. */
async function createPost(request: APIRequestContext, baseURL: string | undefined, token: string) {
  const resp = await request.post(`${baseURL}/api/posts`, {
    headers: { Authorization: `Bearer ${token}` },
    multipart: { content: 'Post for comment tests', postType: 'Text', privacy: 'Everyone' },
  })
  const body = await resp.json()
  return body.id as string
}

// ── POST /api/posts/{postId}/comments ─────────────────────────────────────────

test.describe('POST /api/posts/:postId/comments', () => {
  test('creates a top-level comment on a post', async ({ request, baseURL }) => {
    const { token } = await loginAs(request, baseURL, SEED.user1)
    const postId = await createPost(request, baseURL, token)

    const resp = await request.post(`${baseURL}/api/posts/${postId}/comments`, {
      headers: { Authorization: `Bearer ${token}` },
      data: { content: 'This is a test comment' },
    })
    expect(resp.status()).toBe(201)
    const body = await resp.json()
    expect(body.id).toBeTruthy()
    expect(body.content).toBe('This is a test comment')
    expect(body.postId).toBe(postId)
    expect(body.parentCommentId).toBeNull()
  })

  test('creates a threaded reply to an existing comment', async ({ request, baseURL }) => {
    const { token } = await loginAs(request, baseURL, SEED.user1)
    const postId = await createPost(request, baseURL, token)

    // Create parent comment
    const parentResp = await request.post(`${baseURL}/api/posts/${postId}/comments`, {
      headers: { Authorization: `Bearer ${token}` },
      data: { content: 'Parent comment' },
    })
    const parent = await parentResp.json()

    // Create reply
    const replyResp = await request.post(`${baseURL}/api/posts/${postId}/comments`, {
      headers: { Authorization: `Bearer ${token}` },
      data: { content: 'Reply to parent', parentCommentId: parent.id },
    })
    expect(replyResp.status()).toBe(201)
    const reply = await replyResp.json()
    expect(reply.parentCommentId).toBe(parent.id)
    expect(reply.content).toBe('Reply to parent')
  })

  test('returns 404 for a non-existent post', async ({ request, baseURL }) => {
    const { token } = await loginAs(request, baseURL, SEED.user1)
    const resp = await request.post(
      `${baseURL}/api/posts/00000000-0000-0000-0000-000000000000/comments`,
      {
        headers: { Authorization: `Bearer ${token}` },
        data: { content: 'Comment on missing post' },
      },
    )
    expect(resp.status()).toBe(404)
  })

  test('returns 401 without auth', async ({ request, baseURL }) => {
    const resp = await request.post(
      `${baseURL}/api/posts/00000000-0000-0000-0000-000000000000/comments`,
      { data: { content: 'Unauthorized comment' } },
    )
    expect(resp.status()).toBe(401)
  })
})

// ── GET /api/posts/{postId}/comments ──────────────────────────────────────────

test.describe('GET /api/posts/:postId/comments', () => {
  test('returns paginated comments for a post', async ({ request, baseURL }) => {
    const { token } = await loginAs(request, baseURL, SEED.user1)
    const postId = await createPost(request, baseURL, token)

    // Add a comment so the list is non-empty
    await request.post(`${baseURL}/api/posts/${postId}/comments`, {
      headers: { Authorization: `Bearer ${token}` },
      data: { content: 'Comment for list test' },
    })

    const resp = await request.get(`${baseURL}/api/posts/${postId}/comments`)
    expect(resp.status()).toBe(200)
    const body = await resp.json()
    const items = body.items ?? body
    expect(Array.isArray(items)).toBe(true)
    expect(items.length).toBeGreaterThan(0)
  })

  test('supports page and pageSize query parameters', async ({ request, baseURL }) => {
    const { token } = await loginAs(request, baseURL, SEED.user1)
    const postId = await createPost(request, baseURL, token)

    const resp = await request.get(`${baseURL}/api/posts/${postId}/comments?page=1&pageSize=5`)
    expect(resp.status()).toBe(200)
  })

  test('is accessible anonymously for public posts', async ({ request, baseURL }) => {
    const { token } = await loginAs(request, baseURL, SEED.user1)
    const postId = await createPost(request, baseURL, token)

    const resp = await request.get(`${baseURL}/api/posts/${postId}/comments`)
    expect(resp.status()).toBe(200)
  })

  test('returns 404 for a non-existent post', async ({ request, baseURL }) => {
    const resp = await request.get(
      `${baseURL}/api/posts/00000000-0000-0000-0000-000000000000/comments`,
    )
    expect(resp.status()).toBe(404)
  })
})

// ── GET /api/comments/{commentId}/replies ─────────────────────────────────────

test.describe('GET /api/comments/:commentId/replies', () => {
  test('returns replies for a top-level comment', async ({ request, baseURL }) => {
    const { token } = await loginAs(request, baseURL, SEED.user1)
    const postId = await createPost(request, baseURL, token)

    // Create parent + reply
    const parentResp = await request.post(`${baseURL}/api/posts/${postId}/comments`, {
      headers: { Authorization: `Bearer ${token}` },
      data: { content: 'Parent for replies test' },
    })
    const parent = await parentResp.json()

    await request.post(`${baseURL}/api/posts/${postId}/comments`, {
      headers: { Authorization: `Bearer ${token}` },
      data: { content: 'A reply', parentCommentId: parent.id },
    })

    const resp = await request.get(`${baseURL}/api/comments/${parent.id}/replies`)
    expect(resp.status()).toBe(200)
    const items = (await resp.json()) as unknown[]
    expect(Array.isArray(items)).toBe(true)
  })

  test('returns 404 for a non-existent comment', async ({ request, baseURL }) => {
    const resp = await request.get(
      `${baseURL}/api/comments/00000000-0000-0000-0000-000000000000/replies`,
    )
    expect(resp.status()).toBe(404)
  })
})

// ── PUT /api/comments/{commentId} ─────────────────────────────────────────────

test.describe('PUT /api/comments/:commentId', () => {
  test('owner can update their comment', async ({ request, baseURL }) => {
    const { token } = await loginAs(request, baseURL, SEED.user1)
    const postId = await createPost(request, baseURL, token)

    const createResp = await request.post(`${baseURL}/api/posts/${postId}/comments`, {
      headers: { Authorization: `Bearer ${token}` },
      data: { content: 'Original content' },
    })
    const comment = await createResp.json()

    const updateResp = await request.put(`${baseURL}/api/comments/${comment.id}`, {
      headers: { Authorization: `Bearer ${token}` },
      data: { content: 'Updated content' },
    })
    expect(updateResp.status()).toBe(200)
    const updated = await updateResp.json()
    expect(updated.content).toBe('Updated content')
  })

  test('returns 403 when a non-owner tries to update', async ({ request, baseURL }) => {
    const { token: token1 } = await loginAs(request, baseURL, SEED.user1)
    const { token: token2 } = await loginAs(request, baseURL, SEED.user2)
    const postId = await createPost(request, baseURL, token1)

    const createResp = await request.post(`${baseURL}/api/posts/${postId}/comments`, {
      headers: { Authorization: `Bearer ${token1}` },
      data: { content: 'User1 comment' },
    })
    const comment = await createResp.json()

    const updateResp = await request.put(`${baseURL}/api/comments/${comment.id}`, {
      headers: { Authorization: `Bearer ${token2}` },
      data: { content: 'Hacked!' },
    })
    expect([403, 404]).toContain(updateResp.status())
  })

  test('returns 404 for a non-existent comment', async ({ request, baseURL }) => {
    const { token } = await loginAs(request, baseURL, SEED.user1)
    const resp = await request.put(
      `${baseURL}/api/comments/00000000-0000-0000-0000-000000000000`,
      {
        headers: { Authorization: `Bearer ${token}` },
        data: { content: 'Does not exist' },
      },
    )
    expect(resp.status()).toBe(404)
  })

  test('returns 401 without auth', async ({ request, baseURL }) => {
    const resp = await request.put(
      `${baseURL}/api/comments/00000000-0000-0000-0000-000000000000`,
      { data: { content: 'No auth' } },
    )
    expect(resp.status()).toBe(401)
  })
})

// ── DELETE /api/comments/{commentId} ──────────────────────────────────────────

test.describe('DELETE /api/comments/:commentId', () => {
  test('owner can delete their comment', async ({ request, baseURL }) => {
    const { token } = await loginAs(request, baseURL, SEED.user1)
    const postId = await createPost(request, baseURL, token)

    const createResp = await request.post(`${baseURL}/api/posts/${postId}/comments`, {
      headers: { Authorization: `Bearer ${token}` },
      data: { content: 'To be deleted' },
    })
    const comment = await createResp.json()

    const deleteResp = await request.delete(`${baseURL}/api/comments/${comment.id}`, {
      headers: { Authorization: `Bearer ${token}` },
    })
    expect([200, 204]).toContain(deleteResp.status())
  })

  test('returns 403 when a non-owner tries to delete', async ({ request, baseURL }) => {
    const { token: token1 } = await loginAs(request, baseURL, SEED.user1)
    const { token: token2 } = await loginAs(request, baseURL, SEED.user2)
    const postId = await createPost(request, baseURL, token1)

    const createResp = await request.post(`${baseURL}/api/posts/${postId}/comments`, {
      headers: { Authorization: `Bearer ${token1}` },
      data: { content: 'Protected comment' },
    })
    const comment = await createResp.json()

    const deleteResp = await request.delete(`${baseURL}/api/comments/${comment.id}`, {
      headers: { Authorization: `Bearer ${token2}` },
    })
    expect([403, 404]).toContain(deleteResp.status())
  })

  test('returns 404 for a non-existent comment', async ({ request, baseURL }) => {
    const { token } = await loginAs(request, baseURL, SEED.user1)
    const resp = await request.delete(
      `${baseURL}/api/comments/00000000-0000-0000-0000-000000000000`,
      { headers: { Authorization: `Bearer ${token}` } },
    )
    expect(resp.status()).toBe(404)
  })

  test('returns 401 without auth', async ({ request, baseURL }) => {
    const resp = await request.delete(
      `${baseURL}/api/comments/00000000-0000-0000-0000-000000000000`,
    )
    expect(resp.status()).toBe(401)
  })
})
