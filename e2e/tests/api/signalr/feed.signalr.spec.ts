import { test, expect, type APIRequestContext } from '../../../support/fixtures'
import {
  connectToHub,
  disposeAll,
  waitForEvent,
} from '../../../support/signalr-client'
import { getSharedUser } from '../../../support/shared-auth'
import type { HubConnection } from '@microsoft/signalr'
import { request as playwrightRequest } from '@playwright/test'

/**
 * SignalR coverage for FeedHub (/hubs/feed).
 *
 * Exercises:
 *   - NewPost — friend connects, author posts → friend receives `NewPost`.
 *   - ReactionCountsUpdated — viewer subscribes to a post, another user
 *     reacts → `ReactionCountsUpdated` fires.
 *   - CommentReactionCountsUpdated — a post subscriber receives absolute
 *     comment counts for add, update, and remove mutations.
 *   - NewSharedPost — friend connects, author shares a post → friend
 *     receives `NewSharedPost`.
 *
 * Auth tokens come from the shared cache populated by `globalSetup`
 * (see `support/shared-auth.ts`) so the suite stays under the `strict`
 * auth rate-limit policy (10 req/min/IP).
 */

/** Ensure A & B are accepted friends. Idempotent. */
async function ensureFriends(
  request: APIRequestContext,
  baseURL: string,
  tokenA: string, userIdA: string,
  tokenB: string, userIdB: string,
) {
  const sendResp = await request.post(`${baseURL}/api/friends/request/${userIdB}`, {
    headers: { Authorization: `Bearer ${tokenA}` },
  })
  if (sendResp.status() !== 409) {
    await request.post(`${baseURL}/api/friends/accept/${userIdA}`, {
      headers: { Authorization: `Bearer ${tokenB}` },
    })
  }
}

/** Create a text post via the multipart endpoint — returns the parsed DTO. */
async function createPost(
  request: APIRequestContext,
  baseURL: string,
  token: string,
  content: string,
  privacy: 'Friends' | 'Everyone' = 'Friends',
) {
  const resp = await request.post(`${baseURL}/api/posts`, {
    headers: { Authorization: `Bearer ${token}` },
    multipart: {
      content,
      postType: 'Text',
      privacy,
    },
  })
  expect([200, 201]).toContain(resp.status())
  return await resp.json()
}

test.describe('FeedHub — /hubs/feed', () => {
  const connections: HubConnection[] = []

  let baseURL: string
  let sharedRequest: APIRequestContext
  let tokenAuthor: string, authorId: string
  let tokenFriend: string, friendId: string

  test.beforeAll(async ({ baseURL: configBaseURL }) => {
    baseURL = configBaseURL ?? 'http://localhost:80'
    ;({ token: tokenAuthor, userId: authorId } = getSharedUser('user1'))
    ;({ token: tokenFriend, userId: friendId } = getSharedUser('user2'))

    // user1 (author) and user2 (friend) must be accepted friends so user2
    // receives NewPost/NewSharedPost broadcasts.
    sharedRequest = await playwrightRequest.newContext({ baseURL })
    await ensureFriends(sharedRequest, baseURL, tokenAuthor, authorId, tokenFriend, friendId)
  })

  test.afterAll(async () => {
    await sharedRequest?.dispose()
  })

  test.afterEach(async () => {
    await disposeAll(connections.splice(0))
  })

  test('delivers NewPost to a connected friend when the author publishes', async ({ request }) => {
    const friendConn = await connectToHub(baseURL, 'feed', tokenFriend)
    connections.push(friendConn)

    const content = `signalr-newpost-${Date.now()}`
    const newPostEvent = waitForEvent<{ id: string; content: string }>(
      friendConn,
      'NewPost',
      {
        timeoutMs: 8000,
        filter: (p) => p.content === content,
      },
    )

    await createPost(request, baseURL, tokenAuthor, content)

    const received = await newPostEvent
    expect(received.content).toBe(content)
    expect(received.id).toBeTruthy()
  })

  test('delivers ReactionCountsUpdated to subscribers when a user reacts', async ({ request }) => {
    // Author posts something everyone can see.
    const post = await createPost(
      request, baseURL, tokenAuthor,
      `signalr-reaction-${Date.now()}`,
      'Everyone',
    )

    // A viewer subscribes to per-post updates.
    const viewerConn = await connectToHub(baseURL, 'feed', tokenAuthor)
    connections.push(viewerConn)
    await viewerConn.invoke('SubscribeToPost', post.id)

    const updateEvent = waitForEvent<{ postId: string; totalCount: number }>(
      viewerConn,
      'ReactionCountsUpdated',
      {
        timeoutMs: 8000,
        filter: (e) => String(e.postId).toLowerCase() === String(post.id).toLowerCase(),
      },
    )

    // Someone else reacts.
    const reactResp = await request.post(`${baseURL}/api/posts/${post.id}/reactions`, {
      headers: { Authorization: `Bearer ${tokenFriend}` },
      data: { reactionType: 'Like' },
    })
    expect(reactResp.status()).toBe(200)

    const update = await updateEvent
    expect(update.totalCount).toBeGreaterThanOrEqual(1)
  })

  test('delivers CommentReactionCountsUpdated for add, update, and remove', async ({ request }) => {
    const post = await createPost(
      request, baseURL, tokenAuthor,
      `signalr-comment-reaction-${Date.now()}`,
      'Everyone',
    )
    const commentResponse = await request.post(`${baseURL}/api/posts/${post.id}/comments`, {
      headers: { Authorization: `Bearer ${tokenAuthor}` },
      data: { content: `SignalR reaction comment ${Date.now()}` },
    })
    expect(commentResponse.status()).toBe(201)
    const comment = await commentResponse.json()

    const subscriber = await connectToHub(baseURL, 'feed', tokenAuthor)
    connections.push(subscriber)
    await subscriber.invoke('SubscribeToPost', post.id)

    type CommentReactionEvent = {
      postId: string
      commentId: string
      counts: { reactionType: string; count: number }[]
      total: number
    }
    const matchingComment = (event: CommentReactionEvent) =>
      String(event.postId).toLowerCase() === String(post.id).toLowerCase() &&
      String(event.commentId).toLowerCase() === String(comment.id).toLowerCase()

    try {
      const addEvent = waitForEvent<CommentReactionEvent>(
        subscriber,
        'CommentReactionCountsUpdated',
        { timeoutMs: 8000, filter: matchingComment },
      )
      const addResponse = await request.post(
        `${baseURL}/api/comments/${comment.id}/reactions`,
        {
          headers: { Authorization: `Bearer ${tokenAuthor}` },
          data: { reactionType: 'Like' },
        },
      )
      expect(addResponse.status()).toBe(200)
      expect(await addEvent).toEqual({
        postId: post.id,
        commentId: comment.id,
        counts: [{ reactionType: 'Like', count: 1 }],
        total: 1,
      })

      const updateEvent = waitForEvent<CommentReactionEvent>(
        subscriber,
        'CommentReactionCountsUpdated',
        { timeoutMs: 8000, filter: matchingComment },
      )
      const updateResponse = await request.post(
        `${baseURL}/api/comments/${comment.id}/reactions`,
        {
          headers: { Authorization: `Bearer ${tokenAuthor}` },
          data: { reactionType: 'Wow' },
        },
      )
      expect(updateResponse.status()).toBe(200)
      expect(await updateEvent).toEqual({
        postId: post.id,
        commentId: comment.id,
        counts: [{ reactionType: 'Wow', count: 1 }],
        total: 1,
      })

      const removeEvent = waitForEvent<CommentReactionEvent>(
        subscriber,
        'CommentReactionCountsUpdated',
        { timeoutMs: 8000, filter: matchingComment },
      )
      const removeResponse = await request.delete(
        `${baseURL}/api/comments/${comment.id}/reactions`,
        { headers: { Authorization: `Bearer ${tokenAuthor}` } },
      )
      expect(removeResponse.status()).toBe(200)
      expect(await removeEvent).toEqual({
        postId: post.id,
        commentId: comment.id,
        counts: [],
        total: 0,
      })
    } finally {
      const reactionCleanup = await request.delete(
        `${baseURL}/api/comments/${comment.id}/reactions`,
        { headers: { Authorization: `Bearer ${tokenAuthor}` } },
      )
      expect([200, 404]).toContain(reactionCleanup.status())
      const cleanup = await request.delete(`${baseURL}/api/posts/${post.id}`, {
        headers: { Authorization: `Bearer ${tokenAuthor}` },
      })
      expect([204, 404]).toContain(cleanup.status())
    }
  })

  test('delivers NewSharedPost to a connected friend when a post is shared', async ({ request }) => {
    // user2 plays the author/sharer here so we spread the per-user upload
    // rate-limit budget (POST /api/posts is `upload` = 20/hour/user — user1
    // is already the author in the previous two tests).
    const post = await createPost(
      request, baseURL, tokenFriend,
      `signalr-share-source-${Date.now()}`,
    )

    // user1 listens for the shared variant (they're user2's friend, so they
    // are a recipient of NewSharedPost).
    const listenerConn = await connectToHub(baseURL, 'feed', tokenAuthor)
    connections.push(listenerConn)

    const shareEvent = waitForEvent<{
      itemType: string
      sharedPost?: { id: string; originalPost: { id: string } }
    }>(listenerConn, 'NewSharedPost', {
      timeoutMs: 8000,
      filter: (evt) =>
        String(evt.sharedPost?.originalPost?.id ?? '').toLowerCase() ===
        String(post.id).toLowerCase(),
    })

    const shareResp = await request.post(`${baseURL}/api/posts/${post.id}/share`, {
      headers: { Authorization: `Bearer ${tokenFriend}` },
      data: { targetType: 'OwnWall', message: 'reshare via signalr e2e' },
    })
    expect([200, 201]).toContain(shareResp.status())

    const received = await shareEvent
    expect(received.itemType).toBe('SharedPost')
    expect(received.sharedPost?.originalPost.id).toBeTruthy()
  })
})
