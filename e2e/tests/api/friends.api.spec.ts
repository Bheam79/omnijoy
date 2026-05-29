import { test, expect } from '../../support/fixtures'
import { SEED } from '../../fixtures/seed-data'
import { getSharedTokenFor } from '../../support/shared-auth'

/**
 * API E2E tests for /api/friends/* endpoints.
 */


test.describe('GET /api/friends', () => {
  test('returns friend list for authenticated user', async ({ request, baseURL }) => {
    const { token } = getSharedTokenFor(SEED.user1)
    const resp = await request.get(`${baseURL}/api/friends`, {
      headers: { Authorization: `Bearer ${token}` },
    })
    expect(resp.status()).toBe(200)
    const body = await resp.json()
    expect(Array.isArray(body.items ?? body)).toBeTruthy()
  })

  test('returns 401 without auth', async ({ request, baseURL }) => {
    const resp = await request.get(`${baseURL}/api/friends`)
    expect(resp.status()).toBe(401)
  })
})

test.describe('GET /api/friends/requests', () => {
  test('returns pending friend requests', async ({ request, baseURL }) => {
    const { token } = getSharedTokenFor(SEED.user2)
    const resp = await request.get(`${baseURL}/api/friends/requests`, {
      headers: { Authorization: `Bearer ${token}` },
    })
    expect(resp.status()).toBe(200)
    const body = await resp.json()
    expect(Array.isArray(body)).toBeTruthy()
  })
})

test.describe('GET /api/friends/sent', () => {
  test('returns sent friend requests', async ({ request, baseURL }) => {
    const { token } = getSharedTokenFor(SEED.user1)
    const resp = await request.get(`${baseURL}/api/friends/sent`, {
      headers: { Authorization: `Bearer ${token}` },
    })
    expect(resp.status()).toBe(200)
    const body = await resp.json()
    expect(Array.isArray(body)).toBeTruthy()
  })
})

test.describe('GET /api/friends/suggestions', () => {
  test('returns friend suggestions', async ({ request, baseURL }) => {
    const { token } = getSharedTokenFor(SEED.user1)
    const resp = await request.get(`${baseURL}/api/friends/suggestions?limit=5`, {
      headers: { Authorization: `Bearer ${token}` },
    })
    expect(resp.status()).toBe(200)
    const body = await resp.json()
    expect(Array.isArray(body)).toBeTruthy()
  })
})

test.describe('GET /api/friends/requests/count', () => {
  test('returns pending request count', async ({ request, baseURL }) => {
    const { token } = getSharedTokenFor(SEED.user1)
    const resp = await request.get(`${baseURL}/api/friends/requests/count`, {
      headers: { Authorization: `Bearer ${token}` },
    })
    expect(resp.status()).toBe(200)
    const body = await resp.json()
    expect(typeof body.count).toBe('number')
  })
})

test.describe('Friend request lifecycle', () => {
  test('send, check status, cancel friend request', async ({ request, baseURL }) => {
    const { token: token1, userId: userId1 } = getSharedTokenFor(SEED.user1)
    const { token: token2, userId: userId2 } = getSharedTokenFor(SEED.user2)

    // Clean up: remove any existing friendship / request
    await request.delete(`${baseURL}/api/friends/${userId2}`, {
      headers: { Authorization: `Bearer ${token1}` },
    })
    await request.delete(`${baseURL}/api/friends/request/${userId2}`, {
      headers: { Authorization: `Bearer ${token1}` },
    })

    // Send request
    const sendResp = await request.post(`${baseURL}/api/friends/request/${userId2}`, {
      headers: { Authorization: `Bearer ${token1}` },
    })
    expect([200, 201, 409]).toContain(sendResp.status())

    // Check status from user2's perspective
    const statusResp = await request.get(`${baseURL}/api/friends/status/${userId1}`, {
      headers: { Authorization: `Bearer ${token2}` },
    })
    expect(statusResp.status()).toBe(200)

    // Cancel request (if it was sent)
    if (sendResp.status() !== 409) {
      const cancelResp = await request.delete(`${baseURL}/api/friends/request/${userId2}`, {
        headers: { Authorization: `Bearer ${token1}` },
      })
      expect([200, 204]).toContain(cancelResp.status())
    }
  })

  test('send, accept, remove friend', async ({ request, baseURL }) => {
    const { token: token1, userId: userId1 } = getSharedTokenFor(SEED.user1)
    const { token: token2, userId: userId2 } = getSharedTokenFor(SEED.user2)

    // Clean state
    await request.delete(`${baseURL}/api/friends/${userId2}`, {
      headers: { Authorization: `Bearer ${token1}` },
    })
    await request.delete(`${baseURL}/api/friends/request/${userId2}`, {
      headers: { Authorization: `Bearer ${token1}` },
    })
    await request.delete(`${baseURL}/api/friends/request/${userId1}`, {
      headers: { Authorization: `Bearer ${token2}` },
    })

    // Send
    const sendResp = await request.post(`${baseURL}/api/friends/request/${userId2}`, {
      headers: { Authorization: `Bearer ${token1}` },
    })
    const alreadyFriends = sendResp.status() === 409

    if (!alreadyFriends) {
      // Accept
      const acceptResp = await request.post(`${baseURL}/api/friends/accept/${userId1}`, {
        headers: { Authorization: `Bearer ${token2}` },
      })
      expect([200, 201]).toContain(acceptResp.status())
    }

    // Verify friendship - items have shape { user: { id }, ... }
    const friendsResp = await request.get(`${baseURL}/api/friends`, {
      headers: { Authorization: `Bearer ${token1}` },
    })
    const friends = await friendsResp.json()
    const list = friends.items ?? friends
    const isFriend = list.some(
      (f: { id?: string; user?: { id: string } }) =>
        (f.user?.id ?? f.id) === userId2,
    )
    expect(isFriend).toBeTruthy()

    // Remove friend
    const removeResp = await request.delete(`${baseURL}/api/friends/${userId2}`, {
      headers: { Authorization: `Bearer ${token1}` },
    })
    expect([200, 204]).toContain(removeResp.status())
  })

  test('decline friend request', async ({ request, baseURL }) => {
    const { token: token1, userId: userId1 } = getSharedTokenFor(SEED.user3)
    const { token: token2, userId: userId2 } = getSharedTokenFor(SEED.user2)

    await request.delete(`${baseURL}/api/friends/request/${userId2}`, {
      headers: { Authorization: `Bearer ${token1}` },
    })
    const sendResp = await request.post(`${baseURL}/api/friends/request/${userId2}`, {
      headers: { Authorization: `Bearer ${token1}` },
    })

    if (sendResp.status() !== 409) {
      const declineResp = await request.post(`${baseURL}/api/friends/decline/${userId1}`, {
        headers: { Authorization: `Bearer ${token2}` },
      })
      expect([200, 204]).toContain(declineResp.status())
    }
  })
})

test.describe('Block/Unblock', () => {
  test('block and unblock a user', async ({ request, baseURL }) => {
    const { token: token1, userId: userId1 } = getSharedTokenFor(SEED.user1)
    const { userId: userId2 } = getSharedTokenFor(SEED.user2)

    const blockResp = await request.post(`${baseURL}/api/friends/block/${userId2}`, {
      headers: { Authorization: `Bearer ${token1}` },
    })
    expect([200, 204]).toContain(blockResp.status())

    const unblockResp = await request.delete(`${baseURL}/api/friends/block/${userId2}`, {
      headers: { Authorization: `Bearer ${token1}` },
    })
    expect([200, 204]).toContain(unblockResp.status())
  })
})

test.describe('PUT /api/friends/:userId/relation (family relation)', () => {
  test('sets a family relation on a friend', async ({ request, baseURL }) => {
    const { token: token1, userId: userId1 } = getSharedTokenFor(SEED.user1)
    const { token: token2, userId: userId2 } = getSharedTokenFor(SEED.user2)

    // Ensure they are friends first
    await request.delete(`${baseURL}/api/friends/${userId2}`, { headers: { Authorization: `Bearer ${token1}` } })
    const sendResp = await request.post(`${baseURL}/api/friends/request/${userId2}`, { headers: { Authorization: `Bearer ${token1}` } })
    if (sendResp.status() !== 409) {
      await request.post(`${baseURL}/api/friends/accept/${userId1}`, { headers: { Authorization: `Bearer ${token2}` } })
    }

    const relResp = await request.put(`${baseURL}/api/friends/${userId2}/relation`, {
      headers: { Authorization: `Bearer ${token1}` },
      data: { relation: 'Sibling' },
    })
    expect([200, 201]).toContain(relResp.status())
  })
})
