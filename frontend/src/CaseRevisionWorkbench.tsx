import { useEffect, useRef, useState } from 'react';
import { parse, stringify, LosslessNumber } from 'lossless-json';
import de from './de.json';
import { exampleValues, requestJson, type Pack } from './model';
import type { ReviewedCaseDetail } from './ReviewedCaseWorkQueues';
const text=de.caseCorrections;
type Context={canCorrect:boolean;policyId:string|null;policyVersion:string|null;normalizedInputJson:string|null;workCase:ReviewedCaseDetail};
type Entry={version:string;workCase:ReviewedCaseDetail;assessmentRecordJson:string;correctionJson:string|null};
type Page={entries:Entry[];nextPageCursor:string|null};
type Normalized={provenance:{upstreamRevision:LosslessNumber};input:object};
export function CaseRevisionWorkbench({detail,credential,pack,historical,onHistorical,onCurrent,onUnauthorized}:{
 detail:ReviewedCaseDetail;credential:string;pack:Pack|undefined;historical:boolean;
 onHistorical:(value:ReviewedCaseDetail)=>void;onCurrent:()=>void;onUnauthorized:(message:string)=>void;
}){
 const [context,setContext]=useState<Context|null>(null),[entries,setEntries]=useState<Entry[]>([]),[cursor,setCursor]=useState<string|null>(null);
 const [date,setDate]=useState(''),[values,setValues]=useState<Record<string,string>>({}),[evidence,setEvidence]=useState<Record<string,string>>({});
 const [reason,setReason]=useState(''),[busy,setBusy]=useState(false),[error,setError]=useState('');
 const pending=useRef<AbortController|null>(null);
 function begin(){pending.current?.abort();const controller=new AbortController();pending.current=controller;setBusy(true);setError('');return controller;}
 function active(controller:AbortController){return pending.current===controller&&!controller.signal.aborted;}
 async function request(path:string,controller:AbortController,body?:object){
  const response=await fetch('/api/review/work-cases/'+encodeURIComponent(detail.caseId)+path,{
   signal:controller.signal,cache:'no-store',credentials:'omit',method:body?'POST':'GET',
   headers:{Authorization:'Bearer '+credential,...(body?{'Content-Type':'application/json'}:{})},body:body?JSON.stringify(body):undefined});
  if(!active(controller))throw new Error('cancelled');
  if(!response.ok)throw new RevisionHttpError(response.status);
  const result=await response.json();if(!active(controller))throw new Error('cancelled');return result;
 }
 function fail(exception:unknown,controller:AbortController,mutation=false){
  if(!active(controller))return;
  if(exception instanceof RevisionHttpError&&exception.status===401){onUnauthorized(de.reviewedWorkQueues.authenticationExpired);return;}
  setError(exception instanceof RevisionHttpError?exception.status===409?de.reviewedWorkQueues.conflict:exception.status===403?de.reviewedWorkQueues.forbidden:de.inputError:
   mutation?text.unknownCompletion:de.networkError);
 }
 useEffect(()=>{
  const controller=begin();
  if(!historical)void (async()=>{
   try{const next=await request('/correction-context',controller) as Context;if(active(controller)){
    setContext(next);
    if(next.normalizedInputJson){
     const original=parse(next.normalizedInputJson) as Normalized;
     const input=exampleValues(stringify(original.input)!);setDate(input.date);setValues(input.values);setEvidence(input.evidence);
    }
   }}catch(exception){fail(exception,controller);}finally{if(active(controller))setBusy(false);}
  })();else setBusy(false);
  return()=>{controller.abort();pending.current?.abort();pending.current=null;};
 },[credential,detail.caseId,detail.assessmentId,historical]);
 async function history(after:string|null=null){
  const controller=begin();
  try{const page=await request('/history'+(after?'?afterVersion='+encodeURIComponent(after):''),controller) as Page;
   if(active(controller)){setEntries(previous=>after?[...previous,...page.entries]:page.entries);setCursor(page.nextPageCursor);}
  }catch(exception){fail(exception,controller);}finally{if(active(controller))setBusy(false);}
 }
 async function inspect(version:string){
  const controller=begin();
  try{const entry=await request('/history/'+encodeURIComponent(version),controller) as Entry;if(active(controller))onHistorical(entry.workCase);}
  catch(exception){fail(exception,controller);}finally{if(active(controller))setBusy(false);}
 }
 async function correct(){
  if(!context?.canCorrect||!context.normalizedInputJson||!pack||busy)return;
  let inputJson:string;
  try{if(!reason.trim())throw new Error('reason');inputJson=requestJson(pack,date,values,evidence);}
  catch{setError(de.inputError);return;}
  const normalized=parse(context.normalizedInputJson) as Normalized;
  const upstreamRevision=(BigInt(normalized.provenance.upstreamRevision.value)+1n).toString();
  const controller=begin();
  try{
   await request('/corrections',controller,{correctionId:'workbench-correction-'+crypto.randomUUID(),
    messageId:'workbench-input-'+crypto.randomUUID(),upstreamRevision,policyId:context.policyId,policyVersion:context.policyVersion,
    expectedCaseRevision:context.workCase.caseRevision,expectedProcessRevision:context.workCase.processRevision,
    expectedAuditRevision:context.workCase.auditRevision,reason,inputJson});
   if(active(controller)){setContext(null);setReason('');onCurrent();}
  }catch(exception){fail(exception,controller,true);}finally{if(active(controller))setBusy(false);}
 }
 return <section className="case-revisions" aria-label={text.heading}><h4>{text.heading}</h4><p>{text.help}</p>
  {historical&&<p role="status"><strong>{text.historical}</strong></p>}
  <div className="actions"><button type="button" className="secondary" disabled={busy} onClick={()=>history()}>{text.history}</button>
   <button type="button" className="secondary" disabled={busy} onClick={onCurrent}>{text.current}</button>
   {cursor&&<button type="button" className="secondary" disabled={busy} onClick={()=>history(cursor)}>{text.more}</button>}</div>
  {error&&<p className="error" role="alert">{error}</p>}
  <ol>{entries.map(entry=><li key={entry.version}><button type="button" className="secondary" disabled={busy}
    onClick={()=>inspect(entry.version)}>{text.open}: {de.reviewedWorkQueues.caseRevision} {entry.workCase.caseRevision} · {de.reviewedWorkQueues.processRevision} {entry.workCase.processRevision}</button>
    {entry.correctionJson&&(()=>{const link=parse(entry.correctionJson) as {actorId:string;reason:string};return <p>{text.changedBy}: {link.actorId} · {text.correctionReason}: {link.reason}</p>;})()}
   </li>)}</ol>
  {!historical&&context?.canCorrect&&pack&&<fieldset disabled={busy}><legend>{text.edit}</legend>
   <p>{text.policy}: {(text.policies as Record<string,string>)[context.policyId??'']??de.unknown}</p>
   <label className="field">{de.date}<input type="date" value={date} onChange={event=>setDate(event.target.value)} aria-label={de.date}/></label>
   {pack.fields.map(field=><label className="field" key={field.id}>{pack.presentation?.fields[field.id]??de.fieldReference}
    {field.type==='number'?<input inputMode="decimal" value={values[field.id]??''} onChange={event=>setValues(previous=>({...previous,[field.id]:event.target.value}))} aria-label={pack.presentation?.fields[field.id]??field.id}/>:
     <select value={values[field.id]??'UNKNOWN'} onChange={event=>setValues(previous=>({...previous,[field.id]:event.target.value}))} aria-label={pack.presentation?.fields[field.id]??field.id}>
      <option value="UNKNOWN">{de.unknown}</option><option value="YES">{de.yes}</option><option value="NO">{de.no}</option><option value="NOT_APPLICABLE">{de.na}</option>
     </select>}</label>)}
   {pack.evidenceRequirements.map(id=><label className="field" key={id}>{pack.presentation?.evidence[id]??de.evidenceReference}
    <select aria-label={pack.presentation?.evidence[id]??id} value={evidence[id]??'MISSING'} onChange={event=>setEvidence(previous=>({...previous,[id]:event.target.value}))}>
     <option value="MISSING">{de.missing}</option><option value="PRESENT">{de.present}</option><option value="CONFLICTING">{de.conflicting}</option></select></label>)}
   <label className="field">{text.reason}<textarea aria-label={text.reason} value={reason} maxLength={1000} onChange={event=>setReason(event.target.value)}/></label>
   <button type="button" className="primary" onClick={correct}>{text.submit}</button></fieldset>}
 </section>;
}
class RevisionHttpError extends Error{constructor(readonly status:number){super('revision_request_failed');}}
