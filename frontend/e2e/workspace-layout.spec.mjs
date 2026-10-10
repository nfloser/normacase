import {test,expect} from '@playwright/test';

test('shared workspace keeps navigation and three case panels inside a desktop window',async({page})=>{
 await page.setViewportSize({width:1440,height:900});
 await page.goto('/');
 const nav=page.getByRole('navigation',{name:'Arbeitsbereiche'});
 const queues=page.getByRole('region',{name:'Fallwarteschlangen'});
 await queues.getByRole('button',{name:'Fall öffnen: demo-g-supported',exact:true}).click();
 await expect(queues.locator('iframe')).toBeVisible();
 const sizes=await page.evaluate(()=>({height:document.documentElement.scrollHeight,width:document.documentElement.scrollWidth,windowHeight:innerHeight,windowWidth:innerWidth}));
 expect(sizes.height).toBeLessThanOrEqual(sizes.windowHeight);
 expect(sizes.width).toBeLessThanOrEqual(sizes.windowWidth);
 const left=await queues.locator('.case-list-panel').boundingBox();
 const middle=await queues.locator('.document-main-panel').boundingBox();
 const right=await queues.locator('.case-analysis-panel').boundingBox();
 expect(left.x+left.width).toBeLessThanOrEqual(middle.x);
 expect(middle.x+middle.width).toBeLessThanOrEqual(right.x);
 await nav.getByRole('link',{name:'Referenzfälle',exact:true}).click();
 await expect(queues).toBeHidden();
 const references=page.getByRole('region',{name:'Dokumentfälle nach öffentlichen Grundlagen'});
 await references.getByRole('button',{name:'Krankenfahrt: widersprüchliche Nachweise',exact:true}).click();
 await expect(references.getByText(/Widersprüchliche Angaben: Pflegegrad/)).toBeVisible();
 await nav.getByRole('link',{name:'Arbeitslisten',exact:true}).click();
 await expect(queues.locator('iframe')).toHaveAttribute('src',/demo-g-supported/);
 await expect(nav.getByRole('link',{name:'Arbeitslisten',exact:true})).toHaveAttribute('aria-current','page');
 await page.screenshot({path:'test-results/compact-workspace-desktop.png'});
});

test('workspace routes survive reload and keyboard focus stays in the visible view',async({page})=>{
 await page.goto('/#workbench');
 await expect(page.getByLabel('Prüfbereich',{exact:true})).toBeVisible();
 await expect(page.getByRole('region',{name:'Fallwarteschlangen'})).toBeHidden();
 const nav=page.getByRole('navigation',{name:'Arbeitsbereiche'});
 await nav.getByRole('link',{name:'Arbeitslisten',exact:true}).focus();
 await page.keyboard.press('Enter');
 await expect(page.getByRole('region',{name:'Fallwarteschlangen'})).toBeVisible();
 await page.setViewportSize({width:390,height:844});
 await expect(nav).toBeVisible();
 expect(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth)).toBeTruthy();
});
