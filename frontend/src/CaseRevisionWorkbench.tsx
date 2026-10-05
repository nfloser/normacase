import { useEffect, useRef, useState } from 'react';
import { parse, stringify, LosslessNumber } from 'lossless-json';
import de from './de.json';
import { exampleValues, requestJson, type Pack } from './model';
import type { ReviewedCaseDetail } from './ReviewedCaseWorkQueues';
const text=de.caseCorrections;
type Context={canClarify:boolean;clarificationPolicyId:string|null;clarificationPolicyVersion:string|null;clarificationMissingOnly:boolean;canCorrect:boolean;policyId:string|null;policyVersion:string|null;normalizedInputJson:string|null;workCase:ReviewedCaseDetail};
type Entry={version:string;workCase:ReviewedCaseDetail;assessmentRecordJson:string;correctionJson:string|null};
type Page={entries:Entry[];nextPageCursor:string|null};
type Question={clarificationId:string;requestJson:string;status:string;resolvedByCorrectionId:string|null};
type QuestionRecord={caseRevision:LosslessNumber;reason:string;requestedFields:string[];requestedEvidence:string[];actorId:string};
type Questions={entries:Question[];nextPageCursor:string|null};
type Normalized={provenance:{upstreamRevision:LosslessNumber};input:object};
export function CaseRevisionWorkbench({detail,credential,pack,historical,onHistorical,onCurrent,onUnauthorized}:{
 detail:ReviewedCaseDetail;credential:string;pack:Pack|undefined;historical:boolean;
 onHistorical:(value:ReviewedCaseDetail)=>void;onCurrent:()=>void;onUnauthorized:(message:string)=>void;
}){
 const [context,setContext]=useState<Context|null>(null),[entries,setEntries]=useState<Entry[]>([]),[cursor,setCursor]=useState<string|null>(null);
 const [date,setDate]=useState(''),[values,setValues]=useState<Record<string,string>>({}),[evidence,setEvidence]=useState<Record<string,string>>({});
 const [reason,setReason]=useState(''),[busy,setBusy]=useState(false),[error,setError]=useState('');
 const [questions,setQuestions]=useState<Question[]>([]),[questionCursor,setQuestionCursor]=useState<string|null>(null);
 const [clarificationId,setClarificationId]=useState(''),[questionReason,setQuestionReason]=useState('');
 const [requestedFields,setRequestedFields]=useState<string[]>([]),[requestedEvidence,setRequestedEvidence]=useState<string[]>([]);
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
  setError(exception instanceof RevisionHttpError?exception.status===409?text.conflict:exception.status===403?de.reviewedWorkQueues.forbidden:de.inputError:
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
 async function loadQuestions(controller:AbortController,after:string|null=null){
  const page=await request('/clarifications'+(after?'?afterId='+encodeURIComponent(after):''),controller) as Questions;
  if(active(controller)){setQuestions(previous=>after?[...previous,...page.entries]:page.entries);setQuestionCursor(page.nextPageCursor);}
 }
 async function questionPage(after:string|null=null){const controller=begin();try{await loadQuestions(controller,after);}
  catch(exception){fail(exception,controller);}finally{if(active(controller))setBusy(false);}}
 async function ask(){
  if(!context?.canClarify||!questionReason.trim()||!requestedFields.length&&!requestedEvidence.length){setError(de.inputError);return;}
  const controller=begin();
  try{
   const question=await request('/clarifications',controller,{clarificationId:'workbench-question-'+crypto.randomUUID(),
    policyId:context.clarificationPolicyId,policyVersion:context.clarificationPolicyVersion,
    expectedCaseRevision:context.workCase.caseRevision,expectedProcessRevision:context.workCase.processRevision,
    expectedAuditRevision:context.workCase.auditRevision,reason:questionReason,requestedFields,requestedEvidence});
   if(active(controller)){setClarificationId(question.clarificationId);setQuestionReason('');setRequestedFields([]);setRequestedEvidence([]);}
   await loadQuestions(controller);
  }catch(exception){fail(exception,controller,true);}finally{if(active(controller))setBusy(false);}
 }
 function toggle(id:string,values:string[],set:(value:string[])=>void){set(values.includes(id)?values.filter(value=>value!==id):[...values,id]);}
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
    expectedAuditRevision:context.workCase.auditRevision,reason,inputJson,clarificationId:clarificationId||null});
   if(active(controller)){setContext(null);setReason('');onCurrent();}
  }catch(exception){fail(exception,controller,true);}finally{if(active(controller))setBusy(false);}
 }
 const original=context?.normalizedInputJson?exampleValues(stringify((parse(context.normalizedInputJson) as Normalized).input)!):null;
 const eligibleQuestions=questions.filter(question=>question.status==='OPEN'&&(parse(question.requestJson) as {request:QuestionRecord}).request.caseRevision.value===detail.caseRevision);
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
  <h4>{text.clarifications}</h4>
  <button type="button" className="secondary" disabled={busy} onClick={()=>questionPage()}>{text.loadClarifications}</button>
  {questionCursor&&<button type="button" className="secondary" disabled={busy} onClick={()=>questionPage(questionCursor)}>{text.more}</button>}
  <ol>{questions.map(question=>{const record=(parse(question.requestJson) as {request:QuestionRecord}).request;return <li key={question.clarificationId}>
   <strong>{(text.clarificationStatuses as Record<string,string>)[question.status]??de.unknown}</strong>
   <p>{record.reason}</p><p>{record.actorId}</p><ul>
    {record.requestedFields.map(id=><li key={'field:'+id}>{pack?.presentation?.fields[id]??de.fieldReference}</li>)}
    {record.requestedEvidence.map(id=><li key={'evidence:'+id}>{pack?.presentation?.evidence[id]??de.evidenceReference}</li>)}
   </ul></li>;})}</ol>
  {!historical&&context?.canClarify&&pack&&original&&<fieldset disabled={busy}><legend>{text.requestClarification}</legend>
   {pack.fields.filter(field=>!context.clarificationMissingOnly||original.values[field.id]==='UNKNOWN').map(field=>
    <label className="field" key={field.id}><input type="checkbox" checked={requestedFields.includes(field.id)}
     onChange={()=>toggle(field.id,requestedFields,setRequestedFields)} aria-label={text.fieldTarget+': '+(pack.presentation?.fields[field.id]??field.id)}/>
     {text.fieldTarget}: {pack.presentation?.fields[field.id]??de.fieldReference}</label>)}
   {pack.evidenceRequirements.filter(id=>!context.clarificationMissingOnly||original.evidence[id]!=='PRESENT').map(id=>
    <label className="field" key={id}><input type="checkbox" checked={requestedEvidence.includes(id)}
     onChange={()=>toggle(id,requestedEvidence,setRequestedEvidence)} aria-label={text.evidenceTarget+': '+(pack.presentation?.evidence[id]??id)}/>
     {text.evidenceTarget}: {pack.presentation?.evidence[id]??de.evidenceReference}</label>)}
   <label className="field">{text.clarificationReason}<textarea aria-label={text.clarificationReason} value={questionReason} maxLength={1000} onChange={event=>setQuestionReason(event.target.value)}/></label>
   <button type="button" className="secondary" onClick={ask}>{text.sendClarification}</button></fieldset>}
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
   {eligibleQuestions.length>0&&<label className="field">{text.resolveClarification}<select aria-label={text.resolveClarification}
    value={clarificationId} onChange={event=>setClarificationId(event.target.value)}><option value="">{text.noClarification}</option>
    {eligibleQuestions.map(question=><option key={question.clarificationId} value={question.clarificationId}>
     {(parse(question.requestJson) as {request:QuestionRecord}).request.reason}</option>)}</select></label>}
   <label className="field">{text.reason}<textarea aria-label={text.reason} value={reason} maxLength={1000} onChange={event=>setReason(event.target.value)}/></label>
   <button type="button" className="primary" onClick={correct}>{text.submit}</button></fieldset>}
 </section>;
}
class RevisionHttpError extends Error{constructor(readonly status:number){super('revision_request_failed');}}
