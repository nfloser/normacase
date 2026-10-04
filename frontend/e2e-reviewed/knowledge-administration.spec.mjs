import { test, expect } from '@playwright/test';
async function login(page,credential){
 await page.goto('/');
 const region=page.getByRole('region',{name:'Persistente synthetische Fallprüfung'});
 await region.getByLabel('Lokaler Review-Schlüssel').fill(credential);
 await region.getByRole('button',{name:'Review-Modus anmelden'}).click();
 return region.getByRole('region',{name:'Wissen verwalten'});
}
test('distinct synthetic users propose review and activate exact Knowledge in German',async({page,browser})=>{
 const proposer=await login(page,process.env.NORMACASE_REVIEW_E2E_CREDENTIAL);
 await expect(proposer.getByRole('heading',{name:'Wissen verwalten'})).toBeVisible();
 await proposer.getByLabel('Quellenreferenz',{exact:true}).fill('source:browser-synthetic');
 await proposer.getByLabel('Referenz der Auswirkungsanalyse',{exact:true}).fill('impact:browser-synthetic');
 await proposer.getByLabel('Referenz der Testnachweise',{exact:true}).fill('tests:browser-synthetic');
 await proposer.getByRole('button',{name:'Wissensänderung vorschlagen',exact:true}).click();
 await expect(proposer.getByRole('status')).toHaveText('Wissensvorgang gespeichert.');
 const changeId=(await proposer.getByRole('heading',{level:4}).textContent()).replace('Wissensänderung: ','');
 await expect(proposer.getByRole('button',{name:'Wissensänderung freigeben',exact:true})).toHaveCount(0);
 const reviewerContext=await browser.newContext();
 const reviewerPage=await reviewerContext.newPage();
 const reviewer=await login(reviewerPage,process.env.NORMACASE_REVIEW_E2E_OTHER_CREDENTIAL);
 await reviewer.getByRole('button',{name:'Wissensänderung öffnen: '+changeId,exact:true}).click();
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
