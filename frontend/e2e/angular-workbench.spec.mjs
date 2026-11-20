import { test, expect } from '@playwright/test';

// Runs against the anonymous local preview host with the staged Angular build.
async function openWorkbench(page) {
  await page.addInitScript(() => {
    window.__cspViolations = [];
    document.addEventListener('securitypolicyviolation', event => window.__cspViolations.push(event.violatedDirective));
  });
  const response = await page.goto('/angular-pilot/index.html');
  expect(response.status()).toBe(200);
  await expect(page.locator('tr[data-case-id="reference-transport-complete"]')).toBeVisible();
}
const row = (page, id) => page.locator(`tr[data-case-id="${id}"]`);

test('the Angular workbench lists reference files and opens a case with its documents', async ({ page }) => {
  await openWorkbench(page);
  await expect(page.locator('tr[data-case-id]')).toHaveCount(22);
  await expect(row(page, 'reference-transport-conflicting')).toContainText('Manuelle Prüfung erforderlich');

  await row(page, 'reference-transport-conflicting').click();
  const pane = page.locator('.reading-pane');
  await expect(pane.locator('.case-title')).toContainText('Sabine Winter');
  await expect(pane.getByRole('tree', { name: 'Begründung' })).toBeVisible();
  await expect(pane.locator('.callout')).toContainText('Widersprüchliche Angaben');

  // A provenance link opens the reader on the cited page.
  await pane.locator('.fact-list .link').first().click();
  await expect(pane.locator('.page-field input')).toHaveValue('2');
  await expect(pane.locator('.sheet.highlighted')).toHaveAttribute('data-page', '2');
  const image = pane.locator('.sheet[data-page="2"] img');
  await expect(image).toBeVisible();
  expect(await image.evaluate(element => element.complete && element.naturalWidth > 0)).toBe(true);

  // Keyboard: back to the list, move with the arrows, open with Enter, return with Alt+←.
  await row(page, 'reference-transport-complete').click();
  await page.keyboard.press('ArrowDown');
  await expect(row(page, 'reference-transport-missing')).toBeFocused();
  await page.keyboard.press('Enter');
  await expect(page.getByRole('button', { name: 'Zur Liste' })).toBeVisible();
  await page.keyboard.press('Alt+ArrowLeft');
  await expect(row(page, 'reference-transport-missing')).toBeFocused();

  expect(await page.evaluate(() => ({ local: localStorage.length, session: sessionStorage.length }))).toEqual({ local: 0, session: 0 });
  expect(await page.evaluate(() => window.__cspViolations)).toEqual([]);
});

test('folders, confirmation and the simulated dispatch use the local workspace', async ({ page }) => {
  await openWorkbench(page);
  await page.getByRole('button', { name: 'Neuer Ordner' }).first().click();
  await page.getByLabel('Ordnername').fill('Rückfragen');
  await page.locator('dialog').getByRole('button', { name: 'Speichern' }).click();
  await expect(page.locator('.nav-item', { hasText: 'Rückfragen' })).toBeVisible();

  await row(page, 'reference-transport-complete').click({ button: 'right' });
  await page.getByRole('menuitem', { name: /Verschieben nach/ }).click();
  await page.getByRole('menuitem', { name: 'Rückfragen' }).click();
  await expect(row(page, 'reference-transport-complete')).toHaveCount(0);
  await page.locator('.nav-item', { hasText: 'Rückfragen' }).click();
  await expect(row(page, 'reference-transport-complete')).toBeVisible();

  await row(page, 'reference-transport-complete').click();
  await page.locator('.reading-pane').getByRole('button', { name: /Fall bestätigen/ }).click();
  await page.locator('dialog').getByRole('button', { name: 'Bestätigen' }).click();
  await expect(page.locator('.reading-pane .chip').first()).toHaveText('Bestätigt');
  const download = page.waitForEvent('download');
  await page.locator('.reading-pane').getByRole('button', { name: /Versandpaket erstellen/ }).click();
  await page.locator('dialog').getByRole('button', { name: 'Versand simulieren' }).click();
  expect((await download).suggestedFilename()).toMatch(/^normacase-versand-demo-\d+\.json$/);
  await page.locator('.nav-item', { hasText: 'Versand simuliert' }).click();
  await expect(row(page, 'reference-transport-complete')).toBeVisible();
});

test('the workbench adapts to dark mode and a narrow window', async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await openWorkbench(page);
  await page.getByLabel('Darstellung').selectOption('dark');
  await expect(page.locator('html')).toHaveAttribute('data-theme', 'dark');
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
  await row(page, 'reference-care-complete').click();
  await expect(page.locator('.reading-pane .case-title')).toContainText('Erika Stein');
  await expect(page.locator('a', { hasText: 'Bisherige Prüfwerkstatt öffnen' })).toHaveAttribute('href', '/');
});
