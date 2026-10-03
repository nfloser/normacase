import { test, expect } from '@playwright/test';

test('German synthetic work queues expose read-only assessed and technical drill-down', async ({ page }) => {
  await page.goto('/');

  await expect(page.getByRole('heading', { name: 'Synthetische Arbeitsvorräte' })).toBeVisible();
  await expect(page.getByRole('heading', { name: 'Zur Freigabe' })).toBeVisible();
  await expect(page.getByRole('heading', { name: 'Angaben nachfordern' })).toBeVisible();
  await expect(page.getByRole('heading', { name: 'Manuelle Prüfung' })).toBeVisible();
  await expect(page.getByRole('heading', { name: 'Technische Klärung' })).toBeVisible();

  await page.getByRole('button', { name: 'Fall synthetic-approval-001 öffnen' }).click();
  await expect(page.getByRole('heading', { name: 'Falldetails' })).toBeVisible();
  await expect(page.locator('.work-item-detail')).toContainText('Voraussetzungen erfüllt');
  await expect(page.locator('.work-item-detail')).toContainText('Bereit zur Freigabe');
  await expect(page.locator('.work-item-detail')).toContainText('Fiktive Regelquelle für die NormaCase Pitch-Demo');
  await expect(page.locator('.work-item-detail')).toContainText('DEMO-G-DECISION');

  await page.getByRole('button', { name: 'Fall synthetic-technical-001 öffnen' }).click();
  await expect(page.locator('.work-item-detail')).toContainText('Noch keine fachliche Prüfung aufgezeichnet');
  await expect(page.locator('.work-item-detail')).toContainText('Integrationsfehler');

  await expect(page.getByRole('button', { name: /freigeben|ablehnen|überschreiben/i })).toHaveCount(0);
});

test('synthetic work queues fit a narrow viewport', async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto('/');
  await expect(page.getByRole('heading', { name: 'Synthetische Arbeitsvorräte' })).toBeVisible();
  await page.getByRole('button', { name: 'Fall synthetic-review-001 öffnen' }).click();
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
  await page.screenshot({ path: 'test-results/work-queues-mobile.png', fullPage: true });
});
