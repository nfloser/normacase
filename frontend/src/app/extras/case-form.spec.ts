import { caseJson, formFromCase, invalidNumbers } from './case-form';
import { parseExact } from '../shared/json';

describe('Falleingabe der Prüfwerkstatt', () => {
  const release = {
    releaseId: 'r', fields: [{ id: 'a', type: 'truth' as const, required: true }, { id: 'n', type: 'number' as const, required: false }],
    evidenceRequirements: ['doc'],
  };

  it('liest ein Beispiel exakt ein und schreibt es exakt zurück', () => {
    const text = '{"formatVersion":1,"assessmentDate":"2026-10-03","facts":{"a":{"kind":"TRUTH","truth":"YES"},"n":{"kind":"NUMBER","number":12.50}},"evidence":{"doc":"PRESENT"}}';
    const form = formFromCase(release, parseExact(text));
    expect(form.fields.map((f) => f.value)).toEqual(['YES', '12,50']);
    expect(caseJson(form)).toBe(text);
  });

  it('kennzeichnet unbekannte Angaben ausdrücklich und prüft Zahlen', () => {
    const form = { date: '2026-01-01', fields: [{ id: 'a', type: 'truth' as const, required: true, value: 'UNKNOWN' }, { id: 'n', type: 'number' as const, required: false, value: '1.5' }], evidence: [] };
    expect(invalidNumbers(form)).toEqual(['n']);
    expect(caseJson({ ...form, fields: [form.fields[0]] })).toBe('{"formatVersion":1,"assessmentDate":"2026-01-01","facts":{"a":{"kind":"UNKNOWN"}},"evidence":{}}');
  });
});
