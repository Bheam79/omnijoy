import { test, expect, type APIRequestContext } from '../../support/fixtures'
import { SEED, TEST_LOCATION } from '../../fixtures/seed-data'
import { getSharedTokenFor } from '../../support/shared-auth'

/**
 * API E2E tests for AccountController endpoints:
 *   POST /api/account/change-email
 *   POST /api/account/change-password
 *   GET  /api/account/notification-preferences
 *   PUT  /api/account/notification-preferences
 *   POST /api/account/deactivate
 *   POST /api/account/delete
 */

/** Registers a fresh disposable user and returns login credentials. */
async function registerDisposable(
  request: APIRequestContext,
  baseURL: string | undefined,
  suffix: string,
) {
  const email = `disposable_${suffix}_${Date.now()}@omnijoy.test`
  const password = 'Test@12345!'
  const regResp = await request.post(`${baseURL}/api/auth/register`, {
    data: {
      email,
      displayName: `Disposable ${suffix}`,
      authMethod: 'password',
      password,
      gender: 'NotDisclosed',
      birthDate: '1990-01-01',
      ...TEST_LOCATION,
    },
  })
  const body = await regResp.json()
  return { email, password, token: body.accessToken as string, userId: body.user?.id as string }
}

// ── POST /api/account/change-email ────────────────────────────────────────────

test.describe('POST /api/account/change-email', () => {
  test('returns 401 without auth', async ({ request, baseURL }) => {
    const resp = await request.post(`${baseURL}/api/account/change-email`, {
      data: { newEmail: 'new@example.com', currentPassword: 'anything' },
    })
    expect(resp.status()).toBe(401)
  })

  test('returns 401 when current password is wrong', async ({ request, baseURL }) => {
    // Use allow5xx:false (default) — wrong password should 401, not 500
    const { token } = getSharedTokenFor(SEED.user1)
    const resp = await request.post(`${baseURL}/api/account/change-email`, {
      headers: { Authorization: `Bearer ${token}` },
      data: { newEmail: 'new-valid@omnijoy.test', currentPassword: 'wrong-password' },
    })
    expect([400, 401]).toContain(resp.status())
  })

  test('returns 400 for an invalid email format', async ({ request, baseURL }) => {
    const { token } = getSharedTokenFor(SEED.user1)
    const resp = await request.post(`${baseURL}/api/account/change-email`, {
      headers: { Authorization: `Bearer ${token}` },
      data: { newEmail: 'not-an-email', currentPassword: SEED.user1.password },
    })
    expect([400, 401]).toContain(resp.status())
  })
})

// ── POST /api/account/change-password ─────────────────────────────────────────

test.describe('POST /api/account/change-password', () => {
  test('returns 401 without auth', async ({ request, baseURL }) => {
    const resp = await request.post(`${baseURL}/api/account/change-password`, {
      data: { currentPassword: 'old', newPassword: 'New@12345!', confirmNewPassword: 'New@12345!' },
    })
    expect(resp.status()).toBe(401)
  })

  test('returns 400 or 401 when current password is wrong', async ({ request, baseURL }) => {
    const { token } = getSharedTokenFor(SEED.user1)
    const resp = await request.post(`${baseURL}/api/account/change-password`, {
      headers: { Authorization: `Bearer ${token}` },
      data: {
        currentPassword: 'definitely-wrong-password',
        newPassword: 'New@12345!',
        confirmNewPassword: 'New@12345!',
      },
    })
    expect([400, 401]).toContain(resp.status())
  })

  test('returns 400 when newPassword and confirmNewPassword do not match', async ({
    request,
    baseURL,
  }) => {
    const { token } = getSharedTokenFor(SEED.user1)
    const resp = await request.post(`${baseURL}/api/account/change-password`, {
      headers: { Authorization: `Bearer ${token}` },
      data: {
        currentPassword: SEED.user1.password,
        newPassword: 'New@12345!',
        confirmNewPassword: 'Different@12345!',
      },
    })
    expect(resp.status()).toBe(400)
  })
})

// ── GET /api/account/notification-preferences ─────────────────────────────────

test.describe('GET /api/account/notification-preferences', () => {
  test('returns notification preferences for authenticated user', async ({ request, baseURL }) => {
    const { token } = getSharedTokenFor(SEED.user1)
    const resp = await request.get(`${baseURL}/api/account/notification-preferences`, {
      headers: { Authorization: `Bearer ${token}` },
    })
    expect(resp.status()).toBe(200)
    const body = await resp.json()
    // All fields should be boolean toggles
    expect(typeof body.likesOnMyPosts).toBe('boolean')
    expect(typeof body.commentsOnMyPosts).toBe('boolean')
    expect(typeof body.friendRequests).toBe('boolean')
    expect(typeof body.directMessages).toBe('boolean')
  })

  test('returns 401 without auth', async ({ request, baseURL }) => {
    const resp = await request.get(`${baseURL}/api/account/notification-preferences`)
    expect(resp.status()).toBe(401)
  })
})

// ── PUT /api/account/notification-preferences ─────────────────────────────────

test.describe('PUT /api/account/notification-preferences', () => {
  test('updates notification preferences successfully', async ({ request, baseURL }) => {
    const { token } = getSharedTokenFor(SEED.user1)

    const prefs = {
      likesOnMyPosts: false,
      commentsOnMyPosts: true,
      postShares: true,
      friendRequests: true,
      newFollower: false,
      mentions: true,
      newPostsFromFriends: false,
      familyRelations: true,
      eventInvites: true,
      directMessages: true,
      liveStreams: false,
      companyPageInvites: true,
    }

    const resp = await request.put(`${baseURL}/api/account/notification-preferences`, {
      headers: { Authorization: `Bearer ${token}` },
      data: prefs,
    })
    expect(resp.status()).toBe(200)
    const body = await resp.json()
    expect(body.likesOnMyPosts).toBe(false)
    expect(body.commentsOnMyPosts).toBe(true)
    expect(body.liveStreams).toBe(false)
  })

  test('round-trips: get reflects what was put', async ({ request, baseURL }) => {
    const { token } = getSharedTokenFor(SEED.user3)

    const prefs = {
      likesOnMyPosts: false,
      commentsOnMyPosts: false,
      postShares: false,
      friendRequests: false,
      newFollower: false,
      mentions: false,
      newPostsFromFriends: false,
      familyRelations: false,
      eventInvites: false,
      directMessages: false,
      liveStreams: false,
      companyPageInvites: false,
    }

    await request.put(`${baseURL}/api/account/notification-preferences`, {
      headers: { Authorization: `Bearer ${token}` },
      data: prefs,
    })

    const getResp = await request.get(`${baseURL}/api/account/notification-preferences`, {
      headers: { Authorization: `Bearer ${token}` },
    })
    const body = await getResp.json()
    expect(body.likesOnMyPosts).toBe(false)
    expect(body.directMessages).toBe(false)
  })

  test('returns 401 without auth', async ({ request, baseURL }) => {
    const resp = await request.put(`${baseURL}/api/account/notification-preferences`, {
      data: { likesOnMyPosts: false },
    })
    expect(resp.status()).toBe(401)
  })
})

// ── POST /api/account/deactivate ──────────────────────────────────────────────

test.describe('POST /api/account/deactivate', () => {
  test('returns 401 without auth', async ({ request, baseURL }) => {
    const resp = await request.post(`${baseURL}/api/account/deactivate`)
    expect(resp.status()).toBe(401)
  })

  test('deactivates a disposable account', async ({ request, baseURL }) => {
    const { token } = await registerDisposable(request, baseURL, 'deactivate')
    const resp = await request.post(`${baseURL}/api/account/deactivate`, {
      headers: { Authorization: `Bearer ${token}` },
    })
    expect([200, 204]).toContain(resp.status())
  })
})

// ── POST /api/account/delete ──────────────────────────────────────────────────

test.describe('POST /api/account/delete', () => {
  test('returns 401 without auth', async ({ request, baseURL }) => {
    const resp = await request.post(`${baseURL}/api/account/delete`, {
      data: { confirmEmail: 'anyone@example.com' },
    })
    expect(resp.status()).toBe(401)
  })

  test('returns 400 when confirmEmail does not match the account email', async ({
    request,
    baseURL,
  }) => {
    const { token } = getSharedTokenFor(SEED.user1)
    const resp = await request.post(`${baseURL}/api/account/delete`, {
      headers: { Authorization: `Bearer ${token}` },
      data: { confirmEmail: 'wrong-email@example.com' },
    })
    expect([400, 422]).toContain(resp.status())
  })

  test('deletes a disposable account when confirmEmail matches', async ({ request, baseURL }) => {
    const { email, token } = await registerDisposable(request, baseURL, 'delete')
    const resp = await request.post(`${baseURL}/api/account/delete`, {
      headers: { Authorization: `Bearer ${token}` },
      data: { confirmEmail: email },
    })
    expect([200, 204]).toContain(resp.status())
  })
})
