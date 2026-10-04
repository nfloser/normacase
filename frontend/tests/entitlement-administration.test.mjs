import {test} from 'node:test';
import assert from 'node:assert/strict';
import {entitlementProposalRequest,entitlementDecisionRequest} from '../src/entitlementAdministrationRequest.ts';

test('entitlement proposal preserves exact revision and excludes server-owned audit fields',()=>{
 const body=JSON.parse(entitlementProposalRequest({actorId:'synthetic-local:user-alice',effectiveRevision:'9007199254740993',
  actions:['READ'],caseIds:['demo-g-supported'],availableCaseIds:['demo-g-supported']},['READ','ACCEPT'],['demo-g-supported'],'  Antrag  ',
  'entitlement-12345678-1234-1234-1234-123456789abc'));
 assert.deepEqual(body,{changeId:'entitlement-12345678-1234-1234-1234-123456789abc',targetActorId:'synthetic-local:user-alice',
  expectedEntitlementRevision:'9007199254740993',actions:['ACCEPT','READ'],caseIds:['demo-g-supported'],reason:'Antrag'});
 assert.equal('proposerActorId' in body,false);assert.equal('proposedAtUtc' in body,false);
});

test('entitlement decisions contain no actor or timestamp and require a bounded reason',()=>{
 assert.deepEqual(JSON.parse(entitlementDecisionRequest(true,'  Gegenprüfung  ')),{approved:true,reason:'Gegenprüfung'});
 assert.throws(()=>entitlementDecisionRequest(false,' '),/reason_required/);
 assert.throws(()=>entitlementDecisionRequest(false,'x'.repeat(1001)),/reason_required/);
});
