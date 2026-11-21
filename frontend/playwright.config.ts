import { defineConfig } from '@playwright/test';

export default defineConfig({
  testDir: 'e2e',
  timeout: 60_000,
  retries: process.env['CI'] ? 1 : 0,
  reporter: [['list'], ['html', { open: 'never' }]],
  use: {
    baseURL: process.env['NORMACASE_URL'] ?? 'http://localhost:4200',
    locale: 'de-DE',
    trace: 'retain-on-failure',
  },
});
