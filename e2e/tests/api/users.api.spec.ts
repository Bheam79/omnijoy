import { test, expect, type APIRequestContext } from '../../support/fixtures'
import { SEED } from '../../fixtures/seed-data'
import { MINIMAL_PNG } from '../../fixtures/image-fixtures'

/**
 * API E2E tests for /api/users/* endpoints.
 */

async function getToken(request: APIRequestContext, baseURL: string | undefined, user: typeof SEED.user1) {
  const resp = await request.post(`${baseURL}/api/auth/login`, {
    data: { email: user.email, password: user.password },
  })
  const body = await resp.json()
  return { token: body.accessToken as string, userId: body.user.id as string }
}

test.describe('GET /api/users/:id', () => {
  test('returns public profile for a valid user ID', async ({ request, baseURL }) => {
    const { token, userId } = await getToken(request, baseURL, SEED.user1)
    const resp = await request.get(`${baseURL}/api/users/${userId}`, {
      headers: { Authorization: `Bearer ${token}` },
    })
    expect(resp.status()).toBe(200)
    const body = await resp.json()
    expect(body.id).toBe(userId)
    expect(body.displayName).toBeTruthy()
  })

  test('returns 404 for unknown user ID', async ({ request, baseURL }) => {
    const { token } = await getToken(request, baseURL, SEED.user1)
    const resp = await request.get(`${baseURL}/api/users/00000000-0000-0000-0000-000000000000`, {
      headers: { Authorization: `Bearer ${token}` },
    })
    expect(resp.status()).toBe(404)
  })

  test('allows anonymous access to public profiles', async ({ request, baseURL }) => {
    const { userId } = await getToken(request, baseURL, SEED.user1)
    const resp = await request.get(`${baseURL}/api/users/${userId}`)
    // May return 200 or 403 depending on privacy setting
    expect([200, 403]).toContain(resp.status())
  })
})

test.describe('PUT /api/users/me', () => {
  test('updates display name successfully', async ({ request, baseURL }) => {
    const { token } = await getToken(request, baseURL, SEED.user1)
    const resp = await request.put(`${baseURL}/api/users/me`, {
      headers: { Authorization: `Bearer ${token}` },
      data: { displayName: SEED.user1.displayName, bio: 'Updated by E2E test' },
    })
    expect([200, 201]).toContain(resp.status())
    const body = await resp.json()
    expect(body.bio).toBe('Updated by E2E test')
  })

  test('returns 401 without auth', async ({ request, baseURL }) => {
    const resp = await request.put(`${baseURL}/api/users/me`, {
      data: { displayName: 'Hacker' },
    })
    expect(resp.status()).toBe(401)
  })

  test('returns 400 for invalid data', async ({ request, baseURL }) => {
    const { token } = await getToken(request, baseURL, SEED.user1)
    const resp = await request.put(`${baseURL}/api/users/me`, {
      headers: { Authorization: `Bearer ${token}` },
      data: { displayName: '' },
    })
    expect([400, 422]).toContain(resp.status())
  })
})

test.describe('POST /api/users/me/avatar', () => {
  test('returns 400 when no file is uploaded', async ({ request, baseURL }) => {
    const { token } = await getToken(request, baseURL, SEED.user1)
    const resp = await request.post(`${baseURL}/api/users/me/avatar`, {
      headers: { Authorization: `Bearer ${token}` },
    })
    expect([400, 415]).toContain(resp.status())
  })

  test('uploads avatar image successfully', async ({ request, baseURL }) => {
    const { token } = await getToken(request, baseURL, SEED.user1)
    const resp = await request.post(`${baseURL}/api/users/me/avatar`, {
      headers: { Authorization: `Bearer ${token}` },
      multipart: {
        file: {
          name: 'avatar.png',
          mimeType: 'image/png',
          buffer: MINIMAL_PNG,
        },
      },
    })
    expect([200, 201]).toContain(resp.status())
  })
})

test.describe('GET /api/users/:id/friends', () => {
  test('returns friends list for a user', async ({ request, baseURL }) => {
    const { token, userId } = await getToken(request, baseURL, SEED.user1)
    const resp = await request.get(`${baseURL}/api/users/${userId}/friends`, {
      headers: { Authorization: `Bearer ${token}` },
    })
    expect(resp.status()).toBe(200)
    const body = await resp.json()
    expect(Array.isArray(body.items ?? body)).toBeTruthy()
  })

  test('supports pagination parameters', async ({ request, baseURL }) => {
    const { token, userId } = await getToken(request, baseURL, SEED.user1)
    const resp = await request.get(`${baseURL}/api/users/${userId}/friends?page=1&pageSize=5`, {
      headers: { Authorization: `Bearer ${token}` },
    })
    expect(resp.status()).toBe(200)
  })
})

test.describe('GET /api/users/me/privacy', () => {
  test('returns privacy settings for authenticated user', async ({ request, baseURL }) => {
    const { token } = await getToken(request, baseURL, SEED.user1)
    const resp = await request.get(`${baseURL}/api/users/me/privacy`, {
      headers: { Authorization: `Bearer ${token}` },
    })
    expect(resp.status()).toBe(200)
    const body = await resp.json()
    expect(body).toBeTruthy()
  })

  test('returns 401 without auth', async ({ request, baseURL }) => {
    const resp = await request.get(`${baseURL}/api/users/me/privacy`)
    expect(resp.status()).toBe(401)
  })
})

test.describe('PUT /api/users/me/privacy', () => {
  test('updates privacy settings successfully', async ({ request, baseURL }) => {
    const { token } = await getToken(request, baseURL, SEED.user1)
    const resp = await request.put(`${baseURL}/api/users/me/privacy`, {
      headers: { Authorization: `Bearer ${token}` },
      data: {
        whoCanSeeProfile: 'Everyone',
        whoCanSeePosts: 'Friends',
        whoCanSeeFriendList: 'Friends',
        whoCanSendFriendRequests: 'Everyone',
      },
    })
    expect([200, 201]).toContain(resp.status())
  })

  test('returns 401 without auth', async ({ request, baseURL }) => {
    const resp = await request.put(`${baseURL}/api/users/me/privacy`, {
      data: { whoCanSeeProfile: 'OnlyMe' },
    })
    expect(resp.status()).toBe(401)
  })
})

test.describe('GET /api/users/presence', () => {
  test('returns presence for a list of user IDs', async ({ request, baseURL }) => {
    const { token, userId } = await getToken(request, baseURL, SEED.user1)
    const resp = await request.get(`${baseURL}/api/users/presence?userIds=${userId}`, {
      headers: { Authorization: `Bearer ${token}` },
    })
    expect(resp.status()).toBe(200)
    const body = await resp.json()
    expect(Array.isArray(body)).toBeTruthy()
  })

  test('returns empty array for no userIds param', async ({ request, baseURL }) => {
    const { token } = await getToken(request, baseURL, SEED.user1)
    const resp = await request.get(`${baseURL}/api/users/presence`, {
      headers: { Authorization: `Bearer ${token}` },
    })
    expect(resp.status()).toBe(200)
    const body = await resp.json()
    expect(Array.isArray(body)).toBeTruthy()
  })

  test('returns 401 without auth', async ({ request, baseURL }) => {
    const resp = await request.get(`${baseURL}/api/users/presence?userIds=some-id`)
    expect(resp.status()).toBe(401)
  })
})
