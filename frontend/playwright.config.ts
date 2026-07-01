import { defineConfig, devices } from '@playwright/test';

/**
 * E2E config. Tests run against the already-running local stack (frontend 3003 → gateway
 * 5055 → Identity 5047). Start the stack first (see frontend/AUTH.md), then `npm run e2e`.
 */
export default defineConfig({
  testDir: './e2e',
  // Serial + generous timeouts: the local dev Identity service is cold-start slow
  // (ephemeral RSA key, debug build, no warmup) — parallel hits cause false timeouts,
  // not real failures. A warmed prod build would not need this.
  fullyParallel: false,
  workers: 1,
  timeout: 120_000,
  expect: { timeout: 30_000 },
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 1 : 0,
  reporter: [['list'], ['html', { open: 'never' }]],
  use: {
    baseURL: 'http://localhost:3003',
    trace: 'on-first-retry',
    screenshot: 'only-on-failure',
    actionTimeout: 15_000,
    navigationTimeout: 30_000,
  },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
});
