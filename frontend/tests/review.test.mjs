import { test } from 'node:test';
import assert from 'node:assert/strict';
import { bearerHeaders, reviewCommandJson } from '../src/review.ts';

const detail={
  caseId:'demo-g-supported',caseRevision:'4',processRevision:'2',stateId:'awaiting-approval',
  assessmentId:'assessment-1',packId:'synthetic.demo-g',assessmentJson:'{}',evidence:{},
  allowedActions:['ACCEPT_SYSTEM_RESULT','OVERRIDE'],auditRevision:'7',audit:[]
};

test('review commands preserve displayed concurrency revisions',()=>{
  const body=JSON.parse(reviewCommandJson(detail,'ACCEPT_SYSTEM_RESULT','  geprüft  '));
  assert.deepEqual(body,{
    expectedCaseRevision:'4',expectedProcessRevision:'2',expectedAuditRevision:'7',
    disposition:'ACCEPT_SYSTEM_RESULT',reason:'geprüft',overrideOutcome:null
  });
});

test('override requires an explicit generic outcome',()=>{
  assert.throws(()=>reviewCommandJson(detail,'OVERRIDE','Begründung'),/missing_override/);
  const body=JSON.parse(reviewCommandJson(detail,'OVERRIDE','Begründung','HUMAN_REVIEW'));
  assert.equal(body.overrideOutcome,'HUMAN_REVIEW');
});

test('empty review reasons are rejected before sending',()=>{
  assert.throws(()=>reviewCommandJson(detail,'ACCEPT_SYSTEM_RESULT','   '),/missing_reason/);
});

test('credential transport is limited to the Authorization header helper',()=>{
  assert.deepEqual(bearerHeaders('synthetic-secret'),{Authorization:'Bearer synthetic-secret'});
});
