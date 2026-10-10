import { test, expect } from '@playwright/test';
const credential=process.env.NORMACASE_REVIEW_E2E_CREDENTIAL;
if(!credential)throw new Error('Reviewed workbench test configuration is required');
async function login(page){
 await page.goto('/#review');
 const region=page.getByRole('region',{name:'Persistente synthetische Fallprüfung'});
 await region.getByLabel('Lokaler Review-Schlüssel').fill(credential);
 await region.getByRole('button',{name:'Review-Modus anmelden'}).click();
 await expect(region.getByText('Angemeldet als synthetic-local:user-alice',{exact:true})).toBeVisible();
 await expect(region.getByLabel('Lokaler Review-Schlüssel')).toHaveCount(0);
 await expect(region.getByRole('heading',{name:'Identitäten verwalten'})).toHaveCount(0);
 return region;
}
async function noStoredSession(page){
 expect(await page.evaluate(()=>[localStorage.length,sessionStorage.length,document.cookie])).toEqual([0,0,'']);
 expect(await page.context().cookies()).toEqual([]);
}
test('bounded queue pages use the real API and refresh without retaining a session',async({page})=>{
 await page.route('**/api/review/work-queues**',route=>{
  const url=new URL(route.request().url());url.searchParams.set('pageSize','1');
  return route.continue({url:url.toString()});
 });
 const region=await login(page);
 const ids=['demo-g-batch-not-supported','demo-g-batch-supported','demo-g-incomplete','demo-g-not-supported','demo-g-review','demo-g-supported'];
 for(let index=0;index<ids.length;index++){
  await expect(region.getByRole('button',{name:'Fall öffnen: '+ids[index],exact:true})).toBeVisible();
  await expect(region.getByRole('button',{name:/^Fall öffnen:/})).toHaveCount(1);
  if(index<ids.length-1)await region.getByRole('button',{name:'Weitere Fälle anzeigen',exact:true}).click();
 }
 await expect(region.getByRole('button',{name:'Weitere Fälle anzeigen',exact:true})).toHaveCount(0);
 await region.getByRole('button',{name:'Arbeitsliste aktualisieren',exact:true}).click();
 await expect(region.getByRole('button',{name:'Fall öffnen: demo-g-batch-not-supported',exact:true})).toBeVisible();
 await region.getByRole('button',{name:'Review-Modus abmelden',exact:true}).click();
 await expect(region.getByRole('button',{name:/^Fall öffnen:/})).toHaveCount(0);
 await noStoredSession(page);
});
test('cancelled batch is retried with the same identity and shows German per-case results',async({page})=>{
 const region=await login(page);
 for(const id of ['demo-g-batch-not-supported','demo-g-batch-supported'])
  await region.getByLabel('Für Sammelfreigabe auswählen: '+id,{exact:true}).check();
 await expect(region.getByText('Ausgewählte Fälle: 2',{exact:true})).toBeVisible();
 await region.getByLabel('Gemeinsame Begründung der Sammelfreigabe').fill('Synthetische Browser-Sammelfreigabe');

 let release;const hold=new Promise(resolve=>{release=resolve;});
 let reached;const completed=new Promise(resolve=>{reached=resolve;});
 let retainedRequest='';
 await page.route('**/api/review/batch-reviews',async route=>{
  retainedRequest=route.request().postData()??'';
  const response=await route.fetch();reached();await hold;
  await route.fulfill({response}).catch(()=>{});
 });
 await region.getByRole('button',{name:'Ausgewählte Systemergebnisse übernehmen',exact:true}).click();
 await completed;
 await region.getByRole('button',{name:'Übertragung abbrechen',exact:true}).click();
 await expect(region.getByRole('alert')).toContainText('Abschluss ist unbekannt');
 await expect(region.getByRole('button',{name:'Dieselbe Sammelanfrage erneut senden',exact:true})).toBeVisible();
 release();await page.unroute('**/api/review/batch-reviews');

 let retriedRequest='';
 await page.route('**/api/review/batch-reviews',async route=>{
  retriedRequest=route.request().postData()??'';await route.continue();
 });
 await region.getByRole('button',{name:'Dieselbe Sammelanfrage erneut senden',exact:true}).click();
 await expect(region.getByText('Sammelfreigabe abgeschlossen. Prüfe die Einzelergebnisse.',{exact:true})).toBeVisible();
 expect(retriedRequest).toBe(retainedRequest);
 const result=region.getByRole('heading',{name:'Einzelergebnisse der Sammelfreigabe'}).locator('..');
 await expect(result.getByText('demo-g-batch-not-supported',{exact:true})).toBeVisible();
 await expect(result.getByText('demo-g-batch-supported',{exact:true})).toBeVisible();
 await expect(result.getByText('Freigabe gespeichert',{exact:true})).toHaveCount(2);
 for(const id of ['demo-g-batch-not-supported','demo-g-batch-supported']){
  const response=await page.request.get('/api/review/work-cases/'+id,{headers:{Authorization:'Bearer '+credential}});
  const detail=await response.json();
  expect(detail.audit.filter(item=>item.kind==='HUMAN_REVIEW_RECORDED')).toHaveLength(1);
  expect(detail.audit.at(-1).reason).toBe('Synthetische Browser-Sammelfreigabe');
 }
 await region.getByRole('button',{name:'Review-Modus abmelden'}).click();
 await expect(region.getByRole('heading',{name:'Einzelergebnisse der Sammelfreigabe'})).toHaveCount(0);
 await noStoredSession(page);
});
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
 await expect(region.getByRole('heading',{name:'Abgeschlossen (3)'})).toBeVisible();
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
 await expect(region.getByText('synthetic-local:user-alice',{exact:true})).toHaveCount(0);
 await noStoredSession(page);
});

test('administrator resolves a stale suspension, changes live access and sees the audit',async({page})=>{
 const administrator=process.env.NORMACASE_REVIEW_E2E_ADMINISTRATOR_CREDENTIAL;
 if(!administrator)throw new Error('Administrator test credential is required');
 await page.goto('/#review');
 const region=page.getByRole('region',{name:'Persistente synthetische Fallprüfung'});
 await region.getByLabel('Lokaler Review-Schlüssel').fill(administrator);
 await region.getByRole('button',{name:'Review-Modus anmelden'}).click();
 await expect(region.getByText('Angemeldet als synthetic-local:administrator',{exact:true})).toBeVisible();
 await expect(region.getByRole('heading',{name:'Identitäten verwalten'})).toBeVisible();
 const administration=region.getByRole('region',{name:'Identitäten verwalten'});
 await expect(administration.getByText('synthetic-local:user-bob',{exact:true})).toBeVisible();
 await administration.getByRole('button',{name:'Identität verwalten: synthetic-local:user-alice',exact:true}).click();
 const detail=administration.locator('article');
 await expect(detail.getByText('Aktiv',{exact:true})).toBeVisible();

 const target=encodeURIComponent('synthetic-local:user-alice');
 const external=await page.request.post('/api/review/administration/identities/'+target+'/status',{
  headers:{Authorization:'Bearer '+administrator},
  data:{expectedRevision:'0',suspended:true,reason:'Synthetische konkurrierende Sperre'}
 });
 expect(external.ok()).toBeTruthy();
 await administration.getByLabel('Begründung der Zugriffsänderung').fill('Veralteter Browserversuch');
 await administration.getByRole('button',{name:'Identität sperren'}).click();
 await expect(administration.getByRole('alert')).toContainText('zwischenzeitlich geändert');
 await expect(detail.getByText('Gesperrt',{exact:true})).toBeVisible();
 await expect(detail.getByText('Synthetische konkurrierende Sperre',{exact:true})).toBeVisible();

 await administration.getByLabel('Begründung der Zugriffsänderung').fill('Synthetische Browser-Reaktivierung');
 await administration.getByRole('button',{name:'Identität reaktivieren'}).click();
 await expect(administration.getByText('Identität wurde reaktiviert.',{exact:true})).toBeVisible();
 await administration.getByLabel('Begründung der Zugriffsänderung').fill('Synthetische Browser-Sperre');
 await administration.getByRole('button',{name:'Identität sperren'}).click();
 await expect(administration.getByText('Identität wurde gesperrt.',{exact:true})).toBeVisible();
 const denied=await page.request.get('/api/review-session',{headers:{Authorization:'Bearer '+credential}});
 expect(denied.status()).toBe(401);
 await expect(detail.getByText('Synthetische Browser-Sperre',{exact:true})).toBeVisible();

 await administration.getByLabel('Begründung der Zugriffsänderung').fill('Synthetische Abschluss-Reaktivierung');
 await administration.getByRole('button',{name:'Identität reaktivieren'}).click();
 await expect(administration.getByText('Identität wurde reaktiviert.',{exact:true})).toBeVisible();
 const restored=await page.request.get('/api/review-session',{headers:{Authorization:'Bearer '+credential}});
 expect(restored.ok()).toBeTruthy();

 let release;const hold=new Promise(resolve=>{release=resolve;});
 let reached;const started=new Promise(resolve=>{reached=resolve;});
 await page.route('**/api/review/administration/identities/synthetic-local%3Auser-alice',async route=>{
  const response=await route.fetch();reached();await hold;
  await route.fulfill({response}).catch(()=>{});
 });
 await administration.getByRole('button',{name:'Identität verwalten: synthetic-local:user-alice',exact:true}).click();await started;
 await region.getByRole('button',{name:'Review-Modus abmelden'}).click();
 release();
 await expect(region.getByLabel('Lokaler Review-Schlüssel')).toHaveValue('');
 await expect(region.getByRole('heading',{name:'Identitäten verwalten'})).toHaveCount(0);
 await noStoredSession(page);
});
test('separate administrators propose and approve a durable entitlement snapshot',async({page})=>{
 const administrator=process.env.NORMACASE_REVIEW_E2E_ADMINISTRATOR_CREDENTIAL;
 const approver=process.env.NORMACASE_REVIEW_E2E_ENTITLEMENT_APPROVER_CREDENTIAL;
 if(!administrator||!approver)throw new Error('Entitlement administration credentials are required');
 await page.goto('/#review');const region=page.getByRole('region',{name:'Persistente synthetische Fallprüfung'});
 await region.getByLabel('Lokaler Review-Schlüssel').fill(administrator);
 await region.getByRole('button',{name:'Review-Modus anmelden'}).click();
 const entitlements=region.getByRole('region',{name:'Fall- und Aktionsberechtigungen verwalten'});
 await expect(entitlements.getByLabel('Synthetische Identität')).toHaveValue('synthetic-local:user-alice');
 await entitlements.getByLabel('Begründung des Antrags').fill('Synthetischer Browser-Antrag');
 await entitlements.getByRole('button',{name:'Änderung beantragen'}).click();
 await expect(entitlements.getByText('Berechtigungsänderung wurde zur getrennten Prüfung eingereicht.')).toBeVisible();
 await region.getByRole('button',{name:'Review-Modus abmelden'}).click();
 await region.getByLabel('Lokaler Review-Schlüssel').fill(approver);
 await region.getByRole('button',{name:'Review-Modus anmelden'}).click();
 await expect(region.getByRole('heading',{name:'Identitäten verwalten'})).toHaveCount(0);
 const decisions=region.getByRole('region',{name:'Fall- und Aktionsberechtigungen verwalten'});
 await expect(decisions.getByText('Synthetischer Browser-Antrag',{exact:true})).toBeVisible();
 await decisions.getByLabel('Begründung der Entscheidung').fill('Synthetische Browser-Gegenprüfung');
 await decisions.getByRole('button',{name:'Antrag freigeben'}).click();
 await expect(decisions.getByText('Berechtigungsantrag wurde freigegeben und dauerhaft gespeichert.')).toBeVisible();
 await expect(decisions.getByText('Es liegen keine offenen Berechtigungsanträge vor.')).toBeVisible();
 await region.getByRole('button',{name:'Review-Modus abmelden'}).click();await noStoredSession(page);
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
 await expect(region.getByText('Angemeldet als synthetic-local:user-alice',{exact:true})).toHaveCount(0);
 await noStoredSession(page);
});
