/**
 * Omnijoy E2E custom fixtures.
 *
 * All test files should import `test` and `expect` from here instead of
 * `@playwright/test` so that the automatic 5xx-detection is active.
 *
 * # How it works
 *
 * **API tests** — the `request` fixture is wrapped via a Proxy so that every
 * `get / post / put / delete / patch / head / fetch` call checks the response
 * status before returning it.  Any HTTP 5xx throws an error and fails the test
 * immediately, with the URL and (truncated) response body in the message.
 *
 * **Browser tests** — the `page` fixture attaches `page.on('response', …)` and
 * `page.on('requestfailed', …)` listeners.  Issues are collected per-test and
 * thrown as a single error after the test body finishes.
 *
 * # Opt-out
 *
 * When a test intentionally exercises a 5xx path, add this at the top of the
 * describe block or individual test:
 *
 *   ```ts
 *   test.use({ allow5xx: true })
 *   ```
 *
 * This disables both the `request` wrapper and the `page` monitoring for that
 * scope so the test can assert `expect(resp.status()).toBe(500)` normally.
 */

import {
  test as base,
  expect,
  type APIRequestContext,
  type APIResponse,
  type Page,
} from '@playwright/test'

// ─── helpers ─────────────────────────────────────────────────────────────────

const HTTP_METHODS = new Set(['get', 'post', 'put', 'delete', 'patch', 'head', 'fetch'])

/**
 * Wraps an `APIRequestContext` so that any HTTP response with status ≥ 500
 * causes an immediate error.  Non-HTTP methods (dispose, storageState, …) are
 * passed through unchanged.
 */
function wrapRequest(ctx: APIRequestContext): APIRequestContext {
  return new Proxy(ctx, {
    get(target, prop, receiver) {
      if (typeof prop !== 'string') return Reflect.get(target, prop, receiver)

      const val = Reflect.get(target, prop, receiver)

      // Non-function properties and non-HTTP methods pass through untouched.
      if (typeof val !== 'function' || !HTTP_METHODS.has(prop)) {
        return typeof val === 'function' ? (val as Function).bind(target) : val
      }

      // Wrap HTTP methods to detect 5xx.
      return async function (...args: unknown[]) {
        const resp: APIResponse = await (val as Function).apply(target, args)
        const status = resp.status()
        if (status >= 500) {
          let body = ''
          try {
            body = (await resp.text()).slice(0, 500)
          } catch {
            // Body unreadable — that's fine, URL + status is enough.
          }
          throw new Error(
            `Unexpected 5xx response: HTTP ${status} from ${resp.url()}\n` +
            `Body: ${body || '(empty)'}`,
          )
        }
        return resp
      }
    },
  })
}

// ─── fixture type ─────────────────────────────────────────────────────────────

type OmnijoyFixtures = {
  /**
   * Set to `true` to disable automatic 5xx detection for the current scope.
   *
   * Example:
   *   test.use({ allow5xx: true })
   */
  allow5xx: boolean
}

// ─── extended test ────────────────────────────────────────────────────────────

export const test = base.extend<OmnijoyFixtures>({
  // Declare the opt-out flag as an overridable option (default: false).
  allow5xx: [false, { option: true }],

  // ── API request wrapper ───────────────────────────────────────────────────
  request: async ({ request, allow5xx }, use) => {
    await use(allow5xx ? request : wrapRequest(request))
  },

  // ── Browser page monitor ──────────────────────────────────────────────────
  page: async ({ page, allow5xx }, use) => {
    if (allow5xx) {
      await use(page)
      return
    }

    const issues: string[] = []

    page.on('response', (response) => {
      if (response.status() >= 500) {
        issues.push(`[${response.status()}] ${response.url()}`)
      }
    })

    page.on('requestfailed', (request) => {
      const err = request.failure()?.errorText ?? 'unknown error'
      issues.push(`[NETWORK FAIL] ${request.url()} — ${err}`)
    })

    await use(page)

    if (issues.length > 0) {
      throw new Error(
        `5xx / network failures detected during browser test:\n` +
        issues.map((i) => `  ${i}`).join('\n'),
      )
    }
  },
})

// Re-export everything else from @playwright/test so callers only need one import.
export { expect } from '@playwright/test'
export type { APIRequestContext, APIResponse, Page } from '@playwright/test'
