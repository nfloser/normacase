import { test, expect } from '@playwright/test';

async function loadAndEvaluate(page, exampleId) {
  await page.getByLabel('Beispiel auswählen').selectOption(exampleId);
  await page.getByRole('button', { name: 'Beispiel laden' }).click();
  await expect(page.getByLabel('Prüfdatum')).toHaveValue('2026-10-03');
  await page.getByRole('button', { name: 'Jetzt prüfen' }).click();
}

test('the synthetic pitch story is repeatable from incomplete through explicit outcomes', async ({ page }) => {
  await page.goto('/');
  await page.getByRole('combobox', { name: 'Prüfbereich' }).selectOption('synthetic.demo-g');
  await expect(page.getByText('Vollständig fiktives Showcase ohne medizinische oder sozialmedizinische Aussagekraft.')).toBeVisible();

  await loadAndEvaluate(page, 'incomplete');
  await expect(page.getByRole('heading', { name: 'Angaben unvollständig' })).toBeVisible();
  await expect(page.getByText('Pflichtangaben vollständig', { exact: true })).toBeVisible();

  await loadAndEvaluate(page, 'review');
  await expect(page.getByRole('heading', { name: 'Manuelle Prüfung erforderlich' })).toBeVisible();
  await expect(page.getByText('Fiktive Regelquelle für die NormaCase Pitch-Demo', { exact: true })).toBeVisible();
  await expect(page.getByText('DEMO-G-DECISION', { exact: true })).toBeVisible();

  await loadAndEvaluate(page, 'supported');
  await expect(page.getByRole('heading', { name: 'Voraussetzungen erfüllt' })).toBeVisible();
  await page.getByText('Technische Prüfspur anzeigen').click();
  await expect(page.locator('pre')).toContainText('"knowledgeRelease":"demo-g-2026.1"');
  await expect(page.locator('pre')).toContainText('"sourceId":"SYNTH-DEMO-G-001"');
  await page.screenshot({ path: 'test-results/pitch-demo-supported.png', fullPage: true });

  await loadAndEvaluate(page, 'not-supported');
  await expect(page.getByRole('heading', { name: 'Voraussetzungen nicht erfüllt' })).toBeVisible();
});
