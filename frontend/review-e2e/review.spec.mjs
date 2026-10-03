import {test,expect} from '@playwright/test';
async function login(page){
  await page.goto('/');
  const queues=page.getByRole('region',{name:'Fallwarteschlangen'});
  await queues.getByLabel('Lokaler Demo-Zugangsschlüssel').fill('a'.repeat(64));
  await queues.getByRole('button',{name:'Demo-Zugang öffnen',exact:true}).click();
  await expect(queues.getByRole('button',{name:'Demo-Zugang schließen',exact:true})).toBeVisible();
  await expect(queues.getByLabel('Lokaler Demo-Zugangsschlüssel')).toHaveCount(0);
  expect(await page.evaluate(()=>localStorage.length)).toBe(0);
  return queues;
}
test('authenticated acceptance persists audit and moves the case out of the approval queue',async({page})=>{
  const queues=await login(page);
  await queues.getByRole('button',{name:'Fall öffnen: demo-g-supported',exact:true}).click();
  await expect(queues.getByText('Wartet auf menschliche Freigabe',{exact:true})).toBeVisible();
  await queues.getByLabel('Begründung der menschlichen Prüfung').fill('Synthetische End-to-End-Gegenprüfung vollständig.');
  await queues.getByRole('button',{name:'Systemergebnis annehmen',exact:true}).click();
  await expect(queues.getByText('Menschlich geprüft – synthetische Demo',{exact:true})).toBeVisible();
  await expect(queues.getByText('Systemergebnis angenommen',{exact:true})).toBeVisible();
  await expect(queues.getByRole('heading',{name:'Abgeschlossene Prüfungen (1)',exact:true})).toBeVisible();
  await page.reload();
  const again=await login(page);
  await again.getByRole('button',{name:'Fall öffnen: demo-g-supported',exact:true}).click();
  await expect(again.getByText('Synthetische End-to-End-Gegenprüfung vollständig.',{exact:true})).toBeVisible();
  await expect(again.getByRole('button',{name:'Systemergebnis annehmen',exact:true})).toHaveCount(0);
  await again.screenshot({path:'test-results/review-persistent-desktop.png'});
});
test('human override retains original assessment and technical/incomplete cases have no actions',async({page})=>{
  const queues=await login(page);
  await queues.getByRole('button',{name:'Fall öffnen: demo-g-review',exact:true}).click();
  await expect(queues.getByRole('button',{name:'Systemergebnis annehmen',exact:true})).toHaveCount(0);
  await queues.getByLabel('Begründung der menschlichen Prüfung').fill('Synthetische menschliche Abweichung mit eigener Begründung.');
  await queues.getByLabel('Menschlich festgelegtes Ergebnis').selectOption('NOT_SUPPORTED');
  await queues.getByRole('button',{name:'Ergebnis übersteuern',exact:true}).click();
  await expect(queues.getByRole('heading',{name:/Prüfergebnis:/})).toContainText('Manuelle');
  await expect(queues.getByText('Ergebnis übersteuert',{exact:true})).toBeVisible();
  await queues.getByRole('button',{name:'Fall öffnen: demo-g-incomplete',exact:true}).click();
  await expect(queues.getByText('Informationen fehlen',{exact:true})).toBeVisible();
  await expect(queues.getByRole('button',{name:'Ergebnis übersteuern',exact:true})).toHaveCount(0);
  await queues.getByRole('button',{name:'Fall öffnen: demo-technical',exact:true}).click();
  await expect(queues.getByText(/Für diesen technischen Fehler liegt keine Bewertung vor/)).toBeVisible();
  await page.setViewportSize({width:390,height:844});
  expect(await page.evaluate(()=>document.documentElement.scrollWidth<=window.innerWidth)).toBeTruthy();
  await queues.screenshot({path:'test-results/review-technical-mobile.png'});
  await queues.getByRole('button',{name:'Demo-Zugang schließen',exact:true}).click();
  await expect(queues.getByLabel('Lokaler Demo-Zugangsschlüssel')).toBeVisible();
  await expect(queues.getByRole('button',{name:/Fall öffnen:/})).toHaveCount(0);
});
