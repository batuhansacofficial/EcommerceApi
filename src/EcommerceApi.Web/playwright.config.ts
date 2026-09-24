import { defineConfig } from '@playwright/test';

export default defineConfig({
  testDir: './tests',
  fullyParallel: false,
  workers: 1,
  timeout: 45_000,
  use: { baseURL: process.env.E2E_BASE_URL ?? 'http://127.0.0.1:5211', trace: 'retain-on-failure' },
  reporter: [['list'], ['html', { open: 'never' }]],
});
