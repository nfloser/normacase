import { defineConfig } from '@playwright/test';
export default defineConfig({
  testDir:'./e2e',
  testMatch:'review-workbench.spec.mjs',
  workers:1,
  use:{baseURL:'http://localhost:5080',browserName:'chromium'},
  webServer:{
    command:'dotnet ../src/NormaCase.Api/bin/Release/net10.0/NormaCase.Api.dll',
    url:'http://localhost:5080/api/packs',
    timeout:30000,
    reuseExistingServer:false
  },
  reporter:'list'
});
