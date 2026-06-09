import { randomUUID } from 'crypto'
import { test, expect, type APIRequestContext } from '../../support/fixtures'
import { TEST_LOCATION } from '../../fixtures/seed-data'

/**
 * API E2E tests for the email verification endpoints (OMNIJOY-241 / 243):
 *
 *   POST /api/auth/verify-email?token=…       — confirm a verification link
 *   POST /api/auth/resend-verification        — send a fresh link (auth required)
 *
 * The verification token never appears in any user-visible response (it's
 * delivered by email in production). For E2E we use a dev-only helper:
 *
 *   POST /api/auth/test/get-verification-token?email=…
 *
 * The helper is guarded by `IWebHostEnvironment.IsDevelopment()` and returns
 * 404 in Staging/Production. `make test-e2e` (dotnet watch — Development) is
 * the supported runtime; `make test-e2e-prod` runs the production stack
 * where this route does not exist, so the suite skips itself with a
 * descriptive message instead of cascade-failing.
 *
 * Each test uses a fresh user (uuid-suffixed) and a unique synthetic
 * `X-Forwarded-For` IP so the strict rate-limiter buckets stay separate.
 */

const SUITE_ID = randomUUID().slice(0, 8)

interface FreshUser {
  email: string
  password: string
  displayName: string
  /** Per-user XFF — keeps the strict limiter scoped to this test. */
  xff: string
}

let suiteCounter = 0
function nextXff(): string {
  // RFC 5737 TEST-NET-2 — never routed on the public internet.
  const i = ++suiteCounter
  return `198.51.100.${100 + (i % 150)}-evfy-${SUITE_ID}-${i}`
}

async function registerFreshUser(
  request: APIRequestContext,
  baseURL: string,
  xff?: string,
): Promise<FreshUser> {
  const id = randomUUID().slice(0, 12)
  const user: FreshUser = {
    email: `evfy_${id}@omnijoy.test`,
    password: 'EvFy_Pass123!',
    displayName: `EvFy_${id}`,
    xff: xff ?? nextXff(),
  }
  const resp = await request.post(`${baseURL}/api/auth/register`, {
    headers: { 'X-Forwarded-For': user.xff },
    data: {
      email:       user.email,
      displayName: user.displayName,
      authMethod:  'password',
      password:    user.password,
      gender:      'NotDisclosed',
      ...TEST_LOCATION,
    },
  })
  expect(resp.status(), `register ${user.email} should succeed`).toBe(200)
  return user
}

/**
 * Reads the live verification token for an email from the dev-only debug
 * endpoint. Returns `null` when the user has no pending token (verified or
 * never had one) — `undefined` cannot be distinguished from the dev-only
 * 404 below.
 *
 * Throws when the endpoint is missing — meaning the suite is running
 * against a non-Development build and these specs should be skipped.
 */
async function fetchVerificationToken(
  request: APIRequestContext,
  baseURL: string,
  email: string,
  xff: string,
): Promise<{ token: string | null; isEmailVerified: boolean }> {
  const resp = await request.post(
    `${baseURL}/api/auth/test/get-verification-token?email=${encodeURIComponent(email)}`,
    { headers: { 'X-Forwarded-For': xff } },
  )

  if (resp.status() === 404) {
    // Endpoint disabled — either the user doesn't exist (we just made it,
    // so unlikely) or the build is non-Development. We rely on the
    // suite-level probe below to decide which.
    const body = await resp.text()
    throw new Error(`verification-token endpoint unavailable (404). body=${body}`)
  }

  expect(resp.status(), 'test/get-verification-token must succeed in Development').toBe(200)
  const body = await resp.json()
  return { token: body.token ?? null, isEmailVerified: !!body.isEmailVerified }
}

/**
 * Probe the dev-only endpoint once per worker. If it returns 404 the whole
 * spec file is skipped — these tests cannot meaningfully run without it.
 */
let testEndpointAvailable: boolean | null = null
async function ensureTestEndpoint(
  request: APIRequestContext,
  baseURL: string,
): Promise<boolean> {
  if (testEndpointAvailable !== null) return testEndpointAvailable
  const resp = await request.post(
    `${baseURL}/api/auth/test/get-verification-token?email=probe-${SUITE_ID}@nowhere.test`,
    { headers: { 'X-Forwarded-For': `probe-${SUITE_ID}` } },
  )
  // 404 = either "user not found" (endpoint is on) or "route not found"
  // (endpoint is off). Disambiguate via the body: when on, the JSON body
  // matches `{ error: "User not found." }`.
  if (resp.status() === 404) {
    try {
      const body = await resp.json()
      testEndpointAvailable = typeof body?.error === 'string'
    } catch {
      testEndpointAvailable = false
    }
  } else {
    testEndpointAvailable = resp.status() === 200 || resp.status() === 400
  }
  return testEndpointAvailable
}

test.describe('Email verification — POST /api/auth/verify-email', () => {
  test.describe.configure({ mode: 'serial' })

  test.beforeAll(async ({ request, baseURL }) => {
    const ok = await ensureTestEndpoint(request, baseURL!)
    test.skip(!ok,
      'POST /api/auth/test/get-verification-token is disabled outside ' +
      'Development. These specs only run against `make test-e2e` ' +
      '(dotnet watch / ASPNETCORE_ENVIRONMENT=Development).')
  })

  test('valid token verifies the email and flips IsEmailVerified to true', async ({ request, baseURL }) => {
    const u = await registerFreshUser(request, baseURL!)

    // Sanity check: a fresh password user is unverified and carries a token.
    const before = await fetchVerificationToken(request, baseURL!, u.email, u.xff)
    expect(before.isEmailVerified).toBe(false)
    expect(before.token, 'fresh password user must have a verification token').toBeTruthy()

    const verify = await request.post(
      `${baseURL}/api/auth/verify-email?token=${encodeURIComponent(before.token!)}`,
      { headers: { 'X-Forwarded-For': u.xff } },
    )
    expect(verify.status()).toBe(200)
    const verifyBody = await verify.json()
    expect(verifyBody.message).toMatch(/verified/i)

    // The DB row was flipped and the token cleared.
    const after = await fetchVerificationToken(request, baseURL!, u.email, u.xff)
    expect(after.isEmailVerified).toBe(true)
    expect(after.token).toBeNull()

    // Subsequent logins surface the same fact via the auth response.
    const login = await request.post(`${baseURL}/api/auth/login`, {
      headers: { 'X-Forwarded-For': u.xff },
      data:    { email: u.email, password: u.password },
    })
    expect(login.status()).toBe(200)
    const loginBody = await login.json()
    expect(loginBody.user.isEmailVerified).toBe(true)
  })

  test('invalid token is rejected with 400', async ({ request, baseURL }) => {
    const xff = nextXff()
    const resp = await request.post(
      `${baseURL}/api/auth/verify-email?token=bogus-token-that-does-not-exist`,
      { headers: { 'X-Forwarded-For': xff } },
    )
    expect(resp.status()).toBe(400)
    const body = await resp.json()
    expect(body.error).toMatch(/invalid or expired/i)
  })

  test('replaying the same token after success is rejected with 400', async ({ request, baseURL }) => {
    const u = await registerFreshUser(request, baseURL!)
    const { token } = await fetchVerificationToken(request, baseURL!, u.email, u.xff)
    expect(token).toBeTruthy()

    const first = await request.post(
      `${baseURL}/api/auth/verify-email?token=${encodeURIComponent(token!)}`,
      { headers: { 'X-Forwarded-For': u.xff } },
    )
    expect(first.status()).toBe(200)

    // The token has been consumed (cleared from the row). Sending it again
    // hits the !IsEmailVerified filter on the lookup and returns 400.
    const second = await request.post(
      `${baseURL}/api/auth/verify-email?token=${encodeURIComponent(token!)}`,
      { headers: { 'X-Forwarded-For': u.xff } },
    )
    expect(second.status()).toBe(400)
    const body = await second.json()
    expect(body.error).toMatch(/invalid or expired/i)
  })
})

test.describe('Email verification — POST /api/auth/resend-verification', () => {
  test.describe.configure({ mode: 'serial' })

  test.beforeAll(async ({ request, baseURL }) => {
    const ok = await ensureTestEndpoint(request, baseURL!)
    test.skip(!ok,
      'POST /api/auth/test/get-verification-token is disabled outside ' +
      'Development. These specs only run against `make test-e2e` ' +
      '(dotnet watch / ASPNETCORE_ENVIRONMENT=Development).')
  })

  test('authenticated unverified user gets a new token (200)', async ({ request, baseURL }) => {
    const u = await registerFreshUser(request, baseURL!)

    // Capture the original token so we can confirm it got rotated.
    const originalToken =
      (await fetchVerificationToken(request, baseURL!, u.email, u.xff)).token
    expect(originalToken).toBeTruthy()

    // Log in to obtain a bearer token.
    const login = await request.post(`${baseURL}/api/auth/login`, {
      headers: { 'X-Forwarded-For': u.xff },
      data:    { email: u.email, password: u.password },
    })
    expect(login.status()).toBe(200)
    const { accessToken } = await login.json()
    expect(accessToken).toBeTruthy()

    const resend = await request.post(`${baseURL}/api/auth/resend-verification`, {
      headers: {
        Authorization:    `Bearer ${accessToken}`,
        'X-Forwarded-For': u.xff,
      },
    })
    expect(resend.status()).toBe(200)
    const resendBody = await resend.json()
    expect(resendBody.message).toMatch(/sent/i)

    // Token rotated, account still unverified.
    const after = await fetchVerificationToken(request, baseURL!, u.email, u.xff)
    expect(after.isEmailVerified).toBe(false)
    expect(after.token).toBeTruthy()
    expect(after.token).not.toBe(originalToken)
  })

  test('already-verified user is rejected with 400', async ({ request, baseURL }) => {
    const u = await registerFreshUser(request, baseURL!)

    // Verify the address up front so the resend path hits the
    // "already verified" branch.
    const { token } = await fetchVerificationToken(request, baseURL!, u.email, u.xff)
    expect(token).toBeTruthy()
    const verify = await request.post(
      `${baseURL}/api/auth/verify-email?token=${encodeURIComponent(token!)}`,
      { headers: { 'X-Forwarded-For': u.xff } },
    )
    expect(verify.status()).toBe(200)

    const login = await request.post(`${baseURL}/api/auth/login`, {
      headers: { 'X-Forwarded-For': u.xff },
      data:    { email: u.email, password: u.password },
    })
    expect(login.status()).toBe(200)
    const { accessToken } = await login.json()

    const resend = await request.post(`${baseURL}/api/auth/resend-verification`, {
      headers: {
        Authorization:    `Bearer ${accessToken}`,
        'X-Forwarded-For': u.xff,
      },
    })
    expect(resend.status()).toBe(400)
    const body = await resend.json()
    expect(body.error).toMatch(/already verified/i)
  })

  test('unauthenticated request is rejected with 401', async ({ request, baseURL }) => {
    const xff = nextXff()
    const resp = await request.post(`${baseURL}/api/auth/resend-verification`, {
      headers: { 'X-Forwarded-For': xff },
    })
    expect(resp.status()).toBe(401)
  })
})
