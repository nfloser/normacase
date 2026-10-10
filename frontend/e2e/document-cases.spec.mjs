import {test,expect} from '@playwright/test';

test('case file opens PDF, scan and text with source links and keeps cases isolated',async({page})=>{
 await page.goto('/');
 const queues=page.getByRole('region',{name:'Fallwarteschlangen'});
 await queues.getByRole('button',{name:'Fall öffnen: demo-g-supported',exact:true}).click();
 const file=queues.getByRole('region',{name:'Dokumentakte'});
 await expect(file.getByRole('heading',{name:'Prüfauftrag und Anlagenübersicht',exact:true})).toBeVisible();
 await expect(file.locator('iframe')).toHaveAttribute('src',/demo-g-supported\/documents\/document-1#page=1$/);
 const downloadPromise=page.waitForEvent('download');await file.getByRole('link',{name:'Herunterladen'}).click();
 expect((await downloadPromise).suggestedFilename()).toBe('demo-g-supported-document-1.pdf');
 await file.getByLabel('Dokument auswählen').selectOption({label:'Scan-Anlage (ohne automatische Texterkennung)'});
 await expect(file.getByRole('img')).toBeVisible();
 await file.getByRole('button',{name:'Scan vergrößern'}).click();await expect(file.locator('.scan-preview')).toHaveClass(/zoomed/);
 await file.getByLabel('Dokument auswählen').selectOption({label:'Textübermittlung der Test-PDFs'});
 await expect(file.locator('.document-text')).toContainText('NCF1');
 await queues.getByRole('button',{name:'Fall öffnen: demo-g-review',exact:true}).click();
 await expect(file.getByText(/Ein erforderlicher Nachweis fehlt/)).toBeVisible();
 await expect(file.locator('option').filter({hasText:'Bestätigung'})).toHaveCount(0);
});

test('reference documents expose conflicts, German provenance and actual engine outcome on mobile',async({page})=>{
 await page.goto('/#reference-cases');const references=page.getByRole('region',{name:'Dokumentfälle nach öffentlichen Grundlagen'});
 await references.getByRole('button',{name:'Krankenfahrt: widersprüchliche Nachweise',exact:true}).click();
 await expect(references.getByText(/Widersprüchliche Angaben: Pflegegrad/)).toBeVisible();
 await expect(references.getByRole('heading',{name:'Prüfergebnis: Manuelle Prüfung erforderlich',exact:true})).toBeVisible();
 await references.getByText('Angaben und Fundstellen',{exact:true}).click();
 await expect(references.locator('.document-observations').getByText('Ambulante Behandlung',{exact:true})).toBeVisible();
 await references.getByRole('button',{name:/Angaben zur Verordnung, Seite 2/}).first().click();
 await expect(references.locator('iframe')).toHaveAttribute('src',/#page=2$/);
 await references.screenshot({path:'test-results/document-cases-desktop.png'});
 await page.setViewportSize({width:390,height:844});
 expect(await page.evaluate(()=>document.documentElement.scrollWidth<=window.innerWidth)).toBeTruthy();
 await references.screenshot({path:'test-results/document-cases-mobile.png'});
 await references.getByRole('button',{name:'Pflege-Score: Modulsumme fehlt',exact:true}).click();
 await expect(references.getByText(/Fehlende Angabe: Modul 4/)).toBeVisible();
});
