import { test, expect } from '@playwright/test';
const credential=process.env.NORMACASE_REVIEW_E2E_CREDENTIAL;
if(!credential)throw new Error('Reviewed workbench test configuration is required');
async function login(page){
 await page.goto('/');
 const region=page.getByRole('region',{name:'Persistente synthetische Fallprüfung'});
 await region.getByLabel('Lokaler Review-Schlüssel').fill(credential);
 await region.getByRole('button',{name:'Review-Modus anmelden'}).click();
 await expect(region.getByText('Angemeldet als synthetic-local:reviewer',{exact:true})).toBeVisible();
 await expect(region.getByLabel('Lokaler Review-Schlüssel')).toHaveCount(0);
 return region;
}
async function noStoredSession(page){
 expect(await page.evaluate(()=>[localStorage.length,sessionStorage.length,document.cookie])).toEqual([0,0,'']);
 expect(await page.context().cookies()).toEqual([]);
}
test('two browsers review, recover a real stale conflict and preserve immutable assessment',async({page,browser})=>{
 const region=await login(page);await noStoredSession(page);
 await expect(region.getByRole('heading',{name:'Zur Freigabe vorbereitet (2)'})).toBeVisible();
 await region.getByRole('button',{name:'Fall öffnen: demo-g-supported',exact:true}).click();
 await region.getByRole('button',{name:'Systemergebnis übernehmen',exact:true}).click();
 await expect(region.getByRole('alert')).toContainText('ausdrückliche Begründung');
 await region.getByLabel('Begründung der Review-Entscheidung').fill('Synthetische Browser-Freigabe');
 // A real forbidden API response must not replace the displayed authoritative case.
 await page.route('**/api/review/work-cases/demo-g-supported/reviews',route=>route.continue({url:route.request().url().replace('demo-g-supported','demo-g-incomplete')}));
 await region.getByRole('button',{name:'Systemergebnis übernehmen',exact:true}).click();
 await expect(region.getByRole('alert')).toContainText('nicht erlaubt');
 await expect(region.getByText('Wartet auf menschliche Freigabe',{exact:true})).toBeVisible();
 await page.unroute('**/api/review/work-cases/demo-g-supported/reviews');
 const otherContext=await browser.newContext();const other=await otherContext.newPage();
 const otherRegion=await login(other);
 await otherRegion.getByRole('button',{name:'Fall öffnen: demo-g-supported',exact:true}).click();
 await expect(otherRegion.getByText('Wartet auf menschliche Freigabe',{exact:true})).toBeVisible();
 const before=await page.request.get('/api/review/work-cases/demo-g-supported',{headers:{Authorization:'Bearer '+credential}});
 const original=(await before.json()).assessmentJson;
 await region.getByRole('button',{name:'Systemergebnis übernehmen',exact:true}).click();
 await expect(region.getByText('Review wurde persistent gespeichert.',{exact:true})).toBeVisible();
 await expect(region.getByRole('heading',{name:'Abgeschlossen (1)'})).toBeVisible();
 await otherRegion.getByLabel('Begründung der Review-Entscheidung').fill('Synthetischer konkurrierender Versuch');
 await otherRegion.getByRole('button',{name:'Systemergebnis übernehmen',exact:true}).click();
 await expect(otherRegion.getByRole('alert')).toContainText('zwischenzeitlich geändert');
 await expect(otherRegion.getByText('Freigegeben',{exact:true})).toBeVisible();
 await expect(otherRegion.getByRole('button',{name:'Systemergebnis übernehmen',exact:true})).toHaveCount(0);
 await expect(otherRegion.getByText('Synthetische Browser-Freigabe',{exact:true})).toBeVisible();
 const after=await page.request.get('/api/review/work-cases/demo-g-supported',{headers:{Authorization:'Bearer '+credential}});
 expect((await after.json()).assessmentJson).toBe(original);
 await otherContext.close();
 await region.getByRole('button',{name:'Review-Modus abmelden'}).click();
 await expect(region.getByLabel('Lokaler Review-Schlüssel')).toHaveValue('');
 await expect(region.getByText('synthetic-local:reviewer',{exact:true})).toHaveCount(0);
 await noStoredSession(page);
});
test('override requires explicit selection; unknown/incomplete cases expose no review actions',async({page})=>{
 const region=await login(page);
 for(const id of ['demo-g-incomplete','demo-g-review']){
  await region.getByRole('button',{name:'Fall öffnen: '+id,exact:true}).click();
  await expect(region.getByRole('heading',{name:'Gespeicherter Fall: '+id})).toBeVisible();
  await expect(region.getByRole('button',{name:'Systemergebnis übernehmen',exact:true})).toHaveCount(0);
  await expect(region.getByRole('button',{name:'Ergebnis übersteuern',exact:true})).toHaveCount(0);
 }
 await region.getByRole('button',{name:'Fall öffnen: demo-g-not-supported',exact:true}).click();
 await region.getByLabel('Begründung der Review-Entscheidung').fill('Synthetischer Browser-Override');
 await region.getByRole('button',{name:'Ergebnis übersteuern',exact:true}).click();
 await expect(region.getByRole('alert')).toContainText('ausdrücklich aus');
 await region.getByLabel('Abweichendes generisches Ergebnis').selectOption('SUPPORTED');
 await region.getByRole('button',{name:'Ergebnis übersteuern',exact:true}).click();
 await expect(region.getByText('Übersteuert',{exact:true})).toBeVisible();
 await expect(region.getByRole('heading',{name:'Prüfergebnis: Voraussetzungen nicht erfüllt'})).toBeVisible();
 await expect(region.getByText('Abweichendes Ergebnis: Voraussetzungen erfüllt',{exact:true})).toBeVisible();
 await page.setViewportSize({width:390,height:844});
 expect(await page.evaluate(()=>document.documentElement.scrollWidth<=window.innerWidth)).toBeTruthy();
 await noStoredSession(page);
});
test('real 401 clears memory and prevents stale authenticated controls',async({page})=>{
 const region=await login(page);
 await page.route('**/api/review/work-cases/demo-g-review',route=>route.continue({headers:{...route.request().headers(),authorization:'Bearer invalid'}}));
 await region.getByRole('button',{name:'Fall öffnen: demo-g-review',exact:true}).click();
 await expect(region.getByRole('button',{name:'Review-Modus anmelden'})).toBeVisible();
 await expect(region.getByLabel('Lokaler Review-Schlüssel')).toHaveValue('');
 await expect(region.getByRole('heading',{name:/Gespeicherter Fall:/})).toHaveCount(0);
 await noStoredSession(page);
});
test('logout cancels a delayed case response so it cannot restore sensitive UI',async({page})=>{
 const region=await login(page);
 let release;const hold=new Promise(resolve=>{release=resolve;});
 let reached;const started=new Promise(resolve=>{reached=resolve;});
 await page.route('**/api/review/work-cases/demo-g-review',async route=>{
  const response=await route.fetch();reached();await hold;
  await route.fulfill({response}).catch(()=>{});
 });
 await region.getByRole('button',{name:'Fall öffnen: demo-g-review',exact:true}).click();await started;
 await region.getByRole('button',{name:'Review-Modus abmelden'}).click();release();
 await expect(region.getByLabel('Lokaler Review-Schlüssel')).toHaveValue('');
 await expect(region.getByRole('heading',{name:/Gespeicherter Fall:/})).toHaveCount(0);
 await expect(region.getByText('Angemeldet als synthetic-local:reviewer',{exact:true})).toHaveCount(0);
 await noStoredSession(page);
});
