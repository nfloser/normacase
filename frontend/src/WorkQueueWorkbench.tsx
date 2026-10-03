import React, { useEffect, useState } from 'react';
import { parse } from 'lossless-json';
import de from './de.json';
import { DecisionTrace } from './DecisionTrace';
import type { Pack } from './model';
import type { OutputTrace, RuleTrace } from './trace';

type WorkItemSummary = {
  caseId:string;
  caseRevision:number;
  workflowId:string;
  workflowVersion:number;
  stateId:string;
  processRevision:number;
  hasAssessment:boolean;
};
type QueueSummary = {queueId:string;items:WorkItemSummary[]};
type QueueCatalog = {configurationId:string;configurationVersion:number;queues:QueueSummary[]};
type EvidenceDetail = {id:string;status:string};
type AssessmentDetail = {
  assessmentId:string;
  outcome:string;
  routingDisposition:string;
  knowledgePackId:string;
  knowledgeRelease:string;
  assessmentDate:string;
  evidence:EvidenceDetail[];
  assessmentJson:string;
};
type WorkItemDetail = WorkItemSummary & {
  queueId:string;
  assessmentStatus:string;
  assessment:AssessmentDetail|null;
};
type AssessmentEnvelope = {
  platformVersion:string;
  assessment:{
    outcome:string;
    assessmentDate:string;
    knowledgeRelease:string;
    ruleTrace?:RuleTrace;
    domainOutputs?:OutputTrace[];
  };
};

const text = de.workQueues;
const queueLabels = text.queues as Record<string,string>;
const stateLabels = text.states as Record<string,string>;
const routingLabels = text.routing as Record<string,string>;
const evidenceLabels:Record<string,string> = {
  PRESENT:de.present,
  MISSING:de.missing,
  CONFLICTING:de.conflicting
};
const outcomeLabels:Record<string,string> = {
  SUPPORTED:de.supported,
  NOT_SUPPORTED:de.notSupported,
  INCOMPLETE:de.incomplete,
  HUMAN_REVIEW:de.review,
  NOT_APPLICABLE:de.na
};

export function WorkQueueWorkbench({packs}:{packs:Pack[]}) {
  const [catalog,setCatalog]=useState<QueueCatalog|null>(null);
  const [detail,setDetail]=useState<WorkItemDetail|null>(null);
  const [error,setError]=useState('');
  const [busy,setBusy]=useState(false);

  useEffect(()=>{
    const controller=new AbortController();
    fetch('/api/work-queues',{signal:controller.signal})
      .then(response=>{if(!response.ok)throw new Error();return response.json();})
      .then(data=>{if(!controller.signal.aborted)setCatalog(data as QueueCatalog);})
      .catch(()=>{if(!controller.signal.aborted)setError(de.networkError);});
    return()=>controller.abort();
  },[]);

  async function openCase(caseId:string) {
    setBusy(true);setError('');setDetail(null);
    try {
      const response=await fetch('/api/work-items/'+encodeURIComponent(caseId));
      if(!response.ok)throw new Error();
      setDetail(await response.json() as WorkItemDetail);
    } catch {
      setError(de.networkError);
    } finally {
      setBusy(false);
    }
  }

  let assessment:AssessmentEnvelope|null=null;
  if(detail?.assessment) {
    try {assessment=parse(detail.assessment.assessmentJson) as AssessmentEnvelope;}
    catch {assessment=null;}
  }
  const presentation=detail?.assessment
    ? packs.find(pack=>pack.packId===detail.assessment!.knowledgePackId)?.presentation
    : undefined;

  return <section className="card work-queues" aria-live="polite">
    <div className="section-head"><span className="step">03</span><div>
      <h2>{text.heading}</h2><p>{text.help}</p>
    </div></div>
    <p className="work-queue-notice">{text.notice}</p>
    {error&&<div className="error" role="alert"><strong>{de.errorHeading}</strong><p>{error}</p></div>}
    {!catalog&&!error&&<p>{de.loading}</p>}
    {catalog&&<div className="queue-grid">
      {catalog.queues.map(queue=><article className="queue-panel" key={queue.queueId}>
        <div className="queue-title"><h3>{queueLabels[queue.queueId]??text.unknownQueue}</h3>
          <span>{queue.items.length}</span></div>
        {queue.items.map(item=><button
          type="button"
          className="work-item"
          key={item.caseId}
          aria-label={text.openCase.replace('{caseId}',item.caseId)}
          disabled={busy}
          onClick={()=>openCase(item.caseId)}>
          <strong>{item.caseId}</strong>
          <span>{stateLabels[item.stateId]??text.unknownState}</span>
          <small>{text.revisions
            .replace('{caseRevision}',String(item.caseRevision))
            .replace('{processRevision}',String(item.processRevision))}</small>
        </button>)}
      </article>)}
    </div>}
    {busy&&<p>{text.loadingDetail}</p>}
    {detail&&<div className="work-item-detail">
      <h3>{text.detail}</h3>
      <dl>
        <dt>{text.caseId}</dt><dd>{detail.caseId}</dd>
        <dt>{text.queue}</dt><dd>{queueLabels[detail.queueId]??text.unknownQueue}</dd>
        <dt>{text.state}</dt><dd>{stateLabels[detail.stateId]??text.unknownState}</dd>
        <dt>{text.caseRevision}</dt><dd>{detail.caseRevision}</dd>
        <dt>{text.processRevision}</dt><dd>{detail.processRevision}</dd>
      </dl>
      {detail.assessment&&assessment?<>
        <h4>{text.assessment}</h4>
        <dl>
          <dt>{de.result}</dt><dd>{outcomeLabels[detail.assessment.outcome]??de.unknown}</dd>
          <dt>{text.routingLabel}</dt><dd>{routingLabels[detail.assessment.routingDisposition]??de.unknown}</dd>
          <dt>{de.date}</dt><dd>{detail.assessment.assessmentDate.split('-').reverse().join('.')}</dd>
          <dt>{de.release}</dt><dd>{detail.assessment.knowledgeRelease}</dd>
          <dt>{de.platform}</dt><dd>{assessment.platformVersion}</dd>
        </dl>
        <h4>{de.evidence}</h4>
        <dl>{detail.assessment.evidence.map(item=><React.Fragment key={item.id}>
          <dt>{presentation?.evidence[item.id]??de.evidenceReference}</dt>
          <dd>{evidenceLabels[item.status]??de.unknown}</dd>
        </React.Fragment>)}</dl>
        {assessment.assessment.ruleTrace&&<div className="source">
          <h4>{de.source}</h4>
          <p>{assessment.assessment.ruleTrace.source.title}</p>
          <span>{assessment.assessment.ruleTrace.source.authority}</span>
          <dl><dt>{de.rule}</dt><dd>{assessment.assessment.ruleTrace.ruleId}</dd>
            <dt>{de.sourceRevision}</dt><dd>{assessment.assessment.ruleTrace.source.version??'—'}</dd></dl>
        </div>}
        <DecisionTrace
          rule={assessment.assessment.ruleTrace}
          outputs={assessment.assessment.domainOutputs}
          presentation={presentation}/>
      </>:<div className="technical-empty">
        <strong>{text.noAssessment}</strong>
        <p>{text.noAssessmentHelp}</p>
      </div>}
    </div>}
  </section>;
}
