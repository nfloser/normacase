import { test, expect } from '@playwright/test';

const credential = process.env.NORMACASE_ANGULAR_E2E_CREDENTIAL;

test('Angular queue pilot uses the authenticated scoped local host and clears its session', async ({ page }) => {
  const cspViolations = [];
  await page.addInitScript(() => {
    window.__cspViolations = [];
    document.addEventListener('securitypolicyviolation', event => window.__cspViolations.push(event.violatedDirective));
  });
  const documentResponse = await page.goto('/angular-pilot/index.html');
  expect(documentResponse.status()).toBe(200);
  expect(documentResponse.headers()['content-security-policy']).toContain("style-src 'self'");
  await page.getByLabel('Zugangsschlüssel').fill(credential);
  await page.getByRole('button', { name: 'Arbeitslisten öffnen' }).click();
  await expect(page.getByRole('heading', { name: /Prüfung erforderlich/ })).toBeVisible();
  await expect(page.getByText('demo-g-review', { exact: true })).toBeVisible();
  await expect(page.getByText('demo-g-supported', { exact: true })).toHaveCount(0);
  expect(await page.evaluate(() => ({ local: localStorage.length, session: sessionStorage.length }))).toEqual({ local: 0, session: 0 });
  cspViolations.push(...await page.evaluate(() => window.__cspViolations));
  expect(cspViolations).toEqual([]);
  await page.getByRole('button', { name: 'Abmelden' }).click();
  await expect(page.getByText('demo-g-review', { exact: true })).toHaveCount(0);
  await expect(page.getByLabel('Zugangsschlüssel')).toHaveValue('');
});

test('Angular queue pilot rejects an invalid credential', async ({ page }) => {
  await page.goto('/angular-pilot/index.html');
  await page.getByLabel('Zugangsschlüssel').fill('invalid');
  await page.getByRole('button', { name: 'Arbeitslisten öffnen' }).click();
  await expect(page.getByRole('alert')).toContainText('ungültig oder abgelaufen');
  await expect(page.getByLabel('Zugangsschlüssel')).toHaveValue('');
});

test('Angular queue pilot supports keyboard login on a narrow screen', async ({ page }) => {
  await page.setViewportSize({ width: 375, height: 812 });
  await page.goto('/angular-pilot/index.html');
  await page.keyboard.press('Tab');
  await expect(page.getByLabel('Zugangsschlüssel')).toBeFocused();
  await page.keyboard.type(credential);
  await page.keyboard.press('Enter');
  await expect(page.getByText('demo-g-review', { exact: true })).toBeVisible();
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
  await expect(page.getByRole('button', { name: 'Abmelden' })).toBeFocused();
  await page.keyboard.press('Enter');
  await expect(page.getByLabel('Zugangsschlüssel')).toBeFocused();
});
