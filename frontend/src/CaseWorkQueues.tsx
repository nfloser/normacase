import { useEffect, useRef, useState } from 'react';
import { parse } from 'lossless-json';
import de from './de.json';
import type { Pack } from './model';
import { DecisionTrace } from './DecisionTrace';
import type { RuleTrace, OutputTrace } from './trace';
const text = de.workQueues;
class ApiFailure extends Error {}
type Item = {caseId:string;caseRevision:string;processRevision:string;stateId:string};
type Detail = Item & {queueId:string;packId:string|null;assessmentId:string|null;assessmentJson:string|null;recordedAtUtc:string|null;evidence:Record<string,string>;auditRevision?:string;allowedActions?:string[];auditJson?:string};
type Assessment = {assessment:{outcome:string;ruleTrace?:RuleTrace;domainOutputs?:OutputTrace[];missingRequiredFields:string[];knowledgeRelease:string}};
type Audit = {events:{sequence:{toString():string};occurredAt:string;actorId:string;review?:{disposition:string;reason:string;overrideOutcome?:string}}[]};
const outcomes:Record<string,string> = {SUPPORTED:de.supported,NOT_SUPPORTED:de.notSupported,INCOMPLETE:de.incomplete,HUMAN_REVIEW:de.review,NOT_APPLICABLE:de.na};
export function CaseWorkQueues({packs}:{packs:Pack[]}) {
  const [queues,setQueues]=useState<{queueId:string;items:Item[]}[]>([]);
  const [selected,setSelected]=useState('');
  const [detail,setDetail]=useState<Detail|null>(null);
  const [error,setError]=useState('');
  const [busy,setBusy]=useState(false);
  const [mode,setMode]=useState<boolean|null>(null);
  const [credential,setCredential]=useState('');
  const [token,setToken]=useState('');
  const [reason,setReason]=useState('');
  const [override,setOverride]=useState('NOT_SUPPORTED');
  const [refresh,setRefresh]=useState(0);
  const [saved,setSaved]=useState(false);
  const mutation=useRef<AbortController|null>(null);
  const headers=():Record<string,string>=>token?{Authorization:'Bearer '+token}:{};
  useEffect(()=>{
    const controller=new AbortController();
    fetch('/api/review-mode',{signal:controller.signal}).then(response=>{if(!response.ok)throw new ApiFailure();return response.json();})
      .then(data=>{if(!controller.signal.aborted)setMode(data.enabled===true);})
      .catch(()=>{if(!controller.signal.aborted)setError(de.networkError);});
    return()=>{controller.abort();mutation.current?.abort();};
  },[]);
  useEffect(()=>{
    if(mode===null||(mode&&!token))return;
    const controller=new AbortController();
    fetch('/api/work-queues',{headers:headers(),signal:controller.signal}).then(async response=>{if(!response.ok)throw new ApiFailure((await response.json()).message??de.networkError);return response.json();})
      .then(data=>{if(!controller.signal.aborted)setQueues(data.queues);})
      .catch(exception=>{if(!controller.signal.aborted)setError(exception instanceof ApiFailure?exception.message:de.networkError);});
    return()=>controller.abort();
  },[mode,token,refresh]);
  useEffect(()=>{
    if(!selected||(mode&&!token))return;
    const controller=new AbortController();setBusy(true);setError('');
    fetch('/api/work-cases/'+encodeURIComponent(selected),{headers:headers(),signal:controller.signal})
      .then(async response=>{if(!response.ok)throw new ApiFailure((await response.json()).message??de.networkError);return response.json();})
      .then(data=>{if(!controller.signal.aborted)setDetail(data);})
      .catch(exception=>{if(!controller.signal.aborted)setError(exception instanceof ApiFailure?exception.message:de.networkError);})
      .finally(()=>{if(!controller.signal.aborted)setBusy(false);});
    return()=>controller.abort();
  },[selected,token,refresh]);
  async function login(event:React.FormEvent) {
    event.preventDefault();setError('');setBusy(true);
    const key=credential;setCredential('');
    const controller=new AbortController();mutation.current=controller;
    try {
      const response=await fetch('/api/work-queues',{headers:{Authorization:'Bearer '+key},signal:controller.signal});
      const data=await response.json();
      if(!response.ok)throw new ApiFailure(data.message??de.networkError);
      if(!controller.signal.aborted){setToken(key);setQueues(data.queues);}
    } catch(exception){if(!controller.signal.aborted)setError(exception instanceof ApiFailure?exception.message:de.networkError);}
    finally {if(!controller.signal.aborted)setBusy(false);}
  }
  function logout(){mutation.current?.abort();setToken('');setCredential('');setQueues([]);setDetail(null);setSelected('');setReason('');setError('');setSaved(false);setBusy(false);}
  async function review(disposition:string) {
    if(!detail||!token)return;
    const target=detail;const controller=new AbortController();mutation.current=controller;
    setBusy(true);setError('');setSaved(false);
    try {
      const body=JSON.stringify({assessmentId:target.assessmentId,reviewId:crypto.randomUUID(),caseRevision:target.caseRevision,
        processRevision:target.processRevision,auditRevision:target.auditRevision,disposition,reason,
        overrideOutcome:disposition==='OVERRIDE'?override:null});
      const response=await fetch('/api/work-cases/'+encodeURIComponent(target.caseId)+'/reviews',{method:'POST',headers:{...headers(),'Content-Type':'application/json'},body,signal:controller.signal});
      const data=await response.json();
      if(!response.ok)throw new ApiFailure(data.message??de.networkError);
      if(!controller.signal.aborted){setDetail(data);setReason('');setSaved(true);setRefresh(value=>value+1);}
    } catch(exception){if(!controller.signal.aborted)setError(exception instanceof ApiFailure?exception.message:de.networkError);}
    finally {if(!controller.signal.aborted)setBusy(false);}
  }
  const assessment=detail?.assessmentJson ? parse(detail.assessmentJson) as Assessment : null;
  const audit=detail?.auditJson?parse(detail.auditJson) as Audit:null;
  const candidate=packs.find(pack=>pack.packId===detail?.packId);
  const pack=candidate?.releaseId===assessment?.assessment.knowledgeRelease?candidate:undefined;
  const statuses:Record<string,string>={PRESENT:de.present,MISSING:de.missing,CONFLICTING:de.conflicting};
  return <section className="card work-queues" aria-label={text.heading}>
    <h2>{text.heading}</h2><p>{mode?text.reviewHelp:text.help}</p>
    {mode&&!token&&<form onSubmit={login}><p>{text.loginHelp}</p><label className="field">{text.key}<input type="password" autoComplete="off" minLength={64} maxLength={64} pattern="[a-fA-F0-9]{64}" value={credential} onChange={event=>setCredential(event.target.value)} required/></label><button type="submit" className="primary" disabled={busy}>{text.login}</button></form>}
    {mode&&token&&<button type="button" className="secondary" onClick={logout}>{text.logout}</button>}
    <div className="queue-grid">{queues.map(queue=><section key={queue.queueId}>
      <h3>{(text.queues as Record<string,string>)[queue.queueId]??de.unknown} ({queue.items.length})</h3>
      <ul>{queue.items.map(item=><li key={item.caseId}><button type="button" className="secondary" disabled={busy} aria-pressed={selected===item.caseId} onClick={()=>{if(selected!==item.caseId){mutation.current?.abort();setDetail(null);setReason('');setSaved(false);setSelected(item.caseId);}}}>{text.select}: {item.caseId}</button></li>)}</ul>
    </section>)}</div>
    <div aria-live="polite">{busy&&<p>{text.loading}</p>}{error&&<p role="alert">{error}</p>}{saved&&<p>{text.saved}</p>}
    {detail&&<article><h3>{text.detail}: {detail.caseId}</h3>
      <p>{(text.states as Record<string,string>)[detail.stateId]??de.unknown}</p>
      <dl><dt>{text.caseRevision}</dt><dd>{detail.caseRevision}</dd><dt>{text.processRevision}</dt><dd>{detail.processRevision}</dd></dl>
      {assessment?<><h4>{de.result}: {outcomes[assessment.assessment.outcome]??de.unknown}</h4>
        <dl><dt>{de.release}</dt><dd>{assessment.assessment.knowledgeRelease}</dd><dt>{text.recorded}</dt><dd>{detail.recordedAtUtc&&new Date(detail.recordedAtUtc).toLocaleString('de-DE',{timeZone:'UTC'})} UTC</dd></dl>
        {!!assessment.assessment.missingRequiredFields.length&&<><h4>{de.missingFields}</h4><ul>{assessment.assessment.missingRequiredFields.map(id=><li key={id}>{pack?.presentation?.fields[id]??de.fieldReference}</li>)}</ul></>}
        <h4>{de.evidence}</h4><dl>{Object.entries(detail.evidence).map(([id,status])=><div key={id}><dt>{pack?.presentation?.evidence[id]??de.evidenceReference}</dt><dd>{statuses[status]??de.unknown}</dd></div>)}</dl>
        <DecisionTrace rule={assessment.assessment.ruleTrace} outputs={assessment.assessment.domainOutputs} presentation={pack?.presentation}/>
      </>:<p>{text.noAssessment}</p>}
      {mode&&detail.allowedActions?.length?<form onSubmit={event=>event.preventDefault()}>
        <label className="field">{text.reason}<textarea value={reason} onChange={event=>setReason(event.target.value)} maxLength={2000} required disabled={busy}/></label>
        {detail.allowedActions.includes('ACCEPT_SYSTEM_RESULT')&&<button type="button" className="primary" disabled={busy||!reason.trim()} onClick={()=>review('ACCEPT_SYSTEM_RESULT')}>{text.accept}</button>}
        {detail.allowedActions.includes('OVERRIDE')&&<><label className="field">{text.overrideOutcome}<select value={override} onChange={event=>setOverride(event.target.value)} disabled={busy}>
          {['SUPPORTED','NOT_SUPPORTED','NOT_APPLICABLE'].map(id=><option key={id} value={id}>{outcomes[id]}</option>)}
        </select></label><button type="button" className="secondary" disabled={busy||!reason.trim()} onClick={()=>review('OVERRIDE')}>{text.override}</button></>}
      </form>:null}
      {audit&&<section><h4>{text.audit}</h4><ol>{audit.events.map(event=><li key={event.sequence.toString()}>
        <h5>{event.review?(event.review.disposition==='OVERRIDE'?text.overridden:text.accepted):text.intake}</h5>
        <dl><dt>{text.sequence}</dt><dd>{event.sequence.toString()}</dd><dt>{text.actor}</dt><dd>{event.actorId}</dd><dt>{text.recorded}</dt><dd>{new Date(event.occurredAt).toLocaleString('de-DE',{timeZone:'UTC'})} UTC</dd></dl>
        {event.review&&<><p>{event.review.reason}</p>{event.review.overrideOutcome&&<p>{text.overrideOutcome}: {outcomes[event.review.overrideOutcome]??de.unknown}</p>}</>}
      </li>)}</ol></section>}
      {mode&&<button type="button" className="secondary" disabled={busy} onClick={()=>{setDetail(null);setRefresh(value=>value+1);}}>{text.refresh}</button>}
    </article>}</div>
  </section>;
}
