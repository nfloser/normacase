import {test,expect} from '@playwright/test';
for(const width of [1366,1920])test(`bookmarks preserve the common case text alignment at ${width}`,async({page})=>{
 await page.setViewportSize({width,height:1080});await page.goto('/#work-queues');
 const explorer=page.getByRole('region',{name:'Fallverwaltung'});
 const rows=explorer.locator('.explorer-table tbody tr');await expect(rows).toHaveCount(22);
 const first=rows.nth(0),second=rows.nth(1);
 const label=first.locator('.case-label');await expect(label).toBeVisible();
 const before=await label.boundingBox();
 expect(Math.abs(before.x-(await second.locator('.case-label').boundingBox()).x)).toBeLessThan(1);
 const set=await first.locator('[aria-label="Lesezeichen gesetzt"]').count()>0;
 await first.click({button:'right'});
 await page.getByRole('menuitem',{name:set?'Lesezeichen entfernen':'Lesezeichen setzen',exact:true}).click();
 if(set)await expect(first.locator('[aria-label="Lesezeichen gesetzt"]')).toHaveCount(0);
 else await expect(first.locator('[aria-label="Lesezeichen gesetzt"]')).toBeVisible();
 const after=await label.boundingBox();expect(Math.abs(after.x-before.x)).toBeLessThan(1);
 expect(Math.abs(after.x-(await second.locator('.case-label').boundingBox()).x)).toBeLessThan(1);
 expect(await rows.evaluateAll(items=>items.every(item=>{const mark=item.querySelector('.case-bookmark-slot'),label=item.querySelector('.case-label');return mark&&label&&mark.getBoundingClientRect().right<=label.getBoundingClientRect().left;}))).toBeTruthy();
 await page.screenshot({path:`test-results/case-row-alignment-${width}.png`});
});
