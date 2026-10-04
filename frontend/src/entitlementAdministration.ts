export type EntitlementProposal={
  changeId:string;
  targetActorId:string;
  expectedEntitlementRevision:string;
  actions:string[];
  caseIds:string[];
  proposerActorId:string;
  proposedAtUtc:string;
  reason:string;
};
export type EntitlementDecision={
  changeId:string;
  decisionActorId:string;
  decidedAtUtc:string;
  approved:boolean;
  reason:string;
};
export type EntitlementChange={proposal:EntitlementProposal;decision:EntitlementDecision|null};
export type EffectiveEntitlement={actorId:string;revision:string;actions:string[];caseIds:string[]};

function validReason(reason:string){
  const trimmed=reason.trim();
  if(!trimmed||trimmed.length>1000||Array.from(trimmed).some(character=>/\p{Cc}/u.test(character)))
    throw new Error('reason_required');
  return trimmed;
}
function unique(values:string[]){
  return new Set(values).size===values.length;
}

export function entitlementProposalRequest(
  targetActorId:string,
  revision:string,
  actions:string[],
  caseIds:string[],
  reason:string
):string{
  if(!/^(0|[1-9][0-9]*)$/.test(revision))throw new Error('invalid_revision');
  const sortedActions=[...actions].sort();
  const sortedCases=[...caseIds].sort();
  if(!targetActorId||!unique(sortedActions)||!unique(sortedCases)
    ||(sortedActions.length===0)!==(sortedCases.length===0)
    ||(sortedActions.some(action=>action!=='READ')&&!sortedActions.includes('READ')))
    throw new Error('invalid_scope');
  return JSON.stringify({
    targetActorId,
    expectedEntitlementRevision:revision,
    actions:sortedActions,
    caseIds:sortedCases,
    reason:validReason(reason)
  });
}

export function entitlementDecisionRequest(approved:boolean,reason:string):string{
  return JSON.stringify({approved,reason:validReason(reason)});
}
