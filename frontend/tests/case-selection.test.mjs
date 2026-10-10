import {test} from 'node:test';
import assert from 'node:assert/strict';
import {selectCaseRange} from '../src/caseSelection.ts';
test('range follows visible order in either direction',()=>{
 assert.deepEqual(selectCaseRange(['a','b','c','d'],'c','a'),['a','b','c']);
 assert.deepEqual(selectCaseRange(['a','b','c','d'],'b','d'),['b','c','d']);
});
test('hidden anchor falls back to target without selecting hidden cases',()=>{
 assert.deepEqual(selectCaseRange(['b','d'],'a','d'),['d']);
 assert.deepEqual(selectCaseRange(['b','d'],'b','missing'),[]);
});
test('oversized range is rejected instead of silently selecting a partial batch',()=>{
 const ids=Array.from({length:101},(_,i)=>String(i));
 assert.equal(selectCaseRange(ids,'0','100'),null);
 assert.equal(selectCaseRange(ids,'0','99').length,100);
});
