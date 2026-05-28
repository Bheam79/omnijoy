import { test, expect, type APIRequestContext } from '@playwright/test'

/**
 * API E2E tests for /api/meta-preview endpoint.
 * No auth required for this endpoint.
 */

test.describe('GET /api/meta-preview', () => {
  test('returns 400 when url parameter is missing', async ({ request, baseURL }) => {
    const resp = await request.get(`${baseURL}/api/meta-preview`)
    expect(resp.status()).toBe(400)
    const body = await resp.json()
    expect(body.error).toBeTruthy()
  })

  test('returns 400 for non-HTTP URL', async ({ request, baseURL }) => {
    const resp = await request.get(`${baseURL}/api/meta-preview?url=ftp://example.com`)
    expect(resp.status()).toBe(400)
    const body = await resp.json()
    expect(body.error).toBeTruthy()
  })

  test('returns 400 for localhost URL (SSRF protection)', async ({ request, baseURL }) => {
    const resp = await request.get(`${baseURL}/api/meta-preview?url=http://localhost`)
    expect(resp.status()).toBe(400)
    const body = await resp.json()
    expect(body.error).toContain('private')
  })

  test('returns 400 for private network IP (SSRF protection)', async ({ request, baseURL }) => {
    const resp = await request.get(`${baseURL}/api/meta-preview?url=http://192.168.1.1`)
    expect(resp.status()).toBe(400)
  })

  test('returns 400 for 10.x.x.x range (SSRF protection)', async ({ request, baseURL }) => {
    const resp = await request.get(`${baseURL}/api/meta-preview?url=http://10.0.0.1`)
    expect(resp.status()).toBe(400)
  })

  test('returns 200 with preview object for public URL', async ({ request, baseURL }) => {
    // This may timeout in offline environments — test for shape only
    const resp = await request.get(`${baseURL}/api/meta-preview?url=https://example.com`, {
      timeout: 15_000,
    })
    // 200 whether or not fetch succeeds (controller catches errors)
    expect(resp.status()).toBe(200)
    const body = await resp.json()
    // Shape: { url, title, description, image, siteName }
    expect(body.url).toBeTruthy()
  })

  test('returns empty preview for unreachable URL (no 5xx)', async ({ request, baseURL }) => {
    const resp = await request.get(
      `${baseURL}/api/meta-preview?url=https://this-domain-does-not-exist-e2e.invalid`,
      { timeout: 20_000 },
    )
    expect(resp.status()).toBe(200)
    const body = await resp.json()
    expect(body.url).toBeTruthy()
  })

  test('returns 400 for malformed URL', async ({ request, baseURL }) => {
    const resp = await request.get(`${baseURL}/api/meta-preview?url=not-a-url`)
    expect(resp.status()).toBe(400)
  })
})
