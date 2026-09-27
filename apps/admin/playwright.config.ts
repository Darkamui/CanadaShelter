import { defineConfig, devices } from '@playwright/test';

// Runs against `vite preview` of the production build; /api is mocked per test, so no backend is needed.
const port = 4173;
const ci = Boolean(process.env.CI);

export default defineConfig({
  testDir: './tests/e2e',
  forbidOnly: ci,
  retries: ci ? 1 : 0,
  reporter: ci ? [['github'], ['html', { open: 'never' }]] : 'list',
  use: {
    baseURL: `http://localhost:${port}`,
    // The service worker would bypass page.route() mocks.
    serviceWorkers: 'block',
    trace: 'retain-on-failure',
  },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
  webServer: {
    // Run vite directly (on PATH via the pnpm script). Behind `pnpm exec`, vite escapes Playwright's
    // process-group kill on Linux and teardown hangs until the CI timeout.
    command: `vite preview --port ${port} --strictPort`,
    url: `http://localhost:${port}`,
    reuseExistingServer: !ci,
  },
});
