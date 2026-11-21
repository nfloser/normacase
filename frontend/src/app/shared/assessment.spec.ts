import { displayValue, readAssessment } from './assessment';
import { germanDecimal, parseExact, ExactNumber } from './json';

const labels = { yes: 'Ja', no: 'Nein', na: 'Nicht anwendbar', unknown: 'Unbekannt', unrecorded: 'nicht erfasst' };
const document = `{"formatVersion":2,"platformVersion":"0.1.0","assessment":{"knowledgeRelease":"r1","assessmentDate":"2026-10-10","outcome":"HUMAN_REVIEW",
 "missingRequiredFields":["care_grade"],"ruleTrace":{"ruleId":"R","ruleVersion":3,"sourceId":"S","conditionResult":"UNKNOWN","outcome":"HUMAN_REVIEW",
 "source":{"id":"S","title":"Quelle","authority":"A","version":"1"},
 "condition":{"kind":"all","result":"UNKNOWN","field":null,"expected":null,"actual":null,"minimum":null,"maximum":null,"children":[
  {"kind":"number_in_range","result":"MATCHED","field":"score","expected":null,"actual":{"kind":"NUMBER","number":123456789.1234567890123456789},"minimum":12.50,"maximum":100,"children":[]},
  {"kind":"requires_evidence","result":"UNKNOWN","field":null,"expected":null,"actual":null,"minimum":null,"maximum":null,"evidenceRequirementId":"proof","evidenceStatus":"CONFLICTING","children":[]}]}},
 "domainOutputs":[{"outputId":"o","outputVersion":1,"value":{"kind":"CHOICE","choice":"A"},"conditionResult":"MATCHED","source":{"title":"Q"},"condition":{"kind":"all","result":"MATCHED","children":[]}}]}}`;

describe('assessment presentation', () => {
  it('keeps every decimal digit from the source text', () => {
    const view = readAssessment(document)!;
    const range = view.rule!.condition.children[0];
    expect(range.actual!.number!.text).toBe('123456789.1234567890123456789');
    expect(displayValue(range.actual, labels)).toBe('123456789,1234567890123456789');
    expect(germanDecimal(range.minimum!)).toBe('12,50');
    expect(view.rule!.ruleVersion).toBe('3');
  });
  it('reads outcome, missing fields, evidence status, source and outputs without evaluating', () => {
    const view = readAssessment(document)!;
    expect(view.outcome).toBe('HUMAN_REVIEW');
    expect(view.missingRequiredFields).toEqual(['care_grade']);
    expect(view.rule!.condition.children[1].evidenceStatus).toBe('CONFLICTING');
    expect(view.rule!.source.title).toBe('Quelle');
    expect(view.outputs[0].value).toEqual({ kind: 'CHOICE', code: 'A' });
  });
  it('separates absent from unreadable assessments', () => {
    expect(readAssessment(null)).toBeNull();
    expect(readAssessment('')).toBeNull();
    expect(readAssessment('{broken')).toBeUndefined();
    expect(readAssessment('{"assessment":{"ruleTrace":{"ruleId":1}}}')).toBeUndefined();
    expect(readAssessment('{"assessment":{"outcome":"SUPPORTED","ruleTrace":null}}')!.rule).toBeNull();
  });
  it('refuses absurdly deep traces', () => {
    let node = '{"kind":"not","result":"UNKNOWN","children":[]}';
    for (let i = 0; i < 80; i++) node = `{"kind":"not","result":"UNKNOWN","children":[${node}]}`;
    expect(readAssessment(`{"assessment":{"outcome":"SUPPORTED","ruleTrace":{"ruleId":"R","outcome":"SUPPORTED","conditionResult":"MATCHED","condition":${node}}}}`)).toBeUndefined();
  });
  it('shows truth values in German and never invents a value', () => {
    expect(displayValue({ kind: 'TRUTH', code: 'YES' }, labels)).toBe('Ja');
    expect(displayValue({ kind: 'TRUTH', code: 'NOT_APPLICABLE' }, labels)).toBe('Nicht anwendbar');
    expect(displayValue({ kind: 'UNKNOWN', code: 'UNKNOWN' }, labels)).toBe('Unbekannt');
    expect(displayValue(null, labels)).toBe('nicht erfasst');
    expect(parseExact('[1e3]')).toEqual([new ExactNumber('1e3')]);
    expect(germanDecimal(new ExactNumber('1e3'))).toBe('1e3');
  });
});
