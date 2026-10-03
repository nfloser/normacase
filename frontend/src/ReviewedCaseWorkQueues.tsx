import { useState } from 'react';
import { parse } from 'lossless-json';
import de from './de.json';
import type { Pack } from './model';
import { DecisionTrace } from './DecisionTrace';
import type { RuleTrace, OutputTrace } from './trace';

const text = de.reviewedWorkQueues;
type Item = {caseId:string;caseRevision:string;processRevision:string;stateId:string;assessmentId:string};
type Queue = {queueId:string;items:Item[]};
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
  const [selected,setSelected]=useState('');
  const [detail,setDetail]=useState<Detail|null>(null);
  const [reason,setReason]=useState('');
  const [overrideOutcome,setOverrideOutcome]=useState('SUPPORTED');
  const [error,setError]=useState('');
  const [notice,setNotice]=useState('');
  const [busy,setBusy]=useState(false);

  function clearReviewForm(){setReason('');setOverrideOutcome('SUPPORTED');}
  function clearSession(message=''){
    setCredential('');setCredentialInput('');setActorId('');setQueues([]);
    setSelected('');setDetail(null);clearReviewForm();setNotice('');setError(message);
  }
  async function message(response:Response,fallback:string){
    try {const data=await response.json() as {message?:string};return data.message??fallback;} catch {return fallback;}
  }
  function headers(token:string, json=false){
    const result:Record<string,string>={Authorization:'Bearer '+token};
    if(json)result['Content-Type']='application/json';
    return result;
  }
  async function loadQueues(token:string){
    const response=await fetch('/api/review/work-queues',{headers:headers(token)});
    if(response.status===401){clearSession(text.authenticationExpired);return false;}
    if(!response.ok){setError(await message(response,de.networkError));return false;}
    const data=await response.json() as {queues:Queue[]};
    setQueues(data.queues);return true;
  }
  async function loadDetail(caseId:string,token=credential){
    setBusy(true);setError('');setNotice('');
    try {
      const response=await fetch('/api/review/work-cases/'+encodeURIComponent(caseId),{headers:headers(token)});
      if(response.status===401){clearSession(text.authenticationExpired);return null;}
      if(!response.ok){setError(await message(response,de.networkError));return null;}
      const next=await response.json() as Detail;
      setDetail(next);setSelected(caseId);clearReviewForm();return next;
    } catch {setError(de.networkError);return null;}
    finally {setBusy(false);}
  }
  async function login(event:React.FormEvent){
    event.preventDefault();setError('');setNotice('');
    const candidate=credentialInput;
    if(!candidate){setError(text.credentialRequired);return;}
    setBusy(true);
    try {
      const response=await fetch('/api/review-session',{headers:headers(candidate)});
      if(response.status===401){clearSession(await message(response,text.authenticationFailed));return;}
      if(response.status===404){clearSession(text.unavailable);return;}
      if(!response.ok){clearSession(await message(response,de.networkError));return;}
      const data=await response.json() as {actorId:string};
      setCredential(candidate);setCredentialInput('');setActorId(data.actorId);
      setSelected('');setDetail(null);clearReviewForm();
      const loaded=await loadQueues(candidate);
      if(loaded)setNotice(text.authenticated);
    } catch {clearSession(de.networkError);}
    finally {setBusy(false);}
  }
  async function submitReview(disposition:'ACCEPT_SYSTEM_RESULT'|'OVERRIDE'){
    if(!detail||!credential)return;
    const trimmed=reason.trim();
    if(!trimmed){setError(text.reasonRequired);return;}
    setBusy(true);setError('');setNotice('');
    const body:Record<string,string>={
      expectedCaseRevision:detail.caseRevision,
      expectedProcessRevision:detail.processRevision,
      expectedAuditRevision:detail.auditRevision,
      disposition,
      reason:trimmed
    };
    if(disposition==='OVERRIDE')body.overrideOutcome=overrideOutcome;
    try {
      const response=await fetch('/api/review/work-cases/'+encodeURIComponent(detail.caseId)+'/reviews',{
        method:'POST',headers:headers(credential,true),body:JSON.stringify(body)
      });
      if(response.status===401){clearSession(text.authenticationExpired);return;}
      if(response.status===409){
        setError(await message(response,text.conflict));
        await loadQueues(credential);
        await loadDetail(detail.caseId,credential);
        return;
      }
      if(response.status===403){setError(await message(response,text.forbidden));return;}
      if(!response.ok){setError(await message(response,de.inputError));return;}
      const committed=await response.json() as Detail;
      setDetail(committed);clearReviewForm();setNotice(text.saved);
      await loadQueues(credential);
    } catch {setError(de.networkError);}
    finally {setBusy(false);}
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
          onChange={event=>setCredentialInput(event.target.value)}
          aria-label={text.credential}/>
      </label>
      {error&&<div className="error" role="alert"><strong>{de.errorHeading}</strong><p>{error}</p></div>}
      <button className="primary" disabled={busy}>{text.login}</button>
    </form>:<>
      <div className="review-session"><p>{text.loggedIn} {actorId}</p>
        <button type="button" className="secondary" onClick={()=>clearSession()}>{text.logout}</button>
      </div>
      {notice&&<p className="review-notice" role="status">{notice}</p>}
      {error&&<div className="error" role="alert"><strong>{de.errorHeading}</strong><p>{error}</p></div>}
      <div className="queue-grid">{queues.map(queue=><section key={queue.queueId}>
        <h3>{(text.queues as Record<string,string>)[queue.queueId]??de.unknown} ({queue.items.length})</h3>
        <ul>{queue.items.map(item=><li key={item.caseId}><button type="button" className="secondary"
          aria-pressed={selected===item.caseId}
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
          {item.overrideOutcome&&<span>{text.overrideResult}: {outcomes[item.overrideOutcome]??item.overrideOutcome}</span>}
        </li>)}</ol>
        {(canAccept||canOverride)&&<div className="review-actions">
          <label className="field">{text.reason}<textarea value={reason}
            onChange={event=>setReason(event.target.value)} aria-label={text.reason}/></label>
          {canOverride&&<label className="field">{text.overrideOutcome}<select aria-label={text.overrideOutcome}
            value={overrideOutcome} onChange={event=>setOverrideOutcome(event.target.value)}>
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
