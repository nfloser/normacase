import { type FormEvent, useEffect, useState } from 'react';
import { parse } from 'lossless-json';
import de from './de.json';
import type { Pack } from './model';
import { DecisionTrace } from './DecisionTrace';
import type { OutputTrace, RuleTrace } from './trace';
import {
  reviewCommandJson,
  reviewFetch,
  type ReviewDetail,
  type ReviewDisposition,
  type ReviewQueues,
  type ReviewSession
} from './review';

const text = de.reviewWorkbench;
const outcomes:Record<string,string> = {SUPPORTED:de.supported,NOT_SUPPORTED:de.notSupported,INCOMPLETE:de.incomplete,HUMAN_REVIEW:de.review,NOT_APPLICABLE:de.na};
type Assessment = {assessment:{outcome:string;ruleTrace?:RuleTrace;domainOutputs?:OutputTrace[];knowledgeRelease:string}};

export function ReviewedCaseWorkQueues({packs}:{packs:Pack[]}) {
  const [available,setAvailable]=useState(false);
  const [probing,setProbing]=useState(true);
  const [credential,setCredential]=useState('');
  const [actor,setActor]=useState('');
  const [queues,setQueues]=useState<ReviewQueues|null>(null);
  const [selected,setSelected]=useState('');
  const [detail,setDetail]=useState<ReviewDetail|null>(null);
  const [reason,setReason]=useState('');
  const [overrideOutcome,setOverrideOutcome]=useState('');
  const [message,setMessage]=useState('');
  const [busy,setBusy]=useState(false);

  useEffect(()=>{
    const controller=new AbortController();
    fetch('/api/review-session',{signal:controller.signal})
      .then(response=>{if(response.status===401)setAvailable(true);})
      .catch(()=>{})
      .finally(()=>{if(!controller.signal.aborted)setProbing(false);});
    return()=>controller.abort();
  },[]);

  function clearSession(nextMessage='') {
    setCredential('');setActor('');setQueues(null);setSelected('');setDetail(null);
    setReason('');setOverrideOutcome('');setMessage(nextMessage);setBusy(false);
  }

  async function loadQueues(activeCredential=credential) {
    const result=await reviewFetch<ReviewQueues>('/api/review/work-queues',activeCredential);
    if(result.kind==='unauthorized'){clearSession(text.loginExpired);return false;}
    if(result.kind==='forbidden'){setMessage(text.forbidden);return false;}
    if(result.kind==='conflict'){setMessage(text.conflict);return false;}
    if(result.kind==='disabled'){setMessage(text.persistenceUnavailable);return false;}
    if(result.kind==='error'){setMessage(de.networkError);return false;}
    setQueues(result.value);return true;
  }

  async function loadDetail(caseId:string,activeCredential=credential) {
    const result=await reviewFetch<ReviewDetail>('/api/review/work-cases/'+encodeURIComponent(caseId),activeCredential);
    if(result.kind==='unauthorized'){clearSession(text.loginExpired);return false;}
    if(result.kind==='forbidden'){setMessage(text.forbidden);return false;}
    if(result.kind==='error'||result.kind==='disabled'){setMessage(de.networkError);return false;}
    if(result.kind==='conflict'){setMessage(text.conflict);return false;}
    setDetail(result.value);setSelected(caseId);setReason('');setOverrideOutcome('');return true;
  }

  async function login(event:FormEvent) {
    event.preventDefault();setMessage('');setBusy(true);
    const activeCredential=credential;
    const result=await reviewFetch<ReviewSession>('/api/review-session',activeCredential);
    if(result.kind==='unauthorized'){clearSession(text.loginInvalid);setAvailable(true);return;}
    if(result.kind!=='ok'){setMessage(result.kind==='forbidden'?text.forbidden:de.networkError);setBusy(false);return;}
    setActor(result.value.actorId);
    await loadQueues(activeCredential);
    setBusy(false);
  }

  async function submit(disposition:ReviewDisposition) {
    if(!detail)return;
    setMessage('');
    let body:string;
    try {body=reviewCommandJson(detail,disposition,reason,overrideOutcome);}
    catch(exception){setMessage(exception instanceof Error&&exception.message==='missing_override'?text.overrideRequired:text.reasonRequired);return;}
    setBusy(true);
    const result=await reviewFetch<ReviewDetail>('/api/review/work-cases/'+encodeURIComponent(detail.caseId)+'/reviews',credential,{
      method:'POST',headers:{'Content-Type':'application/json'},body
    });
    if(result.kind==='unauthorized'){clearSession(text.loginExpired);setAvailable(true);return;}
    if(result.kind==='forbidden'){setMessage(text.forbidden);setBusy(false);return;}
    if(result.kind==='conflict'){
      setDetail(null);
      setMessage(text.conflict);
      await loadQueues();
      await loadDetail(detail.caseId);
      setBusy(false);return;
    }
    if(result.kind!=='ok'){setMessage(de.networkError);setBusy(false);return;}
    await loadQueues();
    await loadDetail(detail.caseId);
    setMessage(text.saved);
    setBusy(false);
  }

  if(probing||!available)return null;
  const assessment=detail?.assessmentJson ? parse(detail.assessmentJson) as Assessment : null;
  const pack=packs.find(item=>item.packId===detail?.packId);
  const queueLabels:Record<string,string>={approval:text.approval,clarification:text.clarification,review:text.reviewQueue,technical:text.technical};
  const stateLabels:Record<string,string>={...de.workQueues.states};
  const dispositionLabels:Record<string,string>={ACCEPT_SYSTEM_RESULT:text.accepted,OVERRIDE:text.overridden};
  const overrideOptions=['SUPPORTED','NOT_SUPPORTED','INCOMPLETE','HUMAN_REVIEW','NOT_APPLICABLE'];

  return <section className="card reviewed-workbench" aria-label={text.heading}>
    <h2>{text.heading}</h2><p>{text.help}</p>
    {!actor?<form onSubmit={login} className="review-login">
      <label className="field">{text.credential}<input type="password" autoComplete="off" value={credential} onChange={event=>setCredential(event.target.value)} required/></label>
      <button className="primary" disabled={busy}>{busy?text.loggingIn:text.login}</button>
    </form>:<>
      <div className="review-session"><p>{text.actor}: <strong>{actor}</strong></p><button type="button" className="text-button" onClick={()=>clearSession()}>{text.logout}</button></div>
      <div className="queue-grid">{queues?.queues.map(queue=><section key={queue.queueId}>
        <h3>{queueLabels[queue.queueId]??de.unknown} ({queue.items.length})</h3>
        <ul>{queue.items.map(item=><li key={item.caseId}><button type="button" className="secondary" aria-pressed={selected===item.caseId} onClick={()=>loadDetail(item.caseId)}>{text.open}: {item.caseId}</button></li>)}</ul>
      </section>)}</div>
      {detail&&<article>
        <h3>{text.case}: {detail.caseId}</h3><p>{stateLabels[detail.stateId]??de.unknown}</p>
        <dl><dt>{de.workQueues.caseRevision}</dt><dd>{detail.caseRevision}</dd><dt>{de.workQueues.processRevision}</dt><dd>{detail.processRevision}</dd><dt>{text.auditRevision}</dt><dd>{detail.auditRevision}</dd></dl>
        {assessment&&<section className="review-system-result"><h4>{text.systemResult}</h4>
          <p className="review-outcome">{outcomes[assessment.assessment.outcome]??de.unknown}</p>
          <p>{de.release}: {assessment.assessment.knowledgeRelease}</p>
          <DecisionTrace rule={assessment.assessment.ruleTrace} outputs={assessment.assessment.domainOutputs} presentation={pack?.presentation}/>
        </section>}
        <section className="review-history"><h4>{text.history}</h4><ol>{detail.audit.map(entry=><li key={entry.sequence}>
          <strong>{entry.kind==='ASSESSMENT_CREATED'?text.assessmentCreated:(dispositionLabels[entry.disposition??'']??de.unknown)}</strong>
          <span>{new Date(entry.occurredAtUtc).toLocaleString('de-DE',{timeZone:'UTC'})} UTC · {entry.actorId}</span>
          {entry.reason&&<p>{entry.reason}</p>}{entry.overrideOutcome&&<p>{text.overrideResult}: {outcomes[entry.overrideOutcome]??de.unknown}</p>}
        </li>)}</ol></section>
        {!!detail.allowedActions.length&&<section className="review-actions"><h4>{text.actions}</h4>
          <label className="field">{text.reason}<textarea value={reason} maxLength={1000} onChange={event=>setReason(event.target.value)} required/></label>
          {detail.allowedActions.includes('OVERRIDE')&&<label className="field">{text.overrideOutcome}<select value={overrideOutcome} onChange={event=>setOverrideOutcome(event.target.value)}><option value="">{text.selectOutcome}</option>{overrideOptions.map(value=><option key={value} value={value}>{outcomes[value]}</option>)}</select></label>}
          <div className="actions">{detail.allowedActions.includes('ACCEPT_SYSTEM_RESULT')&&<button type="button" className="primary" disabled={busy} onClick={()=>submit('ACCEPT_SYSTEM_RESULT')}>{text.accept}</button>}
          {detail.allowedActions.includes('OVERRIDE')&&<button type="button" className="secondary" disabled={busy} onClick={()=>submit('OVERRIDE')}>{text.override}</button>}</div>
        </section>}
      </article>}
    </>}
    {message&&<div className="review-message" role="status">{message}</div>}
  </section>;
}
