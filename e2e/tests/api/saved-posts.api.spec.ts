import { randomUUID } from 'node:crypto'
import {
  test,
  expect,
  type APIRequestContext,
  type APIResponse,
} from '../../support/fixtures'
import { SEED, TEST_LOCATION } from '../../fixtures/seed-data'
import { getSharedTokenFor } from '../../support/shared-auth'

/**
 * API E2E coverage for private post bookmarks:
 *   POST   /api/posts/{postId}/save
 *   DELETE /api/posts/{postId}/save
 *   GET    /api/users/me/saved-posts
 *
 * A fresh saver account is used on every run, so pagination assertions cannot
 * be polluted by bookmarks from another spec or an earlier interrupted run.
 * The post author uses shared auth (and has their privacy restored in afterAll).
 */

type AuthState = {
  email: string
  token: string
  userId: string
}

type PostDto = {
  id: string
  content: string
  isSavedByMe: boolean
  savesCount?: number
}

type SavedPostsPage = {
  items: PostDto[]
  page: number
  pageSize: number
  hasMore: boolean
}

const RUN_ID = `${Date.now().toString(36)}-${process.pid}`
const PASSWORD = 'Test@12345!'

function authHeaders(token: string) {
  return { Authorization: `Bearer ${token}` }
}

function isolatedAuthHeaders(purpose: string) {
  return { 'X-Forwarded-For': `198.51.100.25-${RUN_ID}-${purpose}` }
}

async function registerIsolatedUser(
  request: APIRequestContext,
  baseURL: string | undefined,
  purpose: string,
): Promise<AuthState> {
  const email = `saved-posts-${purpose}-${RUN_ID}@omnijoy.test`
  const resp = await request.post(`${baseURL}/api/auth/register`, {
    headers: isolatedAuthHeaders(`register-${purpose}`),
    data: {
      email,
      displayName: `Saved Posts ${purpose} ${RUN_ID}`,
      authMethod: 'password',
      password: PASSWORD,
      gender: 'NotDisclosed',
      birthDate: '1990-01-01',
      ...TEST_LOCATION,
    },
  })

  expect(resp.status()).toBe(200)
  const body = await resp.json()
  return { email, token: body.accessToken as string, userId: body.user.id as string }
}

async function createPost(
  request: APIRequestContext,
  baseURL: string | undefined,
  token: string,
  content: string,
  privacy = 'Everyone',
) {
  const resp = await request.post(`${baseURL}/api/posts`, {
    headers: authHeaders(token),
    multipart: { content, postType: 'Text', privacy },
  })
  expect(resp.status()).toBe(201)
  return (await resp.json()).id as string
}

async function savedPosts(
  request: APIRequestContext,
  baseURL: string | undefined,
  token: string,
  query = '',
): Promise<SavedPostsPage> {
  const resp = await request.get(`${baseURL}/api/users/me/saved-posts${query}`, {
    headers: authHeaders(token),
  })
  expect(resp.status()).toBe(200)
  return await resp.json() as SavedPostsPage
}

test.describe.serial('Saved posts API', () => {
  let authorToken = ''
  let saver: AuthState
  let originalAuthorPrivacy: Record<string, string>
  const postIds: string[] = []
  const posts: Record<'first' | 'second' | 'third' | 'visibility' | 'deletion' | 'private', string> = {
    first: '',
    second: '',
    third: '',
    visibility: '',
    deletion: '',
    private: '',
  }

  test.beforeAll(async ({ request, baseURL }) => {
    authorToken = getSharedTokenFor(SEED.user1).token
    saver = await registerIsolatedUser(request, baseURL, 'functional')

    const privacyResp = await request.get(`${baseURL}/api/users/me/privacy`, {
      headers: authHeaders(authorToken),
    })
    expect(privacyResp.status()).toBe(200)
    originalAuthorPrivacy = await privacyResp.json()

    // Per-post Everyone is not enough when the author's global post setting is
    // Friends. Make these posts visible to the isolated saver for this describe.
    const publicPrivacyResp = await request.put(`${baseURL}/api/users/me/privacy`, {
      headers: authHeaders(authorToken),
      data: { whoCanSeePosts: 'Everyone' },
    })
    expect(publicPrivacyResp.status()).toBe(200)

    for (const key of ['first', 'second', 'third', 'visibility', 'deletion'] as const) {
      posts[key] = await createPost(
        request,
        baseURL,
        authorToken,
        `saved-posts ${key} ${RUN_ID}`,
      )
      postIds.push(posts[key])
    }
    posts.private = await createPost(
      request,
      baseURL,
      authorToken,
      `saved-posts private ${RUN_ID}`,
      'OnlyMe',
    )
    postIds.push(posts.private)
  })

  test.afterAll(async ({ request, baseURL }) => {
    if (saver?.token) {
      for (const postId of postIds) {
        await request.delete(`${baseURL}/api/posts/${postId}/save`, {
          headers: authHeaders(saver.token),
        })
      }
      await request.post(`${baseURL}/api/account/delete`, {
        headers: authHeaders(saver.token),
        data: { confirmEmail: saver.email },
      })
    }

    for (const postId of postIds) {
      await request.delete(`${baseURL}/api/posts/${postId}`, {
        headers: authHeaders(authorToken),
      })
    }

    if (originalAuthorPrivacy) {
      await request.put(`${baseURL}/api/users/me/privacy`, {
        headers: authHeaders(authorToken),
        data: originalAuthorPrivacy,
      })
    }
  })

  test('requires authentication for saving and listing', async ({ request, baseURL }) => {
    const saveResp = await request.post(`${baseURL}/api/posts/${posts.first}/save`)
    expect(saveResp.status()).toBe(401)

    const listResp = await request.get(`${baseURL}/api/users/me/saved-posts`)
    expect(listResp.status()).toBe(401)
  })

  test('saves idempotently and hydrates private viewer state', async ({ request, baseURL }) => {
    const firstSave = await request.post(`${baseURL}/api/posts/${posts.first}/save`, {
      headers: authHeaders(saver.token),
    })
    expect(firstSave.status()).toBe(200)
    expect(await firstSave.json()).toEqual({ isSaved: true, changed: true })

    const repeatedSave = await request.post(`${baseURL}/api/posts/${posts.first}/save`, {
      headers: authHeaders(saver.token),
    })
    expect(repeatedSave.status()).toBe(200)
    expect(await repeatedSave.json()).toEqual({ isSaved: true, changed: false })

    // The list exposes normal PostDto objects, not private SavedPost row IDs.
    const list = await savedPosts(request, baseURL, saver.token, '?pageSize=50')
    expect(list.items.filter((post) => post.id === posts.first)).toHaveLength(1)
    expect(list.items.find((post) => post.id === posts.first)?.isSavedByMe).toBe(true)

    const saverViewResp = await request.get(`${baseURL}/api/posts/${posts.first}`, {
      headers: authHeaders(saver.token),
    })
    const saverView = await saverViewResp.json() as PostDto
    expect(saverView.isSavedByMe).toBe(true)
    expect(Object.prototype.hasOwnProperty.call(saverView, 'savesCount')).toBe(false)

    const authorViewResp = await request.get(`${baseURL}/api/posts/${posts.first}`, {
      headers: authHeaders(authorToken),
    })
    const authorView = await authorViewResp.json() as PostDto
    expect(authorView.isSavedByMe).toBe(false)
    expect(authorView.savesCount).toBe(1)

    const anonymousViewResp = await request.get(`${baseURL}/api/posts/${posts.first}`)
    const anonymousView = await anonymousViewResp.json() as PostDto
    expect(anonymousView.isSavedByMe).toBe(false)
    expect(Object.prototype.hasOwnProperty.call(anonymousView, 'savesCount')).toBe(false)
  })

  test('orders newest saves first and enforces page/pageSize boundaries', async ({
    request,
    baseURL,
  }) => {
    const secondSave = await request.post(`${baseURL}/api/posts/${posts.second}/save`, {
      headers: authHeaders(saver.token),
    })
    expect(secondSave.status()).toBe(200)
    await new Promise((resolve) => setTimeout(resolve, 10))

    const thirdSave = await request.post(`${baseURL}/api/posts/${posts.third}/save`, {
      headers: authHeaders(saver.token),
    })
    expect(thirdSave.status()).toBe(200)

    const firstPage = await savedPosts(request, baseURL, saver.token, '?page=1&pageSize=2')
    expect(firstPage).toMatchObject({ page: 1, pageSize: 2, hasMore: true })
    expect(firstPage.items.map((post) => post.id)).toEqual([posts.third, posts.second])
    expect(firstPage.items.every((post) => post.isSavedByMe)).toBe(true)

    const secondPage = await savedPosts(request, baseURL, saver.token, '?page=2&pageSize=2')
    expect(secondPage).toMatchObject({ page: 2, pageSize: 2, hasMore: false })
    expect(secondPage.items.map((post) => post.id)).toEqual([posts.first])

    const emptyPage = await savedPosts(request, baseURL, saver.token, '?page=4&pageSize=1')
    expect(emptyPage).toMatchObject({ items: [], page: 4, pageSize: 1, hasMore: false })

    const normalized = await savedPosts(request, baseURL, saver.token, '?page=0&pageSize=0')
    expect(normalized).toMatchObject({ page: 1, pageSize: 20, hasMore: false })
    expect(normalized.items.map((post) => post.id)).toEqual([
      posts.third,
      posts.second,
      posts.first,
    ])

    const maxPageSize = await savedPosts(request, baseURL, saver.token, '?page=1&pageSize=50')
    expect(maxPageSize.pageSize).toBe(50)

    const oversized = await savedPosts(request, baseURL, saver.token, '?page=1&pageSize=51')
    expect(oversized.pageSize).toBe(20)
  })

  test('keeps saves across a fresh login', async ({ request, baseURL }) => {
    const loginResp = await request.post(`${baseURL}/api/auth/login`, {
      headers: isolatedAuthHeaders('fresh-login'),
      data: { email: saver.email, password: PASSWORD },
    })
    expect(loginResp.status()).toBe(200)
    const loginBody = await loginResp.json()
    expect(loginBody.accessToken).not.toBe(saver.token)
    saver.token = loginBody.accessToken as string

    const persisted = await savedPosts(request, baseURL, saver.token, '?pageSize=50')
    expect(persisted.items.map((post) => post.id)).toEqual([
      posts.third,
      posts.second,
      posts.first,
    ])
  })

  test('does not leak whether an inaccessible post exists', async ({ request, baseURL }) => {
    const missingId = randomUUID()
    const invisibleResp = await request.post(`${baseURL}/api/posts/${posts.private}/save`, {
      headers: authHeaders(saver.token),
    })
    const missingResp = await request.post(`${baseURL}/api/posts/${missingId}/save`, {
      headers: authHeaders(saver.token),
    })

    expect(invisibleResp.status()).toBe(404)
    expect(missingResp.status()).toBe(404)
    expect(await invisibleResp.json()).toEqual({ error: 'Post not found.' })
    expect(await missingResp.json()).toEqual({ error: 'Post not found.' })
  })

  test('silently omits saves after visibility and deletion changes', async ({
    request,
    baseURL,
  }) => {
    for (const postId of [posts.visibility, posts.deletion]) {
      const saveResp = await request.post(`${baseURL}/api/posts/${postId}/save`, {
        headers: authHeaders(saver.token),
      })
      expect(saveResp.status()).toBe(200)
    }

    const hideResp = await request.put(`${baseURL}/api/posts/${posts.visibility}`, {
      headers: authHeaders(authorToken),
      data: { privacy: 'OnlyMe' },
    })
    expect(hideResp.status()).toBe(200)

    const deleteResp = await request.delete(`${baseURL}/api/posts/${posts.deletion}`, {
      headers: authHeaders(authorToken),
    })
    expect(deleteResp.status()).toBe(204)

    const afterChanges = await savedPosts(request, baseURL, saver.token, '?pageSize=50')
    expect(afterChanges.items.map((post) => post.id)).not.toContain(posts.visibility)
    expect(afterChanges.items.map((post) => post.id)).not.toContain(posts.deletion)

    // A visibility-filtered bookmark is retained and returns when visible again.
    const revealResp = await request.put(`${baseURL}/api/posts/${posts.visibility}`, {
      headers: authHeaders(authorToken),
      data: { privacy: 'Everyone' },
    })
    expect(revealResp.status()).toBe(200)
    const afterReveal = await savedPosts(request, baseURL, saver.token, '?pageSize=50')
    expect(afterReveal.items.map((post) => post.id)).toContain(posts.visibility)
  })

  test('unsaves idempotently and clears PostDto viewer state', async ({ request, baseURL }) => {
    const unsaveResp = await request.delete(`${baseURL}/api/posts/${posts.first}/save`, {
      headers: authHeaders(saver.token),
    })
    expect(unsaveResp.status()).toBe(200)
    expect(await unsaveResp.json()).toEqual({ isSaved: false, changed: true })

    const repeatedUnsave = await request.delete(`${baseURL}/api/posts/${posts.first}/save`, {
      headers: authHeaders(saver.token),
    })
    expect(repeatedUnsave.status()).toBe(200)
    expect(await repeatedUnsave.json()).toEqual({ isSaved: false, changed: false })

    const detailResp = await request.get(`${baseURL}/api/posts/${posts.first}`, {
      headers: authHeaders(saver.token),
    })
    const detail = await detailResp.json() as PostDto
    expect(detail.isSavedByMe).toBe(false)

    const list = await savedPosts(request, baseURL, saver.token, '?pageSize=50')
    expect(list.items.map((post) => post.id)).not.toContain(posts.first)

    const authorResp = await request.get(`${baseURL}/api/posts/${posts.first}`, {
      headers: authHeaders(authorToken),
    })
    expect((await authorResp.json() as PostDto).savesCount).toBe(0)
  })

  test('applies one shared interaction bucket to save and unsave writes', async ({
    request,
    baseURL,
  }) => {
    test.setTimeout(180_000)
    const rateUser = await registerIsolatedUser(request, baseURL, 'rate-limit')
    const missingId = randomUUID()
    const statuses: number[] = []
    let rejectedResponse: APIResponse | undefined

    try {
      // Production permits 60 interactions/minute; the documented E2E profile
      // raises that to 1000. Alternate both saved-post write routes so a missing
      // policy on either action changes the rejection boundary and fails here.
      for (let attempt = 0; attempt <= 1000; attempt++) {
        const url = `${baseURL}/api/posts/${missingId}/save`
        const resp = attempt % 2 === 0
          ? await request.post(url, { headers: authHeaders(rateUser.token) })
          : await request.delete(url, { headers: authHeaders(rateUser.token) })
        statuses.push(resp.status())
        if (resp.status() === 429) {
          rejectedResponse = resp
          break
        }
        // Save deliberately hides a missing post (404); idempotent unsave has
        // no row to remove and therefore returns its normal unchanged 200.
        expect(resp.status()).toBe(attempt % 2 === 0 ? 404 : 200)
      }

      const rejectionIndex = statuses.indexOf(429)
      expect(rejectionIndex, 'interaction limiter never rejected the isolated user').toBeGreaterThan(-1)
      expect([60, 1000], 'rejection must match the production or E2E interaction limit')
        .toContain(rejectionIndex)

      const retryAfter = Number.parseInt(
        // Playwright lower-cases response header names.
        rejectedResponse?.headers()['retry-after'] ?? '',
        10,
      )
      expect(Number.isFinite(retryAfter)).toBe(true)
      expect(retryAfter).toBeGreaterThan(0)
      expect(retryAfter).toBeLessThanOrEqual(60)
    } finally {
      await request.post(`${baseURL}/api/account/delete`, {
        headers: authHeaders(rateUser.token),
        data: { confirmEmail: rateUser.email },
      })
    }
  })
})
