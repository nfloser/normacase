// Capture the exact previous source tree; this does not mock clinical API results.
import {spawn} from 'node:child_process';
import {mkdir} from 'node:fs/promises';
import {resolve} from 'node:path';
import {chromium} from '@playwright/test';
const root=resolve(process.argv[2]);
const server=spawn('dotnet',[resolve(root,'src/NormaCase.Api/bin/Release/net10.0/NormaCase.Api.dll')],{cwd:root,stdio:'ignore'});
let browser;
try{
 let ready=false;
 for(let attempt=0;attempt<100;attempt++){
  try{ready=(await fetch('http://localhost:5080/api/packs')).ok;}catch{}
  if(ready)break;if(server.exitCode!==null)throw new Error('Previous API exited');
  await new Promise(resolve=>setTimeout(resolve,200));
 }
 if(!ready)throw new Error('Previous API not ready');
 await mkdir('test-results/before',{recursive:true});browser=await chromium.launch();
 for(const width of [1920,1366]){
  const page=await browser.newPage({viewport:{width,height:width===1920?1080:768}});
  await page.goto('http://localhost:5080/#work-queues');
  await page.getByRole('button',{name:'Fall öffnen: reference-care-complete',exact:true}).waitFor();
  await page.screenshot({path:`test-results/before/overview-${width}.png`});
  await page.getByRole('button',{name:'Fall öffnen: reference-care-complete',exact:true}).click();
  await page.getByRole('heading',{name:'Prüfergebnis: Voraussetzungen erfüllt',exact:true}).waitFor();
  await page.screenshot({path:`test-results/before/result-${width}.png`});
  await page.getByRole('button',{name:'Dokumente',exact:true}).click();
  await page.locator('.pdf-page-image').waitFor();
  await page.screenshot({path:`test-results/before/documents-${width}.png`});await page.close();
 }
}finally{
 await browser?.close();server.kill('SIGTERM');
 if(server.exitCode===null)await new Promise(resolve=>server.once('exit',resolve));
}
