import { test, expect } from '../../support/fixtures'

/**
 * Stack smoke test — the very first spec Playwright runs.
 *
 * Hits GET /api/health and asserts a 200 response.  If this fails it means
 * the backend is not up at all; a single loud failure here is far more useful
 * than 100+ cascading assertion errors in every other spec.
 *
 * This test runs against *both* `make test-e2e` (dev stack) and
 * `make test-e2e-prod` (production stack with MinIO + Redis), so it acts as
 * the canonical "is the stack alive?" gate for both targets.
 */

test('GET /api/health returns 200 (stack is up)', async ({ request, baseURL }) => {
  const resp = await request.get(`${baseURL}/api/health`)
  expect(resp.status()).toBe(200)
  const body = await resp.json()
  expect(body.status).toBe('healthy')
})
