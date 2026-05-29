import { test, expect } from '../../../support/fixtures'
import {
  connectToHub,
  disposeAll,
  waitForEvent,
} from '../../../support/signalr-client'
import { getSharedUser } from '../../../support/shared-auth'
import type { HubConnection } from '@microsoft/signalr'

/**
 * SignalR coverage for LiveHub (/hubs/live).
 *
 * Exercises:
 *   - Two viewers join the same stream group via `JoinStream`; both receive
 *     `ViewerJoined` with the updated viewer count.
 *   - Round-trips a live chat message: viewer A invokes `SendLiveChat` →
 *     viewer B receives `LiveChatMessage`.
 *   - Leaving a stream bumps `ViewerLeft` to the remaining viewers.
 *
 * These tests do NOT require an active MediaMTX RTMP ingest — `JoinStream` /
 * `SendLiveChat` are pure hub operations that work on any string `streamId`.
 * The REST-side StreamStarted/StreamEnded flow is covered by
 * `tests/api/live.api.spec.ts`.
 */

test.describe('LiveHub — /hubs/live', () => {
  const connections: HubConnection[] = []

  let baseURL: string
  let tokenU1: string, idU1: string
  let tokenU2: string

  test.beforeAll(({ baseURL: configBaseURL }) => {
    baseURL = configBaseURL ?? 'http://localhost:80'
    ;({ token: tokenU1, userId: idU1 } = getSharedUser('user1'))
    ;({ token: tokenU2 } = getSharedUser('user2'))
  })

  test.afterEach(async () => {
    await disposeAll(connections.splice(0))
  })

  test('JoinStream broadcasts ViewerJoined to all viewers in the stream', async () => {
    // Use a unique stream id per test run so the static viewer dictionary
    // in LiveHub doesn't carry state from previous runs.
    const streamId = `e2e-stream-${Date.now()}-${Math.random().toString(36).slice(2, 8)}`

    const hostConn   = await connectToHub(baseURL, 'live', tokenU1)
    const viewerConn = await connectToHub(baseURL, 'live', tokenU2)
    connections.push(hostConn, viewerConn)

    // Host joins first.
    await hostConn.invoke('JoinStream', streamId)

    // When the viewer joins, both host and viewer should receive ViewerJoined(streamId, count).
    const hostSawJoin = new Promise<number>((resolve, reject) => {
      const timer = setTimeout(() => {
        hostConn.off('ViewerJoined', handler)
        reject(new Error('Host never received ViewerJoined for the new viewer'))
      }, 8000)
      const handler = (sid: string, count: number) => {
        if (sid !== streamId) return
        if (count < 2) return
        hostConn.off('ViewerJoined', handler)
        clearTimeout(timer)
        resolve(count)
      }
      hostConn.on('ViewerJoined', handler)
    })

    await viewerConn.invoke('JoinStream', streamId)

    const count = await hostSawJoin
    expect(count).toBeGreaterThanOrEqual(2)

    // Sanity: GetViewerCount returns the same value.
    const reported = await hostConn.invoke<number>('GetViewerCount', streamId)
    expect(reported).toBeGreaterThanOrEqual(2)
  })

  test('SendLiveChat broadcasts LiveChatMessage to all stream viewers', async () => {
    const streamId = `e2e-stream-chat-${Date.now()}-${Math.random().toString(36).slice(2, 8)}`

    const connA = await connectToHub(baseURL, 'live', tokenU1)
    const connB = await connectToHub(baseURL, 'live', tokenU2)
    connections.push(connA, connB)

    await connA.invoke('JoinStream', streamId)
    await connB.invoke('JoinStream', streamId)

    const message = `hello from A — ${Date.now()}`

    const chatEvent = waitForEvent<{
      streamId: string
      userId: string
      message: string
    }>(connB, 'LiveChatMessage', {
      timeoutMs: 8000,
      filter: (m) => m.streamId === streamId && m.message === message,
    })

    await connA.invoke('SendLiveChat', streamId, message)

    const received = await chatEvent
    expect(received.userId).toBe(idU1)
    expect(received.message).toBe(message)
  })

  test('LeaveStream broadcasts ViewerLeft to remaining viewers', async () => {
    const streamId = `e2e-stream-leave-${Date.now()}-${Math.random().toString(36).slice(2, 8)}`

    const connA = await connectToHub(baseURL, 'live', tokenU1)
    const connB = await connectToHub(baseURL, 'live', tokenU2)
    connections.push(connA, connB)

    await connA.invoke('JoinStream', streamId)
    await connB.invoke('JoinStream', streamId)

    const leftEvent = new Promise<number>((resolve, reject) => {
      const timer = setTimeout(() => {
        connA.off('ViewerLeft', handler)
        reject(new Error('Did not receive ViewerLeft after the other viewer left'))
      }, 8000)
      const handler = (sid: string, count: number) => {
        if (sid !== streamId) return
        connA.off('ViewerLeft', handler)
        clearTimeout(timer)
        resolve(count)
      }
      connA.on('ViewerLeft', handler)
    })

    await connB.invoke('LeaveStream', streamId)

    const remainingCount = await leftEvent
    expect(remainingCount).toBeGreaterThanOrEqual(0)
  })
})
