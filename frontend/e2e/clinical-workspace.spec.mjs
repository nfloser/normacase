import {test,expect} from '@playwright/test';
for(const viewport of [{width:1920,height:1080},{width:1366,height:768}]){
 test(`clinical workplace is compact and readable at ${viewport.width}`,async({page})=>{
  await page.setViewportSize(viewport);await page.goto('/#work-queues');
  const explorer=page.getByRole('region',{name:'Fallverwaltung'});
  await expect(explorer.locator('tbody tr')).toHaveCount(22);
  await expect(explorer.locator('.explorer-table')).not.toContainText('Score-Schwelle');
  await expect(explorer.locator('.explorer-table')).not.toContainText('Synthetischer Arbeitslistenfall');
  await expect(explorer.locator('tbody .case-identifier').first()).toContainText('NC-2026-');
  await expect(explorer.locator('thead')).not.toContainText('Eigener Ordner');
  await expect(explorer.locator('tbody button').filter({hasText:'⋯'})).toHaveCount(0);
  await expect(explorer.getByRole('button',{name:'Markieren',exact:true})).toBeDisabled();
  expect(await page.locator('.workspace-navigation').evaluate(e=>e.getBoundingClientRect().height)).toBe(32);
  expect(await explorer.locator('.compact-workplace-toolbar').evaluate(e=>e.getBoundingClientRect().height)).toBe(36);
  expect(await explorer.locator('.explorer-footer').evaluate(e=>e.getBoundingClientRect().height)).toBe(24);
  const items=explorer.locator('.tree-filter');
  await expect(items.filter({hasText:'Lesezeichen'})).toHaveCount(1);
  expect(await items.first().evaluate(e=>e.getBoundingClientRect().height)).toBe(28);
  expect(await items.filter({hasText:'Lesezeichen'}).evaluate(e=>e.getBoundingClientRect().height)).toBe(28);
  if(viewport.width===1920){const list=await explorer.locator('.explorer-table-scroll').boundingBox();expect(Math.floor((list.height-54)/28)).toBeGreaterThanOrEqual(12);}
  await page.screenshot({path:`test-results/clinical-overview-${viewport.width}.png`});
  await page.keyboard.press('Control+k');await expect(page.getByLabel('Fälle suchen')).toBeFocused();
  await page.getByLabel('Fälle suchen').fill('reference-care-complete');
  const row=explorer.locator('tbody tr');await row.locator('td').nth(2).click();await row.dblclick();
  await expect(explorer.getByRole('heading',{name:'Prüfergebnis: Voraussetzungen erfüllt',exact:true})).toBeVisible();
  await expect(explorer.locator('.case-domain-results')).toBeHidden();
  await expect(explorer.locator('.document-observations tbody tr')).toHaveCount(6);
  if(viewport.width===1920){const result=explorer.locator('.case-result-view');expect(await result.evaluate(e=>e.scrollHeight<=e.clientHeight)).toBeTruthy();}
  await page.screenshot({path:`test-results/clinical-result-${viewport.width}.png`});
  await explorer.getByText('Prüfung und Regelgrundlagen',{exact:true}).click();
  await expect(explorer.locator('.case-domain-results')).toContainText('Score-Schwelle 12,5');
  await explorer.getByText('Kriterienvergleich',{exact:true}).click();
  await expect(explorer.locator('.recorded-criteria')).toContainText('Regelversion');
  await expect(explorer.locator('.recorded-criteria table')).toContainText('≥');
  await expect(explorer.locator('.recorded-criteria table')).toContainText('12,5');
  await explorer.locator('.source-page-link').first().click();
  await expect(explorer.locator('.page-stage')).toHaveAttribute('data-zoom-mode','width');
  await expect(explorer.locator('.viewer-page')).toHaveCount(2);
  await expect(explorer.getByLabel('Seitenzahl')).toHaveValue('2');
  const image=await explorer.locator('.pdf-page-image').first().boundingBox();expect(image.width).toBeGreaterThan(600);
  await expect(explorer.getByLabel('Dokument auswählen')).toHaveCount(0);
  await page.screenshot({path:`test-results/clinical-documents-${viewport.width}.png`});
  await explorer.getByLabel('Zoommodus').selectOption('150');
  await explorer.locator('.case-file-list button').filter({hasText:'Hausärztlicher Arztbrief'}).click();
  await expect(explorer.getByLabel('Zoommodus')).toHaveValue('150');
  await explorer.locator('.page-stage').focus();await page.keyboard.press('Control+0');
  await expect(explorer.getByLabel('Zoommodus')).toHaveValue('width');
  await page.keyboard.press('Alt+ArrowLeft');await expect(explorer.locator('tbody tr')).toHaveCount(1);
  await page.getByLabel('Fälle suchen').fill('');await page.keyboard.press('F4');
  const list=await explorer.locator('.explorer-table-scroll').boundingBox();if(viewport.width===1920)expect(Math.floor((list.height-54)/28)).toBeGreaterThanOrEqual(24);
  expect(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth&&document.documentElement.scrollHeight<=innerHeight)).toBeTruthy();
 });
}
test('column filters and navigation context colors preserve clinical outcome',async({page})=>{
 await page.goto('/#work-queues');const explorer=page.getByRole('region',{name:'Fallverwaltung'});
 await explorer.getByLabel('Fallauftrag filtern').fill('Rehabilitation');await expect(explorer.locator('tbody tr')).toHaveCount(2);
 await explorer.getByRole('button',{name:'Filter zurücksetzen'}).click();await expect(explorer.locator('tbody tr')).toHaveCount(22);
 const queue=explorer.locator('.tree-filter').filter({hasText:'Unterlagensichtung'});await queue.click({button:'right'});
 await page.getByRole('menuitem',{name:'Farbe ändern …'}).click();await expect(page.getByRole('dialog')).toBeVisible();
 await page.getByRole('dialog').getByRole('button',{name:'Abbrechen'}).click();
});
