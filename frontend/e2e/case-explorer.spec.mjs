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
 await explorer.getByRole('button',{name:'Auswahl bestätigen',exact:true}).click();
 await page.getByRole('dialog').getByRole('button',{name:'Bestätigen',exact:true}).click();
 await expect(explorer.getByRole('table').getByText('Bestätigt',{exact:true})).toBeVisible();
 await explorer.getByRole('button',{name:'Versand vorbereiten',exact:true}).click();
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
 await row.click({button:'right'});await expect(page.getByRole('menuitem',{name:'Aus Ordner entfernen',exact:true})).toBeVisible();await page.getByRole('menuitem',{name:'Aus Ordner entfernen',exact:true}).click();
 await expect(row.locator('td').nth(4)).toHaveText('—');
});

test('compact workplace shows status color, case context and bounded list rows',async({page})=>{
 await page.setViewportSize({width:1920,height:1080});await page.goto('/#work-queues');
 const explorer=page.getByRole('region',{name:'Fallverwaltung'});
 await expect(explorer.getByText('Kein Fall ausgewählt')).toBeVisible();
 const row=explorer.getByRole('row').filter({hasText:'reference-md-mueller'});
 await row.click();
 await expect(explorer.getByText(/Fall: reference-md-mueller/)).toBeVisible();
 await expect(row).toHaveAttribute('data-status',/approval|clarification|review|technical|documents|complete/);
 const table=explorer.locator('.explorer-table');
 await expect(table).toHaveClass(/status-row-colors/);
 await explorer.getByLabel('Zeilenfärbung').uncheck();
 await expect(table).not.toHaveClass(/status-row-colors/);
 await explorer.getByLabel('Zeilenfärbung').check();
 await expect(table).toHaveClass(/status-row-colors/);
 await expect(explorer.locator('.explorer-table tbody tr').first()).toHaveCSS('height','28px');
});

test('row selection and hover color command work without opening the file',async({page})=>{
 await page.goto('/#work-queues');
 const explorer=page.getByRole('region',{name:'Fallverwaltung'});
 await explorer.getByLabel('Fälle suchen').fill('reference-md-mueller');
 const row=explorer.getByRole('row').filter({hasText:'reference-md-mueller'});
 await row.locator('td').nth(2).click();
 await expect(row).toHaveAttribute('aria-selected','true');
 await row.click({button:'right'});
 await page.getByRole('menuitem',{name:/Markieren/}).hover();
 await page.getByRole('group',{name:'Farbmarkierung'}).getByRole('button',{name:'Blau'}).click();
 await expect(row.locator('.personal-mark-chip')).toBeVisible();
 await row.dblclick();
 await expect(explorer.getByRole('button',{name:'Verschieben nach …'})).toBeVisible();
 await explorer.getByRole('button',{name:'Markieren',exact:true}).click();
 await expect(page.getByRole('dialog')).toBeVisible();
});

test('personal folders can contain nested folders without changing case decisions',async({page})=>{
 await page.goto('/#work-queues');
 const explorer=page.getByRole('region',{name:'Fallverwaltung'});
 await explorer.getByRole('button',{name:'Neuer Ordner',exact:true}).click();
 const dialog=page.getByRole('dialog');
 await dialog.getByLabel('Ordnername').fill('Arbeitsmappe');
 await dialog.getByRole('button',{name:'Ordner speichern'}).click();
 await explorer.locator('.custom-folder').filter({hasText:'Arbeitsmappe'}).hover();
 await explorer.getByRole('button',{name:'Unterordner in Arbeitsmappe anlegen'}).click();
 await dialog.getByLabel('Ordnername').fill('Heute prüfen');
 await dialog.getByRole('button',{name:'Ordner speichern'}).click();
 await expect(explorer.locator('.custom-folder > .tree-filter').filter({hasText:'Heute prüfen'})).toBeVisible();
});

test('selected case displays documents below the list and F4 toggles the resizable pane',async({page})=>{
 await page.setViewportSize({width:1440,height:900});
 await page.goto('/#work-queues');
 const explorer=page.getByRole('region',{name:'Fallverwaltung'});
 const row=explorer.getByRole('row').filter({hasText:'reference-md-mueller'});
 const pane=explorer.getByRole('region',{name:'Fallakte und Dokumente'});
 await expect(pane.getByText(/Fall in der Tabelle auswählen/)).toBeVisible();
 await row.locator('td').nth(2).click();
 await expect(row).toHaveAttribute('aria-selected','true');
 await expect(row).toBeVisible();
 await expect(pane.locator('.case-file-list')).toBeVisible();
 await expect(pane.locator('.pdf-page-image')).toBeVisible();
 const splitter=explorer.getByRole('separator',{name:/Fallliste und Detailbereich/});
 await expect(splitter).toHaveAttribute('aria-valuenow','45');
 await splitter.focus();await page.keyboard.press('ArrowUp');
 await expect(splitter).toHaveAttribute('aria-valuenow','50');
 await page.keyboard.press('F4');
 await expect(pane).toHaveCount(0);
 await page.keyboard.press('F4');
 await expect(pane).toBeVisible();
 await expect(row).toBeVisible();
 await row.dblclick();
 await expect(explorer.locator('.opened-case')).toBeVisible();
});

test('coarse-pointer submenu expands inline and applies a personal folder color',async({browser})=>{
 const context=await browser.newContext({hasTouch:true,isMobile:true,viewport:{width:900,height:850}});
 try{
  const page=await context.newPage();await page.goto('/#work-queues');
  const explorer=page.getByRole('region',{name:'Fallverwaltung'});
  await explorer.getByLabel('Fälle suchen').fill('reference-md-mueller');
  const row=explorer.getByRole('row').filter({hasText:'reference-md-mueller'});
  await row.getByRole('button',{name:'Fallaktionen',exact:true}).click();
  const submenu=page.getByRole('menuitem',{name:/Markieren/});
  await expect(submenu).toHaveAttribute('aria-expanded','false');
  await submenu.click();
  await expect(submenu).toHaveAttribute('aria-expanded','true');
  const group=page.getByRole('group',{name:'Farbmarkierung'});
  await expect(group.getByLabel('Eigene Markierungsfarbe')).toBeVisible();
  await group.getByRole('button',{name:'Grün',exact:true}).click();
  await expect(row.locator('.personal-mark-chip')).toBeVisible();
 }finally{await context.close();}
});

test('layout settings survive reload without storing case identifiers',async({page})=>{
 await page.goto('/#work-queues');
 const explorer=page.getByRole('region',{name:'Fallverwaltung'});
 const row=explorer.getByRole('row').filter({hasText:'reference-md-mueller'});
 await row.locator('td').nth(2).click();
 const splitter=explorer.getByRole('separator',{name:/Fallliste und Detailbereich/});
 await splitter.focus();await page.keyboard.press('ArrowUp');
 await expect(splitter).toHaveAttribute('aria-valuenow','50');
 await explorer.getByLabel('Zeilenfärbung').uncheck();
 await page.keyboard.press('F4');
 await expect(explorer.getByRole('region',{name:'Fallakte und Dokumente'})).toHaveCount(0);
 const raw=await page.evaluate(()=>localStorage.getItem('normacase.ui.layout.v1'));
 expect(raw).toContain('"detailPercent":50');
 expect(raw).not.toContain('reference-md-mueller');
 await page.reload();
 await explorer.getByRole('button',{name:'Details anzeigen (F4)'}).click();
 await expect(splitter).toHaveAttribute('aria-valuenow','50');
 await expect(explorer.getByLabel('Zeilenfärbung')).not.toBeChecked();
 await page.evaluate(()=>localStorage.setItem('normacase.ui.layout.v1','{"detailPercent":"corrupt"}'));
 await page.reload();
 await expect(splitter).toHaveAttribute('aria-valuenow','45');
});
