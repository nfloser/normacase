import {useEffect,useRef,useState} from 'react';
import de from './de.json';
import {entitlementDecisionRequest,entitlementProposalRequest,
  type EntitlementChange,type EntitlementContext} from './entitlementAdministrationRequest';

const text=de.entitlementAdministration;
type Props={credential:string;canPropose:boolean;canDecide:boolean;onUnauthorized:(message:string)=>void};

export function EntitlementAdministration({credential,canPropose,canDecide,onUnauthorized}:Props){
  const [identities,setIdentities]=useState<EntitlementContext[]>([]);const [pending,setPending]=useState<EntitlementChange[]>([]);
  const [selected,setSelected]=useState('');const [actions,setActions]=useState<string[]>([]);const [caseIds,setCaseIds]=useState<string[]>([]);
  const [reason,setReason]=useState('');const [decisionReasons,setDecisionReasons]=useState<Record<string,string>>({});
  const [error,setError]=useState('');const [notice,setNotice]=useState('');const [busy,setBusy]=useState(false);
  const controller=useRef<AbortController|null>(null);
  useEffect(()=>{void refresh();return()=>controller.current?.abort();},[credential,canPropose,canDecide]);
  async function call(path:string,body?:string){const response=await fetch(path,{signal:controller.current?.signal,cache:'no-store',credentials:'omit',
    method:body===undefined?'GET':'POST',headers:{Authorization:'Bearer '+credential,...(body===undefined?{}:{'Content-Type':'application/json'})},body});
    if(!response.ok)throw new EntitlementHttpError(response.status);return response;}
  function fail(exception:unknown){if(exception instanceof DOMException&&exception.name==='AbortError')return;
    if(exception instanceof EntitlementHttpError&&exception.status===401){onUnauthorized(text.authenticationExpired);return;}
    setError(exception instanceof EntitlementHttpError?(exception.status===403?text.forbidden:exception.status===409?text.conflict:de.inputError):de.networkError);}
  async function refresh(){controller.current?.abort();controller.current=new AbortController();setBusy(true);setError('');
    try{if(canPropose){const result=await (await call('/api/review/administration/entitlement-changes/context')).json() as {identities:EntitlementContext[]};
      setIdentities(result.identities);if(result.identities.length&&!selected)choose(result.identities[0]);}
      if(canDecide){const result=await (await call('/api/review/administration/entitlement-changes/pending')).json() as {changes:EntitlementChange[]};setPending(result.changes);}}
    catch(exception){fail(exception);}finally{setBusy(false);}}
  function choose(identity:EntitlementContext){setSelected(identity.actorId);setActions(identity.actions);setCaseIds(identity.caseIds);setReason('');}
  function toggle(value:string,values:string[],update:(next:string[])=>void){update(values.includes(value)?values.filter(item=>item!==value):[...values,value]);}
  async function propose(){const identity=identities.find(item=>item.actorId===selected);if(!identity)return;
    let body:string;try{body=entitlementProposalRequest(identity,actions,caseIds,reason,'entitlement-'+crypto.randomUUID());}
    catch{setError(text.reasonRequired);return;}setBusy(true);setError('');
    try{await call('/api/review/administration/entitlement-changes/',body);setReason('');setNotice(text.proposed);await refresh();}
    catch(exception){fail(exception);}finally{setBusy(false);}}
  async function decide(change:EntitlementChange,approved:boolean){let body:string;
    try{body=entitlementDecisionRequest(approved,decisionReasons[change.changeId]??'');}catch{setError(text.reasonRequired);return;}
    setBusy(true);setError('');try{await call('/api/review/administration/entitlement-changes/'+encodeURIComponent(change.changeId)+'/decision',body);
      setNotice(approved?text.approved:text.rejected);setDecisionReasons(values=>({...values,[change.changeId]:''}));await refresh();}
    catch(exception){fail(exception);}finally{setBusy(false);}}
  const identity=identities.find(item=>item.actorId===selected);
  return <section className="entitlement-administration" aria-label={text.heading}><h3>{text.heading}</h3><p>{text.help}</p>
    <p className="batch-warning">{text.notLive}</p>{notice&&<p className="review-notice" role="status">{notice}</p>}
    {error&&<div className="error" role="alert"><strong>{de.errorHeading}</strong><p>{error}</p></div>}
    {canPropose&&<article><h4>{text.proposeHeading}</h4><label className="field">{text.identity}<select value={selected}
      onChange={event=>{const next=identities.find(item=>item.actorId===event.target.value);if(next)choose(next);}}>
      {identities.map(item=><option key={item.actorId}>{item.actorId}</option>)}</select></label>
      {identity&&<><p>{text.revision}: {identity.effectiveRevision}</p><fieldset><legend>{text.actions}</legend>
        {['READ','ACCEPT','OVERRIDE','EXPORT','INTAKE','BATCH','CORRECT','CLARIFY'].map(action=><label key={action}><input type="checkbox" checked={actions.includes(action)}
          onChange={()=>toggle(action,actions,setActions)}/>{(text.actionLabels as Record<string,string>)[action]}</label>)}</fieldset>
        <fieldset><legend>{text.cases}</legend>{identity.availableCaseIds.map(caseId=><label key={caseId}><input type="checkbox" checked={caseIds.includes(caseId)}
          onChange={()=>toggle(caseId,caseIds,setCaseIds)}/>{caseId}</label>)}</fieldset>
        <label className="field">{text.reason}<textarea value={reason} maxLength={1000} onChange={event=>setReason(event.target.value)}/></label>
        <button type="button" className="primary" disabled={busy} onClick={propose}>{text.propose}</button></>}</article>}
    {canDecide&&<article><h4>{text.pendingHeading}</h4>{!pending.length&&<p>{text.nonePending}</p>}<ol className="review-audit">
      {pending.map(change=><li key={change.changeId}><strong>{change.targetActorId}</strong><span>{text.requestedBy}: {change.proposerActorId}</span>
        <span>{text.actions}: {change.actions.join(', ')||text.none}</span><span>{text.cases}: {change.caseIds.join(', ')||text.none}</span><span>{change.reason}</span>
        <label className="field">{text.decisionReason}<textarea maxLength={1000} value={decisionReasons[change.changeId]??''}
          onChange={event=>setDecisionReasons(values=>({...values,[change.changeId]:event.target.value}))}/></label><div className="actions">
          <button type="button" className="primary" disabled={busy} onClick={()=>decide(change,true)}>{text.approve}</button>
          <button type="button" className="secondary" disabled={busy} onClick={()=>decide(change,false)}>{text.reject}</button></div></li>)}</ol></article>}
  </section>;
}
class EntitlementHttpError extends Error{constructor(readonly status:number){super('entitlement_administration_failed');}}
