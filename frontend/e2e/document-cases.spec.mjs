import {test,expect} from '@playwright/test';

test('case file opens PDF, scan and text with source links and keeps cases isolated',async({page})=>{
 await page.goto('/#volume-cases');
 const queues=page.getByRole('region',{name:'Fallwarteschlangen'});
 await queues.getByRole('button',{name:'Fall öffnen: demo-g-supported',exact:true}).click();
 await queues.getByRole('button',{name:'Dokumente',exact:true}).click();
 const file=queues.getByRole('region',{name:'Dokumentakte'});
 await expect(file.getByLabel('Dokument auswählen')).toHaveValue('document-1');
 await expect(file.locator('.pdf-page-image')).toHaveAttribute('src',/demo-g-supported\/documents\/document-1\/pages\/1$/);
 const downloadPromise=page.waitForEvent('download');await file.getByRole('link',{name:'Herunterladen'}).click();
 expect((await downloadPromise).suggestedFilename()).toBe('demo-g-supported-document-1.pdf');
 await file.getByLabel('Dokument auswählen').selectOption({label:'Scan-Anlage (ohne automatische Texterkennung)'});
 await expect(file.getByRole('img')).toBeVisible();
 await file.getByRole('button',{name:'Scan vergrößern'}).click();await expect(file.locator('.scan-preview')).toHaveClass(/zoomed/);
 await file.getByLabel('Dokument auswählen').selectOption({label:'Textübermittlung der Test-PDFs'});
 await expect(file.locator('.document-text')).toContainText('NCF1');
 await queues.getByRole('button',{name:'Zur Fallübersicht',exact:true}).click();
 await queues.getByRole('button',{name:'Fall öffnen: demo-g-review',exact:true}).click();
 await expect(file.getByText(/Ein erforderlicher Nachweis fehlt/)).toBeVisible();
 await expect(file.locator('option').filter({hasText:'Bestätigung'})).toHaveCount(0);
});

test('reference documents expose conflicts, German provenance and actual engine outcome on mobile',async({page})=>{
 await page.goto('/#reference-cases');const references=page.getByRole('region',{name:'Dokumentfälle nach öffentlichen Grundlagen'});
 await references.getByRole('button',{name:'NC-2026-4103 · Sabine Winter · Krankenfahrt: widersprüchliche Nachweise',exact:true}).click();
 await expect(references.getByText(/Widersprüchliche Angaben: Pflegegrad/)).toBeVisible();
 await expect(references.getByRole('heading',{name:'Prüfergebnis: Manuelle Prüfung erforderlich',exact:true})).toBeVisible();
 await expect(references.locator('.document-observations').getByText('Ambulante Behandlung',{exact:true})).toBeVisible();
 await references.getByRole('button',{name:/Angaben zur Verordnung, Seite 2/}).first().click();
 await expect(references.locator('.pdf-page-image')).toHaveAttribute('src',/pages\/2$/);
 await references.screenshot({path:'test-results/document-cases-desktop.png'});
 await page.setViewportSize({width:390,height:844});
 expect(await page.evaluate(()=>document.documentElement.scrollWidth<=window.innerWidth)).toBeTruthy();
 await references.screenshot({path:'test-results/document-cases-mobile.png'});
 await references.getByRole('button',{name:'Zur Fallübersicht',exact:true}).click();
 await references.getByRole('button',{name:'NC-2026-4106 · Karl Neumann · Pflege-Score: Modulsumme fehlt',exact:true}).click();
 await expect(references.getByText(/Fehlende Angabe: Modul 4/)).toBeVisible();
});

test('source-backed case families include clinical files without inventing decisions',async({page})=>{
 await page.goto('/#reference-cases');
 const cases=page.getByRole('region',{name:'Dokumentfälle nach öffentlichen Grundlagen'});
 await cases.getByRole('button',{name:'NC-2026-4115 · Stefan Kranz · Arbeitsunfall: Handgelenkverletzung',exact:true}).click();
 await expect(cases.getByText(/Für diesen Dokumenttyp ist keine fachlich geprüfte Regel/)).toBeVisible();
 await cases.getByRole('button',{name:'Dokumente',exact:true}).click();
 await expect(cases.locator('.pdf-page-image')).toBeVisible();
 await expect(cases.locator('.case-file-list')).toContainText('Durchgangsarztbericht');
 await cases.getByRole('button',{name:'Zur Fallübersicht',exact:true}).click();
 await cases.getByRole('button',{name:'NC-2026-4113 · Helga Brandt · Pflege: Demenz und Unterstützung im Alltag',exact:true}).click();
 await expect(cases.getByRole('heading',{name:'Prüfergebnis: Voraussetzungen erfüllt',exact:true})).toBeVisible();
});

test('clipboard result summary includes only recorded outcome and provenance',async({page})=>{
 await page.addInitScript(()=>{
  Object.defineProperty(navigator,'clipboard',{configurable:true,value:{writeText:async(value)=>{window.__normacaseCopied=value;}}});
 });
 await page.goto('/#reference-cases');
 const cases=page.getByRole('region',{name:'Dokumentfälle nach öffentlichen Grundlagen'});
 await cases.getByRole('button',{name:'NC-2026-4101 · Marlene Berg · Krankenfahrt: ambulante Behandlung',exact:true}).click();
 const file=cases.getByRole('region',{name:'Dokumentakte'});
 await file.getByRole('button',{name:'Ergebnistext kopieren'}).click();
 const summary=await page.evaluate(()=>window.__normacaseCopied);
 expect(summary).toContain('Prüfergebnis: Voraussetzungen erfüllt');
 expect(summary).toContain('Wissensstand:');
 expect(summary).toContain('Prüfdatum:');
 expect(summary).toContain('Synthetischer Schulungsfall');
 await file.getByRole('button',{name:'Aktenzeichen kopieren'}).click();
 expect(await page.evaluate(()=>window.__normacaseCopied)).toBe('reference-transport-complete');
 await cases.getByRole('button',{name:'Zur Fallübersicht',exact:true}).click();
 await cases.getByRole('button',{name:'NC-2026-4115 · Stefan Kranz · Arbeitsunfall: Handgelenkverletzung',exact:true}).click();
 await expect(cases.getByRole('button',{name:'Ergebnistext kopieren'})).toBeDisabled();
});

test('reference rehabilitation file contains coherent reports and keeps missing medical evidence absent',async({page})=>{
 await page.goto('/#reference-cases');
 const cases=page.getByRole('region',{name:'Dokumentfälle nach öffentlichen Grundlagen'});
 await cases.getByRole('button',{name:'NC-2026-4109 · Claudia Becker · Rehabilitation - Alltagsmobilität',exact:true}).click();
 await cases.getByRole('button',{name:'Dokumente',exact:true}).click();
 const file=cases.getByRole('region',{name:'Dokumentakte'});
 const selector=file.getByLabel('Dokument auswählen');
 await expect(selector.locator('option')).toHaveCount(6);
 await selector.selectOption({label:'Ärztlicher Befundbericht zur Alltagsmobilität'});
 await expect(file.locator('.pdf-page-image')).toHaveAttribute('src',/reference-reha-complete\/documents\/document-2\/pages\/1$/);
 await selector.selectOption({label:'Lesefassung der Aktenunterlagen'});
 await expect(file.locator('.document-text')).toContainText('Claudia Becker');
 await expect(file.locator('.document-text')).toContainText('NC-2026-4109');
 await expect(file.locator('.document-text')).toContainText('200 Meter');
 await expect(file.locator('.document-text')).toContainText('Therapie- und Belastungsverlauf');
 await cases.getByRole('button',{name:'Zur Fallübersicht',exact:true}).click();
 await cases.getByRole('button',{name:'NC-2026-4111 · Martin Engel · Rehabilitation - ärztliche Unterlagen ausstehend',exact:true}).click();
 await cases.getByRole('button',{name:'Dokumente',exact:true}).click();
 await expect(selector.locator('option').filter({hasText:'Ärztlicher Befundbericht zur Alltagsmobilität'})).toHaveCount(0);
 await expect(selector.locator('option').filter({hasText:'Anforderung ärztlicher Unterlagen'})).toHaveCount(1);
});
