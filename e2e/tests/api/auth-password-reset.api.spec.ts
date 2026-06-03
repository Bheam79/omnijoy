import { randomUUID } from 'crypto'
import { test, expect, type APIRequestContext } from '../../support/fixtures'
import { TEST_LOCATION } from '../../fixtures/seed-data'
import { fetchLatestPasswordResetCode } from '../../support/password-reset-helpers'

/**
 * API E2E tests for the forgot-password / password-reset flow (OMNIJOY-226).
 *
 * The endpoints exercised here are under the existing `strict` rate-limit
 * policy (10 req/min/IP by default).  Two layers of headroom keep the suite
 * green:
 *
 *   1. Each test runs through a unique synthetic `X-Forwarded-For` IP, so
 *      they don't share a rate-limit bucket with each other or with other
 *      auth specs (the backend's `GetClientIp` honours XFF — see
 *      RateLimitingExtensions.cs).
 *
 *   2. Both `appsettings.Development.json` and `make test-e2e-prod` raise
 *      the strict permit limit to 200 — see CLAUDE.md.
 *
 * Each test uses a freshly-registered user (uuid-suffixed) so the suite is
 * parallel-safe and never pollutes the shared seed accounts.
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
  return `198.51.100.${100 + (i % 150)}-pwr-${SUITE_ID}-${i}`
}

async function registerFreshUser(
  request: APIRequestContext,
  baseURL: string,
  xff?: string,
): Promise<FreshUser> {
  const id = randomUUID().slice(0, 12)
  const user: FreshUser = {
    email: `pwreset_${id}@omnijoy.test`,
    password: 'OrigStr0ngPass!',
    displayName: `PwReset_${id}`,
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

test.describe('Password reset (forgot / reset) — happy path round-trip', () => {
  test.describe.configure({ mode: 'serial' })

  test('forgot → reset → old password rejected → new password works → pre-existing refresh token revoked', async ({ request, baseURL }) => {
    const u = await registerFreshUser(request, baseURL!)
    const xff = u.xff

    // Pre-fetch a refresh token — we'll verify it's revoked after the reset.
    const oldLogin = await request.post(`${baseURL}/api/auth/login`, {
      headers: { 'X-Forwarded-For': xff },
      data:    { email: u.email, password: u.password },
    })
    expect(oldLogin.status()).toBe(200)
    const { refreshToken: oldRefreshToken } = await oldLogin.json()
    expect(oldRefreshToken).toBeTruthy()

    // 1. Request the reset code.
    const forgotResp = await request.post(`${baseURL}/api/auth/password/forgot`, {
      headers: { 'X-Forwarded-For': xff },
      data:    { email: u.email },
    })
    expect(forgotResp.status()).toBe(200)
    const forgotBody = await forgotResp.json()
    expect(forgotBody.message).toMatch(/if your email is registered/i)

    // 2. Extract the code (brute-force the SHA256 hash from the DB).
    const code = fetchLatestPasswordResetCode(u.email)
    expect(code, `expected a reset code for ${u.email} in the OtpCodes table`).not.toBeNull()
    expect(code).toMatch(/^\d{6}$/)

    // 3. Confirm the reset.
    const newPassword = 'NewStr0ngPass!'
    const resetResp = await request.post(`${baseURL}/api/auth/password/reset`, {
      headers: { 'X-Forwarded-For': xff },
      data:    { email: u.email, code, newPassword },
    })
    expect(resetResp.status()).toBe(200)
    const resetBody = await resetResp.json()
    expect(resetBody.message).toMatch(/reset/i)

    // 4. Login with the OLD password is now rejected.
    const oldPwLogin = await request.post(`${baseURL}/api/auth/login`, {
      headers: { 'X-Forwarded-For': xff },
      data:    { email: u.email, password: u.password },
    })
    expect(oldPwLogin.status()).toBe(401)

    // 5. Login with the NEW password works and issues a fresh token pair.
    const newPwLogin = await request.post(`${baseURL}/api/auth/login`, {
      headers: { 'X-Forwarded-For': xff },
      data:    { email: u.email, password: newPassword },
    })
    expect(newPwLogin.status()).toBe(200)
    const newBody = await newPwLogin.json()
    expect(newBody.accessToken).toBeTruthy()
    expect(newBody.refreshToken).toBeTruthy()
    expect(newBody.refreshToken).not.toBe(oldRefreshToken)

    // 6. The refresh token issued BEFORE the reset has been revoked.
    const refreshResp = await request.post(`${baseURL}/api/auth/refresh`, {
      headers: { 'X-Forwarded-For': xff },
      data:    { refreshToken: oldRefreshToken },
    })
    expect(refreshResp.status()).toBe(401)
  })
})

test.describe('Password reset — error paths', () => {
  test.describe.configure({ mode: 'serial' })

  test('unknown email still returns 200 with the same generic body (no enumeration)', async ({ request, baseURL }) => {
    const xff = nextXff()

    // Use a freshly-registered user as the "known" email so we don't
    // burn a shared seed account.
    const u = await registerFreshUser(request, baseURL!, xff)

    const known = await request.post(`${baseURL}/api/auth/password/forgot`, {
      headers: { 'X-Forwarded-For': xff },
      data:    { email: u.email },
    })
    expect(known.status()).toBe(200)
    const knownBody = await known.json()

    const unknown = await request.post(`${baseURL}/api/auth/password/forgot`, {
      headers: { 'X-Forwarded-For': xff },
      data:    { email: `nobody-${randomUUID()}@nowhere.test` },
    })
    expect(unknown.status()).toBe(200)
    const unknownBody = await unknown.json()

    expect(unknownBody).toEqual(knownBody)
    expect(unknownBody.message).toMatch(/if your email is registered/i)
  })

  test('wrong code is rejected with 401 and the generic "invalid or expired" message', async ({ request, baseURL }) => {
    const xff = nextXff()
    const u = await registerFreshUser(request, baseURL!, xff)

    // Issue a code so the DB row exists; we'll deliberately submit the wrong one.
    const forgot = await request.post(`${baseURL}/api/auth/password/forgot`, {
      headers: { 'X-Forwarded-For': xff },
      data:    { email: u.email },
    })
    expect(forgot.status()).toBe(200)

    const realCode = fetchLatestPasswordResetCode(u.email)!
    expect(realCode).toMatch(/^\d{6}$/)

    // Use a 6-digit code we know is not equal to the real one.
    const wrongCode = realCode === '000000' ? '111111' : '000000'

    const resp = await request.post(`${baseURL}/api/auth/password/reset`, {
      headers: { 'X-Forwarded-For': xff },
      data:    { email: u.email, code: wrongCode, newPassword: 'AnotherStr0ng!' },
    })
    expect(resp.status()).toBe(401)
    const body = await resp.json()
    expect(body.error).toMatch(/invalid or expired/i)
  })

  test('password complexity rejected with 400 — and the code remains usable afterwards', async ({ request, baseURL }) => {
    const xff = nextXff()
    const u = await registerFreshUser(request, baseURL!, xff)

    const forgot = await request.post(`${baseURL}/api/auth/password/forgot`, {
      headers: { 'X-Forwarded-For': xff },
      data:    { email: u.email },
    })
    expect(forgot.status()).toBe(200)
    const code = fetchLatestPasswordResetCode(u.email)!
    expect(code).toMatch(/^\d{6}$/)

    // Too short — ArgumentException → 400 BadRequest.
    const bad = await request.post(`${baseURL}/api/auth/password/reset`, {
      headers: { 'X-Forwarded-For': xff },
      data:    { email: u.email, code, newPassword: 'abc' },
    })
    expect(bad.status()).toBe(400)
    const badBody = await bad.json()
    expect(badBody.error).toBeTruthy()

    // The same code MUST still be usable — a complexity rejection should not
    // burn the OTP row (it threw before the IsUsed flag could be set).
    const good = await request.post(`${baseURL}/api/auth/password/reset`, {
      headers: { 'X-Forwarded-For': xff },
      data:    { email: u.email, code, newPassword: 'StillGoodPass1!' },
    })
    expect(good.status()).toBe(200)

    // And the new password works for login.
    const login = await request.post(`${baseURL}/api/auth/login`, {
      headers: { 'X-Forwarded-For': xff },
      data:    { email: u.email, password: 'StillGoodPass1!' },
    })
    expect(login.status()).toBe(200)
  })

  test('a reused code is rejected with 401 on the second attempt', async ({ request, baseURL }) => {
    const xff = nextXff()
    const u = await registerFreshUser(request, baseURL!, xff)

    const forgot = await request.post(`${baseURL}/api/auth/password/forgot`, {
      headers: { 'X-Forwarded-For': xff },
      data:    { email: u.email },
    })
    expect(forgot.status()).toBe(200)
    const code = fetchLatestPasswordResetCode(u.email)!

    const first = await request.post(`${baseURL}/api/auth/password/reset`, {
      headers: { 'X-Forwarded-For': xff },
      data:    { email: u.email, code, newPassword: 'FirstNewPass1!' },
    })
    expect(first.status()).toBe(200)

    const second = await request.post(`${baseURL}/api/auth/password/reset`, {
      headers: { 'X-Forwarded-For': xff },
      data:    { email: u.email, code, newPassword: 'SecondNewPass1!' },
    })
    expect(second.status()).toBe(401)
    const body = await second.json()
    expect(body.error).toMatch(/invalid or expired/i)
  })

  // ── Expired code ────────────────────────────────────────────────────────────
  // TODO: a true "expired code" assertion needs a way to fast-forward time
  // (either a dev-only `OtpCode.ExpiresAt` mutation endpoint or a test
  // helper that updates the row via docker exec).  Skipped here rather
  // than waiting 15+ minutes per spec run.  See OMNIJOY-223 / -226.
  // eslint-disable-next-line @typescript-eslint/no-unused-vars
  test.skip('expired code is rejected with 401 (TODO: needs time fast-forward)', async () => {
    // Intentional skip — see comment above.
  })
})
