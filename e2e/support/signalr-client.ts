/**
 * SignalR client helpers for E2E tests.
 *
 * Builds a `HubConnection` that authenticates using the `access_token` query
 * parameter (matching `Program.cs` JwtBearerEvents.OnMessageReceived).
 *
 * The wrapper provides:
 *   - `connect(token, hubPath)` — returns a started connection.
 *   - `waitForEvent(connection, eventName, opts)` — promise that resolves on
 *     the next matching invocation (with timeout).
 *   - `disposeAll(connections)` — stops every connection cleanly so tests
 *     don't leak transports between specs.
 *
 * Connection failures (e.g. 5xx during negotiate) bubble up as rejected
 * promises so the test fails loudly.
 */

import {
  HubConnection,
  HubConnectionBuilder,
  HttpTransportType,
  LogLevel,
} from '@microsoft/signalr'

/** Hub paths exposed by the backend. */
export const HUB_PATHS = {
  notifications: '/hubs/notifications',
  chat:          '/hubs/chat',
  feed:          '/hubs/feed',
  live:          '/hubs/live',
} as const

export type HubName = keyof typeof HUB_PATHS

export interface ConnectOptions {
  /** Override transport — default is WebSockets only (avoids long-polling flakiness in CI). */
  transport?: HttpTransportType
  /** SignalR client log level (default: Error to keep test output readable). */
  logLevel?: LogLevel
}

/**
 * Build and start a HubConnection authenticated with the supplied JWT.
 *
 * The token is provided lazily via `accessTokenFactory` — the canonical way
 * for SignalR clients to authenticate with the `access_token` query
 * parameter that the backend's JwtBearerEvents pipeline looks for.
 */
export async function connectToHub(
  baseURL: string,
  hub: HubName,
  token: string,
  opts: ConnectOptions = {},
): Promise<HubConnection> {
  const transport = opts.transport ?? HttpTransportType.WebSockets
  const logLevel  = opts.logLevel  ?? LogLevel.Error

  const connection = new HubConnectionBuilder()
    .withUrl(`${baseURL}${HUB_PATHS[hub]}`, {
      accessTokenFactory: () => token,
      transport,
      // Skip the negotiate POST for WebSocket-only transport — keeps the
      // connection lifecycle simpler in tests.
      skipNegotiation: transport === HttpTransportType.WebSockets,
    })
    .configureLogging(logLevel)
    .build()

  await connection.start()
  return connection
}

export interface WaitForEventOptions<T = unknown> {
  /** Reject with timeout if no matching event arrives within this many ms (default: 5000). */
  timeoutMs?: number
  /**
   * Optional filter — only resolve when the predicate returns truthy.
   * Useful when other events on the same name might arrive first.
   */
  filter?: (payload: T) => boolean
}

/**
 * Wait for a single SignalR event on the given connection.
 *
 * Resolves with the first argument the server sent (most hub events are
 * single-argument).  Multi-argument events should use `connection.on()`
 * directly.
 */
export function waitForEvent<T = unknown>(
  connection: HubConnection,
  eventName: string,
  opts: WaitForEventOptions<T> = {},
): Promise<T> {
  const { timeoutMs = 5000, filter } = opts

  return new Promise<T>((resolve, reject) => {
    let settled = false

    const handler = (payload: T) => {
      if (settled) return
      if (filter && !filter(payload)) return
      settled = true
      connection.off(eventName, handler)
      clearTimeout(timer)
      resolve(payload)
    }

    const timer = setTimeout(() => {
      if (settled) return
      settled = true
      connection.off(eventName, handler)
      reject(new Error(
        `Timed out after ${timeoutMs}ms waiting for SignalR event '${eventName}'`,
      ))
    }, timeoutMs)

    connection.on(eventName, handler)
  })
}

/**
 * Wait for a SignalR event with two arguments (e.g. UserTyping(conversationId, userId)).
 */
export function waitForEvent2<A = unknown, B = unknown>(
  connection: HubConnection,
  eventName: string,
  opts: { timeoutMs?: number; filter?: (a: A, b: B) => boolean } = {},
): Promise<[A, B]> {
  const { timeoutMs = 5000, filter } = opts

  return new Promise<[A, B]>((resolve, reject) => {
    let settled = false

    const handler = (a: A, b: B) => {
      if (settled) return
      if (filter && !filter(a, b)) return
      settled = true
      connection.off(eventName, handler)
      clearTimeout(timer)
      resolve([a, b])
    }

    const timer = setTimeout(() => {
      if (settled) return
      settled = true
      connection.off(eventName, handler)
      reject(new Error(
        `Timed out after ${timeoutMs}ms waiting for SignalR event '${eventName}'`,
      ))
    }, timeoutMs)

    connection.on(eventName, handler)
  })
}

/** Stop every connection — best-effort, never throws. */
export async function disposeAll(connections: (HubConnection | null | undefined)[]) {
  await Promise.all(
    connections
      .filter((c): c is HubConnection => !!c)
      .map(async (c) => {
        try { await c.stop() } catch { /* swallow — best-effort cleanup */ }
      }),
  )
  // Give the server a beat to run OnDisconnectedAsync (presence cleanup,
  // group removal, etc.).  Without this, the next test can race against a
  // server-side connection that is still tracked in IPresenceTracker and
  // miss the next UserOnline broadcast.
  await new Promise((r) => setTimeout(r, 500))
}
