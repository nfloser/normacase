// Run the same keyboard, login, scope, CSP and logout scenarios on the Spring origin.
import '../e2e-reviewed/angular-pilot.spec.mjs';
import { test, expect } from '@playwright/test';

test('Spring serves only bounded local UI assets and preserves the origin boundary', async ({ page }) => {
  const response = await page.goto('/angular-pilot/index.html');
  expect(response.status()).toBe(200);
  expect(response.headers()['cache-control']).toBe('no-store');
  expect(response.headers()['content-security-policy']).toContain("connect-src 'self'");
  await expect(page.getByRole('heading', { name: 'Arbeitslisten' })).toBeVisible();
  expect((await page.request.get('/angular-pilot/secrets.json')).status()).toBe(404);
  expect((await page.request.get('/angular-pilot/main-AB12CD34.js.map')).status()).toBe(404);
  expect((await page.request.get('/angular-pilot/index.html?token=synthetic')).status()).toBe(400);
  expect((await page.request.get('/angular-pilot/index.html', { headers: { Origin: 'https://evil.example' } })).status()).toBe(403);
  expect((await page.request.post('/angular-pilot/index.html')).status()).toBe(405);
});
