import AxeBuilder from '@axe-core/playwright';
import { expect, test, type Page } from '@playwright/test';

// Läuft gegen den kompletten Compose-Stack; das synthetische Fachsystem (demo-host) hat Vorgänge eingeliefert.
test.describe.configure({ mode: 'serial' });

async function login(page: Page, user: string) {
  await page.goto('/');
  await page.getByRole('button', { name: 'Anmelden' }).click();
  await page.locator('#username').fill(user);
  await page.locator('#password').fill('demo');
  await page.locator('#kc-login').click();
  const workbench = page.getByRole('navigation', { name: 'Arbeitslisten und Ablage' });
  const problem = page.getByRole('alert');
  await expect(workbench.or(problem)).toBeVisible();
  if (await problem.isVisible()) throw new Error(`Anmeldung fehlgeschlagen: ${await problem.textContent()} (${page.url()})`);
}

async function logout(page: Page) {
  await page.getByRole('button', { name: 'Abmelden' }).click();
  await expect(page.getByRole('heading', { name: 'Anmeldung' })).toBeVisible();
}

let caseNumber = '';

test('Gutachterin prüft eine Fallakte mit Dokumenten und bestätigt', async ({ page }) => {
  await login(page, 'tobias.flöser');
  await page.getByRole('button', { name: /Zur Freigabe vorbereitet/ }).click();
  const row = page.locator('.case-table tbody tr').filter({ hasText: 'Voraussetzungen erfüllt' }).first();
  await expect(row).toBeVisible();
  caseNumber = (await row.locator('.col-number').textContent())!.trim();
  await row.click();

  const file = page.getByRole('region', { name: 'Fallakte' });
  await expect(file.locator('.case-title')).toBeVisible();
  await expect(file.locator('.outcome-badge')).toContainText('Voraussetzungen erfüllt');
  await expect(file.locator('.tree').first()).toBeVisible();
  await expect(file).not.toContainText('SUPPORTED');

  await file.getByRole('tab', { name: /Dokumente/ }).click();
  const firstPage = file.locator('.sheet img').first();
  await expect(firstPage).toBeVisible();
  expect(await firstPage.evaluate((img: HTMLImageElement) => img.naturalWidth)).toBeGreaterThan(0);

  const results = await new AxeBuilder({ page }).analyze();
  const serious = results.violations.filter((v) => v.impact === 'serious' || v.impact === 'critical');
  expect(serious.map((v) => `${v.id}: ${v.nodes.map((n) => n.target.join(' ')).join(', ')}`)).toEqual([]);

  await file.getByRole('tab', { name: 'Prüfung' }).click();
  await file.getByRole('button', { name: /Bestätigen/ }).click();
  await expect(page.locator('.toast')).toContainText('Freigabe');
  await logout(page);
});

test('Fallakte: Fundstelle aus der Begründung, Formate, Großansicht und eigenes Fenster', async ({ page }) => {
  await login(page, 'tobias.flöser');
  await page.getByRole('button', { name: /Gegenprüfung erforderlich/ }).click();
  await page.locator('.case-table tbody tr').filter({ hasText: 'KT-2026-208157' }).click();
  const file = page.getByRole('region', { name: 'Fallakte' });
  await expect(file.locator('.case-meta-line')).toContainText('KT-2026-208157');

  // Merkzeichen aG: Verordnung und Ausweis sind Fundstellen; der Ausweis öffnet sich an seiner Seite.
  await file.getByRole('button', { name: 'Alle aufklappen' }).click();
  await file.locator('.source-link').filter({ hasText: 'Schwerbehindertenausweis' }).first().click();
  await expect(file.getByRole('tab', { name: /Dokumente/ })).toHaveAttribute('aria-selected', 'true');
  await expect(file.locator('.file-item[aria-pressed=true]')).toContainText('Schwerbehindertenausweis');
  await expect(file.locator('.sheet.highlighted img')).toBeVisible();
  await expect(file.locator('.file-group h4')).toContainText(['Auftrag und Antrag', 'Ärztliche Unterlagen', 'Bescheide und Nachweise', 'Sonstige Unterlagen']);

  // Fax als TIFF: Seiten erzeugt der Server; Foto als JPEG; Textexport als Text.
  await file.locator('.file-item').filter({ hasText: 'Fax' }).click();
  const fax = file.locator('.sheet img').first();
  await expect(fax).toBeVisible();
  expect(await fax.evaluate((img: HTMLImageElement) => img.naturalWidth)).toBeGreaterThan(0);
  await file.locator('.file-item').filter({ hasText: 'Foto' }).click();
  await expect(file.locator('.sheet img').first()).toBeVisible();
  await file.locator('.file-item').filter({ hasText: 'Textexport' }).click();
  await expect(file.locator('.reader-text')).toContainText('Terminübersicht');

  const results = await new AxeBuilder({ page }).analyze();
  const serious = results.violations.filter((v) => v.impact === 'serious' || v.impact === 'critical');
  expect(serious.map((v) => `${v.id}: ${v.nodes.map((n) => n.target.join(' ')).join(', ')}`)).toEqual([]);

  await file.locator('.file-item').filter({ hasText: 'Verordnung' }).click();
  await file.getByRole('button', { name: 'Großansicht', exact: true }).click();
  await expect(page.locator('nc-pdf-reader.reader-max')).toBeVisible();
  await page.keyboard.press('Escape');
  await expect(page.locator('nc-pdf-reader.reader-max')).toHaveCount(0);

  const [popup] = await Promise.all([page.waitForEvent('popup'), file.getByRole('button', { name: 'In neuem Fenster öffnen' }).click()]);
  await expect(popup.locator('.sheet img')).toBeVisible();
  expect(await popup.title()).toContain('Verordnung');
  // Mit dem Vorgang schließt auch das Fenster.
  await page.locator('.case-table tbody tr').filter({ hasNotText: 'KT-2026-208157' }).first().click();
  await expect.poll(() => popup.isClosed()).toBe(true);
  await logout(page);
});

test('Spaltenoptionen: Reihenfolge durch Ziehen', async ({ page }) => {
  await login(page, 'tobias.flöser');
  await page.getByRole('button', { name: /Spaltenoptionen/ }).click();
  const names = page.locator('.column-list .column-name');
  await expect(names.first()).toHaveText('Fall / Auftrag');
  const source = await page.locator('.column-list li[data-column=number]').boundingBox();
  const target = await page.locator('.column-list li').first().boundingBox();
  await page.mouse.move(source!.x + source!.width / 2, source!.y + source!.height / 2);
  await page.mouse.down();
  await page.mouse.move(target!.x + target!.width / 2, target!.y + target!.height / 2, { steps: 8 });
  await page.mouse.up();
  await expect(names.first()).toHaveText('Vorgangsnummer');
  await page.getByRole('button', { name: 'Übernehmen' }).click();
  await expect(page.locator('.case-table thead tr:first-child th').nth(1)).toContainText('Vorgangsnummer');
  // zurücksetzen, damit die folgenden Abläufe die Standardansicht sehen
  await page.getByRole('button', { name: /Spaltenoptionen/ }).click();
  await page.getByRole('button', { name: 'Standard wiederherstellen' }).click();
  await page.getByRole('button', { name: 'Übernehmen' }).click();
  await logout(page);
});

test('Freigabe durch eine zweite Person; das Fachsystem holt ab und NormaCase löscht', async ({ page, request }) => {
  expect(caseNumber).not.toBe('');
  await login(page, 'freigabe.c');
  await page.getByRole('button', { name: /^Bestätigt/ }).click();
  const row = page.locator('.case-table tbody tr').filter({ hasText: caseNumber });
  await row.click();
  const file = page.getByRole('region', { name: 'Fallakte' });
  await expect(file.locator('.case-meta-line')).toContainText(caseNumber);
  await file.getByRole('button', { name: /Freigeben/ }).click();
  await expect(page.locator('.toast')).toContainText('Abholung');

  const token = await request.post('http://localhost:8081/realms/normacase/protocol/openid-connect/token', {
    form: { grant_type: 'client_credentials', client_id: 'normacase-host', client_secret: 'synthetic-host-secret' },
  }).then((r) => r.json()).then((j) => j.access_token as string);
  const headers = { Authorization: `Bearer ${token}` };
  const messages = await request.get('/api/v1/outbound', { headers }).then((r) => r.json());
  const message = messages.find((m: { externalReference: { caseId: string } }) => m.externalReference.caseId === caseNumber);
  expect(message.result.approvedBy).toBe('freigabe.c');
  expect(message.result.confirmedBy).toBe('tobias.flöser');
  expect((await request.post(`/api/v1/outbound/${message.messageId}/acknowledgement`, { headers })).status()).toBe(204);

  await page.getByRole('button', { name: /^Versendet/ }).click();
  await page.locator('.case-table tbody tr').filter({ hasText: caseNumber }).click();
  await expect(file).toContainText('gelöscht');
  await logout(page);
});

test('Meine Ablage: Ordner, Kontextmenü, Lesezeichen und Notiz', async ({ page }, info) => {
  const folder = `Rückfragen KW ${48 + info.retry}`;
  await login(page, 'gutachter.b');
  const nav = page.getByRole('navigation', { name: 'Arbeitslisten und Ablage' });

  await page.getByRole('button', { name: 'Neuer Ordner' }).first().click();
  await page.getByRole('textbox', { name: 'Name', exact: true }).fill(folder);
  await page.getByRole('button', { name: 'Grün', exact: true }).click();
  await page.getByRole('button', { name: 'Speichern', exact: true }).click();
  await expect(nav.getByRole('button', { name: new RegExp(`^${folder}`) })).toBeVisible();

  await nav.getByRole('button', { name: /Informationen nachfordern/ }).click();
  await expect(nav.getByRole('button', { name: /Informationen nachfordern/ })).toHaveAttribute('aria-current', 'true');
  const row = page.locator('.case-table tbody tr').first();
  await expect(row.locator('.col-status')).toHaveText('Informationen nachfordern');
  const number = (await row.locator('.col-number').textContent())!.trim();
  await row.click({ button: 'right' });
  const menu = page.getByRole('menu', { name: 'Vorgangsaktionen' });
  await expect(menu).toBeVisible();
  const results = await new AxeBuilder({ page }).include('.context-menu').analyze();
  expect(results.violations.filter((v) => v.impact === 'serious' || v.impact === 'critical').map((v) => v.id)).toEqual([]);
  await menu.getByRole('menuitem', { name: /Verschieben nach/ }).hover();
  await menu.getByRole('menuitem', { name: folder }).click();
  await expect(page.locator('.toast')).toContainText('Gespeichert');

  // Die Arbeitsliste zeigt den Vorgang weiter; „Alle Vorgänge“ nicht mehr, der Ordner schon.
  await expect(page.locator('.case-table tbody tr').filter({ hasText: number })).toHaveCount(1);
  await nav.getByRole('button', { name: new RegExp(`^${folder}`) }).click();
  const filed = page.locator('.case-table tbody tr').filter({ hasText: number });
  await expect(filed).toHaveCount(1);

  await filed.click();
  await filed.press('Shift+F10');
  await menu.getByRole('menuitem', { name: 'Lesezeichen setzen' }).click();
  await expect(filed.locator('.bookmark')).toBeVisible();
  await filed.click({ button: 'right' });
  await menu.getByRole('menuitem', { name: /Notiz/ }).click();
  await page.getByRole('textbox', { name: 'Notiz', exact: true }).fill('Rückruf Dienstag');
  await page.getByRole('button', { name: 'Speichern', exact: true }).click();
  await expect(filed.locator('.case-note')).toHaveText('Rückruf Dienstag');
  await expect(nav.getByRole('button', { name: /^Lesezeichen/ })).toContainText('1');

  await nav.getByRole('button', { name: /^Alle Vorgänge/ }).click();
  await expect(page.locator('.case-table tbody tr').filter({ hasText: number })).toHaveCount(0);
  await logout(page);
});

test('Extras: Prüfwerkstatt mit Beispiel und Referenzfälle mit Abgleich', async ({ page }) => {
  await login(page, 'tobias.flöser');
  const extras = async (name: string) => {
    await page.locator('.menubar summary', { hasText: 'Extras' }).click();
    await page.getByRole('menuitem', { name }).click();
    await expect(page.getByRole('heading', { level: 2, name, exact: true })).toBeVisible();
  };

  await extras('Prüfwerkstatt');
  await page.getByLabel('Regelwerk', { exact: true }).selectOption({ label: 'Pitch-Demo – Synthetische Fallprüfung' });
  await page.getByLabel('Beispiel', { exact: true }).selectOption({ label: '3 · Vollständig – Voraussetzungen erfüllt' });
  await page.getByRole('button', { name: 'Beispiel laden' }).click();
  await page.getByRole('button', { name: 'Prüfen', exact: true }).click();
  await expect(page.locator('.verdict')).toContainText('Voraussetzungen erfüllt');
  await expect(page.getByRole('button', { name: /Snapshot exportieren/ })).toBeVisible();
  const results = await new AxeBuilder({ page }).analyze();
  expect(results.violations.filter((v) => v.impact === 'serious' || v.impact === 'critical').map((v) => v.id)).toEqual([]);

  await extras('Referenzfälle');
  await page.getByRole('button', { name: 'Alle prüfen' }).click();
  await expect(page.getByText(/^13 von 13 Beispielen wie erwartet\.$/)).toBeVisible({ timeout: 30_000 });

  await extras('Massenfälle');
  await expect(page.getByText(/\d+ Plattformfälle/)).toBeVisible();
  await page.getByRole('button', { name: 'Zurück zu den Arbeitslisten' }).click();
  await expect(page.locator('.case-table')).toBeVisible();
  await logout(page);
});
