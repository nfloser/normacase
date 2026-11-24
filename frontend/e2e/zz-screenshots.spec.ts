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
  await expect(page.getByRole('navigation', { name: 'Arbeitslisten und Ablage' })).toBeVisible();
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

  await expect(page.locator('.case-table tbody tr').first()).toBeVisible();
  await shot(page, 'alle-vorgaenge');
  let file = await open(page, /Zur Freigabe vorbereitet/, 'PG-2026-104233');
  await shot(page, 'pruefung-fallakte');
  await page.locator('.case-table tbody tr').filter({ hasText: 'PG-2026-104233' }).dblclick();
  await expect(page.locator('.opened-case')).toBeVisible();
  await expect(page.locator('.split-right .sheet img').first()).toBeVisible();
  await shot(page, 'vollansicht');
  await page.locator('.split-left .source-link').first().click();
  await expect(page.locator('.split-right .sheet.highlighted img')).toBeVisible();
  await shot(page, 'vollansicht-fundstelle');
  await page.getByRole('button', { name: 'Großansicht', exact: true }).click();
  await shot(page, 'dokument-grossansicht');
  await page.keyboard.press('Escape');
  const [popup] = await Promise.all([page.waitForEvent('popup'), page.getByRole('button', { name: 'In neuem Fenster öffnen' }).click()]);
  await popup.setViewportSize({ width: 900, height: 1000 });
  await expect(popup.locator('.sheet img').first()).toBeVisible();
  await popup.waitForTimeout(500);
  await popup.screenshot({ path: `${dir}/${String(++n).padStart(2, '0')}-dokument-fenster.png` });
  await popup.close();
  await page.getByRole('button', { name: /Zurück zur Liste/ }).click();
  await expect(page.locator('.case-table')).toBeVisible();
  await documentTab(page, file, 'Befundbericht');
  await shot(page, 'pruefung-befundbericht');
  await file.locator('.file-item').filter({ hasText: 'Erhebungsbogen' }).click();
  await shot(page, 'pruefung-erhebungsbogen');
  await file.locator('.file-item').filter({ hasText: 'Fax' }).click();
  await expect(file.locator('.sheet img').first()).toBeVisible();
  await shot(page, 'dokument-fax');
  await file.locator('.file-item').filter({ hasText: 'Foto' }).click();
  await expect(file.locator('.sheet img').first()).toBeVisible();
  await shot(page, 'dokument-foto');
  await file.locator('.file-item').filter({ hasText: '(Scan)' }).click();
  await expect(file.locator('.sheet img').first()).toBeVisible();
  await shot(page, 'dokument-scan');
  await file.getByRole('tab', { name: 'Verlauf' }).click();
  await shot(page, 'pruefung-verlauf');
  await file.getByRole('tab', { name: 'Nachweis' }).click();
  await shot(page, 'pruefung-nachweis');
  await file.getByRole('tab', { name: 'Prüfung' }).click();

  file = await open(page, /Gegenprüfung erforderlich/, 'KT-2026-208157');
  await shot(page, 'rueckfrage-widerspruch');
  await documentTab(page, file, 'Schwerbehindertenausweis');
  await shot(page, 'rueckfrage-ausweis');

  file = await open(page, /Informationen nachfordern/, 'PG-2026-104315');
  await shot(page, 'rueckfrage-unvollstaendig');
  await documentTab(page, file, 'Nachtrag');
  await shot(page, 'rueckfrage-nachtrag');
  await file.getByRole('tab', { name: 'Prüfung' }).click();
  await file.getByRole('button', { name: 'Angaben ergänzen' }).click();
  await page.locator('#f-kf_4_4').fill('2');
  await shot(page, 'rueckfrage-formular');
  await page.getByRole('button', { name: 'Neu bewerten' }).click();
  await expect(page.locator('.toast')).toBeVisible();

  file = await open(page, /Zur Freigabe vorbereitet/, 'PG-2026-104315');
  await shot(page, 'nach-rueckfrage-neu-bewertet');

  await page.getByRole('button', { name: /Zur Freigabe vorbereitet/ }).click();
  await page.locator('thead input[type=checkbox]').check();
  await shot(page, 'mehrfachauswahl');
  await page.locator('thead input[type=checkbox]').uncheck();

  await page.getByLabel('Filter für Prüfergebnis').selectOption('Voraussetzungen erfüllt');
  await expect(page.locator('.case-table tbody tr')).not.toHaveCount(0);
  await shot(page, 'spaltenfilter');
  await page.getByRole('button', { name: 'Filter zurücksetzen' }).click();
  await page.getByRole('button', { name: /Spaltenoptionen/ }).click();
  await page.locator('.column-options select').selectOption({ label: 'Frist' });
  await shot(page, 'spaltenoptionen');
  await page.getByRole('button', { name: 'Abbrechen' }).click();
  await page.getByRole('button', { name: /Alle Vorgänge/ }).click();
  await page.getByRole('button', { name: 'Details (F4)' }).click();
  await expect(page.getByRole('region', { name: 'Fallakte' })).toHaveCount(0);
  await expect(page.locator('.toast')).toHaveCount(0, { timeout: 10_000 });
  await shot(page, 'liste-zeilenfaerbung');
  await page.keyboard.press('F4');
  await expect(page.getByRole('region', { name: 'Fallakte' })).toBeVisible();
  await page.getByRole('button', { name: /Vorschau rechts/ }).click();
  await shot(page, 'vorschau-rechts');
  await page.getByRole('button', { name: /Vorschau unten/ }).click();
  await page.getByLabel('Darstellung').selectOption('dark');
  await shot(page, 'dunkel');
  await page.getByLabel('Darstellung').selectOption('system');
  await page.getByRole('button', { name: /^Alle Vorgänge/ }).click();
  await page.locator('.case-table tbody tr').first().click({ button: 'right' });
  await page.getByRole('menu', { name: 'Vorgangsaktionen' }).getByRole('menuitem', { name: /Markieren/ }).hover();
  await shot(page, 'kontextmenue');
  await page.keyboard.press('Escape');
  await expect(page.locator('.context-menu')).toHaveCount(0);
  await page.locator('.menubar summary', { hasText: 'Extras' }).click();
  await page.getByRole('menuitem', { name: 'Prüfwerkstatt' }).click();
  await page.getByLabel('Regelwerk', { exact: true }).selectOption({ label: 'Pitch-Demo – Synthetische Fallprüfung' });
  await page.getByLabel('Beispiel', { exact: true }).selectOption({ label: '2 · Nachweis fehlt – manuelle Prüfung' });
  await page.getByRole('button', { name: 'Beispiel laden' }).click();
  await page.getByRole('button', { name: 'Prüfen', exact: true }).click();
  await expect(page.locator('.verdict')).toBeVisible();
  await shot(page, 'pruefwerkstatt');
  await page.locator('.menubar summary', { hasText: 'Extras' }).click();
  await page.getByRole('menuitem', { name: 'Referenzfälle' }).click();
  await page.getByRole('button', { name: 'Alle prüfen' }).click();
  await expect(page.getByText(/^13 von 13/)).toBeVisible({ timeout: 30_000 });
  await shot(page, 'referenzfaelle');
  await page.locator('.menubar summary', { hasText: 'Extras' }).click();
  await page.getByRole('menuitem', { name: 'Massenfälle' }).click();
  await shot(page, 'massenfaelle');
  await page.getByLabel('Darstellung').selectOption('dark');
  await shot(page, 'massenfaelle-dunkel');
  await page.locator('.menubar summary', { hasText: 'Extras' }).click();
  await page.getByRole('menuitem', { name: 'Prüfwerkstatt' }).click();
  await page.getByLabel('Regelwerk', { exact: true }).selectOption({ label: 'Pitch-Demo – Synthetische Fallprüfung' });
  await page.getByLabel('Beispiel', { exact: true }).selectOption({ label: '3 · Vollständig – Voraussetzungen erfüllt' });
  await page.getByRole('button', { name: 'Beispiel laden' }).click();
  await page.getByRole('button', { name: 'Prüfen', exact: true }).click();
  await expect(page.locator('.verdict')).toBeVisible();
  await shot(page, 'pruefwerkstatt-dunkel');
  await page.getByLabel('Darstellung').selectOption('system');
  await page.getByRole('button', { name: 'Zurück zu den Arbeitslisten' }).click();

  // Meine Ablage: eigener Ordner mit Unterordner, Vorgang darin, Lesezeichen, Markierung und Notiz.
  const nav = page.getByRole('navigation', { name: 'Arbeitslisten und Ablage' });
  await page.getByRole('button', { name: 'Neuer Ordner' }).first().click();
  await page.getByRole('textbox', { name: 'Name', exact: true }).fill('Rückfragen KW 48');
  await page.getByRole('button', { name: 'Bernstein', exact: true }).click();
  await page.getByRole('button', { name: 'Speichern', exact: true }).click();
  await nav.getByRole('button', { name: /^Rückfragen KW 48/ }).hover();
  await page.getByRole('button', { name: 'Unterordner in Rückfragen KW 48 anlegen' }).click();
  await page.getByRole('textbox', { name: 'Name', exact: true }).fill('Arztbrief angefordert');
  await page.getByRole('button', { name: 'Speichern', exact: true }).click();
  await nav.getByRole('button', { name: /Informationen nachfordern/ }).click();
  await expect(nav.getByRole('button', { name: /Informationen nachfordern/ })).toHaveAttribute('aria-current', 'true');
  const pending = page.locator('.case-table tbody tr').first();
  await expect(pending.locator('.col-status')).toHaveText('Informationen nachfordern');
  const menu = page.getByRole('menu', { name: 'Vorgangsaktionen' });
  await pending.click({ button: 'right' });
  await menu.getByRole('menuitem', { name: 'Lesezeichen setzen' }).click();
  await pending.click({ button: 'right' });
  await menu.getByRole('menuitem', { name: /^Markieren/ }).hover();
  await menu.getByRole('menuitem', { name: 'Rosé' }).click();
  await pending.click({ button: 'right' });
  await menu.getByRole('menuitem', { name: /Notiz/ }).click();
  await page.getByRole('textbox', { name: 'Notiz', exact: true }).fill('Arztbrief bis Freitag angefordert');
  await page.getByRole('button', { name: 'Speichern', exact: true }).click();
  await pending.click({ button: 'right' });
  await menu.getByRole('menuitem', { name: /Verschieben nach/ }).hover();
  await menu.getByRole('menuitem', { name: /Arztbrief angefordert/ }).click();
  await nav.getByRole('button', { name: /^Arztbrief angefordert/ }).click();
  await expect(page.locator('.case-table tbody tr .case-note')).toHaveText('Arztbrief bis Freitag angefordert');
  await shot(page, 'meine-ablage');

  // Nachweise des aktiven Vorgangs: nur Prüfsummen, Versionen, Regel und Ergebnis.
  await page.locator('.case-table tbody tr').first().click();
  await page.locator('.menubar summary', { hasText: 'Extras' }).click();
  await page.getByRole('menuitem', { name: 'Nachweise prüfen' }).click();
  await expect(page.locator('.plain-table tbody tr').first()).toBeVisible();
  await shot(page, 'nachweise');
  await page.getByRole('button', { name: 'Zurück zu den Arbeitslisten' }).click();

  file = await open(page, /Zur Freigabe vorbereitet/, 'PG-2026-104233');
  await file.getByRole('button', { name: /Bestätigen/ }).click();
  await expect(page.locator('.toast')).toContainText('Freigabe');
  await shot(page, 'bestaetigt');
  await page.getByRole('button', { name: 'Abmelden' }).click();

  await login(page, 'freigabe.c');
  file = await open(page, /^Bestätigt/, 'PG-2026-104233');
  await shot(page, 'freigabe');
  await file.getByRole('button', { name: 'Zurückweisen' }).click();
  await shot(page, 'freigabe-zurueckweisen');
  await page.getByRole('button', { name: 'Abbrechen' }).click();
  await file.getByRole('button', { name: /Freigeben/ }).click();
  await expect(page.locator('.toast')).toContainText('Abholung');

  await page.getByRole('button', { name: /^Versendet/ }).click();
  await page.locator('.case-table tbody tr').first().click();
  await shot(page, 'erledigt');
});
