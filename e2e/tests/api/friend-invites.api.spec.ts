import { test, expect } from '../../support/fixtures'
import { SEED } from '../../fixtures/seed-data'
import { getSharedTokenFor } from '../../support/shared-auth'

/**
 * API E2E tests for /api/friends/invite/* endpoints.
 */

test.describe('POST /api/friends/invite/link', () => {
  test('returns 401 without auth', async ({ request, baseURL }) => {
    const resp = await request.post(`${baseURL}/api/friends/invite/link`)
    expect(resp.status()).toBe(401)
  })

  test('returns link dto with token and inviteUrl', async ({ request, baseURL }) => {
    const { token } = getSharedTokenFor(SEED.user1)

    const resp = await request.post(`${baseURL}/api/friends/invite/link`, {
      headers: { Authorization: `Bearer ${token}` },
    })
    expect(resp.status()).toBe(200)

    const body = await resp.json()
    expect(typeof body.token).toBe('string')
    expect(body.token.length).toBeGreaterThan(0)
    expect(body.inviteUrl).toContain(body.token)
  })

  test('reuses same link on second call', async ({ request, baseURL }) => {
    const { token } = getSharedTokenFor(SEED.user1)

    const first = await request.post(`${baseURL}/api/friends/invite/link`, {
      headers: { Authorization: `Bearer ${token}` },
    })
    const second = await request.post(`${baseURL}/api/friends/invite/link`, {
      headers: { Authorization: `Bearer ${token}` },
    })
    expect(first.status()).toBe(200)
    expect(second.status()).toBe(200)

    const firstBody = await first.json()
    const secondBody = await second.json()
    expect(secondBody.token).toBe(firstBody.token)
  })

  test('after revoke, a new call returns a fresh token', async ({ request, baseURL }) => {
    const { token } = getSharedTokenFor(SEED.user1)

    const first = await request.post(`${baseURL}/api/friends/invite/link`, {
      headers: { Authorization: `Bearer ${token}` },
    })
    expect(first.status()).toBe(200)
    const firstBody = await first.json()

    const revokeResp = await request.delete(`${baseURL}/api/friends/invite/link`, {
      headers: { Authorization: `Bearer ${token}` },
    })
    expect(revokeResp.status()).toBe(204)

    const second = await request.post(`${baseURL}/api/friends/invite/link`, {
      headers: { Authorization: `Bearer ${token}` },
    })
    expect(second.status()).toBe(200)
    const secondBody = await second.json()

    expect(secondBody.token).not.toBe(firstBody.token)
  })
})

test.describe('GET /api/friends/invite/{token}', () => {
  test('returns 404 for unknown token', async ({ request, baseURL }) => {
    const resp = await request.get(`${baseURL}/api/friends/invite/completely-fake-token-xyz`)
    expect(resp.status()).toBe(404)
  })

  test('returns inviter info for a valid token', async ({ request, baseURL }) => {
    const { token } = getSharedTokenFor(SEED.user1)

    const linkResp = await request.post(`${baseURL}/api/friends/invite/link`, {
      headers: { Authorization: `Bearer ${token}` },
    })
    expect(linkResp.status()).toBe(200)
    const { token: inviteToken } = await linkResp.json()

    const resp = await request.get(`${baseURL}/api/friends/invite/${inviteToken}`)
    expect(resp.status()).toBe(200)
    const body = await resp.json()
    expect(body.inviterDisplayName).toBe(SEED.user1.displayName)
  })

  test('works without authentication (anonymous)', async ({ request, baseURL }) => {
    const { token } = getSharedTokenFor(SEED.user1)

    const linkResp = await request.post(`${baseURL}/api/friends/invite/link`, {
      headers: { Authorization: `Bearer ${token}` },
    })
    const { token: inviteToken } = await linkResp.json()

    // Explicitly no Authorization header.
    const resp = await request.get(`${baseURL}/api/friends/invite/${inviteToken}`)
    expect(resp.status()).toBe(200)
  })
})

test.describe('POST /api/friends/invite/{token}/accept', () => {
  test('returns 401 without auth', async ({ request, baseURL }) => {
    const resp = await request.post(`${baseURL}/api/friends/invite/some-token/accept`)
    expect(resp.status()).toBe(401)
  })

  test('returns 404 for unknown token', async ({ request, baseURL }) => {
    const { token } = getSharedTokenFor(SEED.user2)

    const resp = await request.post(`${baseURL}/api/friends/invite/completely-fake-token-xyz/accept`, {
      headers: { Authorization: `Bearer ${token}` },
    })
    expect(resp.status()).toBe(404)
  })

  test('returns 409 when acceptor is the inviter (self-accept)', async ({ request, baseURL }) => {
    const { token } = getSharedTokenFor(SEED.user1)

    const linkResp = await request.post(`${baseURL}/api/friends/invite/link`, {
      headers: { Authorization: `Bearer ${token}` },
    })
    const { token: inviteToken } = await linkResp.json()

    const acceptResp = await request.post(`${baseURL}/api/friends/invite/${inviteToken}/accept`, {
      headers: { Authorization: `Bearer ${token}` },
    })
    expect(acceptResp.status()).toBe(409)
  })

  test('happy path: user3 accepts user1 link invite and they become friends', async ({ request, baseURL }) => {
    const { token: token1, userId: userId1 } = getSharedTokenFor(SEED.user1)
    const { token: token3, userId: userId3 } = getSharedTokenFor(SEED.user3)

    // Clean up any pre-existing friendship between user1 and user3.
    await request.delete(`${baseURL}/api/friends/${userId3}`, {
      headers: { Authorization: `Bearer ${token1}` },
    })
    await request.delete(`${baseURL}/api/friends/${userId1}`, {
      headers: { Authorization: `Bearer ${token3}` },
    })

    const linkResp = await request.post(`${baseURL}/api/friends/invite/link`, {
      headers: { Authorization: `Bearer ${token1}` },
    })
    expect(linkResp.status()).toBe(200)
    const { token: inviteToken } = await linkResp.json()

    const acceptResp = await request.post(`${baseURL}/api/friends/invite/${inviteToken}/accept`, {
      headers: { Authorization: `Bearer ${token3}` },
    })
    expect(acceptResp.status()).toBe(200)

    const friendsOf1 = await request.get(`${baseURL}/api/friends`, {
      headers: { Authorization: `Bearer ${token1}` },
    })
    const list1 = await friendsOf1.json()
    const items1 = list1.items ?? list1
    expect(
      items1.some((f: { id?: string; user?: { id: string } }) => (f.user?.id ?? f.id) === userId3),
    ).toBeTruthy()

    const friendsOf3 = await request.get(`${baseURL}/api/friends`, {
      headers: { Authorization: `Bearer ${token3}` },
    })
    const list3 = await friendsOf3.json()
    const items3 = list3.items ?? list3
    expect(
      items3.some((f: { id?: string; user?: { id: string } }) => (f.user?.id ?? f.id) === userId1),
    ).toBeTruthy()

    // Cleanup.
    await request.delete(`${baseURL}/api/friends/${userId3}`, {
      headers: { Authorization: `Bearer ${token1}` },
    })
    await request.delete(`${baseURL}/api/friends/${userId1}`, {
      headers: { Authorization: `Bearer ${token3}` },
    })
  })

  test('accepting the same token twice is idempotent', async ({ request, baseURL }) => {
    const { token: token1, userId: userId1 } = getSharedTokenFor(SEED.user1)
    const { token: token3, userId: userId3 } = getSharedTokenFor(SEED.user3)

    // Clean up any pre-existing friendship between user1 and user3.
    await request.delete(`${baseURL}/api/friends/${userId3}`, {
      headers: { Authorization: `Bearer ${token1}` },
    })
    await request.delete(`${baseURL}/api/friends/${userId1}`, {
      headers: { Authorization: `Bearer ${token3}` },
    })

    const linkResp = await request.post(`${baseURL}/api/friends/invite/link`, {
      headers: { Authorization: `Bearer ${token1}` },
    })
    const { token: inviteToken } = await linkResp.json()

    const firstAccept = await request.post(`${baseURL}/api/friends/invite/${inviteToken}/accept`, {
      headers: { Authorization: `Bearer ${token3}` },
    })
    expect(firstAccept.status()).toBe(200)

    const secondAccept = await request.post(`${baseURL}/api/friends/invite/${inviteToken}/accept`, {
      headers: { Authorization: `Bearer ${token3}` },
    })
    expect(secondAccept.status()).toBe(200)

    // Cleanup.
    await request.delete(`${baseURL}/api/friends/${userId3}`, {
      headers: { Authorization: `Bearer ${token1}` },
    })
    await request.delete(`${baseURL}/api/friends/${userId1}`, {
      headers: { Authorization: `Bearer ${token3}` },
    })
  })
})

test.describe('DELETE /api/friends/invite/link', () => {
  test('returns 401 without auth', async ({ request, baseURL }) => {
    const resp = await request.delete(`${baseURL}/api/friends/invite/link`)
    expect(resp.status()).toBe(401)
  })

  test('returns 204 when there are no pending link invites (no-op)', async ({ request, baseURL }) => {
    const { token } = getSharedTokenFor(SEED.user2)

    // First revoke ensures no pending links remain, second call exercises the no-op path.
    await request.delete(`${baseURL}/api/friends/invite/link`, {
      headers: { Authorization: `Bearer ${token}` },
    })
    const resp = await request.delete(`${baseURL}/api/friends/invite/link`, {
      headers: { Authorization: `Bearer ${token}` },
    })
    expect(resp.status()).toBe(204)
  })

  test('revoked token can no longer be accepted', async ({ request, baseURL }) => {
    const { token: token1 } = getSharedTokenFor(SEED.user1)
    const { token: token2 } = getSharedTokenFor(SEED.user2)

    const linkResp = await request.post(`${baseURL}/api/friends/invite/link`, {
      headers: { Authorization: `Bearer ${token1}` },
    })
    const { token: inviteToken } = await linkResp.json()

    const revokeResp = await request.delete(`${baseURL}/api/friends/invite/link`, {
      headers: { Authorization: `Bearer ${token1}` },
    })
    expect(revokeResp.status()).toBe(204)

    const acceptResp = await request.post(`${baseURL}/api/friends/invite/${inviteToken}/accept`, {
      headers: { Authorization: `Bearer ${token2}` },
    })
    expect(acceptResp.status()).toBe(409)
  })
})

test.describe('POST /api/friends/invite/email', () => {
  test('returns 401 without auth', async ({ request, baseURL }) => {
    const resp = await request.post(`${baseURL}/api/friends/invite/email`, { data: { email: 'a@b.com' } })
    expect(resp.status()).toBe(401)
  })

  test('returns 400 for empty email', async ({ request, baseURL }) => {
    const { token } = getSharedTokenFor(SEED.user1)

    const resp = await request.post(`${baseURL}/api/friends/invite/email`, {
      headers: { Authorization: `Bearer ${token}` },
      data: {},
    })
    expect(resp.status()).toBe(400)
  })

  test('returns 400 for self-invite', async ({ request, baseURL }) => {
    const { token } = getSharedTokenFor(SEED.user1)

    const resp = await request.post(`${baseURL}/api/friends/invite/email`, {
      headers: { Authorization: `Bearer ${token}` },
      data: { email: SEED.user1.email },
    })
    expect(resp.status()).toBe(400)
  })

  test('returns 200 with outcome=accepted when target user exists', async ({ request, baseURL }) => {
    const { token: token1, userId: userId1 } = getSharedTokenFor(SEED.user1)
    const { userId: userId2 } = getSharedTokenFor(SEED.user2)

    // Clean up any pre-existing friendship between user1 and user2.
    await request.delete(`${baseURL}/api/friends/${userId2}`, {
      headers: { Authorization: `Bearer ${token1}` },
    })
    const { token: token2 } = getSharedTokenFor(SEED.user2)
    await request.delete(`${baseURL}/api/friends/${userId1}`, {
      headers: { Authorization: `Bearer ${token2}` },
    })

    const resp = await request.post(`${baseURL}/api/friends/invite/email`, {
      headers: { Authorization: `Bearer ${token1}` },
      data: { email: SEED.user2.email },
    })
    expect(resp.status()).toBe(200)
    const body = await resp.json()
    expect(body.outcome).toBe('accepted')

    const friendsOf1 = await request.get(`${baseURL}/api/friends`, {
      headers: { Authorization: `Bearer ${token1}` },
    })
    const list1 = await friendsOf1.json()
    const items1 = list1.items ?? list1
    expect(
      items1.some((f: { id?: string; user?: { id: string } }) => (f.user?.id ?? f.id) === userId2),
    ).toBeTruthy()

    // Cleanup.
    await request.delete(`${baseURL}/api/friends/${userId2}`, {
      headers: { Authorization: `Bearer ${token1}` },
    })
    await request.delete(`${baseURL}/api/friends/${userId1}`, {
      headers: { Authorization: `Bearer ${token2}` },
    })
  })

  test('returns 200 with outcome=invited for unknown email', async ({ request, baseURL }) => {
    const { token } = getSharedTokenFor(SEED.user1)

    const resp = await request.post(`${baseURL}/api/friends/invite/email`, {
      headers: { Authorization: `Bearer ${token}` },
      data: { email: 'no-account-e2e@example.com' },
    })
    expect(resp.status()).toBe(200)
    const body = await resp.json()
    expect(body.outcome).toBe('invited')
  })
})
