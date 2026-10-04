import {test} from 'node:test';
import assert from 'node:assert/strict';
import {
 entitlementProposalRequest,
 entitlementDecisionRequest
} from '../src/entitlementAdministration.ts';

test('entitlement proposal preserves exact revision and complete selected snapshot',()=>{
 const body=JSON.parse(entitlementProposalRequest(
  'synthetic-local:user-alice','9007199254740993',
  ['READ','ACCEPT'],['demo-g-supported'],'  Synthetischer Antrag  '));
 assert.deepEqual(body,{
  targetActorId:'synthetic-local:user-alice',
  expectedEntitlementRevision:'9007199254740993',
  actions:['ACCEPT','READ'],
  caseIds:['demo-g-supported'],
  reason:'Synthetischer Antrag'
 });
});

test('entitlement request builders reject malformed revisions, duplicate scope and invalid reasons',()=>{
 for(const revision of ['', '-1', '01', '1.0', '1e2'])
  assert.throws(()=>entitlementProposalRequest(
   'synthetic-local:user-alice',revision,['READ'],['demo-g-supported'],'Grund'),/invalid_revision/);
 assert.throws(()=>entitlementProposalRequest(
  'synthetic-local:user-alice','0',['READ','READ'],['demo-g-supported'],'Grund'),/invalid_scope/);
 assert.throws(()=>entitlementProposalRequest(
  'synthetic-local:user-alice','0',['ACCEPT'],['demo-g-supported'],'Grund'),/invalid_scope/);
 assert.throws(()=>entitlementProposalRequest(
  'synthetic-local:user-alice','0',['READ'],[],'Grund'),/invalid_scope/);
 assert.throws(()=>entitlementProposalRequest(
  'synthetic-local:user-alice','0',[],[],'  '),/reason_required/);
});

test('entitlement decision carries only explicit decision and reason',()=>{
 assert.deepEqual(JSON.parse(entitlementDecisionRequest(true,'  Geprüft  ')),{
  approved:true,reason:'Geprüft'
 });
 assert.deepEqual(JSON.parse(entitlementDecisionRequest(false,'Abgelehnt')),{
  approved:false,reason:'Abgelehnt'
 });
 assert.throws(()=>entitlementDecisionRequest(true,'x'.repeat(1001)),/reason_required/);
});
