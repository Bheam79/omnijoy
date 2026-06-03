import { test, expect } from '../../support/fixtures'

/**
 * Stack smoke test — the very first spec Playwright runs.
 *
 * Hits the readiness probe (`GET /api/health/ready`) so the smoke test
 * exercises every dependency the API needs to actually serve requests
 * (MariaDB, Redis when configured, MinIO when storage type is s3).
 * If this fails it means the backend is up but degraded, OR not running
 * at all; a single loud failure here is far more useful than 100+
 * cascading assertion errors in every other spec.
 *
 * The liveness probe (`/api/health/live`) is intentionally NOT used here —
 * it always returns 200 as long as the process is alive, so it would mask
 * a degraded DB / Redis / MinIO.
 *
 * This test runs against *both* `make test-e2e` (dev stack) and
 * `make test-e2e-prod` (production stack with MinIO + Redis), so it acts as
 * the canonical "is the stack alive?" gate for both targets.
 */

test('GET /api/health/ready returns 200 (stack is up)', async ({ request, baseURL }) => {
  const resp = await request.get(`${baseURL}/api/health/ready`)
  expect(resp.status()).toBe(200)
  const body = await resp.json()
  // UIResponseWriter payload: { status: "Healthy", entries: { ... } }
  expect(body.status).toBe('Healthy')
})
