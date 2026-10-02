import { test } from 'node:test';
import assert from 'node:assert/strict';
import { decimal, requestJson, exampleValues } from '../src/model.ts';

const pack={packId:'synthetic.test',releaseId:'test',fields:[{id:'value',type:'number',required:true},{id:'confirmed',type:'truth',required:false}],evidenceRequirements:['verification']};
test('decimal input is exact and accepts German separator',()=>{
 const json=requestJson(pack,'2026-10-02',{value:'123456789,1234567890123456789',confirmed:'YES'},{});
 assert.match(json,/"number":123456789\.1234567890123456789/);
 assert.match(json,/"verification":"MISSING"/);
});
test('empty and explicit unknown values stay unknown',()=>{
 assert.match(requestJson(pack,'2026-10-02',{},{}) ,/"value":\{"kind":"UNKNOWN"\}/);
 assert.throws(()=>requestJson(pack,'',{},{}),/missing_date/);
});
test('ambiguous numbers and truth encodings are rejected',()=>{
 for(const value of ['1.000,5','1,2,3','NaN','Infinity','01','+1'])assert.throws(()=>decimal(value));
 assert.throws(()=>requestJson(pack,'2026-10-02',{confirmed:'1'},{}),/invalid_truth/);
});
test('loading an example never roundtrips decimals through Number',()=>{
 const data=exampleValues('{"assessmentDate":"2026-10-02","facts":{"value":{"kind":"NUMBER","number":123456789.1234567890123456789}}}');
 assert.equal(data.values.value,'123456789,1234567890123456789');
});
