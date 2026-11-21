import { test, expect } from '@playwright/test';
import { readFile } from 'node:fs/promises';

async function openWorkflow(page) {
  await page.goto('/#workflow');
  await page.getByRole('combobox', { name: 'Prüfbereich für Vorgänge' }).selectOption('synthetic.demo-f');
  await page.getByRole('button', { name: 'Synthetische Vorgangsangaben laden' }).click();
}

test('German workflow starts, returns, resumes exact JSON and finishes', async ({ page }) => {
  await openWorkflow(page);
  await page.getByRole('button', { name: 'Vorgang starten', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Aktueller Vorgangsstand: In Vorbereitung' })).toBeVisible();
  await page.getByLabel('Begründung des Schritts').fill('Synthetisch zur Prüfung gegeben');
  await page.getByRole('button', { name: 'Übergang ausführen' }).click();
  await expect(page.getByRole('heading', { name: 'Aktueller Vorgangsstand: In Prüfung' })).toBeVisible();
  await page.getByLabel('Übergang', { exact: true }).selectOption('return');
  await page.getByRole('button', { name: 'Übergang ausführen' }).click();
  await expect(page.getByRole('heading', { name: 'Aktueller Vorgangsstand: In Vorbereitung' })).toBeVisible();
  const downloading = page.waitForEvent('download');
  await page.getByRole('button', { name: 'Vorgang als JSON herunterladen' }).click();
  const download = await downloading;
  expect(download.suggestedFilename()).toBe('normacase-vorgang.json');
  const original = await readFile(await download.path(), 'utf8');
  expect(original).toContain('Synthetisch zur Pr');
  const request = page.waitForResponse(response => response.url().endsWith('/api/workflows/verify'));
  await page.getByRole('button', { name: 'Vorgang zurücksetzen' }).click();
  await page.getByLabel('Vorgangsdatei auswählen').setInputFiles({ name: 'vorgang.json', mimeType: 'application/json', buffer: Buffer.from(original) });
  await expect(page.getByRole('heading', { name: 'Vorgangsdatei geprüft und geladen' })).toBeVisible();
  expect((await (await request).json()).runJson).toBe(original);
  await page.getByLabel('Synthetische Bearbeiter-ID').fill('synthetic-reviewer');
  await page.getByLabel('UTC-Zeitpunkt (ISO 8601)', { exact: true }).fill('2026-10-03T12:01:00Z');
  await page.getByLabel('Begründung des Schritts').fill('Synthetisch erneut eingereicht');
  await page.getByRole('button', { name: 'Übergang ausführen' }).click();
  await expect(page.getByRole('heading', { name: 'Aktueller Vorgangsstand: In Prüfung' })).toBeVisible();
  await page.getByLabel('Übergang', { exact: true }).selectOption('finish');
  await page.getByLabel('Begründung des Schritts').fill('Synthetische Prüfung abgeschlossen');
  await page.getByRole('button', { name: 'Übergang ausführen' }).click();
  await expect(page.getByRole('heading', { name: 'Aktueller Vorgangsstand: Abgeschlossen' })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Übergang ausführen' })).toHaveCount(0);
  await expect(page.locator('.workflow-history li')).toHaveCount(5);
  await page.screenshot({ path: 'test-results/workbench-workflow-desktop.png', fullPage: true });
});

test('invalid import clears prior run and untrusted source is never displayed', async ({ page }) => {
  await openWorkflow(page);
  const started = page.waitForResponse(response => response.url().endsWith('/api/workflows/synthetic.demo-f/start'));
  await page.getByRole('button', { name: 'Vorgang starten', exact: true }).click();
  const initial = (await (await started).json()).runJson;
  await expect(page.getByRole('heading', { name: 'Aktueller Vorgangsstand: In Vorbereitung' })).toBeVisible();
  const altered = JSON.parse(initial);
  altered.run.history[0].snapshot.source.title = 'untrusted-source-marker';
  await page.getByLabel('Vorgangsdatei auswählen').setInputFiles({ name: 'altered.json', mimeType: 'application/json', buffer: Buffer.from(JSON.stringify(altered)) });
  await expect(page.locator('.workflow-tools').getByRole('alert')).toContainText('passt nicht');
  await expect(page.getByRole('button', { name: 'Vorgang als JSON herunterladen' })).toHaveCount(0);
  await expect(page.getByText('untrusted-source-marker')).toHaveCount(0);
});

test('editing prospective metadata aborts a pending transition without stale state', async ({ page }) => {
  await openWorkflow(page);
  await page.getByRole('button', { name: 'Vorgang starten', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Aktueller Vorgangsstand: In Vorbereitung' })).toBeVisible();
  let release, observed, completed;
  const gate = new Promise(resolve => { release = resolve; });
  const started = new Promise(resolve => { observed = resolve; });
  const finished = new Promise(resolve => { completed = resolve; });
  await page.route('**/api/workflows/advance', async route => {
    const response = await route.fetch();
    observed(); await gate;
    try { await route.fulfill({ response }); }
    catch { /* The edited form has already cancelled the request. */ }
    finally { completed(); }
  });
  await page.getByRole('button', { name: 'Übergang ausführen' }).click();
  await started;
  const cancelled = page.waitForEvent('requestfailed', request => request.url().endsWith('/api/workflows/advance'));
  await page.getByLabel('Begründung des Schritts').fill('Geänderte synthetische Begründung');
  release(); await Promise.all([cancelled, finished]);
  await expect(page.getByRole('heading', { name: 'Aktueller Vorgangsstand: In Vorbereitung' })).toBeVisible();
  await expect(page.locator('.workflow-history li')).toHaveCount(1);
  await expect(page.locator('.workflow-tools').getByRole('alert')).toHaveCount(0);
});

test('mobile workflow fits and oversized or invalid UTF-8 files never leave browser', async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await openWorkflow(page);
  let sent = 0;
  page.on('request', request => { if (request.url().endsWith('/api/workflows/verify')) sent++; });
  await page.getByLabel('Vorgangsdatei auswählen').setInputFiles({ name: 'large.json', mimeType: 'application/json', buffer: Buffer.alloc(1024 * 1024 + 1, 32) });
  await expect(page.locator('.workflow-tools').getByRole('alert')).toContainText('1 MiB');
  await page.getByLabel('Vorgangsdatei auswählen').setInputFiles({ name: 'invalid.json', mimeType: 'application/json', buffer: Buffer.from([0xff, 0xfe, 0xff]) });
  await expect(page.locator('.workflow-tools').getByRole('alert')).toContainText('kein gültiges UTF-8');
  expect(sent).toBe(0);
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
  await page.screenshot({ path: 'test-results/workbench-workflow-mobile.png', fullPage: true });
});
