import { test, expect, type APIRequestContext } from '../../support/fixtures'
import { SEED } from '../../fixtures/seed-data'
import { getSharedTokenFor } from '../../support/shared-auth'

type NotificationItem = {
  type: string
  referenceId: string | null
}

type FeedItem = {
  post?: { content?: string } | null
  sharedPost?: { originalPost?: { content?: string } | null } | null
}

type CommentItem = {
  id: string
  content: string
}

const authHeader = (token: string) => ({ Authorization: `Bearer ${token}` })

async function assignSlug(
  request: APIRequestContext,
  baseURL: string,
  token: string,
  slug: string,
) {
  const response = await request.put(`${baseURL}/api/users/me/slug`, {
    headers: authHeader(token),
    data: { slug },
  })
  expect(response.status()).toBe(200)
  expect((await response.json()).slug).toBe(slug)
}

async function enableMentionNotifications(
  request: APIRequestContext,
  baseURL: string,
  token: string,
) {
  const getResponse = await request.get(`${baseURL}/api/account/notification-preferences`, {
    headers: authHeader(token),
  })
  expect(getResponse.status()).toBe(200)

  const preferences = await getResponse.json()
  const updateResponse = await request.put(
    `${baseURL}/api/account/notification-preferences`,
    {
      headers: authHeader(token),
      data: { ...preferences, mentions: true },
    },
  )
  expect(updateResponse.status()).toBe(200)
}

async function prepareMentionUsers(request: APIRequestContext, baseURL: string) {
  const alice = getSharedTokenFor(SEED.user1)
  const bob = getSharedTokenFor(SEED.user2)

  await assignSlug(request, baseURL, alice.token, 'alice')
  await assignSlug(request, baseURL, bob.token, 'bob')
  await enableMentionNotifications(request, baseURL, alice.token)
  await enableMentionNotifications(request, baseURL, bob.token)

  return { alice, bob, actor: getSharedTokenFor(SEED.user3) }
}

async function createPost(
  request: APIRequestContext,
  baseURL: string,
  token: string,
  content: string,
) {
  return request.post(`${baseURL}/api/posts`, {
    headers: authHeader(token),
    multipart: { content, postType: 'Text', privacy: 'Everyone' },
  })
}

async function getMentionNotifications(
  request: APIRequestContext,
  baseURL: string,
  token: string,
  type: 'MentionInPost' | 'MentionInComment',
): Promise<NotificationItem[]> {
  const response = await request.get(
    `${baseURL}/api/notifications?page=1&pageSize=100`,
    { headers: authHeader(token) },
  )
  expect(response.status()).toBe(200)
  return ((await response.json()).items as NotificationItem[])
    .filter((item) => item.type === type)
}

async function notificationsForReference(
  request: APIRequestContext,
  baseURL: string,
  token: string,
  type: 'MentionInPost' | 'MentionInComment',
  referenceId: string,
) {
  const items = await getMentionNotifications(request, baseURL, token, type)
  return items.filter((item) => item.type === type && item.referenceId === referenceId)
}

async function getComments(
  request: APIRequestContext,
  baseURL: string,
  postId: string,
): Promise<CommentItem[]> {
  const response = await request.get(
    `${baseURL}/api/posts/${postId}/comments?page=1&pageSize=50`,
  )
  expect(response.status()).toBe(200)
  const body = await response.json()
  return (body.items ?? body) as CommentItem[]
}

test.describe('Post and comment mentions', () => {
  test('creates exactly one post notification when @alice is repeated', async ({
    request,
    baseURL,
  }) => {
    const { alice, actor } = await prepareMentionUsers(request, baseURL!)
    const response = await createPost(
      request,
      baseURL!,
      actor.token,
      `Repeated mention ${Date.now()}: @alice, @ALICE, and @alice.`,
    )

    expect(response.status()).toBe(201)
    const post = await response.json()
    expect(post.mentions).toHaveLength(1)
    expect(post.mentions[0]).toMatchObject({ matchedSlug: 'alice', userId: alice.userId })
    expect(await notificationsForReference(
      request,
      baseURL!,
      alice.token,
      'MentionInPost',
      post.id,
    )).toHaveLength(1)
  })

  test('creates a comment mention notification with the comment reference ID', async ({
    request,
    baseURL,
  }) => {
    const { alice, actor } = await prepareMentionUsers(request, baseURL!)
    const postResponse = await createPost(
      request,
      baseURL!,
      actor.token,
      `Comment mention parent ${Date.now()}`,
    )
    expect(postResponse.status()).toBe(201)
    const post = await postResponse.json()

    const commentResponse = await request.post(`${baseURL}/api/posts/${post.id}/comments`, {
      headers: authHeader(actor.token),
      data: { content: 'A comment for @alice' },
    })
    expect(commentResponse.status()).toBe(201)
    const comment = await commentResponse.json()
    expect(comment.mentions).toHaveLength(1)
    expect(comment.mentions[0]).toMatchObject({ matchedSlug: 'alice', userId: alice.userId })
    expect(await notificationsForReference(
      request,
      baseURL!,
      alice.token,
      'MentionInComment',
      comment.id,
    )).toHaveLength(1)
  })

  test('post and comment edits notify only newly added recipients', async ({
    request,
    baseURL,
  }) => {
    const { alice, bob, actor } = await prepareMentionUsers(request, baseURL!)

    const postResponse = await createPost(
      request,
      baseURL!,
      actor.token,
      `Edit recipients ${Date.now()}: @alice`,
    )
    expect(postResponse.status()).toBe(201)
    const post = await postResponse.json()
    expect(await notificationsForReference(
      request, baseURL!, alice.token, 'MentionInPost', post.id,
    )).toHaveLength(1)

    const updatePostResponse = await request.put(`${baseURL}/api/posts/${post.id}`, {
      headers: authHeader(actor.token),
      data: { content: `${post.content} and @bob` },
    })
    expect(updatePostResponse.status()).toBe(200)
    expect(await notificationsForReference(
      request, baseURL!, alice.token, 'MentionInPost', post.id,
    )).toHaveLength(1)
    expect(await notificationsForReference(
      request, baseURL!, bob.token, 'MentionInPost', post.id,
    )).toHaveLength(1)

    const commentResponse = await request.post(`${baseURL}/api/posts/${post.id}/comments`, {
      headers: authHeader(actor.token),
      data: { content: 'Existing recipient @alice' },
    })
    expect(commentResponse.status()).toBe(201)
    const comment = await commentResponse.json()
    expect(await notificationsForReference(
      request, baseURL!, alice.token, 'MentionInComment', comment.id,
    )).toHaveLength(1)

    const updateCommentResponse = await request.put(`${baseURL}/api/comments/${comment.id}`, {
      headers: authHeader(actor.token),
      data: { content: 'Existing recipient @alice and new recipient @bob' },
    })
    expect(updateCommentResponse.status()).toBe(200)
    expect(await notificationsForReference(
      request, baseURL!, alice.token, 'MentionInComment', comment.id,
    )).toHaveLength(1)
    expect(await notificationsForReference(
      request, baseURL!, bob.token, 'MentionInComment', comment.id,
    )).toHaveLength(1)
  })

  test('rejects over 10 distinct handles without persisting creates or updates', async ({
    request,
    baseURL,
  }) => {
    const { actor } = await prepareMentionUsers(request, baseURL!)
    const marker = `mention-limit-${Date.now()}`
    const tooManyMentions = Array.from(
      { length: 11 },
      (_, index) => `@limit-${String(index + 1).padStart(2, '0')}`,
    ).join(' ')

    const rejectedPostContent = `${marker}-post-create ${tooManyMentions}`
    const rejectedPost = await createPost(
      request,
      baseURL!,
      actor.token,
      rejectedPostContent,
    )
    expect(rejectedPost.status()).toBe(400)

    const feedResponse = await request.get(`${baseURL}/api/feed?page=1&pageSize=50`, {
      headers: authHeader(actor.token),
    })
    expect(feedResponse.status()).toBe(200)
    const feedItems = ((await feedResponse.json()).items ?? []) as FeedItem[]
    const feedContents = feedItems.flatMap((item) => [
      item.post?.content,
      item.sharedPost?.originalPost?.content,
    ])
    expect(feedContents).not.toContain(rejectedPostContent)

    const validPostResponse = await createPost(
      request,
      baseURL!,
      actor.token,
      `${marker}-post-original`,
    )
    expect(validPostResponse.status()).toBe(201)
    const validPost = await validPostResponse.json()

    const rejectedPostUpdate = await request.put(`${baseURL}/api/posts/${validPost.id}`, {
      headers: authHeader(actor.token),
      data: { content: `${marker}-post-update ${tooManyMentions}` },
    })
    expect(rejectedPostUpdate.status()).toBe(400)
    const storedPostResponse = await request.get(`${baseURL}/api/posts/${validPost.id}`, {
      headers: authHeader(actor.token),
    })
    expect(storedPostResponse.status()).toBe(200)
    expect((await storedPostResponse.json()).content).toBe(`${marker}-post-original`)

    const rejectedCommentContent = `${marker}-comment-create ${tooManyMentions}`
    const rejectedComment = await request.post(
      `${baseURL}/api/posts/${validPost.id}/comments`,
      {
        headers: authHeader(actor.token),
        data: { content: rejectedCommentContent },
      },
    )
    expect(rejectedComment.status()).toBe(400)
    expect((await getComments(request, baseURL!, validPost.id)).map((item) => item.content))
      .not.toContain(rejectedCommentContent)

    const validCommentResponse = await request.post(
      `${baseURL}/api/posts/${validPost.id}/comments`,
      {
        headers: authHeader(actor.token),
        data: { content: `${marker}-comment-original` },
      },
    )
    expect(validCommentResponse.status()).toBe(201)
    const validComment = await validCommentResponse.json()

    const rejectedCommentUpdate = await request.put(
      `${baseURL}/api/comments/${validComment.id}`,
      {
        headers: authHeader(actor.token),
        data: { content: `${marker}-comment-update ${tooManyMentions}` },
      },
    )
    expect(rejectedCommentUpdate.status()).toBe(400)
    const storedComments = await getComments(request, baseURL!, validPost.id)
    expect(storedComments.find((item) => item.id === validComment.id)?.content)
      .toBe(`${marker}-comment-original`)
  })
})
