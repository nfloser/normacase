// Run the same keyboard, login, scope, CSP and logout scenarios on the Spring origin.
import '../e2e-reviewed/angular-pilot.spec.mjs';
import { test, expect } from '@playwright/test';

test('Spring serves only bounded local UI assets and preserves the origin boundary', async ({ page }) => {
  const response = await page.goto('/angular-pilot/index.html#review');
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
  await page.goto('/angular-pilot/index.html#review');
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

test('an authorized case opens read-only through Angular, Spring and the real review host', async ({ page }) => {
  const credential = process.env.NORMACASE_JAVA_TEST_CREDENTIAL;
  await page.addInitScript(() => {
    window.__cspViolations = [];
    document.addEventListener('securitypolicyviolation', event => window.__cspViolations.push(event.violatedDirective));
  });
  await page.goto('/angular-pilot/index.html#review');
  await page.getByLabel('Zugangsschlüssel').fill(credential);
  await page.getByRole('button', { name: 'Arbeitslisten öffnen' }).click();
  const detailResponse = page.waitForResponse(response => response.url().endsWith('/api/review/work-cases/demo-g-review'));
  await page.getByRole('button', { name: 'Fall öffnen: demo-g-review' }).click();
  const response = await detailResponse;
  expect(response.status()).toBe(200);
  expect(response.headers()['cache-control']).toBe('no-store');
  // The view only renders a body whose caseId matches the request; reading the body here raced Chromium's cache eviction.
  await expect(page.getByRole('heading', { name: /Fall demo-g-review/ })).toBeFocused();
  await expect(page.getByText('Systemergebnis', { exact: true })).toBeVisible();
  await expect(page.getByRole('heading', { name: 'Unveränderliche Prüfhistorie' })).toBeVisible();
  await expect(page.getByText(/Nur Ansicht/)).toBeVisible();
  // No write controls exist in this slice.
  await expect(page.getByRole('button', { name: /Freigeben|Übersteuern|übernehmen/ })).toHaveCount(0);
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
  await page.keyboard.press('Escape');
  await expect(page.getByRole('button', { name: 'Fall öffnen: demo-g-review' })).toBeFocused();
  expect(await page.evaluate(() => ({ local: localStorage.length, session: sessionStorage.length }))).toEqual({ local: 0, session: 0 });
  expect(await page.evaluate(() => window.__cspViolations)).toEqual([]);
  await page.getByRole('button', { name: 'Abmelden' }).click();
  await expect(page.getByText('demo-g-review', { exact: true })).toHaveCount(0);
});

test('Spring keeps unknown and unauthorized cases indistinguishable', async ({ page }) => {
  const credential = process.env.NORMACASE_JAVA_TEST_CREDENTIAL;
  const headers = { Authorization: 'Bearer ' + credential };
  const responses = [];
  for (const id of ['demo-g-supported', 'not-a-case']) {
    const response = await page.request.get('/api/review/work-cases/' + id, { headers });
    responses.push({ status: response.status(), type: response.headers()['content-type'], body: await response.text() });
  }
  expect(responses[0].status).toBe(404);
  expect(responses[1]).toEqual(responses[0]);
  expect((await page.request.get('/api/review/work-cases/demo-g-review')).status()).toBe(401);
  expect((await page.request.get('/api/review/work-cases/demo-g-review?full=1', { headers })).status()).toBe(400);
  expect((await page.request.get('/api/review/work-cases/demo-g-review/history', { headers })).status()).toBe(404);
  expect((await page.request.post('/api/review/work-cases/demo-g-review/reviews', { headers })).status()).toBe(404);
  expect((await page.request.post('/api/review/work-cases/demo-g-review', { headers })).status()).toBe(405);
});

test('the Angular case view shows the shared message when access is missing', async ({ page }) => {
  const credential = process.env.NORMACASE_JAVA_TEST_CREDENTIAL;
  // Swap the requested ID to a real case outside this user's scope; the UI must not reveal whether it exists.
  await page.route('**/api/review/work-cases/demo-g-review', route =>
    route.continue({ url: route.request().url().replace('demo-g-review', 'demo-g-supported') }));
  await page.goto('/angular-pilot/index.html#review');
  await page.getByLabel('Zugangsschlüssel').fill(credential);
  await page.getByRole('button', { name: 'Arbeitslisten öffnen' }).click();
  await page.getByRole('button', { name: 'Fall öffnen: demo-g-review' }).click();
  await expect(page.getByRole('alert')).toContainText('nicht vorhanden oder für diese Anmeldung nicht freigegeben');
  await expect(page.getByRole('button', { name: 'Abmelden' })).toBeVisible();
});
