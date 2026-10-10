import { useEffect, useState } from 'react';
import { parse } from 'lossless-json';
import de from './de.json';
import type { Pack } from './model';
import { DocumentCaseFile } from './DocumentCaseFile';
import { DecisionTrace } from './DecisionTrace';
import type { RuleTrace, OutputTrace } from './trace';

const text = de.workQueues;
type Item = {caseId:string;caseRevision:string;processRevision:string;stateId:string};
type Queue = {queueId:string;items:Item[]};
type QueuePage = {totalCases:number;queues:Queue[]};
type Detail = Item & {queueId:string;packId:string|null;assessmentId:string|null;assessmentJson:string|null;recordedAtUtc:string|null;evidence:Record<string,string>};
type Assessment = {assessment:{outcome:string;ruleTrace?:RuleTrace;domainOutputs?:OutputTrace[];missingRequiredFields:string[];knowledgeRelease:string}};
const outcomes:Record<string,string> = {SUPPORTED:de.supported,NOT_SUPPORTED:de.notSupported,INCOMPLETE:de.incomplete,HUMAN_REVIEW:de.review,NOT_APPLICABLE:de.na};
const representativeLimit=5;

export function CaseWorkQueues({packs}:{packs:Pack[]}) {
  const [expanded,setExpanded]=useState<Record<string,boolean>>({});
  const [queues,setQueues]=useState<Queue[]>([]);
  const [totalCases,setTotalCases]=useState(0);
  const [selected,setSelected]=useState('');
  const [detail,setDetail]=useState<Detail|null>(null);
  const [error,setError]=useState('');
  const [busy,setBusy]=useState(false);
  useEffect(()=>{
    const controller=new AbortController();
    fetch('/api/work-queues',{signal:controller.signal}).then(response=>{if(!response.ok)throw new Error();return response.json();})
      .then((data:QueuePage)=>{if(!controller.signal.aborted){setQueues(data.queues);setTotalCases(data.totalCases);}})
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
  const assessment=detail?.assessmentJson ? parse(detail.assessmentJson) as Assessment : null;
  const pack=packs.find(pack=>pack.packId===detail?.packId);
  const statuses:Record<string,string>={PRESENT:de.present,MISSING:de.missing,CONFLICTING:de.conflicting};
  const queueLabels=text.queues as Record<string,string>;
  return <section id="work-queues" className="card work-queues" aria-label={text.heading}>
    <div className="case-workspace-grid"><aside className="case-list-panel" aria-label={de.workspace.list}><h2>{text.heading}</h2>
    {!!totalCases&&<div className="workload-summary" aria-label={text.summaryHeading}>
      <div className="workload-total"><strong>{totalCases} {text.summaryTotal}</strong></div>
      <div className="workload-counts">{queues.map(queue=><div key={queue.queueId}>
        <strong>{queue.items.length}</strong><span>{queueLabels[queue.queueId]??de.unknown}</span>
      </div>)}</div>
    </div>}
    <div className="queue-grid">{queues.map(queue=><section key={queue.queueId}>
      <h3>{queueLabels[queue.queueId]??de.unknown} ({queue.items.length})</h3>
      <ul>{queue.items.slice(0,expanded[queue.queueId]?queue.items.length:representativeLimit).map(item=><li key={item.caseId}><button type="button" className="secondary" aria-pressed={selected===item.caseId} onClick={()=>{if(selected!==item.caseId){setDetail(null);setSelected(item.caseId);}}}>{text.select}: {item.caseId}</button></li>)}
        {queue.items.length>representativeLimit&&!expanded[queue.queueId]&&<li className="queue-more">{text.moreCases.replace('{count}',String(queue.items.length-representativeLimit))}</li>}
      </ul>{queue.items.length>representativeLimit&&<button type="button" className="secondary" aria-expanded={!!expanded[queue.queueId]} onClick={()=>setExpanded({...expanded,[queue.queueId]:!expanded[queue.queueId]})}>{expanded[queue.queueId]?de.documents.showLess:de.documents.showAll}</button>}
    </section>)}</div>
    </aside><div className="case-detail-panel" aria-live="polite">{busy&&<p>{text.loading}</p>}{error&&<p role="alert">{error}</p>}
    {detail&&<article><h3>{text.detail}: {detail.caseId}</h3>
      <DocumentCaseFile key={detail.caseId} caseId={detail.caseId} presentation={pack?.presentation} analysis={<>
      <p>{(text.states as Record<string,string>)[detail.stateId]??de.unknown}</p>

{assessment?<><h4>{de.result}: {outcomes[assessment.assessment.outcome]??de.unknown}</h4>
        <details><summary>{de.workspace.details}</summary>      <dl><dt>{text.caseRevision}</dt><dd>{detail.caseRevision}</dd><dt>{text.processRevision}</dt><dd>{detail.processRevision}</dd></dl><dl><dt>{de.release}</dt><dd>{assessment.assessment.knowledgeRelease}</dd><dt>{text.recorded}</dt><dd>{detail.recordedAtUtc&&new Date(detail.recordedAtUtc).toLocaleString('de-DE',{timeZone:'UTC'})} UTC</dd></dl></details>
        {!!assessment.assessment.missingRequiredFields.length&&<><h4>{de.missingFields}</h4><ul>{assessment.assessment.missingRequiredFields.map(id=><li key={id}>{pack?.presentation?.fields[id]??de.fieldReference}</li>)}</ul></>}
        <h4>{de.evidence}</h4><dl>{Object.entries(detail.evidence).map(([id,status])=><div key={id}><dt>{pack?.presentation?.evidence[id]??de.evidenceReference}</dt><dd>{statuses[status]??de.unknown}</dd></div>)}</dl>
        <DecisionTrace rule={assessment.assessment.ruleTrace} outputs={assessment.assessment.domainOutputs} presentation={pack?.presentation}/>
      </>:<><p>{text.noAssessment}</p><details><summary>{de.workspace.details}</summary><dl><dt>{text.caseRevision}</dt><dd>{detail.caseRevision}</dd><dt>{text.processRevision}</dt><dd>{detail.processRevision}</dd></dl></details></>}
    </>}/>
    </article>}{!detail&&!busy&&!error&&<div className="case-empty"><h3>{de.workspace.details}</h3><p>{de.workspace.empty}</p></div>}</div></div>
  </section>;
}
