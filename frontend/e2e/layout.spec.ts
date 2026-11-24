import { expect, test } from '@playwright/test';

test('Prüfwerkstatt auf kleinem Bildschirm anordnen und bedienen', async ({ page, context }) => {
  await page.setViewportSize({ width: 600, height: 870 });
  await context.grantPermissions(['clipboard-read', 'clipboard-write']);
  await page.goto('/');
  await page.getByRole('button', { name: 'Anmelden' }).click();
  await page.locator('#username').fill('gutachter.b');
  await page.locator('#password').fill('demo');
  await page.locator('#kc-login').click();

  const menuToggle = page.getByRole('button', { name: 'Menü', exact: true });
  const commands = page.getByRole('toolbar', { name: 'Aktionen', exact: true });
  await expect(menuToggle).toHaveAttribute('aria-expanded', 'false');
  await expect(commands).toBeHidden();
  await menuToggle.click();
  await expect(commands).toBeVisible();

  const list = page.getByRole('table', { name: 'Alle Vorgänge' });
  const number = list.locator('.copy-number').first();
  await expect(number).toBeVisible();
  const caseId = (await number.textContent())!.trim();
  await number.click();
  await expect.poll(() => page.evaluate(() => navigator.clipboard.readText())).toBe(caseId);

  const widthHandle = list.getByRole('separator', { name: 'Spaltenbreite ändern: Vorgangsnummer' });
  const original = Number(await widthHandle.getAttribute('aria-valuenow'));
  await widthHandle.scrollIntoViewIfNeeded();
  const box = (await widthHandle.boundingBox())!;
  await page.mouse.move(box.x + box.width / 2, box.y + box.height / 2);
  await page.mouse.down();
  await page.mouse.move(box.x + box.width / 2 + 38, box.y + box.height / 2, { steps: 5 });
  await page.mouse.up();
  await expect(widthHandle).toHaveAttribute('aria-valuenow', String(original + 38));

  const position = commands.getByLabel('Fallvorschau');
  await position.selectOption('top');
  await expect(page.locator('.list-and-pane')).toHaveAttribute('data-pane', 'top');
  await page.getByRole('separator', { name: 'Größe von Liste und Vorschau ändern' }).focus();
  await page.keyboard.press('ArrowDown');
  await expect(page.getByRole('separator', { name: 'Größe von Liste und Vorschau ändern' })).toHaveAttribute('aria-valuenow', '50');

  const help = page.locator('.menubar summary', { hasText: 'Hilfe' });
  await help.click();
  await page.getByRole('menuitem', { name: 'Bedienung' }).click();
  await expect(page.getByRole('dialog', { name: 'Bedienung' })).toContainText('Wie schaffe ich Platz?');
  await page.getByRole('tab', { name: 'Bedienung' }).focus();
  await page.keyboard.press('ArrowRight');
  await expect(page.getByRole('tab', { name: 'Tastenkürzel' })).toBeFocused();
  await expect(page.getByRole('tabpanel', { name: 'Tastenkürzel' })).toContainText('Strg+K');
  await page.keyboard.press('Escape');
  await expect(help).toBeFocused();
  await help.click();
  await page.getByRole('menuitem', { name: 'Tastenkürzel' }).click();
  await expect(page.getByRole('dialog', { name: 'Tastenkürzel' })).toContainText('Strg+K');
});
