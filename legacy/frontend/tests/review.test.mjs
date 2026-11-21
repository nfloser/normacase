import {test} from 'node:test';
import assert from 'node:assert/strict';
import {reviewRequest} from '../src/review.ts';
const detail={caseRevision:'9007199254740993',processRevision:'9007199254740994',auditRevision:'9007199254740995',allowedActions:['ACCEPT_SYSTEM_RESULT','OVERRIDE']};
test('review preserves exact revisions and includes no client identity or timestamp',()=>{
 const body=JSON.parse(reviewRequest(detail,'ACCEPT_SYSTEM_RESULT','  Synthetic reason  ',''));
 assert.deepEqual(body,{expectedCaseRevision:detail.caseRevision,expectedProcessRevision:detail.processRevision,expectedAuditRevision:detail.auditRevision,disposition:'ACCEPT_SYSTEM_RESULT',reason:'Synthetic reason'});
});
test('override requires an explicit known result and reason',()=>{
 for(const value of ['', 'UNKNOWN', 'SUPPORTED_OTHER'])assert.throws(()=>reviewRequest(detail,'OVERRIDE','reason',value),/override_required/);
 assert.throws(()=>reviewRequest(detail,'OVERRIDE','  ','SUPPORTED'),/reason_required/);
 assert.equal(JSON.parse(reviewRequest(detail,'OVERRIDE','reason','INCOMPLETE')).overrideOutcome,'INCOMPLETE');
});
test('actions absent from server permission and malformed revisions are rejected',()=>{
 assert.throws(()=>reviewRequest({...detail,allowedActions:[]},'OVERRIDE','reason','SUPPORTED'),/not_allowed/);
 for(const value of ['1e2','-1','1.0','', '01'])assert.throws(()=>reviewRequest({...detail,processRevision:value},'ACCEPT_SYSTEM_RESULT','reason',''),/invalid_revision/);
 assert.throws(()=>reviewRequest(detail,'ACCEPT_SYSTEM_RESULT','x'.repeat(1001),''),/reason_required/);
});
