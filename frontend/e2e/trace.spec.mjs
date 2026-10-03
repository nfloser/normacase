import { test, expect } from '@playwright/test';
import { readFile } from 'node:fs/promises';

async function example(page,pack,id) {
  await page.goto('/');
  await page.getByRole('combobox',{name:'Prüfbereich'}).selectOption(pack);
  await page.getByLabel('Beispiel auswählen').selectOption(id);
  await page.getByRole('button',{name:'Beispiel laden'}).click();
  await expect(page.getByLabel('Prüfdatum',{exact:true})).toHaveValue('2026-10-02');
}
async function evaluate(page) {
  await page.getByRole('button',{name:'Jetzt prüfen',exact:true}).click();
  await expect(page.locator('.decision-trace')).toHaveCount(1);
  await page.getByText('Prüfweg nachvollziehen',{exact:true}).click();
  return page.locator('.decision-trace');
}

test('German AND/OR trace preserves unknown and not-applicable truth values',async({page})=>{
  await example(page,'synthetic.demo-a','incomplete');
  let trace=await evaluate(page);
  await expect(trace).toContainText('Alle Bedingungen');
  await expect(trace).toContainText('Mindestens eine Bedingung');
  await expect(trace).toContainText('Nicht ausreichend beurteilbar');
  await expect(trace).toContainText('Unbekannt');
  await page.getByRole('combobox',{name:/Pflichtkriterium B/}).selectOption('NOT_APPLICABLE');
  trace=await evaluate(page);
  await expect(trace).toContainText('Nicht anwendbar');
  await expect(trace).not.toContainText('NOT_APPLICABLE');
});

test('missing and conflicting evidence retain an unknown parent with a matched child',async({page})=>{
  await example(page,'synthetic.demo-c','review');
  let trace=await evaluate(page);
  await expect(trace).toContainText('Nachweisabhängige Bedingung');
  await expect(trace).toContainText('Verifikationsnachweis');
  await expect(trace).toContainText('Fehlend');
  await expect(trace).toContainText('Nicht ausreichend beurteilbar');
  await expect(trace).toContainText('Bedingung erfüllt');
  await expect(page.getByRole('heading',{name:'Manuelle Prüfung erforderlich',exact:true})).toBeVisible();
  await page.getByLabel('Verifikationsnachweis').selectOption('CONFLICTING');
  trace=await evaluate(page);
  await expect(trace).toContainText('Widersprüchlich');
  await expect(trace).toContainText('Nicht ausreichend beurteilbar');
  await expect(trace).not.toContainText('CONFLICTING');
});

test('exact decimal and inclusive limits are displayed without modifying exports',async({page})=>{
  await example(page,'synthetic.demo-b','supported');
  await page.getByRole('textbox',{name:'Synthetischer Wert',exact:true}).fill('123456789,1234567890123456789');
  const trace=await evaluate(page);
  await expect(trace).toContainText('123456789,1234567890123456789');
  await expect(trace).toContainText('Untergrenze (einschließlich)');
  await expect(trace).toContainText('Obergrenze (einschließlich)');
  const downloading=page.waitForEvent('download');
  await page.getByRole('button',{name:'Ergebnis als JSON herunterladen',exact:true}).click();
  const file=await downloading;
  const original=await readFile(await file.path(),'utf8');
  await page.getByText('Technische Prüfspur anzeigen',{exact:true}).click();
  expect(await page.locator('pre').textContent()).toBe(original);
  expect(original).toContain('123456789.1234567890123456789');
});

test('derived arithmetic shows recorded range, sum, max and unknown without computing locally',async({page})=>{
  await page.setViewportSize({width:390,height:844});
  await example(page,'synthetic.demo-d','supported');
  let trace=await evaluate(page);
  for (const label of ['Summe','Maximum','Bereichszuordnung','Basiswert','Option A','Option B','Zugeordneter Wert']) await expect(trace).toContainText(label);
  expect(await page.evaluate(()=>document.documentElement.scrollWidth<=window.innerWidth)).toBe(true);
  await page.screenshot({path:'test-results/workbench-decision-trace-mobile.png',fullPage:true});
  await page.getByRole('combobox',{name:'Option A – Eingabestatus'}).selectOption('UNKNOWN');
  trace=await evaluate(page);
  await expect(trace).toContainText('Aufgezeichneter Berechnungswert: Unbekannt');
});

test('independent output traces keep their own recorded results and sources',async({page})=>{
  await example(page,'synthetic.demo-e','mixed');
  const trace=await evaluate(page);
  await expect(trace.locator('section')).toHaveCount(6);
  for (const label of ['Entscheidungsstatus','Auswahlstatus','Segment Alpha','Segment Beta','Externer Status']) await expect(trace.getByRole('heading',{name:'Prüfweg der Fachausgabe: '+label,exact:true})).toBeVisible();
  const beta=trace.locator('section').filter({has:page.getByRole('heading',{name:'Prüfweg der Fachausgabe: Segment Beta',exact:true})});
  await expect(beta).toContainText('Nicht ausreichend beurteilbar');
  await expect(beta).toContainText('Synthetic Demo E structured output source');
});

test('historical snapshot replay never gets current presentation labels',async({page})=>{
  await page.goto('/');
  const input=await readFile('../examples/cases/demo-d-supported.json','utf8');
  const captured=await page.request.post('/api/snapshots/synthetic.demo-d',{headers:{'Content-Type':'application/json'},data:input});
  expect(captured.ok()).toBe(true);
  const {snapshotJson}=await captured.json();
  await page.getByLabel('Prüfsnapshot auswählen').setInputFiles({name:'snapshot.json',mimeType:'application/json',buffer:Buffer.from(snapshotJson)});
  await expect(page.getByRole('heading',{name:'Offline-Wiederholung bestätigt',exact:true})).toBeVisible();
  await expect(page.locator('.decision-trace')).toHaveCount(0);
  await expect(page.locator('.snapshot-tools')).not.toContainText('Basiswert');
});

test('a recorded negation is presented explicitly without changing its child',async({page})=>{
  await page.goto('/');
  const input=await readFile('../examples/cases/demo-a-supported.json','utf8');
  const response=await page.request.post('/api/snapshots/synthetic.demo-a',{headers:{'Content-Type':'application/json'},data:input});
  expect(response.ok()).toBe(true);
  const recorded=await response.json();
  // A synthetic presentation fixture exercises NOT, which the installed demo packs
  // do not use. This checks the renderer; engine negation has separate core tests.
  const assessment=JSON.parse(recorded.assessmentJson);
  const child=assessment.assessment.ruleTrace.condition;
  assessment.assessment.ruleTrace.condition={kind:'not',result:'NOT_MATCHED',field:null,expected:null,actual:null,minimum:null,maximum:null,children:[child]};
  assessment.assessment.ruleTrace.conditionResult='NOT_MATCHED';
  assessment.assessment.ruleTrace.outcome='NOT_SUPPORTED';
  assessment.assessment.outcome='NOT_SUPPORTED';
  await page.route('**/api/snapshots/synthetic.demo-a',route=>route.fulfill({status:200,contentType:'application/json',body:JSON.stringify({...recorded,assessmentJson:JSON.stringify(assessment)})}));
  await page.getByLabel('Prüfdatum',{exact:true}).fill('2026-10-02');
  const trace=await evaluate(page);
  await expect(trace).toContainText('Bedingung negiert');
  await expect(trace).toContainText('Bedingung nicht erfüllt');
  await expect(trace).toContainText('Bedingung erfüllt');
  await expect(page.getByRole('heading',{name:'Voraussetzungen nicht erfüllt',exact:true})).toBeVisible();
});
