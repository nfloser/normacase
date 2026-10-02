import { test, expect } from '@playwright/test';
import { readFile } from 'node:fs/promises';

test('a real synthetic case evaluates, exposes source and clears stale output',async({page})=>{
 await page.goto('/');
 await page.getByRole('combobox',{name:'Prüfbereich'}).selectOption('synthetic.demo-c');
 await page.getByLabel('Beispiel auswählen').selectOption('supported');
 await page.getByRole('button',{name:'Beispiel laden'}).click();
 await expect(page.getByLabel('Prüfdatum')).toHaveValue('2026-10-02');
 await page.getByRole('button',{name:'Jetzt prüfen'}).click();
 await expect(page.getByRole('heading',{name:'Voraussetzungen erfüllt'})).toBeVisible();
 await expect(page.getByText('Quellenrevision',{exact:true})).toBeVisible();
 await page.getByText('Technische Prüfspur anzeigen').click();
 await expect(page.locator('pre')).toContainText('"assessmentDate":"2026-10-02"');
 await page.getByLabel('Messwert',{exact:true}).fill('17');
 await expect(page.getByRole('heading',{name:'Bereit für die erste Prüfung'})).toBeVisible();
});

test('missing evidence requires human review and export retains the original JSON',async({page})=>{
 await page.goto('/');
 await page.getByRole('combobox',{name:'Prüfbereich'}).selectOption('synthetic.demo-c');
 await page.getByLabel('Beispiel auswählen').selectOption('review');
 await page.getByRole('button',{name:'Beispiel laden'}).click();
 await expect(page.getByLabel('Prüfdatum')).toHaveValue('2026-10-02');
 await page.getByRole('button',{name:'Jetzt prüfen'}).click();
 await expect(page.getByRole('heading',{name:'Manuelle Prüfung erforderlich'})).toBeVisible();
 const download=page.waitForEvent('download');
 await page.getByRole('button',{name:'Ergebnis als JSON herunterladen'}).click();
 const file = await download;
 expect(file.suggestedFilename()).toBe('normacase-assessment.json');
 await page.getByText('Technische Prüfspur anzeigen').click();
 expect(await readFile(await file.path(),'utf8')).toBe(await page.locator('pre').textContent());
 await page.screenshot({path:'test-results/workbench-desktop.png',fullPage:true});
});

test('incomplete inputs remain unknown and the page fits a narrow viewport',async({page})=>{
 await page.setViewportSize({width:390,height:844});
 await page.goto('/');
 await page.getByLabel('Beispiel auswählen').selectOption('incomplete');
 await page.getByRole('button',{name:'Beispiel laden'}).click();
 await expect(page.getByLabel('Prüfdatum')).toHaveValue('2026-10-02');
 await page.getByRole('button',{name:'Jetzt prüfen'}).click();
 await expect(page.getByRole('heading',{name:'Angaben unvollständig'})).toBeVisible();
 expect(await page.evaluate(()=>document.documentElement.scrollWidth<=window.innerWidth)).toBe(true);
 await page.screenshot({path:'test-results/workbench-mobile.png',fullPage:true});
});

test('the browser preserves decimal precision all the way into the trace',async({page})=>{
 await page.goto('/');
 await page.getByRole('combobox',{name:'Prüfbereich'}).selectOption('synthetic.demo-b');
 await page.getByLabel('Prüfdatum').fill('2026-10-02');
 await page.getByRole('combobox',{name:'Synthetischer Wert – Eingabestatus'}).selectOption('VALUE');
 await page.getByRole('combobox',{name:'Bereichswert – Eingabestatus'}).selectOption('VALUE');
 await page.getByRole('textbox',{name:/Synthetischer Wert/}).fill('123456789,1234567890123456789');
 await page.getByRole('textbox',{name:/Bereichswert/}).fill('30');
 await page.getByRole('button',{name:'Jetzt prüfen'}).click();
 await expect(page.getByRole('heading',{name:'Voraussetzungen erfüllt'})).toBeVisible();
 await page.getByText('Technische Prüfspur anzeigen').click();
 await expect(page.locator('pre')).toContainText('123456789.1234567890123456789');
 await page.getByRole('combobox',{name:'Synthetischer Wert – Eingabestatus'}).selectOption('UNKNOWN');
 await expect(page.getByRole('textbox',{name:/Synthetischer Wert/})).toHaveCount(0);
 await expect(page.getByRole('heading',{name:'Bereit für die erste Prüfung'})).toBeVisible();
 await page.getByRole('button',{name:'Jetzt prüfen'}).click();
 await expect(page.getByRole('heading',{name:'Angaben unvollständig'})).toBeVisible();
});

test('independent outputs keep UNKNOWN and the external pending state distinct',async({page})=>{
 await page.goto('/');
 await page.getByRole('combobox',{name:'Prüfbereich'}).selectOption('synthetic.demo-e');
 await page.getByLabel('Beispiel auswählen').selectOption('mixed');
 await page.getByRole('button',{name:'Beispiel laden'}).click();
 await expect(page.getByLabel('Prüfdatum')).toHaveValue('2026-10-02');
 await page.getByRole('button',{name:'Jetzt prüfen'}).click();
 await expect(page.getByRole('heading',{name:'Einzelne Fachausgaben'})).toBeVisible();
 await expect(page.locator('.domain-outputs')).toContainText('Extern ausstehend');
 await expect(page.locator('.domain-outputs')).toContainText('Unbekannt');
});

test('snapshot download and uploaded replay retain exact original JSON',async({page})=>{
 await page.goto('/');
 await page.getByRole('combobox',{name:'Prüfbereich'}).selectOption('synthetic.demo-b');
 await page.getByLabel('Prüfdatum',{exact:true}).fill('2026-10-02');
 await page.getByRole('combobox',{name:'Synthetischer Wert – Eingabestatus'}).selectOption('VALUE');
 await page.getByRole('combobox',{name:'Bereichswert – Eingabestatus'}).selectOption('VALUE');
 await page.getByRole('textbox',{name:/Synthetischer Wert/}).fill('123456789,1234567890123456789');
 await page.getByRole('textbox',{name:/Bereichswert/}).fill('30');
 await page.getByRole('button',{name:'Jetzt prüfen'}).click();
 await expect(page.getByRole('heading',{name:'Voraussetzungen erfüllt',exact:true})).toBeVisible();
 const downloading=page.waitForEvent('download');
 await page.getByRole('button',{name:'Prüfsnapshot herunterladen',exact:true}).click();
 const download=await downloading;
 expect(download.suggestedFilename()).toBe('normacase-snapshot.json');
 const original=await readFile(await download.path(),'utf8');
 expect(original).toContain('123456789.1234567890123456789');
 await page.getByLabel('Prüfsnapshot auswählen').setInputFiles({name:'snapshot.json',mimeType:'application/json',buffer:Buffer.from(original)});
 await expect(page.getByRole('heading',{name:'Offline-Wiederholung bestätigt',exact:true})).toBeVisible();
 const verified=page.waitForEvent('download');
 await page.getByRole('button',{name:'Bestätigtes Ergebnis herunterladen',exact:true}).click();
 const result=await verified;
 expect(await readFile(await result.path(),'utf8')).toContain('123456789.1234567890123456789');
 await page.screenshot({path:'test-results/workbench-snapshot-replay.png',fullPage:true});
 const altered=JSON.parse(original);altered.contentSha256='0'.repeat(64);
 await page.getByLabel('Prüfsnapshot auswählen').setInputFiles({name:'altered.json',mimeType:'application/json',buffer:Buffer.from(JSON.stringify(altered))});
 await expect(page.getByRole('heading',{name:'Offline-Wiederholung bestätigt',exact:true})).toHaveCount(0);
 await expect(page.getByRole('alert')).toContainText('ungültig');
});

test('input changes abort an uploaded snapshot and cannot restore stale confirmation',async({page})=>{
 await page.goto('/');
 await expect(page.getByRole('combobox',{name:'Prüfbereich'})).toHaveValue('synthetic.demo-a');
 const input=await readFile('../examples/cases/demo-a-supported.json','utf8');
 const response=await page.request.post('/api/snapshots/synthetic.demo-a',{headers:{'Content-Type':'application/json'},data:input});
 expect(response.ok()).toBe(true);
 const captured=await response.json();
 let release,started,completed;
 const gate=new Promise(resolve=>{release=resolve;});
 const observed=new Promise(resolve=>{started=resolve;});
 const finished=new Promise(resolve=>{completed=resolve;});
 await page.route('**/api/snapshots/replay',async route=>{
   started();await gate;
   try {await route.fulfill({status:200,contentType:'application/json',body:JSON.stringify({assessmentJson:captured.assessmentJson})});}
   catch { /* The browser may already have cancelled the request. */ }
   finally {completed();}
 });
 await page.getByLabel('Prüfsnapshot auswählen').setInputFiles({name:'snapshot.json',mimeType:'application/json',buffer:Buffer.from(captured.snapshotJson)});
 await observed;
 const aborted=page.waitForEvent('requestfailed',request=>request.url().endsWith('/api/snapshots/replay'));
 await page.getByLabel('Prüfdatum',{exact:true}).fill('2026-10-03');
 release();
 await Promise.all([aborted,finished]);
 await expect(page.getByRole('heading',{name:'Offline-Wiederholung bestätigt',exact:true})).toHaveCount(0);
 await expect(page.getByRole('button',{name:'Bestätigtes Ergebnis herunterladen',exact:true})).toHaveCount(0);
 await expect(page.getByRole('alert')).toHaveCount(0);
});

test('oversized snapshot files are rejected before sending a request',async({page})=>{
 await page.goto('/');
 let sent=0;
 page.on('request',request=>{if(request.url().endsWith('/api/snapshots/replay'))sent++;});
 await page.getByLabel('Prüfsnapshot auswählen').setInputFiles({name:'large.json',mimeType:'application/json',buffer:Buffer.alloc(1024*1024+1,32)});
 await expect(page.getByRole('alert')).toContainText('1 MiB');
 expect(sent).toBe(0);
});
