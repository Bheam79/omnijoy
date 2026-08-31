import { test, expect, type APIRequestContext } from '../../support/fixtures'
import { SEED } from '../../fixtures/seed-data'
import { getSharedTokenFor } from '../../support/shared-auth'
import { injectTokens } from '../../support/auth-helpers'

const authHeader = (token: string) => ({ Authorization: `Bearer ${token}` })

async function createPost(
  request: APIRequestContext,
  baseURL: string,
  token: string,
  content: string,
) {
  const response = await request.post(`${baseURL}/api/posts`, {
    headers: authHeader(token),
    multipart: { content, postType: 'Text', privacy: 'Everyone' },
  })
  expect(response.status()).toBe(201)
  return (await response.json()).id as string
}

async function createComment(
  request: APIRequestContext,
  baseURL: string,
  token: string,
  postId: string,
  content: string,
) {
  const response = await request.post(`${baseURL}/api/posts/${postId}/comments`, {
    headers: authHeader(token),
    data: { content },
  })
  expect(response.status()).toBe(201)
  return (await response.json()).id as string
}

async function react(
  request: APIRequestContext,
  baseURL: string,
  token: string,
  commentId: string,
  reactionType: string,
) {
  const response = await request.post(`${baseURL}/api/comments/${commentId}/reactions`, {
    headers: authHeader(token),
    data: { reactionType },
  })
  expect(response.status()).toBe(200)
}

test('comment thread picker renders top-three emoji/count and changes then removes a reaction', async ({
  page,
  request,
  baseURL,
}) => {
  const alice = getSharedTokenFor(SEED.user1)
  const bob = getSharedTokenFor(SEED.user2)
  const carol = getSharedTokenFor(SEED.user3)
  const postContent = `Comment reaction browser E2E ${Date.now()}`
  const commentContent = `Three emoji comment ${Date.now()}`
  const postId = await createPost(request, baseURL!, alice.token, postContent)
  const commentId = await createComment(
    request,
    baseURL!,
    alice.token,
    postId,
    commentContent,
  )

  try {
    await react(request, baseURL!, alice.token, commentId, 'Like')
    await react(request, baseURL!, bob.token, commentId, 'Love')
    await react(request, baseURL!, carol.token, commentId, 'Haha')

    await injectTokens(page, baseURL!, SEED.user1.email, SEED.user1.password)
    await page.goto('/wall')

    const post = page.getByTestId('post-card').filter({ hasText: postContent })
    await expect(post).toBeVisible()
    await post.getByTestId('post-comment-button').click()

    const comment = post.getByTestId('comment-item').filter({ hasText: commentContent })
    await expect(comment).toBeVisible()

    const count = comment.getByTestId('comment-reaction-count')
    await expect(count).toContainText('👍')
    await expect(count).toContainText('❤️')
    await expect(count).toContainText('😂')
    await expect(count).toContainText('3')

    const picker = comment.getByTestId('comment-reaction-picker')
    const reactionButton = picker.getByTestId('reaction-button')
    await reactionButton.hover()
    await expect(picker.getByTestId('reaction-picker-popup')).toBeVisible()
    await picker.getByTestId('pick-wow').click()

    await expect(reactionButton).toHaveAttribute('aria-label', 'Wow')
    await expect(count).toContainText('😮')
    await expect(count).toContainText('❤️')
    await expect(count).toContainText('😂')
    await expect(count).toContainText('3')

    await reactionButton.click()
    await expect(reactionButton).toHaveAttribute('aria-label', 'Like')
    await expect(count).not.toContainText('😮')
    await expect(count).toContainText('❤️')
    await expect(count).toContainText('😂')
    await expect(count).toContainText('2')

    await expect.poll(async () => {
      const response = await request.get(`${baseURL}/api/comments/${commentId}/reactions`, {
        headers: authHeader(alice.token),
      })
      return await response.json()
    }).toMatchObject({
      counts: [
        { reactionType: 'Love', count: 1 },
        { reactionType: 'Haha', count: 1 },
      ],
      totalCount: 2,
      currentUserReaction: null,
    })
  } finally {
    for (const user of [alice, bob, carol]) {
      const reactionCleanup = await request.delete(
        `${baseURL}/api/comments/${commentId}/reactions`,
        { headers: authHeader(user.token) },
      )
      expect([200, 404]).toContain(reactionCleanup.status())
    }
    const cleanup = await request.delete(`${baseURL}/api/posts/${postId}`, {
      headers: authHeader(alice.token),
    })
    expect([204, 404]).toContain(cleanup.status())
  }
})
