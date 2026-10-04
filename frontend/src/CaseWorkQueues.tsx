import { useEffect, useState } from 'react';
import { parse } from 'lossless-json';
import de from './de.json';
import type { Pack } from './model';
import { DecisionTrace } from './DecisionTrace';
import type { RuleTrace, OutputTrace } from './trace';
const text = de.workQueues;
type Item = {caseId:string;caseRevision:string;processRevision:string;stateId:string};
type Detail = Item & {queueId:string;packId:string|null;assessmentId:string|null;assessmentJson:string|null;recordedAtUtc:string|null;evidence:Record<string,string>};
type Assessment = {assessment:{outcome:string;ruleTrace?:RuleTrace;domainOutputs?:OutputTrace[];missingRequiredFields:string[];knowledgeRelease:string}};
const outcomes:Record<string,string> = {SUPPORTED:de.supported,NOT_SUPPORTED:de.notSupported,INCOMPLETE:de.incomplete,HUMAN_REVIEW:de.review,NOT_APPLICABLE:de.na};
export function CaseWorkQueues({packs}:{packs:Pack[]}) {
  const [workload,setWorkload]=useState<{totalCases:number;previewLimit:number;queues:{queueId:string;totalCount:number;items:Item[]}[]} | null>(null);
  const [selected,setSelected]=useState('');
  const [detail,setDetail]=useState<Detail|null>(null);
  const [error,setError]=useState('');
  const [busy,setBusy]=useState(false);
  useEffect(()=>{
    const controller=new AbortController();
    fetch('/api/work-queues',{signal:controller.signal}).then(response=>{if(!response.ok)throw new Error();return response.json();})
      .then(data=>{if(!controller.signal.aborted)setWorkload(data);})
      .catch(()=>{if(!controller.signal.aborted)setError(de.networkError);});
    return()=>controller.abort();
  },[]);
  useEffect(()=>{
    if(!selected)return;
    const controller=new AbortController();setBusy(true);setError('');
    fetch('/api/work-cases/'+encodeURIComponent(selected),{signal:controller.signal})
      .then(response=>{if(!response.ok)throw new Error();return response.json();})
      .then(data=>{if(!controller.signal.aborted)setDetail(data);})
      .catch(()=>{if(!controller.signal.aborted)setError(de.networkError);})
      .finally(()=>{if(!controller.signal.aborted)setBusy(false);});
    return()=>controller.abort();
  },[selected]);
  const queues=workload?.queues??[];
  const assessment=detail?.assessmentJson ? parse(detail.assessmentJson) as Assessment : null;
  const pack=packs.find(pack=>pack.packId===detail?.packId);
  const statuses:Record<string,string>={PRESENT:de.present,MISSING:de.missing,CONFLICTING:de.conflicting};
  return <section className="card work-queues" aria-label={text.heading}>
    <h2>{text.heading}</h2><p>{text.help}</p>
    {workload&&<div className="workload-summary" aria-label={text.workload}><strong>{workload.totalCases} {text.cases}</strong><span>{text.preview}</span></div>}
    <div className="queue-grid">{queues.map(queue=><section key={queue.queueId}>
      <h3>{(text.queues as Record<string,string>)[queue.queueId]??de.unknown} ({queue.totalCount})</h3>
      <p className="queue-preview-note">{text.representative}: {queue.items.length} {text.of} {queue.totalCount}</p>
      <ul>{queue.items.map(item=><li key={item.caseId}><button type="button" className="secondary" aria-pressed={selected===item.caseId} onClick={()=>{if(selected!==item.caseId){setDetail(null);setSelected(item.caseId);}}}>{text.select}: {item.caseId}</button></li>)}</ul>
    </section>)}</div>
    <div aria-live="polite">{busy&&<p>{text.loading}</p>}{error&&<p role="alert">{error}</p>}
    {detail&&<article><h3>{text.detail}: {detail.caseId}</h3>
      <p>{(text.states as Record<string,string>)[detail.stateId]??de.unknown}</p>
      <dl><dt>{text.caseRevision}</dt><dd>{detail.caseRevision}</dd><dt>{text.processRevision}</dt><dd>{detail.processRevision}</dd></dl>
      {assessment?<><h4>{de.result}: {outcomes[assessment.assessment.outcome]??de.unknown}</h4>
        <dl><dt>{de.release}</dt><dd>{assessment.assessment.knowledgeRelease}</dd><dt>{text.recorded}</dt><dd>{detail.recordedAtUtc&&new Date(detail.recordedAtUtc).toLocaleString('de-DE',{timeZone:'UTC'})} UTC</dd></dl>
        {!!assessment.assessment.missingRequiredFields.length&&<><h4>{de.missingFields}</h4><ul>{assessment.assessment.missingRequiredFields.map(id=><li key={id}>{pack?.presentation?.fields[id]??de.fieldReference}</li>)}</ul></>}
        <h4>{de.evidence}</h4><dl>{Object.entries(detail.evidence).map(([id,status])=><div key={id}><dt>{pack?.presentation?.evidence[id]??de.evidenceReference}</dt><dd>{statuses[status]??de.unknown}</dd></div>)}</dl>
        <DecisionTrace rule={assessment.assessment.ruleTrace} outputs={assessment.assessment.domainOutputs} presentation={pack?.presentation}/>
      </>:<p>{text.noAssessment}</p>}
    </article>}</div>
  </section>;
}
