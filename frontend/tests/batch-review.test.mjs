import {test} from 'node:test';
import assert from 'node:assert/strict';
import {batchReviewRequest,batchReviewResult} from '../src/batchReview.ts';

const candidates=[
 {caseId:'synthetic-case-a',assessmentId:'assessment-a',caseRevision:'9007199254740993',processRevision:'2',auditRevision:'3',batchAllowed:true},
 {caseId:'synthetic-case-b',assessmentId:'assessment-b',caseRevision:'4',processRevision:'5',auditRevision:'6',batchAllowed:true}
];

test('batch request preserves exact revisions and stable request identities',()=>{
 const body=JSON.parse(batchReviewRequest(candidates,'  Gemeinsame synthetische Begründung  ','workbench-batch-123'));
 assert.deepEqual(body,{
  requestId:'workbench-batch-123',
  policyId:'synthetic-reviewed-batch-policy',
  policyVersion:'1',
  items:[
   {caseId:'synthetic-case-a',assessmentId:'assessment-a',reviewId:'workbench-batch-123-1',expectedCaseRevision:'9007199254740993',expectedProcessRevision:'2',expectedAuditRevision:'3',disposition:'ACCEPT_SYSTEM_RESULT',reason:'Gemeinsame synthetische Begründung'},
   {caseId:'synthetic-case-b',assessmentId:'assessment-b',reviewId:'workbench-batch-123-2',expectedCaseRevision:'4',expectedProcessRevision:'5',expectedAuditRevision:'6',disposition:'ACCEPT_SYSTEM_RESULT',reason:'Gemeinsame synthetische Begründung'}
  ]
 });
});

test('batch request rejects missing permission, stale shapes and unsafe retries',()=>{
 assert.throws(()=>batchReviewRequest([], 'Grund','workbench-batch-123'),/selection_required/);
 assert.throws(()=>batchReviewRequest(candidates,'  ','workbench-batch-123'),/reason_required/);
 assert.throws(()=>batchReviewRequest(candidates,'Grund','ungültig id'),/invalid_request_id/);
 assert.throws(()=>batchReviewRequest([{...candidates[0],batchAllowed:false}],'Grund','workbench-batch-123'),/not_allowed/);
 assert.throws(()=>batchReviewRequest([{...candidates[0],auditRevision:'01'}],'Grund','workbench-batch-123'),/invalid_revision/);
 assert.throws(()=>batchReviewRequest([candidates[0],candidates[0]],'Grund','workbench-batch-123'),/duplicate_case/);
 assert.throws(()=>batchReviewRequest(Array.from({length:101},(_,index)=>({...candidates[0],caseId:'case-'+index})),'Grund','workbench-batch-123'),/selection_too_large/);
});

test('batch result accepts only the ordered result for the retained request',()=>{
 const request=batchReviewRequest(candidates,'Grund','workbench-batch-123');
 const response=JSON.stringify({policyId:'synthetic-reviewed-batch-policy',policyVersion:'1',items:[
  {caseId:'synthetic-case-a',reviewId:'workbench-batch-123-1',status:'COMMITTED'},
  {caseId:'synthetic-case-b',reviewId:'workbench-batch-123-2',status:'CONFLICT'}
 ]});
 assert.deepEqual(batchReviewResult(response,request).items.map(item=>item.status),['COMMITTED','CONFLICT']);
 assert.throws(()=>batchReviewResult(response.replace('CONFLICT','UNKNOWN_STATUS'),request),/invalid_batch_result/);
 assert.throws(()=>batchReviewResult(response.replace('synthetic-case-b','substituted-case'),request),/invalid_batch_result/);
 assert.throws(()=>batchReviewResult(response,request.replace('workbench-batch-123-2','changed-review')),/invalid_batch_result/);
});
