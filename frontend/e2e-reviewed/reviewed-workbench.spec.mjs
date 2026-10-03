import { test, expect } from '@playwright/test';

const credential = process.env.NORMACASE_REVIEW_E2E_CREDENTIAL;
if (!credential) throw new Error('NORMACASE_REVIEW_E2E_CREDENTIAL is required');

async function login(page) {
  await page.goto('/');
  const reviewed = page.getByRole('region', { name: 'Persistente synthetische Fallprüfung' });
  await reviewed.getByLabel('Lokaler Review-Schlüssel').fill(credential);
  await reviewed.getByRole('button', { name: 'Review-Modus anmelden' }).click();
  await expect(reviewed.getByText('Angemeldet als synthetic-local:reviewer', { exact: true })).toBeVisible();
  await expect(reviewed.getByLabel('Lokaler Review-Schlüssel')).toHaveValue('');
  return reviewed;
}

test('review credential stays memory-only and accept commits through the real PostgreSQL API', async ({ page }) => {
  const reviewed = await login(page);
  expect(await page.evaluate(() => ({
    local: Object.keys(localStorage),
    session: Object.keys(sessionStorage),
    cookies: document.cookie
  }))).toEqual({ local: [], session: [], cookies: '' });

  await expect(reviewed.getByRole('heading', { name: 'Zur Freigabe vorbereitet (2)' })).toBeVisible();
  await reviewed.getByRole('button', { name: 'Fall öffnen: demo-g-supported' }).click();
  await expect(reviewed.getByText('Wartet auf menschliche Freigabe', { exact: true })).toBeVisible();
  await reviewed.getByLabel('Begründung der Review-Entscheidung').fill('Synthetische Browser-Freigabe');
  await reviewed.getByRole('button', { name: 'Systemergebnis übernehmen' }).click();

  await expect(reviewed.getByText('Review wurde persistent gespeichert.', { exact: true })).toBeVisible();
  await expect(reviewed.getByText('Freigegeben', { exact: true })).toBeVisible();
  await expect(reviewed.getByRole('heading', { name: 'Abgeschlossen (1)' })).toBeVisible();
  await expect(reviewed.getByText('synthetic-local:reviewer', { exact: true })).toBeVisible();
  await expect(reviewed.getByText('Synthetische Browser-Freigabe', { exact: true })).toBeVisible();

  await reviewed.getByRole('button', { name: 'Review-Modus abmelden' }).click();
  await expect(reviewed.getByRole('button', { name: 'Review-Modus anmelden' })).toBeVisible();
  await expect(reviewed.getByText('synthetic-local:reviewer', { exact: true })).toHaveCount(0);
  expect(await page.evaluate(() => [localStorage.length, sessionStorage.length, document.cookie])).toEqual([0, 0, '']);
});

test('override is offered only from server allowedActions and records an explicit generic outcome', async ({ page }) => {
  const reviewed = await login(page);
  await reviewed.getByRole('button', { name: 'Fall öffnen: demo-g-not-supported' }).click();
  await expect(reviewed.getByRole('button', { name: 'Ergebnis übersteuern' })).toBeVisible();
  await reviewed.getByLabel('Begründung der Review-Entscheidung').fill('Synthetischer Browser-Override');
  await reviewed.getByLabel('Abweichendes generisches Ergebnis').selectOption('SUPPORTED');
  await reviewed.getByRole('button', { name: 'Ergebnis übersteuern' }).click();
  await expect(reviewed.getByText('Übersteuert', { exact: true })).toBeVisible();
  await expect(reviewed.getByText('Voraussetzungen erfüllt', { exact: true })).toBeVisible();
});

test('409 reloads committed state and never applies an optimistic review', async ({ page }) => {
  const reviewed = await login(page);
  await reviewed.getByRole('button', { name: 'Fall öffnen: demo-g-supported' }).click();
  await reviewed.getByLabel('Begründung der Review-Entscheidung').fill('Stale Browser-Versuch');

  await page.route('**/api/review/work-cases/demo-g-supported/reviews', async route => {
    const request = route.request();
    const body = JSON.parse(request.postData() ?? '{}');
    body.expectedProcessRevision = '0';
    await route.continue({ postData: JSON.stringify(body) });
  });

  await reviewed.getByRole('button', { name: 'Systemergebnis übernehmen' }).click();
  await expect(reviewed.getByRole('alert')).toContainText('zwischenzeitlich geändert');
  await expect(reviewed.getByText('Wartet auf menschliche Freigabe', { exact: true })).toBeVisible();
  await expect(reviewed.getByText('Review wurde persistent gespeichert.', { exact: true })).toHaveCount(0);
});

test('401 clears the in-memory session and returns to the masked login form', async ({ page }) => {
  const reviewed = await login(page);
  await page.route('**/api/review/work-cases/demo-g-review', route =>
    route.fulfill({ status: 401, contentType: 'application/json',
      body: JSON.stringify({ code: 'review_authentication_required', message: 'Für die synthetische Fallprüfung ist eine gültige Anmeldung erforderlich.' }) }));
  await reviewed.getByRole('button', { name: 'Fall öffnen: demo-g-review' }).click();
  await expect(reviewed.getByRole('button', { name: 'Review-Modus anmelden' })).toBeVisible();
  await expect(reviewed.getByText('Angemeldet als synthetic-local:reviewer', { exact: true })).toHaveCount(0);
  await expect(reviewed.getByLabel('Lokaler Review-Schlüssel')).toHaveValue('');
});
