import { useEffect, useRef, useState } from 'react';
import type { Pack } from './model';
import de from './de.json';
const text=de.knowledgeAdministration;
type Change={changeId:string;packId:string;releaseId:string;sha256:string;sourceReference:string;impactReference:string;testReference:string;proposerActorId:string;proposedAtUtc:string;decision:null|{reviewerActorId:string;reviewedAtUtc:string;approved:boolean;reason:string}};
type Active={revision:string;releaseId:string;sha256:string;actorId:string;activatedAtUtc:string};
type Evidence={evidenceId:string;kind:string;title:string;content:string;sha256:string;recordedByActorId:string;recordedAtUtc:string};
type Release={packId:string;releaseId:string;sha256:string;lifecycleStatus:string;validationLevel:string};
type ReleaseCursor={packId:string;releaseId:string};
type ReleaseDetail=Release&{packJson:string;import:null|{importedByActorId:string;importedAtUtc:string}};
const releaseKey=(release:{packId:string;releaseId:string})=>JSON.stringify([release.packId,release.releaseId]);
type Detail={change:Change;active:Active|null;evidence:Evidence[];retainedEvidenceComplete:boolean};
export function KnowledgeAdministration({credential,actions,packs,onUnauthorized}:{credential:string;actions:string[];packs:Pack[];onUnauthorized:(message?:string)=>void}){
 const [changes,setChanges]=useState<Change[]>([]),[cursor,setCursor]=useState<string|null>(null),[detail,setDetail]=useState<Detail|null>(null);
 const [packId,setPackId]=useState(packs[0]?.packId??''),[releaseId,setReleaseId]=useState(packs[0]?.releaseId??''),[source,setSource]=useState(''),[impact,setImpact]=useState(''),[tests,setTests]=useState(''),[reason,setReason]=useState('');
 const [sourceContent,setSourceContent]=useState(''),[impactContent,setImpactContent]=useState(''),[testContent,setTestContent]=useState('');
 const [releases,setReleases]=useState<Release[]>([]),[releaseCursor,setReleaseCursor]=useState<ReleaseCursor|null>(null),[releaseDetail,setReleaseDetail]=useState<ReleaseDetail|null>(null);
 const [busy,setBusy]=useState(false),[error,setError]=useState(''),[notice,setNotice]=useState('');
 const pending=useRef<AbortController|null>(null);
 function begin(){pending.current?.abort();const controller=new AbortController();pending.current=controller;setBusy(true);setError('');setNotice('');return controller;}
 function active(controller:AbortController){return pending.current===controller&&!controller.signal.aborted;}
 async function request(path:string,controller:AbortController,body?:object){
  const response=await fetch('/api/review/knowledge'+path,{signal:controller.signal,cache:'no-store',credentials:'omit',method:body?'POST':'GET',headers:{Authorization:'Bearer '+credential,...(body?{'Content-Type':'application/json'}:{})},body:body?JSON.stringify(body):undefined});
  if(!active(controller))throw new Error('cancelled');
  if(!response.ok)throw new KnowledgeHttpError(response.status);
  const result=await response.json();
  if(!active(controller))throw new Error('cancelled');
  return result;
 }
 function failure(exception:unknown,controller:AbortController){
  if(!active(controller))return;
  if(exception instanceof KnowledgeHttpError&&exception.status===401){onUnauthorized(de.reviewedWorkQueues.authenticationExpired);return;}
  setError(exception instanceof KnowledgeHttpError?exception.status===403?de.reviewedWorkQueues.forbidden:exception.status===409?text.conflict:de.inputError:de.networkError);
 }
 async function list(after:string|null=null){
  const controller=begin();setDetail(null);setReason('');
  try{const page=await request('/changes'+(after?'?afterChangeId='+encodeURIComponent(after):''),controller);
   if(active(controller)){setChanges(page.changes);setCursor(page.nextPageCursor);}
   if(!after)await loadReleases(controller);
  }
  catch(exception){failure(exception,controller);}finally{if(active(controller))setBusy(false);}
 }
 async function loadReleases(controller:AbortController,after:ReleaseCursor|null=null){
  const query=after?'?afterPackId='+encodeURIComponent(after.packId)+'&afterReleaseId='+encodeURIComponent(after.releaseId):'';
  const page=await request('/releases'+query,controller);
  if(active(controller)){
   setReleases(previous=>after?[...previous,...page.releases.filter((item:Release)=>!previous.some(known=>releaseKey(known)===releaseKey(item)))]:page.releases);
   setReleaseCursor(page.nextPageCursor);
   if(!after&&!page.releases.some((item:Release)=>item.packId===packId&&item.releaseId===releaseId)&&page.releases.length){setPackId(page.releases[0].packId);setReleaseId(page.releases[0].releaseId);setReleaseDetail(null);}
  }
 }
 async function moreReleases(){const controller=begin();try{await loadReleases(controller,releaseCursor);}catch(exception){failure(exception,controller);}finally{if(active(controller))setBusy(false);}}
 async function inspectRelease(){
  const controller=begin();setReleaseDetail(null);
  try{const result=await request('/release?packId='+encodeURIComponent(packId)+'&releaseId='+encodeURIComponent(releaseId),controller);if(active(controller))setReleaseDetail(result);}
  catch(exception){failure(exception,controller);}finally{if(active(controller))setBusy(false);}
 }
 async function importRelease(file:File){
  const controller=begin();setReleaseDetail(null);
  try{
   if(file.size>65536)throw new KnowledgeFileError();
   const bytes=await file.arrayBuffer();if(!active(controller))return;
   let packJson:string;try{packJson=new TextDecoder('utf-8',{fatal:true,ignoreBOM:true}).decode(bytes);}catch{throw new KnowledgeFileError();}
   const imported=await request('/releases',controller,{packJson});
   if(active(controller)){
    setReleases(previous=>[imported,...previous.filter(item=>releaseKey(item)!==releaseKey(imported))]);
    setPackId(imported.packId);setReleaseId(imported.releaseId);setReleaseDetail(imported);setNotice(text.imported);
   }
  }catch(exception){if(active(controller)&&exception instanceof KnowledgeFileError){setError(text.invalidImport);}else failure(exception,controller);}
  finally{if(active(controller))setBusy(false);}
 }
 async function open(id:string){
  const controller=begin();setDetail(null);setReason('');
  try{const next=await request('/changes/'+encodeURIComponent(id),controller);if(active(controller))setDetail(next);}
  catch(exception){failure(exception,controller);}finally{if(active(controller))setBusy(false);}
 }
 useEffect(()=>{setChanges([]);setReleases([]);setReleaseDetail(null);void list();return()=>{pending.current?.abort();pending.current=null;};},[credential]);
 async function propose(){
  const pack=releases.find(item=>item.packId===packId&&item.releaseId===releaseId);
  if(!pack||![source,impact,tests,sourceContent,impactContent,testContent].every(value=>value.trim())){setError(de.inputError);return;}
  const controller=begin();
  try{
   const artifacts:Evidence[]=[];
   for(const [kind,title,content] of [['SOURCE',source,sourceContent],['IMPACT',impact,impactContent],['TESTS',tests,testContent]])
    artifacts.push(await request('/evidence',controller,{kind,title,content}));
   const created=await request('/changes',controller,{packId,releaseId:pack.releaseId,sourceReference:artifacts[0].evidenceId,impactReference:artifacts[1].evidenceId,testReference:artifacts[2].evidenceId});
   if(active(controller)){setSource('');setImpact('');setTests('');setSourceContent('');setImpactContent('');setTestContent('');setChanges(items=>[created,...items].slice(0,25));setDetail({change:created,active:null,evidence:artifacts,retainedEvidenceComplete:true});
    const loaded=await request('/changes/'+encodeURIComponent(created.changeId),controller);if(active(controller)){setDetail(loaded);setNotice(text.saved);}}
  }catch(exception){failure(exception,controller);}finally{if(active(controller))setBusy(false);}
 }
 async function mutate(kind:'approve'|'reject'|'activate'){
  if(!detail)return;
  if(kind!=='activate'&&!reason.trim()){setError(de.reviewedWorkQueues.reasonRequired);return;}
  const id=detail.change.changeId,controller=begin();
  try{await request('/changes/'+encodeURIComponent(id)+(kind==='activate'?'/activate':'/decision'),controller,
    kind==='activate'?{expectedRevision:detail.active?.revision??'0'}:{approved:kind==='approve',reason});
   const next=await request('/changes/'+encodeURIComponent(id),controller);if(active(controller)){setDetail(next);setReason('');setNotice(text.saved);}
  }catch(exception){
   if(active(controller)&&exception instanceof KnowledgeHttpError&&exception.status===409){
    setDetail(null);setReason('');try{const next=await request('/changes/'+encodeURIComponent(id),controller);if(active(controller)){setDetail(next);setError(text.conflict);}}catch(reload){failure(reload,controller);}
   }else failure(exception,controller);
  }finally{if(active(controller))setBusy(false);}
 }
 function state(change:Change){return change.decision?change.decision.approved?text.approved:text.rejected:text.pending;}
 const date=(value:string)=>new Date(value).toLocaleString('de-DE',{timeZone:'UTC'})+' UTC';
 return <section className="knowledge-administration" aria-label={text.heading}>
  <h3>{text.heading}</h3><p>{text.help}</p>
  {error&&<div className="error" role="alert">{error}</div>}{notice&&<p role="status">{notice}</p>}
  <fieldset disabled={busy}><legend>{text.retainedReleases}</legend>
   <label className="field">{text.pack}<select aria-label={text.pack} value={releaseKey({packId,releaseId})} onChange={event=>{const [pack,release]=JSON.parse(event.target.value) as string[];setPackId(pack);setReleaseId(release);setReleaseDetail(null);}}>{releases.map(item=><option key={releaseKey(item)} value={releaseKey(item)}>{packs.find(pack=>pack.packId===item.packId)?.presentation?.title??item.packId} — {item.releaseId}</option>)}</select></label>
   <button type="button" className="secondary" disabled={!releases.length} onClick={inspectRelease}>{text.inspectRelease}</button>
   {releaseCursor&&<button type="button" className="secondary" onClick={moreReleases}>{text.moreReleases}</button>}
  </fieldset>
  {releaseDetail&&<details open><summary>{text.exactRelease}: {releaseDetail.packId} / {releaseDetail.releaseId}</summary>
   <dl><dt>{text.hash}</dt><dd>{releaseDetail.sha256}</dd><dt>{text.importedBy}</dt><dd>{releaseDetail.import?releaseDetail.import.importedByActorId+' · '+date(releaseDetail.import.importedAtUtc):text.installedRelease}</dd></dl>
   <pre className="knowledge-release-content">{releaseDetail.packJson}</pre></details>}
  {actions.includes('PROPOSE')&&<fieldset disabled={busy}><legend>{text.proposal}</legend>
   <label className="field">{text.importFile}<input aria-label={text.importFile} type="file" accept=".json,application/json" onChange={event=>{const file=event.target.files?.[0];event.target.value='';if(file)void importRelease(file);}}/></label>
   <p>{text.importHelp}</p>
   <label className="field">{text.source}<input aria-label={text.source} maxLength={256} value={source} onChange={event=>setSource(event.target.value)}/></label>
   <label className="field">{text.impact}<input aria-label={text.impact} maxLength={256} value={impact} onChange={event=>setImpact(event.target.value)}/></label>
   <label className="field">{text.tests}<input aria-label={text.tests} maxLength={256} value={tests} onChange={event=>setTests(event.target.value)}/></label>
   <label className="field">{text.sourceContent}<textarea aria-label={text.sourceContent} maxLength={65536} value={sourceContent} onChange={event=>setSourceContent(event.target.value)}/></label>
   <label className="field">{text.impactContent}<textarea aria-label={text.impactContent} maxLength={65536} value={impactContent} onChange={event=>setImpactContent(event.target.value)}/></label>
   <label className="field">{text.testContent}<textarea aria-label={text.testContent} maxLength={65536} value={testContent} onChange={event=>setTestContent(event.target.value)}/></label>
   <p>{text.evidenceHelp}</p>
   <button type="button" className="primary" onClick={propose}>{text.propose}</button></fieldset>}
  <div className="actions"><button type="button" className="secondary" disabled={busy} onClick={()=>list()}>{text.refresh}</button>
   {cursor&&<button type="button" className="secondary" disabled={busy} onClick={()=>list(cursor)}>{text.next}</button>}</div>
  {busy&&<p>{de.reviewedWorkQueues.loading}</p>}
  <ul>{changes.map(change=><li key={change.changeId}><button type="button" className="secondary" disabled={busy} onClick={()=>open(change.changeId)}>{text.open}: {change.changeId}</button> <span>{state(change)}</span></li>)}</ul>
  {!busy&&!changes.length&&<p>{text.empty}</p>}
  {detail&&<article><h4>{text.detail}: {detail.change.changeId}</h4>
   <dl><dt>{text.pack}</dt><dd>{detail.change.packId} / {detail.change.releaseId}</dd><dt>{text.hash}</dt><dd>{detail.change.sha256}</dd>
    <dt>{text.status}</dt><dd>{state(detail.change)}</dd><dt>{text.source}</dt><dd>{detail.change.sourceReference}</dd><dt>{text.impact}</dt><dd>{detail.change.impactReference}</dd><dt>{text.tests}</dt><dd>{detail.change.testReference}</dd>
    <dt>{text.proposedBy}</dt><dd>{detail.change.proposerActorId} · {date(detail.change.proposedAtUtc)}</dd>
    {detail.change.decision&&<><dt>{text.reviewedBy}</dt><dd>{detail.change.decision.reviewerActorId} · {date(detail.change.decision.reviewedAtUtc)}</dd><dt>{text.reason}</dt><dd>{detail.change.decision.reason}</dd></>}
    <dt>{text.activeRevision}</dt><dd>{detail.active?.revision??'0'}</dd>{detail.active&&<><dt>{text.activeRelease}</dt><dd>{detail.active.releaseId}</dd><dt>{text.activatedBy}</dt><dd>{detail.active.actorId} · {date(detail.active.activatedAtUtc)}</dd></>}
   </dl>
   <h4>{text.retainedEvidence}</h4>
   {!detail.retainedEvidenceComplete&&<p>{text.legacyEvidence}</p>}
   {detail.evidence.map(artifact=><details key={artifact.evidenceId}><summary>{(text.evidenceKinds as Record<string,string>)[artifact.kind]??de.unknown}: {artifact.title}</summary>
    <dl><dt>{text.hash}</dt><dd>{artifact.sha256}</dd><dt>{text.recordedBy}</dt><dd>{artifact.recordedByActorId} · {date(artifact.recordedAtUtc)}</dd></dl>
    <pre className="knowledge-evidence-content">{artifact.content}</pre></details>)}
   {!detail.change.decision&&detail.retainedEvidenceComplete&&actions.includes('REVIEW')&&<fieldset disabled={busy}><legend>{text.review}</legend><label className="field">{text.reason}<textarea aria-label={text.reason} maxLength={1000} value={reason} onChange={event=>setReason(event.target.value)}/></label>
    <button type="button" className="primary" onClick={()=>mutate('approve')}>{text.approve}</button><button type="button" className="secondary" onClick={()=>mutate('reject')}>{text.reject}</button></fieldset>}
   {detail.change.decision?.approved&&detail.retainedEvidenceComplete&&actions.includes('ACTIVATE')&&<button type="button" className="primary" disabled={busy} onClick={()=>mutate('activate')}>{text.activate}</button>}
  </article>}
 </section>;
}
class KnowledgeHttpError extends Error{constructor(readonly status:number){super('knowledge_request_failed');}}

class KnowledgeFileError extends Error{}
