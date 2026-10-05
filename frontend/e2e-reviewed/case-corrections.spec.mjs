import { test, expect } from '@playwright/test';
import { readFileSync } from 'node:fs';
import { createHash } from 'node:crypto';
const credential=process.env.NORMACASE_CORRECTION_E2E_CREDENTIAL;
const caseId='synthetic-intake-'+createHash('sha256').update('synthetic-json:browser-correction-order').digest('hex');
test('correct an incomplete intake, inspect readonly history and reapprove through the real API',async({page})=>{
 const input=JSON.parse(readFileSync('../examples/cases/demo-g-incomplete.json','utf8'));
 const accepted=await page.request.post('/api/review/intake/json',{headers:{Authorization:'Bearer '+credential},
  data:{formatVersion:1,order:'browser-correction-order',message:'browser-original-input',revision:'1',input}});
 expect(accepted.status()).toBe(200);
 const original=await (await page.request.get('/api/review/work-cases/'+caseId+'/history/0',{headers:{Authorization:'Bearer '+credential}})).json();
 await page.goto('/');
 const region=page.getByRole('region',{name:'Persistente synthetische Fallprüfung'});
 await region.getByLabel('Lokaler Review-Schlüssel').fill(credential);
 await region.getByRole('button',{name:'Review-Modus anmelden'}).click();
 await region.getByRole('button',{name:'Fall öffnen: '+caseId,exact:true}).click();
 const revisions=region.getByRole('region',{name:'Fallrevisionen und Korrekturen'});
 const form=revisions.getByRole('group',{name:'Angaben als neue Revision korrigieren'});
 await expect(form).toBeVisible();
 await revisions.getByRole('checkbox',{name:/^Angabe nachfordern:/}).first().check();
 await revisions.getByLabel('Begründung der Klärungsanfrage').fill('Synthetische gezielte Browser-Rückfrage');
 await revisions.getByRole('button',{name:'Klärungsanfrage speichern'}).click();
 await expect(revisions.getByText('Synthetische gezielte Browser-Rückfrage',{exact:true}).first()).toBeVisible();
 await expect(form.getByLabel('Klärungsanfrage mit dieser Korrektur beantworten')).not.toHaveValue('');
 const supported=JSON.parse(readFileSync('../examples/cases/demo-g-supported.json','utf8'));
 const catalog=await (await page.request.get('/api/packs')).json();
 const pack=catalog.find(item=>item.packId==='synthetic.demo-g');
 for(const field of pack.fields){
  const value=supported.facts[field.id];
  if(value.kind==='TRUTH')await form.getByLabel(field.label,{exact:true}).selectOption(value.truth);
  else if(value.kind==='NUMBER')await form.getByLabel(field.label,{exact:true}).fill(String(value.number));
 }
 for(const evidence of pack.evidenceRequirements)
  await form.getByLabel(evidence.label,{exact:true}).selectOption(supported.evidence[evidence.id]??'MISSING');
 await form.getByLabel('Begründung der Korrektur').fill('Synthetische Browser-Korrektur');
 await form.getByRole('button',{name:'Korrektur speichern und neu prüfen'}).click();
 await expect(region.getByText('Wartet auf menschliche Freigabe',{exact:true})).toBeVisible();
 await expect(region.getByRole('button',{name:'Systemergebnis übernehmen',exact:true})).toBeVisible();
 await region.getByRole('button',{name:'Gespeicherte Revisionen ansehen'}).click();
 await region.getByRole('button',{name:'Revision ansehen: Fallrevision 1 · Prozessrevision 1',exact:true}).click();
 await expect(region.getByText('Historische Revision · schreibgeschützt').first()).toBeVisible();
 await expect(region.getByRole('button',{name:'Systemergebnis übernehmen',exact:true})).toHaveCount(0);
 await expect(region.getByRole('group',{name:'Angaben als neue Revision korrigieren'})).toHaveCount(0);
 await region.getByRole('button',{name:'Aktuellen Fallstand laden',exact:true}).click();
 await region.getByLabel('Begründung der Review-Entscheidung').fill('Synthetische Freigabe der neuen Revision');
 await region.getByRole('button',{name:'Systemergebnis übernehmen',exact:true}).click();
 await expect(region.getByText('Freigegeben',{exact:true}).first()).toBeVisible();
 await region.getByRole('button',{name:'Klärungsanfragen laden'}).click();
 await expect(region.getByText('Durch neue Fallrevision beantwortet',{exact:true})).toBeVisible();
 const delivered=await page.request.post('/api/review/work-cases/'+caseId+'/outbound',{headers:{Authorization:'Bearer '+credential},
  data:{messageId:'browser-corrected-result',correlationId:'browser-correction',destinationId:'synthetic-inbox',
   expectedCaseRevision:'2',expectedProcessRevision:'2',expectedAuditRevision:'2'}});
 expect(delivered.status()).toBe(200);
 const after=await (await page.request.get('/api/review/work-cases/'+caseId+'/history/0',{headers:{Authorization:'Bearer '+credential}})).json();
 expect(after.assessmentRecordJson).toBe(original.assessmentRecordJson);
 expect(after.workCase.allowedActions).toEqual([]);
 await region.getByRole('button',{name:'Review-Modus abmelden',exact:true}).click();
 await expect(region.getByRole('region',{name:'Fallrevisionen und Korrekturen'})).toHaveCount(0);
 expect(await page.evaluate(()=>[localStorage.length,sessionStorage.length,document.cookie])).toEqual([0,0,'']);
});

test('logout aborts delayed case history and clears retained correction content',async({page})=>{
 await page.goto('/');
 const region=page.getByRole('region',{name:'Persistente synthetische Fallprüfung'});
 await region.getByLabel('Lokaler Review-Schlüssel').fill(credential);
 await region.getByRole('button',{name:'Review-Modus anmelden'}).click();
 await region.getByRole('button',{name:'Fall öffnen: '+caseId,exact:true}).click();
 let release;const hold=new Promise(resolve=>{release=resolve;});
 let reached;const entered=new Promise(resolve=>{reached=resolve;});
 await page.route('**/api/review/work-cases/'+caseId+'/history',async route=>{
  const response=await route.fetch();reached();await hold;await route.fulfill({response}).catch(()=>{});
 });
 await region.getByRole('button',{name:'Gespeicherte Revisionen ansehen'}).click();
 await entered;
 await region.getByRole('button',{name:'Review-Modus abmelden',exact:true}).click();
 release();
 await expect(region.getByRole('region',{name:'Fallrevisionen und Korrekturen'})).toHaveCount(0);
 await expect(region.getByText('Synthetische Browser-Korrektur',{exact:true})).toHaveCount(0);
 await expect(region.getByLabel('Lokaler Review-Schlüssel')).toBeVisible();
 expect(await page.evaluate(()=>[localStorage.length,sessionStorage.length,document.cookie])).toEqual([0,0,'']);
});
