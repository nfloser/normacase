import { test } from 'node:test';
import assert from 'node:assert/strict';
import { parse } from 'lossless-json';
import { displayDecimal, displayValue } from '../src/trace.ts';
const labels={unknown:'Unbekannt',yes:'Ja',no:'Nein',na:'Nicht anwendbar',unrecorded:'Nicht aufgezeichnet'};
test('recorded decimals preserve all digits and trailing zeroes',()=>{
 const trace=parse('{"number":123456789.1234567890123456789,"limit":10.00}');
 assert.equal(displayDecimal(trace.number,labels.unrecorded),'123456789,1234567890123456789');
 assert.equal(displayDecimal(trace.limit,labels.unrecorded),'10,00');
});
test('unknown, no, not applicable and absent trace values remain distinct',()=>{
 assert.equal(displayValue({kind:'UNKNOWN'},labels),'Unbekannt');
 assert.equal(displayValue({kind:'TRUTH',truth:'UNKNOWN'},labels),'Unbekannt');
 assert.equal(displayValue({kind:'TRUTH',truth:'NO'},labels),'Nein');
 assert.equal(displayValue({kind:'TRUTH',truth:'NOT_APPLICABLE'},labels),'Nicht anwendbar');
 assert.equal(displayValue(null,labels),'Nicht aufgezeichnet');
});
test('precision-losing JavaScript numbers are never presented as exact audit values',()=>{
 assert.equal(displayDecimal(0.1,labels.unrecorded),'Nicht aufgezeichnet');
});
