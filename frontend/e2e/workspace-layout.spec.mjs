import {test,expect} from '@playwright/test';

test('case overview opens a result-first detail with explicit documents and a return path',async({page})=>{
 await page.setViewportSize({width:1280,height:720});
 await page.goto('/');
 const cases=page.getByRole('region',{name:'Dokumentfälle nach öffentlichen Grundlagen'});
 await expect(cases.getByRole('button',{name:'Krankenfahrt: vollständig',exact:true})).toBeVisible();
 await cases.getByRole('button',{name:'Krankenfahrt: vollständig',exact:true}).click();
 await expect(cases.getByRole('heading',{name:'Prüfergebnis: Voraussetzungen erfüllt',exact:true})).toBeVisible();
 await expect(cases.locator('iframe')).toHaveCount(0);
 await expect(cases.getByRole('button',{name:'Pflege-Score: vollständig',exact:true})).toHaveCount(0);
 await page.screenshot({path:'test-results/case-result-laptop.png'});
 await cases.getByRole('button',{name:'Dokumente',exact:true}).click();
 await expect(cases.locator('iframe')).toBeVisible();
 await expect(cases.locator('iframe')).toHaveAttribute('src',/#page=1&view=FitH&navpanes=0$/);
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
 await expect(page.getByRole('region',{name:'Fallwarteschlangen'})).toBeVisible();
 await expect(page.getByLabel('Prüfbereich',{exact:true})).toBeHidden();
});

test('document service failure retains the independent recorded assessment',async({page})=>{
 await page.route('**/api/document-cases/demo-g-supported',route=>route.fulfill({status:503,body:''}));
 await page.goto('/#work-queues');
 const queues=page.getByRole('region',{name:'Fallwarteschlangen'});
 await queues.getByRole('button',{name:'Fall öffnen: demo-g-supported',exact:true}).click();
 await expect(queues.getByRole('alert')).toBeVisible();
 await expect(queues.getByRole('heading',{name:'Prüfergebnis: Voraussetzungen erfüllt',exact:true})).toBeVisible();
});

test('concrete transport context is retained and unrelated missing evidence is not a clarification',async({page})=>{
 await page.goto('/');
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
