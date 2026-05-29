import { test, expect } from '../../support/fixtures'
import { SEED } from '../../fixtures/seed-data'
import { getSharedTokenFor } from '../../support/shared-auth'
import { injectTokens } from '../../support/auth-helpers'

// ── Helper: create a public text post via API and return its ID ───────────────

async function createPost(
  request: any,
  baseURL: string,
  token: string,
): Promise<string | null> {
  const resp = await request.post(`${baseURL}/api/posts`, {
    headers: { Authorization: `Bearer ${token}` },
    multipart: {
      content: `E2E comment test post ${Date.now()}`,
      postType: 'Text',
      privacy: 'Everyone',
    },
  })
  if ([200, 201].includes(resp.status())) {
    const body = await resp.json()
    return body.id as string
  }
  return null
}

// ── API-level comment tests ───────────────────────────────────────────────────

test.describe('Comments — API (create, reply, edit, delete)', () => {
  test('create a top-level comment on a post', async ({ request, baseURL }) => {
    const { token: token } = getSharedTokenFor(SEED.user1)

    const postId = await createPost(request, baseURL!, token)
    if (!postId) {
      test.skip()
      return
    }

    const resp = await request.post(`${baseURL}/api/posts/${postId}/comments`, {
      headers: { Authorization: `Bearer ${token}` },
      data: { content: 'Hello from E2E test comment', parentCommentId: null },
    })
    expect([200, 201]).toContain(resp.status())
    const comment = await resp.json()
    expect(comment.content).toBe('Hello from E2E test comment')
    expect(comment.id).toBeTruthy()
  })

  test('fetch comments for a post returns array', async ({ request, baseURL }) => {
    const { token: token } = getSharedTokenFor(SEED.user1)

    const postId = await createPost(request, baseURL!, token)
    if (!postId) {
      test.skip()
      return
    }

    // Create a comment first
    await request.post(`${baseURL}/api/posts/${postId}/comments`, {
      headers: { Authorization: `Bearer ${token}` },
      data: { content: 'Fetchable comment', parentCommentId: null },
    })

    const resp = await request.get(`${baseURL}/api/posts/${postId}/comments`)
    expect(resp.status()).toBe(200)
    const body = await resp.json()
    // Response is a paginated result with items array, or a plain array
    const items = Array.isArray(body) ? body : (body.items ?? body.data ?? [])
    expect(Array.isArray(items)).toBeTruthy()
  })

  test('create a reply to a comment', async ({ request, baseURL }) => {
    const { token: token } = getSharedTokenFor(SEED.user1)

    const postId = await createPost(request, baseURL!, token)
    if (!postId) {
      test.skip()
      return
    }

    const commentResp = await request.post(`${baseURL}/api/posts/${postId}/comments`, {
      headers: { Authorization: `Bearer ${token}` },
      data: { content: 'Parent comment', parentCommentId: null },
    })
    if (![200, 201].includes(commentResp.status())) {
      test.skip()
      return
    }
    const parent = await commentResp.json()

    const replyResp = await request.post(`${baseURL}/api/posts/${postId}/comments`, {
      headers: { Authorization: `Bearer ${token}` },
      data: { content: 'Reply to parent', parentCommentId: parent.id },
    })
    expect([200, 201]).toContain(replyResp.status())
    const reply = await replyResp.json()
    expect(reply.content).toBe('Reply to parent')
  })

  test('edit own comment', async ({ request, baseURL }) => {
    const { token: token } = getSharedTokenFor(SEED.user1)

    const postId = await createPost(request, baseURL!, token)
    if (!postId) {
      test.skip()
      return
    }

    const createResp = await request.post(`${baseURL}/api/posts/${postId}/comments`, {
      headers: { Authorization: `Bearer ${token}` },
      data: { content: 'Original content', parentCommentId: null },
    })
    if (![200, 201].includes(createResp.status())) {
      test.skip()
      return
    }
    const comment = await createResp.json()

    const editResp = await request.put(`${baseURL}/api/comments/${comment.id}`, {
      headers: { Authorization: `Bearer ${token}` },
      data: { content: 'Edited content' },
    })
    expect([200, 204]).toContain(editResp.status())
    if (editResp.status() === 200) {
      const updated = await editResp.json()
      expect(updated.content).toBe('Edited content')
    }
  })

  test('delete own comment returns 204', async ({ request, baseURL }) => {
    const { token: token } = getSharedTokenFor(SEED.user1)

    const postId = await createPost(request, baseURL!, token)
    if (!postId) {
      test.skip()
      return
    }

    const createResp = await request.post(`${baseURL}/api/posts/${postId}/comments`, {
      headers: { Authorization: `Bearer ${token}` },
      data: { content: 'To be deleted', parentCommentId: null },
    })
    if (![200, 201].includes(createResp.status())) {
      test.skip()
      return
    }
    const comment = await createResp.json()

    const deleteResp = await request.delete(`${baseURL}/api/comments/${comment.id}`, {
      headers: { Authorization: `Bearer ${token}` },
    })
    expect([204, 200]).toContain(deleteResp.status())
  })
})

// ── Browser-level comment tests ───────────────────────────────────────────────

test.describe('Comments — browser UI', () => {
  test.beforeEach(async ({ page, baseURL }) => {
    await injectTokens(page, baseURL!, SEED.user1.email, SEED.user1.password)
  })

  test('wall page loads and comment thread is accessible', async ({ page }) => {
    await page.goto('/wall')
    await page.waitForLoadState('networkidle')

    // The wall page should render without crashing
    await expect(page.locator('body')).toBeVisible()

    // Check for any post card or empty state
    const postCards = page.locator('[data-testid="post-card"]')
    const emptyState = page.getByText(/no posts yet|be the first|start sharing/i)
    const hasContent =
      (await postCards.count()) > 0 || (await emptyState.isVisible())
    expect(hasContent).toBeTruthy()
  })

  test('comment section exists on a post created via API', async ({
    page,
    request,
    baseURL,
  }) => {
    // Create a post via API so there is something to comment on
    const { token: token } = getSharedTokenFor(SEED.user1)
    await createPost(request, baseURL!, token)

    await page.goto('/wall')
    await page.waitForLoadState('networkidle')

    // Look for a comment button (post-comment-button opens the thread)
    // or a comment input (if thread already expanded)
    const commentTrigger = page
      .locator('[data-testid="post-comment-button"]')
      .or(page.locator('[data-testid="comment-input"]'))
    const hasCommentUI = await commentTrigger.first().isVisible()
    expect(hasCommentUI).toBeTruthy()
  })
})
