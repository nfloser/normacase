export type ReviewRevision = {caseRevision:string;processRevision:string;auditRevision:string;allowedActions:string[]};
export function reviewRequest(detail:ReviewRevision, disposition:'ACCEPT_SYSTEM_RESULT'|'OVERRIDE', reason:string, overrideOutcome:string):string {
  if(!detail.allowedActions.includes(disposition))throw new Error('not_allowed');
  if(![detail.caseRevision,detail.processRevision,detail.auditRevision].every(value=>/^(0|[1-9][0-9]*)$/.test(value)))throw new Error('invalid_revision');
  const trimmed=reason.trim();
  if(!trimmed||trimmed.length>1000)throw new Error('reason_required');
  const body:Record<string,string>={expectedCaseRevision:detail.caseRevision,expectedProcessRevision:detail.processRevision,expectedAuditRevision:detail.auditRevision,disposition,reason:trimmed};
  if(disposition==='OVERRIDE'){
    if(!['SUPPORTED','NOT_SUPPORTED','INCOMPLETE','HUMAN_REVIEW','NOT_APPLICABLE'].includes(overrideOutcome))throw new Error('override_required');
    body.overrideOutcome=overrideOutcome;
  }
  return JSON.stringify(body);
}
