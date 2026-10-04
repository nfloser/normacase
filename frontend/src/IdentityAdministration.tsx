import {useEffect,useRef,useState} from 'react';
import de from './de.json';
import {identityAccessChangeRequest,type IdentityAccessState} from './identityAdministration';

const text=de.identityAdministration;
type AuditEntry=IdentityAccessState&{administratorActorId:string;changedAtUtc:string;reason:string};
type Detail={state:IdentityAccessState;history:AuditEntry[]};
type Props={credential:string;onUnauthorized:(message:string)=>void};

export function IdentityAdministration({credential,onUnauthorized}:Props){
  const [identities,setIdentities]=useState<IdentityAccessState[]>([]);
  const [detail,setDetail]=useState<Detail|null>(null);
  const [reason,setReason]=useState('');
  const [notice,setNotice]=useState('');
  const [error,setError]=useState('');
  const [busy,setBusy]=useState(false);
  const pending=useRef<AbortController|null>(null);

  useEffect(()=>{const controller=begin();loadIdentities(controller).finally(()=>finish(controller));return()=>pending.current?.abort();},[credential]);
  function begin(){pending.current?.abort();const controller=new AbortController();pending.current=controller;setBusy(true);setError('');setNotice('');return controller;}
  function active(controller:AbortController){return pending.current===controller&&!controller.signal.aborted;}
  function finish(controller:AbortController){if(active(controller))setBusy(false);}
  async function request(path:string,controller:AbortController,body?:string){
    const response=await fetch(path,{signal:controller.signal,cache:'no-store',credentials:'omit',method:body===undefined?'GET':'POST',
      headers:{Authorization:'Bearer '+credential,...(body===undefined?{}:{'Content-Type':'application/json'})},body});
    if(!active(controller))throw new Error('cancelled');
    if(!response.ok)throw new AdministrationHttpError(response.status);
    return response;
  }
  function failure(exception:unknown,controller:AbortController){
    if(!active(controller))return;
    if(exception instanceof AdministrationHttpError&&exception.status===401){onUnauthorized(text.authenticationExpired);return;}
    setError(exception instanceof AdministrationHttpError
      ? exception.status===403?text.forbidden:exception.status===404?text.unknownIdentity:de.inputError
      : de.networkError);
  }
  async function loadIdentities(controller:AbortController){
    try{
      const response=await request('/api/review/administration/identities',controller);
      const result=await response.json() as {identities:IdentityAccessState[]};
      if(active(controller))setIdentities(result.identities);
    }catch(exception){failure(exception,controller);}
  }
  async function openIdentity(actorId:string){
    const controller=begin();setDetail(null);setReason('');
    try{await loadDetail(actorId,controller);}catch(exception){failure(exception,controller);}
    finally{finish(controller);}
  }
  async function loadDetail(actorId:string,controller:AbortController){
    const response=await request('/api/review/administration/identities/'+encodeURIComponent(actorId),controller);
    const result=await response.json() as Detail;
    if(active(controller))setDetail(result);
  }
  async function changeAccess(){
    if(!detail||busy)return;
    let body:string;
    try{body=identityAccessChangeRequest(detail.state,!detail.state.suspended,reason);}
    catch{setError(text.reasonRequired);return;}
    const actorId=detail.state.actorId;const suspended=!detail.state.suspended;const controller=begin();
    try{
      await request('/api/review/administration/identities/'+encodeURIComponent(actorId)+'/status',controller,body);
      await loadDetail(actorId,controller);await loadIdentities(controller);
      if(active(controller)){setReason('');setNotice(suspended?text.suspended:text.reactivated);}
    }catch(exception){
      if(!active(controller))return;
      if(exception instanceof AdministrationHttpError&&exception.status===409){
        try{await loadDetail(actorId,controller);await loadIdentities(controller);if(active(controller)){setReason('');setError(text.conflict);}}
        catch(reloadError){failure(reloadError,controller);}
      }else failure(exception,controller);
    }finally{finish(controller);}
  }

  return <section className="identity-administration" aria-label={text.heading}>
    <h3>{text.heading}</h3><p>{text.help}</p>
    {notice&&<p className="review-notice" role="status">{notice}</p>}
    {error&&<div className="error" role="alert"><strong>{de.errorHeading}</strong><p>{error}</p></div>}
    {busy&&!detail&&<p>{text.loading}</p>}
    <ul className="identity-list">{identities.map(identity=><li key={identity.actorId}>
      <span>{identity.actorId}</span><span>{identity.suspended?text.statusSuspended:text.statusActive}</span>
      <button type="button" className="secondary" disabled={busy}
        onClick={()=>openIdentity(identity.actorId)}>{text.manage}: {identity.actorId}</button>
    </li>)}</ul>
    {detail&&<article><h4>{text.detail}: {detail.state.actorId}</h4>
      <dl><dt>{text.status}</dt><dd>{detail.state.suspended?text.statusSuspended:text.statusActive}</dd>
        <dt>{text.revision}</dt><dd>{detail.state.revision}</dd></dl>
      <label className="field">{text.reason}<textarea value={reason} disabled={busy} maxLength={1000}
        onChange={event=>setReason(event.target.value)} aria-label={text.reason}/></label>
      <button type="button" className={detail.state.suspended?'primary':'secondary'} disabled={busy}
        onClick={changeAccess}>{detail.state.suspended?text.reactivate:text.suspend}</button>
      <h4>{text.audit}</h4>
      {!detail.history.length&&<p>{text.noAudit}</p>}
      <ol className="review-audit">{detail.history.map(item=><li key={item.revision}>
        <strong>{item.suspended?text.auditSuspended:text.auditReactivated}</strong>
        <span>{new Date(item.changedAtUtc).toLocaleString('de-DE',{timeZone:'UTC'})} UTC</span>
        <span>{text.changedBy}: {item.administratorActorId}</span><span>{item.reason}</span>
      </li>)}</ol>
    </article>}
  </section>;
}

class AdministrationHttpError extends Error{
  constructor(readonly status:number){super('identity_administration_request_failed');}
}
