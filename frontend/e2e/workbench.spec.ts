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
  await login(page, 'gutachterin.a');
  await page.getByRole('button', { name: /Zur Freigabe vorbereitet/ }).click();
  const row = page.locator('.case-table tbody tr').filter({ hasText: 'Voraussetzungen erfüllt' }).first();
  await expect(row).toBeVisible();
  caseNumber = (await row.locator('.col-number').textContent())!.trim();
  await row.click();

  const file = page.getByRole('region', { name: 'Fallakte' });
  await expect(file.locator('.case-title')).toBeVisible();
  await expect(file.locator('.outcome-badge')).toContainText('Voraussetzungen erfüllt');
  await expect(file.locator('.tree')).toBeVisible();
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
  expect(message.result.confirmedBy).toBe('gutachterin.a');
  expect((await request.post(`/api/v1/outbound/${message.messageId}/acknowledgement`, { headers })).status()).toBe(204);

  await page.getByRole('button', { name: /^Versendet/ }).click();
  await page.locator('.case-table tbody tr').filter({ hasText: caseNumber }).click();
  await expect(file).toContainText('gelöscht');
  await logout(page);
});
