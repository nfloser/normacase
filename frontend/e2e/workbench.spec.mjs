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
 await page.getByRole('textbox',{name:/Synthetischer Wert/}).fill('123456789,1234567890123456789');
 await page.getByRole('textbox',{name:/Bereichswert/}).fill('30');
 await page.getByRole('button',{name:'Jetzt prüfen'}).click();
 await expect(page.getByRole('heading',{name:'Voraussetzungen erfüllt'})).toBeVisible();
 await page.getByText('Technische Prüfspur anzeigen').click();
 await expect(page.locator('pre')).toContainText('123456789.1234567890123456789');
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
