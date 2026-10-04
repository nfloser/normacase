export type BatchReviewCandidate={
  caseId:string;
  assessmentId:string;
  caseRevision:string;
  processRevision:string;
  auditRevision:string;
  batchAllowed:boolean;
};

const revisionPattern=/^(0|[1-9][0-9]*)$/;
const requestIdPattern=/^[A-Za-z0-9][A-Za-z0-9._@:-]{0,127}$/;
const statuses=['COMMITTED','DENIED','CONFLICT','POLICY_REJECTED','NOT_ATTEMPTED'] as const;
export type BatchReviewStatus=typeof statuses[number];
export type BatchReviewResult={
  policyId:string;
  policyVersion:string;
  items:{caseId:string;reviewId:string;status:BatchReviewStatus}[];
};

export function batchReviewRequest(
  candidates:BatchReviewCandidate[],
  reason:string,
  requestId:string
):string{
  if(candidates.length===0)throw new Error('selection_required');
  if(candidates.length>100)throw new Error('selection_too_large');
  if(!requestIdPattern.test(requestId))throw new Error('invalid_request_id');
  const trimmed=reason.trim();
  if(!trimmed||trimmed.length>1000||Array.from(trimmed).some(character=>/\p{Cc}/u.test(character)))
    throw new Error('reason_required');
  if(candidates.some(candidate=>!candidate.batchAllowed))throw new Error('not_allowed');
  if(new Set(candidates.map(candidate=>candidate.caseId)).size!==candidates.length)
    throw new Error('duplicate_case');
  if(candidates.some(candidate=>![candidate.caseRevision,candidate.processRevision,candidate.auditRevision]
      .every(revision=>revisionPattern.test(revision))))
    throw new Error('invalid_revision');

  return JSON.stringify({
    requestId,
    policyId:'synthetic-reviewed-batch-policy',
    policyVersion:'1',
    items:candidates.map((candidate,index)=>({
      caseId:candidate.caseId,
      assessmentId:candidate.assessmentId,
      reviewId:requestId+'-'+(index+1),
      expectedCaseRevision:candidate.caseRevision,
      expectedProcessRevision:candidate.processRevision,
      expectedAuditRevision:candidate.auditRevision,
      disposition:'ACCEPT_SYSTEM_RESULT',
      reason:trimmed
    }))
  });
}

export function batchReviewResult(responseJson:string,requestJson:string):BatchReviewResult{
  try{
    const response=JSON.parse(responseJson) as BatchReviewResult;
    const request=JSON.parse(requestJson) as {policyId:string;policyVersion:string;items:{caseId:string;reviewId:string}[]};
    if(response?.policyId!==request.policyId||response?.policyVersion!==request.policyVersion
      ||!Array.isArray(response.items)||response.items.length!==request.items.length)
      throw new Error();
    for(let index=0;index<request.items.length;index++){
      const actual=response.items[index];const expected=request.items[index];
      if(actual?.caseId!==expected.caseId||actual?.reviewId!==expected.reviewId
        ||!statuses.includes(actual?.status))throw new Error();
    }
    return response;
  }catch{throw new Error('invalid_batch_result');}
}
