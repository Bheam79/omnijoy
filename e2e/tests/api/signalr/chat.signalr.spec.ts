import { test, expect, type APIRequestContext } from '../../../support/fixtures'
import {
  connectToHub,
  disposeAll,
  waitForEvent,
  waitForEvent2,
} from '../../../support/signalr-client'
import { getSharedUser } from '../../../support/shared-auth'
import type { HubConnection } from '@microsoft/signalr'
import { request as playwrightRequest } from '@playwright/test'

/**
 * SignalR coverage for ChatHub (/hubs/chat).
 *
 * Exercises:
 *   - Two clients join the same conversation; one POSTs a message via REST →
 *     the other receives a `ReceiveMessage` event.
 *   - Typing indicator: caller invokes `SendTyping` → other participant
 *     receives `UserTyping`.
 *
 * Auth tokens come from the shared cache populated by `globalSetup`
 * (see `support/shared-auth.ts`) so the suite stays under the `strict`
 * auth rate-limit policy (10 req/min/IP).
 */

/** Ensure A & B are accepted friends — required by the default `WhoCanSendMessages=Friends`. */
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

/** Open (or fetch existing) direct conversation between two users — returns its id. */
async function openConversation(
  request: APIRequestContext,
  baseURL: string,
  token: string,
  otherUserId: string,
): Promise<string> {
  const resp = await request.post(`${baseURL}/api/conversations/direct/${otherUserId}`, {
    headers: { Authorization: `Bearer ${token}` },
  })
  expect([200, 201]).toContain(resp.status())
  const body = await resp.json()
  return body.id as string
}

test.describe('ChatHub — /hubs/chat', () => {
  const connections: HubConnection[] = []

  let baseURL: string
  let sharedRequest: APIRequestContext
  let tokenU1: string, idU1: string
  let tokenU2: string, idU2: string

  test.beforeAll(async ({ baseURL: configBaseURL }) => {
    baseURL = configBaseURL ?? 'http://localhost:80'
    ;({ token: tokenU1, userId: idU1 } = getSharedUser('user1'))
    ;({ token: tokenU2, userId: idU2 } = getSharedUser('user2'))

    // Guarantee they can talk to each other regardless of prior friend state.
    sharedRequest = await playwrightRequest.newContext({ baseURL })
    await ensureFriends(sharedRequest, baseURL, tokenU1, idU1, tokenU2, idU2)
  })

  test.afterAll(async () => {
    await sharedRequest?.dispose()
  })

  test.afterEach(async () => {
    await disposeAll(connections.splice(0))
  })

  test('pushes ReceiveMessage to the other participant in a conversation', async ({ request }) => {
    const conversationId = await openConversation(request, baseURL, tokenU1, idU2)

    // Both clients connect to ChatHub and join the conversation group.
    const senderConn   = await connectToHub(baseURL, 'chat', tokenU1)
    const receiverConn = await connectToHub(baseURL, 'chat', tokenU2)
    connections.push(senderConn, receiverConn)

    await senderConn.invoke('JoinConversation', conversationId)
    await receiverConn.invoke('JoinConversation', conversationId)

    const messageEvent = waitForEvent<{ id: string; content: string; conversationId: string }>(
      receiverConn,
      'ReceiveMessage',
      { timeoutMs: 8000 },
    )

    const content = `signalr-chat-${Date.now()}`
    const sendResp = await request.post(`${baseURL}/api/messages`, {
      headers: { Authorization: `Bearer ${tokenU1}` },
      multipart: { conversationId, content },
    })
    expect([200, 201]).toContain(sendResp.status())

    const received = await messageEvent
    expect(received.content).toBe(content)
    // Server returns conversationId as a GUID — compare case-insensitively.
    expect(String(received.conversationId).toLowerCase()).toBe(conversationId.toLowerCase())
  })

  test('typing indicator round-trips via SendTyping → UserTyping', async ({ request }) => {
    const conversationId = await openConversation(request, baseURL, tokenU1, idU2)

    const connA = await connectToHub(baseURL, 'chat', tokenU1)
    const connB = await connectToHub(baseURL, 'chat', tokenU2)
    connections.push(connA, connB)

    await connA.invoke('JoinConversation', conversationId)
    await connB.invoke('JoinConversation', conversationId)

    // userB should see userA typing — UserTyping(conversationId, userId).
    const typingEvent = waitForEvent2<string, string>(connB, 'UserTyping', {
      timeoutMs: 8000,
      filter: (convId, uid) =>
        convId.toLowerCase() === conversationId.toLowerCase() && uid === idU1,
    })

    await connA.invoke('SendTyping', conversationId)

    const [convIdReceived, typerId] = await typingEvent
    expect(String(convIdReceived).toLowerCase()).toBe(conversationId.toLowerCase())
    expect(typerId).toBe(idU1)
  })
})
