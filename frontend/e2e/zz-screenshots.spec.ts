import { expect, test, type Page } from '@playwright/test';

// Erzeugt die Screenshots für README und Case Study aus dem echten Stack (läuft nach den Ablauftests).
test.describe.configure({ mode: 'serial' });
test.use({ viewport: { width: 1440, height: 900 }, deviceScaleFactor: 1.5, colorScheme: 'light' });

const dir = 'screenshots';
let n = 0;
async function shot(page: Page, name: string) {
  await page.waitForTimeout(500);
  await page.screenshot({ path: `${dir}/${String(++n).padStart(2, '0')}-${name}.png` });
}

async function login(page: Page, user: string, capture = false) {
  await page.goto('/');
  if (capture) await shot(page, 'anmeldung');
  await page.getByRole('button', { name: 'Anmelden' }).click();
  await page.locator('#username').waitFor();
  if (capture) await shot(page, 'anmeldung-idp');
  await page.locator('#username').fill(user);
  await page.locator('#password').fill('demo');
  await page.locator('#kc-login').click();
  await expect(page.getByRole('navigation', { name: 'Arbeitskörbe' })).toBeVisible();
}

async function open(page: Page, queue: RegExp, caseNumber: string) {
  await page.getByRole('button', { name: queue }).click();
  await page.locator('.case-table tbody tr').filter({ hasText: caseNumber }).click();
  const file = page.getByRole('region', { name: 'Fallakte' });
  await expect(file.locator('.case-meta-line')).toContainText(caseNumber);
  return file;
}

async function documentTab(page: Page, file: ReturnType<Page['getByRole']>, title: string) {
  await file.getByRole('tab', { name: /Dokumente/ }).click();
  await file.locator('.file-item').filter({ hasText: title }).click();
  await expect(file.locator('.sheet img').first()).toBeVisible();
}

test('Screenshots der Prüfwerkstatt', async ({ page }) => {
  test.setTimeout(180_000);
  await login(page, 'gutachterin.a', true);

  let file = await open(page, /Zur Prüfung/, 'PG-2026-104233');
  await shot(page, 'pruefung-fallakte');
  await documentTab(page, file, 'Befundbericht');
  await shot(page, 'pruefung-befundbericht');
  await file.locator('.file-item').filter({ hasText: 'Erhebungsbogen' }).click();
  await shot(page, 'pruefung-erhebungsbogen');
  await file.getByRole('tab', { name: 'Verlauf' }).click();
  await shot(page, 'pruefung-verlauf');
  await file.getByRole('tab', { name: 'Nachweis' }).click();
  await shot(page, 'pruefung-nachweis');
  await file.getByRole('tab', { name: 'Prüfung' }).click();

  file = await open(page, /Rückfragen/, 'KT-2026-208157');
  await shot(page, 'rueckfrage-widerspruch');
  await documentTab(page, file, 'Schwerbehindertenausweis');
  await shot(page, 'rueckfrage-ausweis');

  file = await open(page, /Rückfragen/, 'PG-2026-104315');
  await shot(page, 'rueckfrage-unvollstaendig');
  await documentTab(page, file, 'Nachtrag');
  await shot(page, 'rueckfrage-nachtrag');
  await file.getByRole('tab', { name: 'Prüfung' }).click();
  await file.getByRole('button', { name: 'Angaben ergänzen' }).click();
  await page.locator('#f-module_4_sum').fill('12');
  await shot(page, 'rueckfrage-formular');
  await page.getByRole('button', { name: 'Neu bewerten' }).click();
  await expect(page.locator('.toast')).toBeVisible();

  file = await open(page, /Zur Prüfung/, 'PG-2026-104315');
  await shot(page, 'nach-rueckfrage-neu-bewertet');

  await page.getByRole('button', { name: /Zur Prüfung/ }).click();
  await page.locator('thead input[type=checkbox]').check();
  await shot(page, 'mehrfachauswahl');
  await page.locator('thead input[type=checkbox]').uncheck();

  file = await open(page, /Zur Prüfung/, 'PG-2026-104233');
  await file.getByRole('button', { name: /Bestätigen/ }).click();
  await expect(page.locator('.toast')).toContainText('Freigabe');
  await shot(page, 'bestaetigt');
  await page.getByRole('button', { name: 'Abmelden' }).click();

  await login(page, 'freigabe.c');
  file = await open(page, /Zur Freigabe/, 'PG-2026-104233');
  await shot(page, 'freigabe');
  await file.getByRole('button', { name: 'Zurückweisen' }).click();
  await shot(page, 'freigabe-zurueckweisen');
  await page.getByRole('button', { name: 'Abbrechen' }).click();
  await file.getByRole('button', { name: /Freigeben/ }).click();
  await expect(page.locator('.toast')).toContainText('Abholung');

  await page.getByRole('button', { name: /Erledigt/ }).click();
  await page.locator('.case-table tbody tr').first().click();
  await shot(page, 'erledigt');
});
