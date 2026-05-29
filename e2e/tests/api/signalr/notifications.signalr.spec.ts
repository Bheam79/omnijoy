import { test, expect, type APIRequestContext } from '../../../support/fixtures'
import {
  connectToHub,
  disposeAll,
  waitForEvent,
} from '../../../support/signalr-client'
import { getSharedUser } from '../../../support/shared-auth'
import type { HubConnection } from '@microsoft/signalr'

/**
 * SignalR coverage for NotificationHub (/hubs/notifications).
 *
 * Exercises:
 *   - FriendRequest notification (`NotificationReceived` + `FriendRequestReceived`)
 *     delivered in real time to the recipient.
 *   - Presence broadcasting: when a user connects, their accepted friends
 *     receive `UserOnline`; on disconnect they receive `UserOffline`.
 *
 * Each test owns its connections and tears them down in `afterEach` so the
 * suite never leaves a stray WebSocket open between specs.
 *
 * Auth tokens are minted once by `globalSetup` and read from the shared
 * auth cache here (see `support/shared-auth.ts`) — keeps these specs well
 * under the `strict` (10/min/IP) rate-limit policy on `AuthController`.
 */

/** Remove any pending request / friendship between two users (both directions). */
async function clearFriendState(
  request: APIRequestContext,
  baseURL: string,
  tokenA: string, userIdA: string,
  tokenB: string, userIdB: string,
) {
  await request.delete(`${baseURL}/api/friends/${userIdB}`, { headers: { Authorization: `Bearer ${tokenA}` } })
  await request.delete(`${baseURL}/api/friends/${userIdA}`, { headers: { Authorization: `Bearer ${tokenB}` } })
  await request.delete(`${baseURL}/api/friends/request/${userIdB}`, { headers: { Authorization: `Bearer ${tokenA}` } })
  await request.delete(`${baseURL}/api/friends/request/${userIdA}`, { headers: { Authorization: `Bearer ${tokenB}` } })
}

/** Send a friend request A → B and accept on B's side so they end up accepted-friends. */
async function ensureFriends(
  request: APIRequestContext,
  baseURL: string,
  tokenA: string, userIdA: string,
  tokenB: string, userIdB: string,
) {
  const sendResp = await request.post(`${baseURL}/api/friends/request/${userIdB}`, {
    headers: { Authorization: `Bearer ${tokenA}` },
  })
  // 409 → already friends; otherwise accept the pending request.
  if (sendResp.status() !== 409) {
    await request.post(`${baseURL}/api/friends/accept/${userIdA}`, {
      headers: { Authorization: `Bearer ${tokenB}` },
    })
  }
}

test.describe('NotificationHub — /hubs/notifications', () => {
  const connections: HubConnection[] = []

  let baseURL: string
  let tokenU1: string, idU1: string
  let tokenU2: string, idU2: string
  let tokenU3: string, idU3: string

  test.beforeAll(({ baseURL: configBaseURL }) => {
    baseURL = configBaseURL ?? 'http://localhost:80'
    ;({ token: tokenU1, userId: idU1 } = getSharedUser('user1'))
    ;({ token: tokenU2, userId: idU2 } = getSharedUser('user2'))
    ;({ token: tokenU3, userId: idU3 } = getSharedUser('user3'))
  })

  test.afterEach(async () => {
    await disposeAll(connections.splice(0))
  })

  test('delivers FriendRequest notification in real time', async ({ request }) => {
    // Start clean: no existing friendship / request between user1 and user2.
    await clearFriendState(request, baseURL, tokenU1, idU1, tokenU2, idU2)

    // Recipient (user1) connects to the hub and listens for FriendRequest notifications.
    const recipientConn = await connectToHub(baseURL, 'notifications', tokenU1)
    connections.push(recipientConn)

    // Set up the event waiter BEFORE triggering the REST call so we don't race.
    const friendRequestEvent = waitForEvent<{
      id: string
      type: string
      referenceId?: string | null
    }>(recipientConn, 'NotificationReceived', {
      timeoutMs: 8000,
      filter: (n) => n.type === 'FriendRequest',
    })

    // Sender (user2) fires the REST call which triggers the hub push.
    const sendResp = await request.post(`${baseURL}/api/friends/request/${idU1}`, {
      headers: { Authorization: `Bearer ${tokenU2}` },
    })
    expect([200, 201]).toContain(sendResp.status())

    const notification = await friendRequestEvent
    expect(notification.type).toBe('FriendRequest')
    // Friend-type notifications carry the actor user id in ReferenceId.
    expect(notification.referenceId).toBe(idU2)
  })

  test('legacy FriendRequestReceived transient event is also pushed', async ({ request }) => {
    await clearFriendState(request, baseURL, tokenU1, idU1, tokenU2, idU2)

    const recipientConn = await connectToHub(baseURL, 'notifications', tokenU1)
    connections.push(recipientConn)

    const legacyEvent = waitForEvent<{ from: { id: string } }>(
      recipientConn,
      'FriendRequestReceived',
      { timeoutMs: 8000 },
    )

    const sendResp = await request.post(`${baseURL}/api/friends/request/${idU1}`, {
      headers: { Authorization: `Bearer ${tokenU2}` },
    })
    expect([200, 201]).toContain(sendResp.status())

    const payload = await legacyEvent
    expect(payload.from.id).toBe(idU2)
  })

  test('broadcasts UserOnline / UserOffline to accepted friends', async ({ request }) => {
    // Use user3 as the "comes online" actor.  user3 isn't connected anywhere
    // else in this spec, so we avoid the race where the IPresenceTracker
    // still has a stale connection-id from a previous test, which would
    // suppress the UserOnline broadcast (wasEmpty=false ⇒ no event).
    await ensureFriends(request, baseURL, tokenU1, idU1, tokenU3, idU3)

    // Listener (user1) connects and subscribes to presence events.
    const listenerConn = await connectToHub(baseURL, 'notifications', tokenU1)
    connections.push(listenerConn)

    const onlineEvent = waitForEvent<{ userId: string }>(listenerConn, 'UserOnline', {
      timeoutMs: 10000,
      filter: (p) => p.userId === idU3,
    })

    // user3 connects — should broadcast UserOnline to user1.
    const actorConn = await connectToHub(baseURL, 'notifications', tokenU3)
    connections.push(actorConn)

    const onlinePayload = await onlineEvent
    expect(onlinePayload.userId).toBe(idU3)

    // user3 disconnects — user1 should receive UserOffline.
    const offlineEvent = waitForEvent<{ userId: string }>(listenerConn, 'UserOffline', {
      timeoutMs: 10000,
      filter: (p) => p.userId === idU3,
    })
    await actorConn.stop()
    // Drop the stopped connection from the dispose list — already stopped.
    const idx = connections.indexOf(actorConn)
    if (idx >= 0) connections.splice(idx, 1)

    const offlinePayload = await offlineEvent
    expect(offlinePayload.userId).toBe(idU3)
  })
})
