import { test, expect, type APIRequestContext } from '../../support/fixtures'
import { SEED, TEST_LOCATION } from '../../fixtures/seed-data'

/**
 * API E2E tests for /api/auth/* endpoints.
 * Uses Playwright request context — no browser required.
 */

const RUN_ID = Date.now().toString(36)

test.describe('POST /api/auth/register', () => {
  test('registers a new user successfully', async ({ request, baseURL }) => {
    const resp = await request.post(`${baseURL}/api/auth/register`, {
      data: {
        email: `api_reg_${RUN_ID}@omnijoy.test`,
        displayName: `APIRegUser${RUN_ID}`,
        authMethod: 'password',
        password: 'Test@12345!',
        gender: 'NotDisclosed',
        ...TEST_LOCATION,
      },
    })
    expect(resp.status()).toBe(200)
    const body = await resp.json()
    expect(body.accessToken).toBeTruthy()
    expect(body.refreshToken).toBeTruthy()
    expect(body.user.email).toContain('api_reg_')
  })

  test('returns 409 when email already registered', async ({ request, baseURL }) => {
    const resp = await request.post(`${baseURL}/api/auth/register`, {
      data: {
        email: SEED.user1.email,
        displayName: 'Duplicate',
        authMethod: 'password',
        password: 'Test@12345!',
      },
    })
    expect(resp.status()).toBe(409)
    const body = await resp.json()
    expect(body.error).toBeTruthy()
  })

  test('returns 400 for invalid/missing fields', async ({ request, baseURL }) => {
    const resp = await request.post(`${baseURL}/api/auth/register`, {
      data: { email: 'not-an-email' },
    })
    expect([400, 409]).toContain(resp.status())
  })

  test('registers with OTP method (no password)', async ({ request, baseURL }) => {
    const resp = await request.post(`${baseURL}/api/auth/register`, {
      data: {
        email: `api_otp_${RUN_ID}@omnijoy.test`,
        displayName: `OTPUser${RUN_ID}`,
        authMethod: 'otp',
        gender: 'NotDisclosed',
        ...TEST_LOCATION,
      },
    })
    expect(resp.status()).toBe(200)
    const body = await resp.json()
    expect(body.accessToken).toBeTruthy()
  })
})

test.describe('POST /api/auth/login', () => {
  test('logs in with valid credentials', async ({ request, baseURL }) => {
    const resp = await request.post(`${baseURL}/api/auth/login`, {
      data: { email: SEED.user1.email, password: SEED.user1.password },
    })
    expect(resp.status()).toBe(200)
    const body = await resp.json()
    expect(body.accessToken).toBeTruthy()
    expect(body.refreshToken).toBeTruthy()
    expect(body.user).toBeTruthy()
  })

  test('returns 401 for wrong password', async ({ request, baseURL }) => {
    const resp = await request.post(`${baseURL}/api/auth/login`, {
      data: { email: SEED.user1.email, password: 'WrongPassword!' },
    })
    expect(resp.status()).toBe(401)
    const body = await resp.json()
    expect(body.error).toBeTruthy()
  })

  test('returns 401 for non-existent user', async ({ request, baseURL }) => {
    const resp = await request.post(`${baseURL}/api/auth/login`, {
      data: { email: 'nobody@omnijoy.test', password: 'Test@12345!' },
    })
    expect(resp.status()).toBe(401)
  })

  test('returns 400 for missing body', async ({ request, baseURL }) => {
    const resp = await request.post(`${baseURL}/api/auth/login`, { data: {} })
    expect([400, 401]).toContain(resp.status())
  })
})

test.describe('POST /api/auth/otp/request', () => {
  test('always returns 200 to prevent email enumeration', async ({ request, baseURL }) => {
    const resp = await request.post(`${baseURL}/api/auth/otp/request`, {
      data: { email: 'nonexistent@omnijoy.test' },
    })
    expect(resp.status()).toBe(200)
    const body = await resp.json()
    expect(body.message).toBeTruthy()
  })

  test('returns 200 for valid registered email', async ({ request, baseURL }) => {
    const resp = await request.post(`${baseURL}/api/auth/otp/request`, {
      data: { email: SEED.user2.email },
    })
    expect(resp.status()).toBe(200)
  })
})

test.describe('POST /api/auth/otp/verify', () => {
  test('returns 401 for invalid/expired OTP code', async ({ request, baseURL }) => {
    const resp = await request.post(`${baseURL}/api/auth/otp/verify`, {
      data: { email: SEED.user2.email, code: '000000' },
    })
    expect(resp.status()).toBe(401)
    const body = await resp.json()
    expect(body.error).toBeTruthy()
  })

  test('returns 400 for missing fields', async ({ request, baseURL }) => {
    const resp = await request.post(`${baseURL}/api/auth/otp/verify`, {
      data: {},
    })
    expect([400, 401]).toContain(resp.status())
  })
})

test.describe('POST /api/auth/refresh', () => {
  test('returns 401 for invalid refresh token', async ({ request, baseURL }) => {
    const resp = await request.post(`${baseURL}/api/auth/refresh`, {
      data: { refreshToken: 'invalid-token-xyz' },
    })
    expect(resp.status()).toBe(401)
  })

  test('returns new tokens for a valid refresh token', async ({ request, baseURL }) => {
    // Get a real refresh token first
    const loginResp = await request.post(`${baseURL}/api/auth/login`, {
      data: { email: SEED.user1.email, password: SEED.user1.password },
    })
    const { refreshToken } = await loginResp.json()

    const resp = await request.post(`${baseURL}/api/auth/refresh`, {
      data: { refreshToken },
    })
    expect(resp.status()).toBe(200)
    const body = await resp.json()
    expect(body.accessToken).toBeTruthy()
    expect(body.refreshToken).toBeTruthy()
  })
})

test.describe('POST /api/auth/logout', () => {
  test('returns 200 for a valid refresh token', async ({ request, baseURL }) => {
    const loginResp = await request.post(`${baseURL}/api/auth/login`, {
      data: { email: SEED.user1.email, password: SEED.user1.password },
    })
    const { refreshToken } = await loginResp.json()

    const resp = await request.post(`${baseURL}/api/auth/logout`, {
      data: { refreshToken },
    })
    expect(resp.status()).toBe(200)
    const body = await resp.json()
    expect(body.message).toBeTruthy()
  })
})

test.describe('POST /api/auth/oauth/google', () => {
  test('returns 401 for invalid Google token', async ({ request, baseURL }) => {
    const resp = await request.post(`${baseURL}/api/auth/oauth/google`, {
      data: { token: 'fake-google-token' },
    })
    expect(resp.status()).toBe(401)
  })
})

test.describe('POST /api/auth/oauth/facebook', () => {
  test('returns 401 for invalid Facebook token', async ({ request, baseURL }) => {
    const resp = await request.post(`${baseURL}/api/auth/oauth/facebook`, {
      data: { token: 'fake-facebook-token' },
    })
    expect(resp.status()).toBe(401)
  })
})
