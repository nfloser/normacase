import { defineConfig } from '@playwright/test';
import { randomBytes } from 'node:crypto';

const credential = process.env.NORMACASE_REVIEW_E2E_CREDENTIAL ?? randomBytes(32).toString('base64');
process.env.NORMACASE_REVIEW_E2E_CREDENTIAL = credential;
const connection = process.env.NORMACASE_REVIEW_E2E_CONNECTION;
if (!credential || !connection) throw new Error('Reviewed workbench test configuration is required');

export default defineConfig({
  testDir: './e2e-reviewed',
  workers: 1,
  use: { baseURL: 'http://localhost:5080', browserName: 'chromium' },
  webServer: {
    command: 'dotnet ../src/NormaCase.Api/bin/Release/net10.0/NormaCase.Api.dll',
    url: 'http://localhost:5080/api/packs',
    timeout: 30000,
    reuseExistingServer: false,
    env: {
      ...process.env,
      SyntheticReview__Enabled: 'true',
      SyntheticReview__PersistenceEnabled: 'true',
      SyntheticReview__Credential: credential,
      ConnectionStrings__SyntheticReview: connection
    }
  },
  reporter: 'list'
});
