import {test,expect} from '@playwright/test';

test('case overview opens a result-first detail with explicit documents and a return path',async({page})=>{
 await page.setViewportSize({width:1280,height:720});
 await page.goto('/#reference-cases');
 const cases=page.getByRole('region',{name:'Dokumentfälle nach öffentlichen Grundlagen'});
 await expect(cases.getByRole('button',{name:'Krankenfahrt: vollständig',exact:true})).toBeVisible();
 await cases.getByRole('button',{name:'Krankenfahrt: vollständig',exact:true}).click();
 await expect(cases.getByRole('heading',{name:'Prüfergebnis: Voraussetzungen erfüllt',exact:true})).toBeVisible();
 await expect(cases.locator('iframe')).toHaveCount(0);
 await expect(cases.getByRole('button',{name:'Pflege-Score: vollständig',exact:true})).toHaveCount(0);
 await page.screenshot({path:'test-results/case-result-laptop.png'});
 await cases.getByRole('button',{name:'Dokumente',exact:true}).click();
 await expect(cases.locator('.pdf-page-image')).toBeVisible();
 await expect(cases.locator('.document-case-summary')).toContainText('Voraussetzungen erfüllt');
 const bounds=await cases.locator('.pdf-page-image').boundingBox();
 expect(bounds.width).toBeLessThanOrEqual(560);expect(bounds.height).toBeLessThan(500);
 await expect(cases.locator('.pdf-page-image')).toHaveAttribute('src',/documents\/document-1\/pages\/1$/);
 await cases.getByRole('button',{name:'Nächste Seite',exact:true}).click();
 await expect(cases.locator('.pdf-page-image')).toHaveAttribute('src',/pages\/2$/);
 await cases.getByRole('button',{name:'Vergrößern',exact:true}).click();
 await expect(cases.locator('.page-stage')).toHaveClass(/zoomed/);
 await cases.getByRole('button',{name:'Ganze Seite',exact:true}).click();
 await expect(cases.locator('.page-stage')).not.toHaveClass(/zoomed/);
 expect(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth&&document.documentElement.scrollHeight<=innerHeight)).toBeTruthy();
 await page.screenshot({path:'test-results/case-documents-laptop.png'});
 await cases.getByRole('button',{name:'Zur Fallübersicht',exact:true}).click();
 await expect(cases.getByRole('button',{name:'Krankenfahrt: vollständig',exact:true})).toBeFocused();
 await expect(cases.locator('iframe')).toHaveCount(0);
 await page.setViewportSize({width:390,height:844});
 expect(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth)).toBeTruthy();
});

test('workspace routes and keyboard navigation expose only the current view',async({page})=>{
 await page.goto('/#workbench');
 await expect(page.getByLabel('Prüfbereich',{exact:true})).toBeVisible();
 const nav=page.getByRole('navigation',{name:'Arbeitsbereiche'});
 await nav.getByRole('link',{name:'Arbeitslisten',exact:true}).focus();await page.keyboard.press('Enter');
 await expect(page.getByRole('region',{name:'Fallverwaltung'})).toBeVisible();
 await expect(page.getByLabel('Prüfbereich',{exact:true})).toBeHidden();
});

test('document service failure retains the independent recorded assessment',async({page})=>{
 await page.route('**/api/document-cases/demo-g-supported',route=>route.fulfill({status:503,body:''}));
 await page.goto('/#volume-cases');
 const queues=page.getByRole('region',{name:'Fallwarteschlangen'});
 await queues.getByRole('button',{name:'Fall öffnen: demo-g-supported',exact:true}).click();
 await expect(queues.getByRole('alert')).toBeVisible();
 await expect(queues.getByRole('heading',{name:'Prüfergebnis: Voraussetzungen erfüllt',exact:true})).toBeVisible();
});

test('concrete transport context is retained and unrelated missing evidence is not a clarification',async({page})=>{
 await page.goto('/#reference-cases');
 const cases=page.getByRole('region',{name:'Dokumentfälle nach öffentlichen Grundlagen'});
 await cases.getByRole('button',{name:'Krankenfahrt: vollständig',exact:true}).click();
 await expect(cases.getByText('Krankenfahrt mit Taxi oder Mietwagen zur ambulanten Behandlung',{exact:true})).toBeVisible();
 await expect(cases.getByText('Was muss geklärt werden?',{exact:true})).toHaveCount(0);
 await cases.getByText('Fallhintergrund ansehen',{exact:true}).click();
 await expect(cases.getByText(/Synthetische Testperson K-01, 78 Jahre/)).toBeVisible();
 await cases.getByRole('button',{name:'Dokumente',exact:true}).click();
 const content=await (await page.request.get('/api/document-cases/reference-transport-complete/documents/transmission')).text();
 expect(content).toContain('Synthetische Testperson K-01, 78 Jahre');
});

test('care reference exposes its knowledge-defined score outputs directly',async({page})=>{
 await page.goto('/#reference-cases');
 const cases=page.getByRole('region',{name:'Dokumentfälle nach öffentlichen Grundlagen'});
 await cases.getByRole('button',{name:'Pflege-Score: vollständig',exact:true}).click();
 const outputs=cases.locator('.case-domain-results');
 await expect(outputs.getByText('Score-Schwelle 12,5',{exact:true})).toBeVisible();
 await expect(outputs.locator('dd').first()).toContainText('Erreicht');
 await expect(cases.locator('iframe')).toHaveCount(0);
});

test('extras navigation keeps the primary workplace compact and opens reference files',async({page})=>{
 await page.goto('/#work-queues');
 const nav=page.getByRole('navigation',{name:'Arbeitsbereiche'});
 await expect(nav.getByRole('link',{name:'Arbeitslisten',exact:true})).toBeVisible();
 await expect(nav.getByRole('link',{name:'Referenzfälle',exact:true})).toBeHidden();
 await nav.getByText('Extras').click();
 await nav.getByRole('link',{name:'Referenzfälle',exact:true}).click();
 await expect(page.getByRole('region',{name:'Dokumentfälle nach öffentlichen Grundlagen'})).toBeVisible();
});
