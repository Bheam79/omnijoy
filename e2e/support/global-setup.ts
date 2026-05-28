import { request, FullConfig } from '@playwright/test'
import { SEED } from '../fixtures/seed-data'

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

  const users = [SEED.user1, SEED.user2, SEED.user3]

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

  await context.dispose()
  console.log('[global-setup] Done.\n')
}

export default globalSetup
