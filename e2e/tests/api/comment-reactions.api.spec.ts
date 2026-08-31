import { test, expect, type APIRequestContext } from '../../support/fixtures'
import { SEED } from '../../fixtures/seed-data'
import { getSharedTokenFor } from '../../support/shared-auth'

type ReactionCount = { reactionType: string; count: number }
type ReactionSummary = {
  counts: ReactionCount[]
  totalCount: number
  currentUserReaction: string | null
}
type NotificationItem = {
  type: string
  referenceId: string | null
}

const missingId = '00000000-0000-0000-0000-000000000000'
const authHeader = (token: string) => ({ Authorization: `Bearer ${token}` })

async function createPost(
  request: APIRequestContext,
  baseURL: string,
  token: string,
  label: string,
) {
  const response = await request.post(`${baseURL}/api/posts`, {
    headers: authHeader(token),
    multipart: {
      content: `Comment reaction API E2E ${label} ${Date.now()}`,
      postType: 'Text',
      privacy: 'Everyone',
    },
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

async function deletePost(
  request: APIRequestContext,
  baseURL: string,
  token: string,
  postId: string,
) {
  const response = await request.delete(`${baseURL}/api/posts/${postId}`, {
    headers: authHeader(token),
  })
  expect([204, 404]).toContain(response.status())
}

async function commentLikeNotifications(
  request: APIRequestContext,
  baseURL: string,
  token: string,
  referenceId: string,
) {
  const response = await request.get(
    `${baseURL}/api/notifications?page=1&pageSize=100`,
    { headers: authHeader(token) },
  )
  expect(response.status()).toBe(200)
  const body = await response.json()
  return ((body.items ?? body) as NotificationItem[])
    .filter((item) => item.type === 'CommentLike' && item.referenceId === referenceId)
}

function expectSummary(
  summary: ReactionSummary,
  expected: { counts: ReactionCount[]; totalCount: number; currentUserReaction: string | null },
) {
  expect(summary).toEqual(expected)
}

test.describe('Comment reactions — API lifecycle', () => {
  test('adds, changes, lists, and removes one reaction without duplicating it', async ({
    request,
    baseURL,
  }) => {
    const { token, userId } = getSharedTokenFor(SEED.user1)
    const postId = await createPost(request, baseURL!, token, 'lifecycle')
    const commentId = await createComment(
      request,
      baseURL!,
      token,
      postId,
      'Lifecycle comment',
    )

    try {
      const emptyResponse = await request.get(
        `${baseURL}/api/comments/${commentId}/reactions`,
        { headers: authHeader(token) },
      )
      expect(emptyResponse.status()).toBe(200)
      expectSummary(await emptyResponse.json(), {
        counts: [],
        totalCount: 0,
        currentUserReaction: null,
      })

      const likeResponse = await request.post(
        `${baseURL}/api/comments/${commentId}/reactions`,
        {
          headers: authHeader(token),
          data: { reactionType: 'Like' },
        },
      )
      expect(likeResponse.status()).toBe(200)
      expectSummary(await likeResponse.json(), {
        counts: [{ reactionType: 'Like', count: 1 }],
        totalCount: 1,
        currentUserReaction: 'Like',
      })

      const updateResponse = await request.post(
        `${baseURL}/api/comments/${commentId}/reactions`,
        {
          headers: authHeader(token),
          data: { reactionType: 'Love' },
        },
      )
      expect(updateResponse.status()).toBe(200)
      expectSummary(await updateResponse.json(), {
        counts: [{ reactionType: 'Love', count: 1 }],
        totalCount: 1,
        currentUserReaction: 'Love',
      })

      const whoResponse = await request.get(
        `${baseURL}/api/comments/${commentId}/reactions/who`,
        { headers: authHeader(token) },
      )
      expect(whoResponse.status()).toBe(200)
      expect(await whoResponse.json()).toMatchObject({
        people: [{ id: userId, displayName: SEED.user1.displayName, reactionType: 'Love' }],
        remaining: 0,
      })

      const removeResponse = await request.delete(
        `${baseURL}/api/comments/${commentId}/reactions`,
        { headers: authHeader(token) },
      )
      expect(removeResponse.status()).toBe(200)
      expectSummary(await removeResponse.json(), {
        counts: [],
        totalCount: 0,
        currentUserReaction: null,
      })

      const finalResponse = await request.get(
        `${baseURL}/api/comments/${commentId}/reactions`,
        { headers: authHeader(token) },
      )
      expect(finalResponse.status()).toBe(200)
      expectSummary(await finalResponse.json(), {
        counts: [],
        totalCount: 0,
        currentUserReaction: null,
      })

      const secondRemove = await request.delete(
        `${baseURL}/api/comments/${commentId}/reactions`,
        { headers: authHeader(token) },
      )
      expect(secondRemove.status()).toBe(404)
    } finally {
      const reactionCleanup = await request.delete(
        `${baseURL}/api/comments/${commentId}/reactions`,
        { headers: authHeader(token) },
      )
      expect([200, 404]).toContain(reactionCleanup.status())
      await deletePost(request, baseURL!, token, postId)
    }
  })

  test('rejects invalid types and reactions on soft-deleted or missing comments', async ({
    request,
    baseURL,
  }) => {
    const { token } = getSharedTokenFor(SEED.user1)
    const postId = await createPost(request, baseURL!, token, 'validation')
    const commentId = await createComment(
      request,
      baseURL!,
      token,
      postId,
      'Soon deleted comment',
    )

    try {
      const invalidResponse = await request.post(
        `${baseURL}/api/comments/${commentId}/reactions`,
        {
          headers: authHeader(token),
          data: { reactionType: 'Celebrate' },
        },
      )
      expect(invalidResponse.status()).toBe(400)
      expect(await invalidResponse.json()).toMatchObject({
        error: expect.stringContaining('Invalid ReactionType'),
      })

      const deleteCommentResponse = await request.delete(
        `${baseURL}/api/comments/${commentId}`,
        { headers: authHeader(token) },
      )
      expect(deleteCommentResponse.status()).toBe(204)

      for (const path of ['reactions', 'reactions/who']) {
        const response = await request.get(`${baseURL}/api/comments/${commentId}/${path}`, {
          headers: authHeader(token),
        })
        expect(response.status()).toBe(404)
      }

      const deletedCommentReaction = await request.post(
        `${baseURL}/api/comments/${commentId}/reactions`,
        {
          headers: authHeader(token),
          data: { reactionType: 'Like' },
        },
      )
      expect(deletedCommentReaction.status()).toBe(404)

      const missingCommentReaction = await request.post(
        `${baseURL}/api/comments/${missingId}/reactions`,
        {
          headers: authHeader(token),
          data: { reactionType: 'Like' },
        },
      )
      expect(missingCommentReaction.status()).toBe(404)
    } finally {
      await deletePost(request, baseURL!, token, postId)
    }
  })
})

test.describe('Comment reactions — notification preferences', () => {
  test('notifies another author, suppresses self-reactions, and honors LikesOnMyPosts', async ({
    request,
    baseURL,
  }) => {
    const author = getSharedTokenFor(SEED.user1)
    const reactor = getSharedTokenFor(SEED.user2)
    const preferencesResponse = await request.get(
      `${baseURL}/api/account/notification-preferences`,
      { headers: authHeader(author.token) },
    )
    expect(preferencesResponse.status()).toBe(200)
    const originalPreferences = await preferencesResponse.json()
    const preferenceKeys = Object.keys(originalPreferences).sort()
    expect(originalPreferences).not.toHaveProperty('commentLikes')

    const enabledPreferences = { ...originalPreferences, likesOnMyPosts: true }
    const enableResponse = await request.put(
      `${baseURL}/api/account/notification-preferences`,
      {
        headers: authHeader(author.token),
        data: enabledPreferences,
      },
    )
    expect(enableResponse.status()).toBe(200)

    const postId = await createPost(request, baseURL!, author.token, 'notifications')
    const createdReactions: { commentId: string; token: string }[] = []

    try {
      const otherReactionComment = await createComment(
        request,
        baseURL!,
        author.token,
        postId,
        'Notify for this comment',
      )
      const otherReaction = await request.post(
        `${baseURL}/api/comments/${otherReactionComment}/reactions`,
        {
          headers: authHeader(reactor.token),
          data: { reactionType: 'Like' },
        },
      )
      expect(otherReaction.status()).toBe(200)
      createdReactions.push({ commentId: otherReactionComment, token: reactor.token })
      const otherNotifications = await commentLikeNotifications(
        request,
        baseURL!,
        author.token,
        otherReactionComment,
      )
      expect(otherNotifications).toHaveLength(1)

      const selfReactionComment = await createComment(
        request,
        baseURL!,
        author.token,
        postId,
        'Do not notify for self reaction',
      )
      const selfReaction = await request.post(
        `${baseURL}/api/comments/${selfReactionComment}/reactions`,
        {
          headers: authHeader(author.token),
          data: { reactionType: 'Love' },
        },
      )
      expect(selfReaction.status()).toBe(200)
      createdReactions.push({ commentId: selfReactionComment, token: author.token })
      expect(await commentLikeNotifications(
        request,
        baseURL!,
        author.token,
        selfReactionComment,
      )).toEqual([])

      const disabledPreferences = { ...enabledPreferences, likesOnMyPosts: false }
      const disableResponse = await request.put(
        `${baseURL}/api/account/notification-preferences`,
        {
          headers: authHeader(author.token),
          data: disabledPreferences,
        },
      )
      expect(disableResponse.status()).toBe(200)
      const storedDisabledPreferences = await disableResponse.json()
      expect(Object.keys(storedDisabledPreferences).sort()).toEqual(preferenceKeys)
      expect(storedDisabledPreferences).toEqual(disabledPreferences)
      expect(storedDisabledPreferences).not.toHaveProperty('commentLikes')

      const suppressedComment = await createComment(
        request,
        baseURL!,
        author.token,
        postId,
        'Preference suppresses this reaction',
      )
      const suppressedReaction = await request.post(
        `${baseURL}/api/comments/${suppressedComment}/reactions`,
        {
          headers: authHeader(reactor.token),
          data: { reactionType: 'Haha' },
        },
      )
      expect(suppressedReaction.status()).toBe(200)
      createdReactions.push({ commentId: suppressedComment, token: reactor.token })
      expect(await commentLikeNotifications(
        request,
        baseURL!,
        author.token,
        suppressedComment,
      )).toEqual([])
    } finally {
      for (const reaction of createdReactions) {
        const cleanup = await request.delete(
          `${baseURL}/api/comments/${reaction.commentId}/reactions`,
          { headers: authHeader(reaction.token) },
        )
        expect([200, 404]).toContain(cleanup.status())
      }
      const restoreResponse = await request.put(
        `${baseURL}/api/account/notification-preferences`,
        {
          headers: authHeader(author.token),
          data: originalPreferences,
        },
      )
      expect(restoreResponse.status()).toBe(200)
      await deletePost(request, baseURL!, author.token, postId)
    }
  })
})
