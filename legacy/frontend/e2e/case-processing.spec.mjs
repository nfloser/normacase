import {test,expect} from '@playwright/test';

test('case file explains confirmation and local dispatch with the existing commands',async({page})=>{
 await page.goto('/#work-queues');
 const explorer=page.getByRole('region',{name:'Fallverwaltung'});
 await page.getByLabel('Fälle suchen').fill('reference-transport-complete');
 await explorer.locator('tbody tr').dblclick();
 const actions=explorer.locator('.opened-case').getByRole('region',{name:'Nächster Bearbeitungsschritt'});
 await expect(actions).toContainText('Prüfergebnis und Belege prüfen');
 await expect(actions.getByRole('button',{name:'Versandpaket erstellen'})).toBeDisabled();
 await actions.getByRole('button',{name:'Fall bestätigen',exact:true}).click();
 await expect(page.getByRole('dialog')).toContainText('Nach Bestätigen erscheint der Fall');
 await page.getByRole('dialog').getByRole('button',{name:'Bestätigen',exact:true}).click();
 await expect(actions).toContainText('Der Fall ist bestätigt');
 await expect(actions.getByRole('button',{name:'Fall bestätigen',exact:true})).toBeDisabled();
 const download=page.waitForEvent('download');
 await actions.getByRole('button',{name:'Versandpaket erstellen'}).click();
 await page.getByRole('dialog').getByRole('button',{name:'Versand simulieren'}).click();
 expect((await download).suggestedFilename()).toMatch(/normacase-versand.*json/);
 await expect(actions).toContainText('Versandpaket lokal erstellt');
 await expect(actions.getByRole('button',{name:'Versandpaket erstellen'})).toBeDisabled();
 await explorer.getByRole('button',{name:'← Liste',exact:true}).click();
 await expect(explorer.locator('tbody tr')).toContainText('Versand simuliert');
 await expect(explorer.locator('tbody tr')).toContainText('Lokales Versandpaket erstellt');
});

test('incomplete file explains the blocked action instead of allowing a status move',async({page})=>{
 await page.goto('/#work-queues');const explorer=page.getByRole('region',{name:'Fallverwaltung'});
 await page.getByLabel('Fälle suchen').fill('reference-transport-missing');
 await explorer.locator('tbody tr').dblclick();
 const actions=explorer.locator('.opened-case').getByRole('region',{name:'Nächster Bearbeitungsschritt'});
 await expect(actions).toContainText('Bestätigung gesperrt');
 await expect(actions.getByRole('button',{name:'Fall bestätigen',exact:true})).toBeDisabled();
 await expect(actions.getByRole('button',{name:'Versandpaket erstellen'})).toBeDisabled();
 await explorer.locator('.opened-case').getByRole('button',{name:'Dokumente',exact:true}).click();
 await expect(explorer.locator('.case-file-list')).toBeVisible();
 await actions.getByRole('button',{name:'Ergebnis und offene Punkte ansehen'}).click();
 await expect(explorer.getByRole('heading',{name:'Prüfergebnis: Angaben unvollständig',exact:true})).toBeVisible();
});

for(const width of [1366,1920])test(`mark position and evidence columns align at ${width}`,async({page})=>{
 await page.setViewportSize({width,height:1080});await page.goto('/#work-queues');
 const explorer=page.getByRole('region',{name:'Fallverwaltung'});
 await page.getByLabel('Fälle suchen').fill('reference-transport-conflicting');
 const row=explorer.locator('tbody tr');await row.click({button:'right'});
 await expect(page.getByRole('menuitem',{name:'Verschieben',exact:true})).toHaveCount(0);
 await expect(page.getByRole('menuitem',{name:/Verschieben nach/})).toHaveCount(1);
 await page.getByRole('menuitem',{name:/Markieren/}).hover();
 await page.getByRole('group',{name:'Farbmarkierung'}).getByRole('button',{name:'Violett'}).click();
 await expect(row.locator('td').first().locator('.personal-mark-chip')).toBeVisible();
 const box=await row.getByRole('checkbox').boundingBox(),mark=await row.locator('.personal-mark-chip').boundingBox();
 expect(mark.x).toBeGreaterThan(box.x+box.width);expect(mark.x-box.x-box.width).toBeLessThan(15);
 await row.dblclick();
 const tables=explorer.locator('.document-observations table');await expect(tables.first()).toBeVisible();
 for(const table of await tables.all()){
  const heading=await table.locator('th').nth(1).boundingBox();
  const value=await table.locator('tbody tr').first().locator('td').nth(1).boundingBox();
  expect(Math.abs(heading.x-value.x)).toBeLessThan(1);
  await expect(table.locator('th').nth(1)).toHaveCSS('text-align','left');
  await expect(table.locator('tbody tr').first().locator('td').nth(1)).toHaveCSS('text-align','left');
 }
 await page.screenshot({path:`test-results/case-processing-${width}.png`});
});
