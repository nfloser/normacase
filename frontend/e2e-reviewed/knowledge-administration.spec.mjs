import { test, expect } from '@playwright/test';
import { readFileSync } from 'node:fs';
import { randomUUID, createHash } from 'node:crypto';
async function login(page,credential){
 await page.goto('/#review');
 const region=page.getByRole('region',{name:'Persistente synthetische Fallprüfung'});
 await region.getByLabel('Lokaler Review-Schlüssel').fill(credential);
 await region.getByRole('button',{name:'Review-Modus anmelden'}).click();
 return region.getByRole('region',{name:'Wissen verwalten'});
}
test('distinct synthetic users propose review and activate exact Knowledge in German',async({page,browser})=>{
 const proposer=await login(page,process.env.NORMACASE_REVIEW_E2E_CREDENTIAL);
 await expect(proposer.getByRole('heading',{name:'Wissen verwalten'})).toBeVisible();
 const importedPackId='browser-import-'+randomUUID(),importedReleaseId='browser-release-1';
 const exactPack=readFileSync(new URL('../../knowledge/demo-c/pack.json',import.meta.url),'utf8')
  .replace(/("packId"\s*:\s*)"synthetic.demo-c"/,'$1"'+importedPackId+'"')
  .replace(/("releaseId"\s*:\s*)"[^"]*"/,'$1"'+importedReleaseId+'"')+'\n  ';
 const file=proposer.getByLabel('Synthetischen Wissens-Release einspielen',{exact:true});
 await expect(file).toBeEnabled();
 await file.setInputFiles({name:'synthetic-release.json',mimeType:'application/json',buffer:Buffer.from(exactPack,'utf8')});
 await expect(proposer.getByRole('status')).toHaveText('Wissens-Release unveränderlich gespeichert.');
 expect(await proposer.locator('.knowledge-release-content').textContent()).toBe(exactPack);
 await expect(proposer.locator('dd').filter({hasText:createHash('sha256').update(exactPack,'utf8').digest('hex')})).toHaveCount(1);

 await proposer.getByLabel('Bezeichnung der Quelle',{exact:true}).fill('source:browser-synthetic');
 await proposer.getByLabel('Bezeichnung der Auswirkungsanalyse',{exact:true}).fill('impact:browser-synthetic');
 await proposer.getByLabel('Bezeichnung der Testnachweise',{exact:true}).fill('tests:browser-synthetic');
 const original='Synthetische Quelle\n  <script>window.syntheticEvidenceExecuted=true</script>\n';
 await proposer.getByLabel('Inhalt des synthetischen Quellennachweises',{exact:true}).fill(original);
 await proposer.getByLabel('Inhalt der synthetischen Auswirkungsanalyse',{exact:true}).fill('Synthetische Auswirkungsanalyse');
 await proposer.getByLabel('Inhalt der synthetischen Testnachweise',{exact:true}).fill('Synthetische Testergebnisse');
 await proposer.getByRole('button',{name:'Wissensänderung vorschlagen',exact:true}).click();
 await expect(proposer.getByRole('status')).toHaveText('Wissensvorgang gespeichert.');
 const changeId=(await proposer.getByRole('heading',{level:4,name:/^Wissensänderung:/}).textContent()).replace('Wissensänderung: ','');
 await expect(proposer.getByRole('button',{name:'Wissensänderung freigeben',exact:true})).toHaveCount(0);
 const reviewerContext=await browser.newContext();
 const reviewerPage=await reviewerContext.newPage();
 const reviewer=await login(reviewerPage,process.env.NORMACASE_REVIEW_E2E_OTHER_CREDENTIAL);
 await expect(reviewer.getByLabel('Synthetischen Wissens-Release einspielen',{exact:true})).toHaveCount(0);
 await reviewer.getByLabel('Wissenspaket und Release',{exact:true}).selectOption(JSON.stringify([importedPackId,importedReleaseId]));
 await reviewer.getByRole('button',{name:'Genauen Release ansehen',exact:true}).click();
 expect(await reviewer.locator('.knowledge-release-content').textContent()).toBe(exactPack);

 await reviewer.getByRole('button',{name:'Wissensänderung öffnen: '+changeId,exact:true}).click();
 await reviewer.locator('summary').filter({hasText:'Quelle: source:browser-synthetic'}).click();
 const content=reviewer.locator('.knowledge-evidence-content').first();
 await expect(content).toBeVisible();
 expect(await content.textContent()).toBe(original);
 expect(await reviewerPage.evaluate(()=>window.syntheticEvidenceExecuted)).toBeUndefined();
 await reviewer.getByLabel('Begründung der Wissensprüfung',{exact:true}).fill('Synthetische getrennte Browserprüfung');
 await reviewer.getByRole('button',{name:'Wissensänderung freigeben',exact:true}).click();
 await expect(reviewer.getByRole('status')).toHaveText('Wissensvorgang gespeichert.');
 await reviewer.getByRole('button',{name:'Freigegebenen Release protokolliert aktivieren',exact:true}).click();
 await expect(reviewer.getByRole('status')).toHaveText('Wissensvorgang gespeichert.');
 await expect(reviewer.locator('dt').filter({hasText:'Aktivierungsrevision'}).locator('xpath=following-sibling::dd[1]')).toHaveText('1');
 await reviewerPage.getByRole('button',{name:'Review-Modus abmelden',exact:true}).click();
 await expect(reviewerPage.getByRole('region',{name:'Wissen verwalten'})).toHaveCount(0);
 expect(await reviewerPage.evaluate(()=>[localStorage.length,sessionStorage.length,document.cookie])).toEqual([0,0,'']);
 await reviewerContext.close();
 await page.getByRole('button',{name:'Review-Modus abmelden',exact:true}).click();
 await expect(proposer).toHaveCount(0);
});
test('logout discards a delayed Knowledge list without browser storage',async({page})=>{
 const proposer=await login(page,process.env.NORMACASE_REVIEW_E2E_CREDENTIAL);
 await expect(proposer.getByRole('heading',{name:'Wissen verwalten'})).toBeVisible();
 let release;const hold=new Promise(resolve=>{release=resolve;});
 let reached;const completed=new Promise(resolve=>{reached=resolve;});
 await page.route('**/api/review/knowledge/changes',async route=>{
  const response=await route.fetch();reached();await hold;
  await route.fulfill({response}).catch(()=>{});
 });
 await proposer.getByRole('button',{name:'Wissensänderungen neu laden',exact:true}).click();
 await completed;
 await page.getByRole('button',{name:'Review-Modus abmelden',exact:true}).click();
 release();
 await expect(page.getByRole('region',{name:'Wissen verwalten'})).toHaveCount(0);
 expect(await page.evaluate(()=>[localStorage.length,sessionStorage.length,document.cookie])).toEqual([0,0,'']);
});
