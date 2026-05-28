import { defineConfig, devices } from '@playwright/test'
import path from 'path'

/**
 * Omnijoy E2E test configuration.
 *
 * Browser tests target the running dev/prod server at http://localhost:80.
 * API tests use Playwright's request context (no browser) and also hit
 * http://localhost:80/api/*.
 *
 * Run headless by default; use --headed flag for debugging.
 */
export default defineConfig({
  testDir: './tests',
  fullyParallel: false,       // Tests share seeded fixtures — run sequentially
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 1 : 0,
  workers: process.env.CI ? 1 : 1,
  globalSetup: path.resolve(__dirname, 'support/global-setup.ts'),
  reporter: [
    ['list'],
    ['html', { outputFolder: 'playwright-report', open: 'never' }],
  ],

  use: {
    baseURL: process.env.BASE_URL ?? 'http://localhost:80',
    trace: 'on-first-retry',
    screenshot: 'only-on-failure',
    video: 'retain-on-failure',
  },

  projects: [
    // ── API tests (no browser — pure request context) ─────────────────────
    {
      name: 'api',
      testMatch: 'tests/api/**/*.spec.ts',
      use: {
        // No browser needed; just baseURL
      },
    },

    // ── Browser E2E (Chromium, headless) ─────────────────────────────────
    {
      name: 'chromium',
      testMatch: 'tests/browser/**/*.spec.ts',
      use: {
        ...devices['Desktop Chrome'],
        headless: true,
      },
    },
  ],
})
