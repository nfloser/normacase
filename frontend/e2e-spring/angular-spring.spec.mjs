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

test('authorized cases paginate through Angular, Spring and the real review host', async ({ page }) => {
  const credential = process.env.NORMACASE_JAVA_PAGED_CREDENTIAL;
  expect(credential).toBeTruthy();
  // A small page size forces two real upstream pages without changing the product default.
  await page.route('**/api/review/work-queues*', async route => {
    const url = new URL(route.request().url());
    url.searchParams.set('pageSize', '1');
    await route.continue({ url: url.toString() });
  });
  await page.goto('/angular-pilot/index.html');
  await page.getByLabel('Zugangsschlüssel').fill(credential);
  await page.getByRole('button', { name: 'Arbeitslisten öffnen' }).click();
  await expect(page.getByText('demo-g-review', { exact: true })).toBeVisible();
  await expect(page.getByText('demo-g-supported', { exact: true })).toHaveCount(0);
  await expect(page.getByText('demo-g-incomplete', { exact: true })).toHaveCount(0);
  await page.getByRole('button', { name: 'Weitere Fälle' }).click();
  await expect(page.getByText('demo-g-supported', { exact: true })).toBeVisible();
  await expect(page.getByText('demo-g-review', { exact: true })).toHaveCount(0);
  await expect(page.getByRole('button', { name: 'Weitere Fälle' })).toHaveCount(0);
  await page.getByRole('button', { name: 'Abmelden' }).click();
  await expect(page.getByText('demo-g-supported', { exact: true })).toHaveCount(0);
  expect(await page.evaluate(() => ({ local: localStorage.length, session: sessionStorage.length }))).toEqual({ local: 0, session: 0 });
});
