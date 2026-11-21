import { defineConfig } from '@playwright/test';

if (!process.env.NORMACASE_JAVA_TEST_CREDENTIAL) throw new Error('Synthetic Spring test credential is required');
process.env.NORMACASE_ANGULAR_E2E_CREDENTIAL = process.env.NORMACASE_JAVA_TEST_CREDENTIAL;

// The CI job starts the local Spring and .NET hosts with synthetic PostgreSQL.
export default defineConfig({
  testDir: './e2e-spring',
  outputDir: 'test-results/spring',
  workers: 1,
  use: { baseURL: 'http://127.0.0.1:5081', browserName: 'chromium' },
  reporter: 'list',
});
