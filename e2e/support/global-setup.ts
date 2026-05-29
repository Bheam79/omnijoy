import { execSync } from 'child_process'
import { writeFileSync, mkdirSync } from 'fs'
import { dirname } from 'path'
import { request, FullConfig } from '@playwright/test'
import { SEED } from '../fixtures/seed-data'
import { SHARED_AUTH_PATH, type SharedAuth } from './shared-auth'

/**
 * Global setup — runs once before the entire test suite.
 *
 * Registers the three seed users via the API so all tests can rely on them
 * existing. If a user already exists (409 Conflict), registration is silently
 * skipped — this makes the setup idempotent across re-runs.
 */
async function globalSetup(config: FullConfig) {
  const baseURL = config.projects[0]?.use?.baseURL ?? 'http://localhost:80'

  // Use Playwright's API request context — no browser needed
  const context = await request.newContext({ baseURL })

  const users = [SEED.user1, SEED.user2, SEED.user3, SEED.admin]

  console.log('\n[global-setup] Seeding test users...')

  for (const user of users) {
    try {
      const resp = await context.post(`${baseURL}/api/auth/register`, {
        data: {
          email: user.email,
          displayName: user.displayName,
          authMethod: 'password',
          password: user.password,
          gender: user.gender,
          birthDate: user.birthDate,
        },
      })

      if (resp.status() === 200 || resp.status() === 201) {
        console.log(`  ✓ Registered: ${user.email}`)
      } else if (resp.status() === 409) {
        console.log(`  ↩ Already exists: ${user.email}`)
      } else {
        const body = await resp.text()
        console.warn(`  ✗ Failed to register ${user.email}: HTTP ${resp.status()} — ${body}`)
      }
    } catch (err) {
      console.warn(`  ✗ Error registering ${user.email}: ${err}`)
    }
  }

  // ── Promote the admin seed user to Admin role via a direct DB update ─────────
  // Registration via the API always creates a "User" role account.  There is no
  // public bootstrap endpoint, so we escalate through the MariaDB container.
  // This is idempotent: re-running the setup leaves the role at "Admin".
  console.log('[global-setup] Promoting admin seed user...')
  try {
    const sql = `UPDATE Users SET Role='Admin' WHERE Email='${SEED.admin.email}';`
    execSync(
      `docker exec 07ad0b82_omnijoy_mysql mariadb -uomnijoy -pomnijoy_pass omnijoy -e "${sql}"`,
      { stdio: 'pipe' },
    )
    console.log(`  ✓ Promoted ${SEED.admin.email} to Admin`)
  } catch (err) {
    console.warn(`  ✗ Failed to promote admin user: ${err}`)
  }

  // ── Pre-fetch JWTs for the SignalR specs ─────────────────────────────────────
  // The `strict` rate-limit on AuthController (10 req/min/IP) means that
  // logging in repeatedly across many spec files can hit 429.  By logging
  // in here once for each seed user, persisting tokens to a file, and
  // letting the SignalR spec files read them, we keep the auth-endpoint
  // request count low and deterministic.
  //
  // Each login retries on 429 (the strict policy is a 60-second fixed
  // window) — this keeps the cache deterministic even when global-setup
  // runs right after a hot test loop that already drained the bucket.
  console.log('[global-setup] Fetching JWTs for shared auth cache...')
  const auth: SharedAuth = { baseURL, users: {} }
  for (const [key, user] of Object.entries({
    user1: SEED.user1,
    user2: SEED.user2,
    user3: SEED.user3,
    admin: SEED.admin,
  })) {
    let cached = false
    for (let attempt = 1; attempt <= 6 && !cached; attempt++) {
      try {
        const resp = await context.post(`${baseURL}/api/auth/login`, {
          data: { email: user.email, password: user.password },
        })
        if (resp.ok()) {
          const body = await resp.json()
          auth.users[key] = {
            email:        user.email,
            token:        body.accessToken,
            userId:       body.user.id,
            refreshToken: body.refreshToken,
            user:         body.user,
          }
          console.log(`  ✓ Cached token for ${user.email}`)
          cached = true
          break
        }
        if (resp.status() === 429) {
          const retryAfterSec = Number.parseInt(resp.headers()['retry-after'] ?? '', 10)
          const waitMs = Number.isFinite(retryAfterSec) && retryAfterSec > 0
            ? Math.min(retryAfterSec * 1000, 30_000)
            : 12_000
          console.log(`  ⏳ Rate-limited for ${user.email}, retrying in ${waitMs}ms (attempt ${attempt}/6)`)
          await new Promise((r) => setTimeout(r, waitMs))
          continue
        }
        console.warn(`  ✗ Could not pre-login ${user.email}: HTTP ${resp.status()}`)
        break
      } catch (err) {
        console.warn(`  ✗ Pre-login error for ${user.email}: ${err}`)
        break
      }
    }
  }

  try {
    mkdirSync(dirname(SHARED_AUTH_PATH), { recursive: true })
    writeFileSync(SHARED_AUTH_PATH, JSON.stringify(auth, null, 2))
    console.log(`[global-setup] Wrote shared auth → ${SHARED_AUTH_PATH}`)
  } catch (err) {
    console.warn(`[global-setup] Failed to write shared auth cache: ${err}`)
  }

  await context.dispose()
  console.log('[global-setup] Done.\n')
}

export default globalSetup
