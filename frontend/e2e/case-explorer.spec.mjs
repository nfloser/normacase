import {test,expect} from '@playwright/test';

test('explorer organizes colored folders and bookmarks without changing the result',async({page})=>{
 await page.setViewportSize({width:1440,height:900});await page.goto('/#work-queues');
 const explorer=page.getByRole('region',{name:'Fallverwaltung'});
 await explorer.getByLabel('Fälle suchen').fill('reference-md-mueller');
 const row=explorer.getByRole('row').filter({hasText:'reference-md-mueller'});
 await row.getByRole('checkbox').check();await row.getByRole('button',{name:'Fallaktionen',exact:true}).click();
 await page.getByRole('menuitem',{name:'Lesezeichen setzen',exact:true}).click();
 await explorer.getByRole('button',{name:'Neuer Ordner',exact:true}).click();
 const dialog=page.getByRole('dialog');await dialog.getByLabel('Ordnername').fill('Für die Besprechung');
 await dialog.getByLabel('Ordnerfarbe').evaluate(el=>{el.value='#a855f7';el.dispatchEvent(new Event('change',{bubbles:true}));});await dialog.getByRole('button',{name:'Ordner speichern'}).click();
 await explorer.getByRole('button',{name:'Auswahl verschieben',exact:true}).click();
 await page.getByRole('dialog').getByLabel('Zielordner').selectOption({label:'Für die Besprechung'});
 await page.getByRole('dialog').getByRole('button',{name:'Verschieben',exact:true}).click();
 await expect(row).toContainText('Für die Besprechung');
 await row.click({button:'right'});await page.getByRole('menuitem',{name:'Fall öffnen',exact:true}).click();
 await expect(explorer.getByRole('heading',{name:'Prüfergebnis: Voraussetzungen erfüllt',exact:true})).toBeVisible();
 await explorer.getByRole('button',{name:'Zur Fallübersicht',exact:true}).click();
 await page.reload();await expect(explorer.getByText('Für die Besprechung').first()).toBeVisible();
 await page.screenshot({path:'test-results/medical-blue-explorer.png'});
});

test('eligible batch is confirmed and downloaded as explicit local dispatch simulation',async({page})=>{
 await page.goto('/#work-queues');const explorer=page.getByRole('region',{name:'Fallverwaltung'});
 await explorer.getByLabel('Fälle suchen').fill('reference-md-kraemer');
 await explorer.getByRole('row').filter({hasText:'reference-md-kraemer'}).getByRole('checkbox').check();
 await explorer.getByRole('button',{name:'Auswahl bestätigen (Demo)',exact:true}).click();
 await page.getByRole('dialog').getByRole('button',{name:'Bestätigen',exact:true}).click();
 await expect(explorer.getByRole('table').getByText('Bestätigt (Demo)',{exact:true})).toBeVisible();
 await explorer.getByRole('button',{name:'Auswahl abschicken (Demo)',exact:true}).click();
 const download=page.waitForEvent('download');await page.getByRole('dialog').getByRole('button',{name:'Versand simulieren',exact:true}).click();
 expect((await download).suggestedFilename()).toMatch(/normacase-versand.*json/);
 await expect(explorer.getByRole('table').getByText('Versand simuliert',{exact:true})).toBeVisible();
});

test('keyboard menu returns focus and removes only the folder assignment',async({page})=>{
 await page.goto('/#work-queues');const explorer=page.getByRole('region',{name:'Fallverwaltung'});
 await explorer.getByLabel('Fälle suchen').fill('reference-md-mueller');
 const row=explorer.getByRole('row').filter({hasText:'reference-md-mueller'});
 await row.focus();await row.press('Shift+F10');
 await expect(page.getByRole('menuitem',{name:'Fall öffnen',exact:true})).toBeFocused();
 await page.keyboard.press('End');await page.keyboard.press('Escape');await expect(row).toBeFocused();
 await explorer.getByRole('button',{name:'Neuer Ordner',exact:true}).click();
 await page.getByRole('dialog').getByLabel('Ordnername').fill('Tastaturablage');
 await page.getByRole('button',{name:'Blau',exact:true}).click();
 await page.getByRole('dialog').getByRole('button',{name:'Ordner speichern'}).click();
 await row.getByRole('checkbox').check();await explorer.getByRole('button',{name:'Auswahl verschieben',exact:true}).click();
 await page.getByRole('dialog').getByLabel('Zielordner').selectOption({label:'Tastaturablage'});
 await page.getByRole('dialog').getByRole('button',{name:'Verschieben',exact:true}).click();
 await row.press('Shift+F10');await page.getByRole('menuitem',{name:'Aus Ordner entfernen',exact:true}).click();
 await expect(row).toContainText('Ohne eigenen Ordner');
});

test('personal colored folder expands into case result and document nodes',async({page})=>{
 await page.goto('/#work-queues');
 const explorer=page.getByRole('region',{name:'Fallverwaltung'});
 await explorer.getByLabel('Fälle suchen').fill('reference-md-mueller');
 const row=explorer.getByRole('row').filter({hasText:'reference-md-mueller'});
 await row.getByRole('checkbox').check();
 await explorer.getByRole('button',{name:'Neuer Ordner',exact:true}).click();
 await page.getByRole('dialog').getByLabel('Ordnername').fill('Meine Prüffälle');
 await page.getByRole('dialog').getByRole('button',{name:'Ordner speichern'}).click();
 await explorer.getByRole('button',{name:'Auswahl verschieben',exact:true}).click();
 await page.getByRole('dialog').getByLabel('Zielordner').selectOption({label:'Meine Prüffälle'});
 await page.getByRole('dialog').getByRole('button',{name:'Verschieben',exact:true}).click();
 const folder=explorer.locator('.personal-folder-tree').filter({has:page.locator('summary', {hasText:'Meine Prüffälle'})});
 await folder.locator(':scope > summary').click();
 await expect(folder.getByText('reference-md-mueller')).toBeVisible();
 const caseNode=folder.locator('li > details').filter({has:page.locator('summary',{hasText:'reference-md-mueller'})}).first();
 await caseNode.locator(':scope > summary').click();
 await expect(caseNode.getByRole('button',{name:'Dokumente',exact:true})).toBeVisible();
 await caseNode.getByRole('button',{name:'Dokumente',exact:true}).click();
 await expect(explorer.getByRole('button',{name:'Zur Fallübersicht',exact:true})).toBeVisible();
});
