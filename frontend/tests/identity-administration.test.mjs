import {test} from 'node:test';
import assert from 'node:assert/strict';
import {identityAccessChangeRequest} from '../src/identityAccessAdministration.ts';

const active={actorId:'synthetic-local:user-alice',revision:'9007199254740993',suspended:false};

test('identity access change preserves the exact revision and contains no administrator or time',()=>{
 const body=JSON.parse(identityAccessChangeRequest(active,true,'  Synthetische Sperre  '));
 assert.deepEqual(body,{expectedRevision:'9007199254740993',suspended:true,reason:'Synthetische Sperre'});
});

test('identity access change rejects malformed revisions, no-ops and invalid reasons',()=>{
 for(const revision of ['', '-1', '01', '1.0', '1e2'])
  assert.throws(()=>identityAccessChangeRequest({...active,revision},true,'Grund'),/invalid_revision/);
 assert.throws(()=>identityAccessChangeRequest(active,false,'Grund'),/no_change/);
 assert.throws(()=>identityAccessChangeRequest(active,true,'  '),/reason_required/);
 assert.throws(()=>identityAccessChangeRequest(active,true,'x'.repeat(1001)),/reason_required/);
 assert.throws(()=>identityAccessChangeRequest(active,true,'Zeile\nZwei'),/reason_required/);
});
