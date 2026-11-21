import { expect, test } from '@playwright/test';

// Läuft gegen den kompletten Compose-Stack (Keycloak, Backend, Frontend).
test('Anmeldung über Keycloak und Abmeldung', async ({ page }) => {
  const violations: string[] = [];
  page.on('console', (msg) => {
    if (msg.type() === 'error' && /Content Security Policy|Refused/.test(msg.text())) violations.push(msg.text());
  });

  await page.goto('/');
  await expect(page.getByRole('heading', { name: 'Anmeldung' })).toBeVisible();
  await expect(page.getByRole('status')).toHaveText('Verbunden');

  await page.getByRole('button', { name: 'Anmelden' }).click();
  await page.locator('#username').fill('gutachterin.a');
  await page.locator('#password').fill('demo');
  await page.locator('#kc-login').click();

  await expect(page.getByRole('navigation', { name: 'Arbeitskörbe' })).toBeVisible();
  await expect(page.locator('.topbar')).toContainText('Gutachterin A');
  await expect(page.locator('.topbar')).toContainText('Gutachter:in');
  expect(new URL(page.url()).search).toBe('');

  await page.getByRole('button', { name: 'Abmelden' }).click();
  await expect(page.getByRole('heading', { name: 'Anmeldung' })).toBeVisible();
  expect(violations).toEqual([]);
});
