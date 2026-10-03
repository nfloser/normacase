import { defineConfig } from '@playwright/test';
export default defineConfig({
  testDir:'./review-e2e',workers:1,use:{baseURL:'http://localhost:5080',browserName:'chromium'},
  webServer:{command:'dotnet ../src/NormaCase.Api/bin/Release/net10.0/NormaCase.Api.dll',
    env:{NORMACASE_REVIEW_DEMO:'1',NORMACASE_REVIEW_DEMO_KEY:'a'.repeat(64),NORMACASE_REVIEW_DEMO_CONNECTION:process.env.NORMACASE_POSTGRES_TEST_CONNECTION},
    url:'http://localhost:5080/api/review-mode',timeout:30000,reuseExistingServer:false},reporter:'list'
});
