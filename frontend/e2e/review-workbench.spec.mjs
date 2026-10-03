import { test, expect } from '@playwright/test';

const credential=process.env.NORMACASE_REVIEW_TEST_CREDENTIAL;
test.skip(!credential,'persistent review environment is opt-in');

async function login(page){
  await page.goto('/');
  const review=page.getByRole('region',{name:'Authentifizierte synthetische Fallprüfung'});
  await expect(review).toBeVisible();
  await review.getByLabel('Lokaler synthetischer Zugangsschlüssel').fill(credential);
  await review.getByRole('button',{name:'Anmelden'}).click();
  await expect(review.getByText('synthetic-local:reviewer',{exact:true})).toBeVisible();
  return review;
}

test('persistent review accepts, detects stale concurrency and overrides without browser storage',async({browser,page})=>{
  const review=await login(page);
  expect(await page.evaluate(()=>({local:localStorage.length,session:sessionStorage.length}))).toEqual({local:0,session:0});
  expect((await page.context().cookies()).length).toBe(0);
  expect(page.url()).not.toContain(credential);

  await review.getByRole('button',{name:'Persistenten Fall öffnen: demo-g-supported',exact:true}).click();
  await expect(review.getByRole('heading',{name:'Unverändertes deterministisches Systemergebnis'})).toBeVisible();

  const secondContext=await browser.newContext({baseURL:'http://localhost:5080'});
  const secondPage=await secondContext.newPage();
  const secondReview=await login(secondPage);
  await secondReview.getByRole('button',{name:'Persistenten Fall öffnen: demo-g-supported',exact:true}).click();

  await review.getByLabel('Begründung').fill('Synthetischer Browser-Accept');
  await review.getByRole('button',{name:'Systemergebnis bestätigen'}).click();
  await expect(review.getByText('Systemergebnis bestätigt',{exact:true})).toBeVisible();
  await expect(review.getByRole('button',{name:'Systemergebnis bestätigen'})).toHaveCount(0);

  await secondReview.getByLabel('Begründung').fill('Veralteter synthetischer Browserstand');
  await secondReview.getByRole('button',{name:'Systemergebnis bestätigen'}).click();
  await expect(secondReview.getByRole('status')).toContainText('zwischenzeitlich geändert');
  await expect(secondReview.getByRole('button',{name:'Systemergebnis bestätigen'})).toHaveCount(0);
  await secondContext.close();

  await review.getByRole('button',{name:'Persistenten Fall öffnen: demo-g-not-supported',exact:true}).click();
  await review.getByLabel('Begründung').fill('Synthetisches Override für Browserprüfung');
  await review.getByLabel('Explizites Override-Ergebnis').selectOption('SUPPORTED');
  await review.getByRole('button',{name:'Ergebnis übersteuern'}).click();
  await expect(review.getByText('Systemergebnis übersteuert',{exact:true})).toBeVisible();
  await expect(review.getByText(/Explizites Override-Ergebnis: Voraussetzungen erfüllt/)).toBeVisible();

  await review.getByRole('button',{name:'Abmelden'}).click();
  await expect(review.getByRole('button',{name:'Anmelden'})).toBeVisible();
  await expect(review.getByLabel('Lokaler synthetischer Zugangsschlüssel')).toHaveValue('');
  expect(await page.evaluate(()=>({local:localStorage.length,session:sessionStorage.length}))).toEqual({local:0,session:0});
});
