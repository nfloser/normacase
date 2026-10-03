import { useEffect, useRef, useState, type FormEvent } from 'react';
import { parse } from 'lossless-json';
import de from './de.json';
import { reviewRequest } from './review';
import type { Pack } from './model';
import { DecisionTrace } from './DecisionTrace';
import type { RuleTrace, OutputTrace } from './trace';

const text = de.reviewedWorkQueues;
type Item = {caseId:string;caseRevision:string;processRevision:string;stateId:string;assessmentId:string};
type Queue = {queueId:string;items:Item[]};
type QueuePage = {queues:Queue[];nextPageCursor?:string|null};
type AuditItem = {sequence:string;kind:string;occurredAtUtc:string;actorId:string;disposition:string|null;reason:string|null;overrideOutcome:string|null};
type Detail = Item & {
  packId:string;
  assessmentJson:string;
  evidence:Record<string,string>;
  allowedActions:string[];
  auditRevision:string;
  audit:AuditItem[];
};
type Assessment = {assessment:{outcome:string;ruleTrace?:RuleTrace;domainOutputs?:OutputTrace[];missingRequiredFields:string[];knowledgeRelease:string}};
const outcomes:Record<string,string> = {
  SUPPORTED:de.supported,NOT_SUPPORTED:de.notSupported,INCOMPLETE:de.incomplete,
  HUMAN_REVIEW:de.review,NOT_APPLICABLE:de.na
};
const statuses:Record<string,string>={PRESENT:de.present,MISSING:de.missing,CONFLICTING:de.conflicting};

export function ReviewedCaseWorkQueues({packs}:{packs:Pack[]}) {
  const [credentialInput,setCredentialInput]=useState('');
  const [credential,setCredential]=useState('');
  const [actorId,setActorId]=useState('');
  const [queues,setQueues]=useState<Queue[]>([]);
  const [nextCursor,setNextCursor]=useState<string|null>(null);
  const [selected,setSelected]=useState('');
  const [detail,setDetail]=useState<Detail|null>(null);
  const [reason,setReason]=useState('');
  const [overrideOutcome,setOverrideOutcome]=useState('');
  const [error,setError]=useState('');
  const [notice,setNotice]=useState('');
  const [busy,setBusy]=useState(false);

  const pending=useRef<AbortController|null>(null);
  useEffect(()=>()=>pending.current?.abort(),[]);
  function begin(){pending.current?.abort();const controller=new AbortController();pending.current=controller;setBusy(true);setError('');setNotice('');return controller;}
  function active(controller:AbortController){return pending.current===controller&&!controller.signal.aborted;}
  function finish(controller:AbortController){if(active(controller))setBusy(false);}
  function clearReviewForm(){setReason('');setOverrideOutcome('');}
  function clearSession(message=''){
    pending.current?.abort();pending.current=null;setBusy(false);
    setCredential('');setCredentialInput('');setActorId('');setQueues([]);setNextCursor(null);
    setSelected('');setDetail(null);clearReviewForm();setNotice('');setError(message);
  }
  async function request(path:string,token:string,controller:AbortController,body?:string){
    const response=await fetch(path,{signal:controller.signal,cache:'no-store',credentials:'omit',
      method:body===undefined?'GET':'POST',headers:{Authorization:'Bearer '+token,...(body===undefined?{}:{'Content-Type':'application/json'})},body});
    if(!active(controller))throw new Error('cancelled');
    if(!response.ok)throw new ReviewHttpError(response.status);
    return response;
  }
  function failure(exception:unknown,controller:AbortController){
    if(!active(controller))return;
    if(exception instanceof ReviewHttpError&&exception.status===401){clearSession(text.authenticationExpired);return;}
    setError(exception instanceof ReviewHttpError
      ? exception.status===403?text.forbidden:exception.status===404?text.unavailable:de.inputError
      : de.networkError);
  }
  async function refresh(caseId:string,token:string,controller:AbortController){
    const queueResponse=await request('/api/review/work-queues',token,controller);
    const nextQueues=await queueResponse.json() as QueuePage;
    const caseResponse=await request('/api/review/work-cases/'+encodeURIComponent(caseId),token,controller);
    const nextDetail=await caseResponse.json() as Detail;
    if(active(controller)){setQueues(nextQueues.queues);setNextCursor(nextQueues.nextPageCursor??null);setDetail(nextDetail);setSelected(caseId);clearReviewForm();}
  }
  async function loadPage(cursor:string|null){
    const controller=begin();setDetail(null);setSelected('');clearReviewForm();
    try{
      const response=await request('/api/review/work-queues'+(cursor?'?afterCaseId='+encodeURIComponent(cursor):''),credential,controller);
      const page=await response.json() as QueuePage;
      if(active(controller)){setQueues(page.queues);setNextCursor(page.nextPageCursor??null);}
    }catch(exception){failure(exception,controller);}
    finally{finish(controller);}
  }
  async function loadDetail(caseId:string){
    const controller=begin();setDetail(null);setSelected(caseId);clearReviewForm();
    try{
      const response=await request('/api/review/work-cases/'+encodeURIComponent(caseId),credential,controller);
      const next=await response.json() as Detail;
      if(active(controller))setDetail(next);
    }catch(exception){failure(exception,controller);}
    finally{finish(controller);}
  }
  async function login(event:FormEvent){
    event.preventDefault();const candidate=credentialInput;setCredentialInput('');
    if(!candidate){setError(text.credentialRequired);return;}
    const controller=begin();
    try{
      const response=await request('/api/review-session',candidate,controller);
      const data=await response.json() as {actorId:string};
      const queueResponse=await request('/api/review/work-queues',candidate,controller);
      const next=await queueResponse.json() as QueuePage;
      if(active(controller)){
        setCredential(candidate);setActorId(data.actorId);setQueues(next.queues);setNextCursor(next.nextPageCursor??null);
        setSelected('');setDetail(null);clearReviewForm();setNotice(text.authenticated);
      }
    }catch(exception){failure(exception,controller);}
    finally{finish(controller);}
  }
  async function submitReview(disposition:'ACCEPT_SYSTEM_RESULT'|'OVERRIDE'){
    if(!detail||!credential||busy)return;
    let body:string;
    try{body=reviewRequest(detail,disposition,reason,overrideOutcome);}
    catch(exception){setError(exception instanceof Error&&exception.message==='override_required'?text.overrideRequired:text.reasonRequired);return;}
    const caseId=detail.caseId;const token=credential;const controller=begin();
    try{
      await request('/api/review/work-cases/'+encodeURIComponent(caseId)+'/reviews',token,controller,body);
      if(active(controller)){setDetail(null);clearReviewForm();}
      await refresh(caseId,token,controller);
      if(active(controller))setNotice(text.saved);
    }catch(exception){
      if(!active(controller))return;
      if(exception instanceof ReviewHttpError&&exception.status===409){
        setDetail(null);clearReviewForm();
        try{await refresh(caseId,token,controller);if(active(controller))setError(text.conflict);}
        catch(reloadError){failure(reloadError,controller);}
      }else{failure(exception,controller);}
    }finally{finish(controller);}
  }

  const assessment=detail?.assessmentJson?parse(detail.assessmentJson) as Assessment:null;
  const pack=packs.find(item=>item.packId===detail?.packId);
  const canAccept=detail?.allowedActions.includes('ACCEPT_SYSTEM_RESULT')??false;
  const canOverride=detail?.allowedActions.includes('OVERRIDE')??false;

  return <section className="card reviewed-work-queues" aria-label={text.heading}>
    <h2>{text.heading}</h2><p>{text.help}</p>
    {!credential?<form className="review-login" onSubmit={login}>
      <label className="field">{text.credential}
        <input type="password" autoComplete="off" value={credentialInput}
          disabled={busy} spellCheck={false} maxLength={44} onChange={event=>setCredentialInput(event.target.value)}
          aria-label={text.credential}/>
      </label>
      {error&&<div className="error" role="alert"><strong>{de.errorHeading}</strong><p>{error}</p></div>}
      <button className="primary" disabled={busy}>{text.login}</button>
      {busy&&<button type="button" className="secondary" onClick={()=>clearSession()}>{text.logout}</button>}
    </form>:<>
      <div className="review-session"><p>{text.loggedIn} {actorId}</p>
        <button type="button" className="secondary" onClick={()=>clearSession()}>{text.logout}</button>
      </div>
      {notice&&<p className="review-notice" role="status">{notice}</p>}
      {error&&<div className="error" role="alert"><strong>{de.errorHeading}</strong><p>{error}</p></div>}
      <div className="actions"><button type="button" className="secondary" disabled={busy} onClick={()=>loadPage(null)}>{text.refreshQueues}</button>
        {nextCursor&&<button type="button" className="secondary" disabled={busy} onClick={()=>loadPage(nextCursor)}>{text.nextPage}</button>}
      </div>
      <p>{text.pageHelp}</p>
      <div className="queue-grid">{queues.map(queue=><section key={queue.queueId}>
        <h3>{(text.queues as Record<string,string>)[queue.queueId]??de.unknown} ({queue.items.length})</h3>
        <ul>{queue.items.map(item=><li key={item.caseId}><button type="button" className="secondary"
          disabled={busy} aria-pressed={selected===item.caseId}
          onClick={()=>loadDetail(item.caseId)}>{text.select}: {item.caseId}</button></li>)}</ul>
      </section>)}</div>
      {busy&&<p>{text.loading}</p>}
      {detail&&<article><h3>{text.detail}: {detail.caseId}</h3>
        <p>{(text.states as Record<string,string>)[detail.stateId]??de.unknown}</p>
        <dl><dt>{text.caseRevision}</dt><dd>{detail.caseRevision}</dd>
          <dt>{text.processRevision}</dt><dd>{detail.processRevision}</dd>
          <dt>{text.auditRevision}</dt><dd>{detail.auditRevision}</dd></dl>
        {assessment&&<><h4>{de.result}: {outcomes[assessment.assessment.outcome]??de.unknown}</h4>
          <dl><dt>{de.release}</dt><dd>{assessment.assessment.knowledgeRelease}</dd></dl>
          {!!assessment.assessment.missingRequiredFields.length&&<><h4>{de.missingFields}</h4>
            <ul>{assessment.assessment.missingRequiredFields.map(id=><li key={id}>{pack?.presentation?.fields[id]??de.fieldReference}</li>)}</ul></>}
          <h4>{de.evidence}</h4><dl>{Object.entries(detail.evidence).map(([id,status])=><div key={id}>
            <dt>{pack?.presentation?.evidence[id]??de.evidenceReference}</dt><dd>{statuses[status]??de.unknown}</dd></div>)}</dl>
          <DecisionTrace rule={assessment.assessment.ruleTrace} outputs={assessment.assessment.domainOutputs} presentation={pack?.presentation}/>
        </>}
        <h4>{text.audit}</h4><ol className="review-audit">{detail.audit.map(item=><li key={item.sequence}>
          <strong>{item.kind==='ASSESSMENT_CREATED'?text.auditCreated:text.auditReviewed}</strong>
          <span>{item.actorId}</span><span>{new Date(item.occurredAtUtc).toLocaleString('de-DE',{timeZone:'UTC'})} UTC</span>
          {item.disposition&&<span>{item.disposition==='ACCEPT_SYSTEM_RESULT'?text.accepted:text.overridden}</span>}
          {item.reason&&<span>{item.reason}</span>}
          {item.overrideOutcome&&<span>{text.overrideResult}: {outcomes[item.overrideOutcome]??de.unknown}</span>}
        </li>)}</ol>
        {(canAccept||canOverride)&&<div className="review-actions">
          <label className="field">{text.reason}<textarea value={reason} disabled={busy} maxLength={1000}
            onChange={event=>setReason(event.target.value)} aria-label={text.reason}/></label>
          {canOverride&&<label className="field">{text.overrideOutcome}<select aria-label={text.overrideOutcome}
            disabled={busy} value={overrideOutcome} onChange={event=>setOverrideOutcome(event.target.value)}>
            <option value="">{text.chooseOutcome}</option>
            {Object.entries(outcomes).map(([value,label])=><option key={value} value={value}>{label}</option>)}
          </select></label>}
          <div className="actions">
            {canAccept&&<button type="button" className="primary" disabled={busy}
              onClick={()=>submitReview('ACCEPT_SYSTEM_RESULT')}>{text.accept}</button>}
            {canOverride&&<button type="button" className="secondary" disabled={busy}
              onClick={()=>submitReview('OVERRIDE')}>{text.override}</button>}
          </div>
        </div>}
      </article>}
    </>}
  </section>;
}

class ReviewHttpError extends Error {
  constructor(readonly status:number){super('review_request_failed');}
}
