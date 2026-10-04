export type EntitlementContext={actorId:string;effectiveRevision:string;actions:string[];caseIds:string[];availableCaseIds:string[]};
export type EntitlementChange={changeId:string;targetActorId:string;expectedEntitlementRevision:string;
  actions:string[];caseIds:string[];proposerActorId:string;proposedAtUtc:string;reason:string};

function validReason(reason:string){
  const trimmed=reason.trim();
  if(!trimmed||trimmed.length>1000||Array.from(trimmed).some(character=>/\p{Cc}/u.test(character)))
    throw new Error('reason_required');
  return trimmed;
}

export function entitlementProposalRequest(context:EntitlementContext,actions:string[],caseIds:string[],reason:string,changeId:string){
  if(!/^(0|[1-9][0-9]*)$/.test(context.effectiveRevision))throw new Error('invalid_revision');
  if(!/^entitlement-[a-f0-9-]{36}$/.test(changeId))throw new Error('invalid_change_id');
  return JSON.stringify({changeId,targetActorId:context.actorId,expectedEntitlementRevision:context.effectiveRevision,
    actions:[...actions].sort(),caseIds:[...caseIds].sort(),reason:validReason(reason)});
}

export function entitlementDecisionRequest(approved:boolean,reason:string){
  return JSON.stringify({approved,reason:validReason(reason)});
}
