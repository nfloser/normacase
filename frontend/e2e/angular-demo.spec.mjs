import { test, expect } from '@playwright/test';

// Runs against the anonymous local preview host with the staged Angular build.
test('the Angular demo opens queues and a reference file from the real preview host', async ({ page }) => {
  await page.addInitScript(() => {
    window.__cspViolations = [];
    document.addEventListener('securitypolicyviolation', event => window.__cspViolations.push(event.violatedDirective));
  });
  const response = await page.goto('/angular-pilot/index.html');
  expect(response.status()).toBe(200);
  await page.getByRole('button', { name: 'Demo öffnen' }).click();
  await expect(page.getByText('Fälle insgesamt')).toBeVisible();
  await expect(page.locator('.stat').first()).toContainText('100');
  for (const queue of ['Zur Freigabe vorbereitet', 'Informationen nachfordern', 'Gegenprüfung erforderlich', 'Technische Klärung'])
    await expect(page.getByRole('heading', { name: new RegExp(queue) })).toBeVisible();

  await page.locator('button[data-case-id="demo-g-review"]').click();
  await expect(page.locator('.pill-outcome')).toHaveText('Manuelle Prüfung erforderlich');
  await expect(page.getByRole('tree', { name: 'Begründung' })).toBeVisible();
  await expect(page.locator('.page-image')).toBeVisible();
  expect(await page.locator('.page-image').evaluate(image => image.complete && image.naturalWidth > 0)).toBe(true);
  await page.locator('.link-row').first().click();
  await expect(page.locator('.page-info')).toContainText('Seite 2');
  await page.keyboard.press('Escape');
  await expect(page.locator('button[data-case-id="demo-g-review"]')).toBeFocused();

  await page.getByRole('searchbox', { name: 'Fälle filtern' }).fill('Sabine');
  await page.locator('button[data-case-id="reference-transport-conflicting"]').click();
  await expect(page.getByRole('heading', { name: /Sabine Winter/ })).toBeFocused();
  await expect(page.getByText('Widersprüchlich').first()).toBeVisible();
  await expect(page.getByRole('heading', { name: 'Fallkontext' })).toBeVisible();
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
  expect(await page.evaluate(() => ({ local: localStorage.length, session: sessionStorage.length }))).toEqual({ local: 0, session: 0 });
  expect(await page.evaluate(() => window.__cspViolations)).toEqual([]);
});

test('the Angular demo stays usable on a narrow screen and in dark mode', async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto('/angular-pilot/index.html');
  await page.getByRole('button', { name: 'Dunkelmodus' }).click();
  await page.getByRole('button', { name: 'Demo öffnen' }).click();
  await page.locator('button[data-case-id="demo-g-supported"]').click();
  await expect(page.locator('.pill-outcome')).toHaveText('Voraussetzungen erfüllt');
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
  await page.getByRole('button', { name: 'Zurück zur Übersicht' }).click();
  await page.getByRole('button', { name: 'Demo verlassen' }).click();
  await expect(page.getByRole('button', { name: 'Demo öffnen' })).toBeFocused();
});
