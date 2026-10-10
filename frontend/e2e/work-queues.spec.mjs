import { test, expect } from '@playwright/test';

test('German workload preview summarizes 100 routed cases and keeps representative drill-down', async ({page})=>{
  await page.goto('/#volume-cases');
  const queues=page.getByRole('region',{name:'Fallwarteschlangen'});
  await expect(queues.getByText('100 Fälle automatisch vorbereitet',{exact:true})).toBeVisible();
  await expect(queues.getByRole('heading',{name:'Zur Freigabe vorbereitet (60)'})).toBeVisible();
  await expect(queues.getByRole('heading',{name:'Informationen nachfordern (20)'})).toBeVisible();
  await expect(queues.getByRole('heading',{name:'Gegenprüfung erforderlich (15)'})).toBeVisible();
  await expect(queues.getByRole('heading',{name:'Technische Klärung (5)'})).toBeVisible();
  await expect(queues.getByRole('button',{name:/^Fall öffnen:/})).toHaveCount(20);

  await queues.getByRole('button',{name:'Fall öffnen: demo-g-supported',exact:true}).click();
  await expect(queues.getByRole('heading',{name:/Prüfergebnis:/})).toBeVisible();
  await queues.getByText('Prüfweg nachvollziehen', {exact:true}).click();
  await expect(queues.getByText('Wartet auf menschliche Freigabe', {exact:true})).toBeVisible();
  await queues.screenshot({path:'test-results/work-queues-assessment-desktop.png'});

  await queues.getByRole('button',{name:'Zur Fallübersicht',exact:true}).click();
  await queues.getByRole('button',{name:'Fall öffnen: demo-technical',exact:true}).click();
  await expect(queues.getByText(/Für diesen technischen Fehler liegt keine Bewertung vor/)).toBeVisible();
  await expect(queues.getByText('Prüfweg nachvollziehen',{exact:true})).toHaveCount(0);
  await page.setViewportSize({width:390,height:844});
  await expect(queues.getByRole('button',{name:'Zur Fallübersicht',exact:true})).toBeVisible();
  await queues.screenshot({path:'test-results/work-queues-technical-mobile.png'});
  expect(await page.evaluate(()=>document.documentElement.scrollWidth<=window.innerWidth)).toBeTruthy();
});
