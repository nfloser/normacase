import {test} from 'node:test';
import assert from 'node:assert/strict';
import {caseIdentity} from '../src/casePresentation.ts';
test('clinical identity comes from the existing dossier, not the technical key',()=>{
 assert.deepEqual(caseIdentity('reference-care-complete','NC-2026-4105 · Erika Stein · Pflege-Score: Modulsummenabgleich'),{number:'NC-2026-4105',person:'Erika Stein',subject:'Pflege: Modulsummenabgleich'});
});
test('platform fixtures retain explicit scope without invented clinical identities',()=>{
 assert.deepEqual(caseIdentity('demo-g-supported','Synthetischer Arbeitslistenfall'),{number:'demo-g-supported',person:'',subject:'Plattformprüfung'});
});
