import {test} from 'node:test';
import assert from 'node:assert/strict';
import {processingStep} from '../src/caseProcessing.ts';
test('case actions explain the existing approval boundary and subsequent dispatch',()=>{
 const base={canConfirm:true,organization:{confirmed:false,dispatched:false}};
 assert.equal(processingStep(base),'confirm');
 assert.equal(processingStep({...base,canConfirm:false}),'blocked');
 assert.equal(processingStep({...base,organization:{confirmed:true,dispatched:false}}),'dispatch');
 assert.equal(processingStep({...base,organization:{confirmed:true,dispatched:true}}),'complete');
 assert.equal(processingStep({...base,canConfirm:false,organization:{confirmed:true,dispatched:true}}),'complete');
});
