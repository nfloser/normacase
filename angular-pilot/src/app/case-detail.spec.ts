import { parseCaseDetail } from './case-detail';

const assessment = JSON.stringify({ assessment: { outcome: 'HUMAN_REVIEW', knowledgeRelease: 'demo-g@1.0.0', missingRequiredFields: ['befund'], score: 12.3400 } });
function body(overrides: Record<string, unknown> = {}): string {
  return JSON.stringify({
    caseId: 'demo-g-review', caseRevision: '90071992547409931234', processRevision: '2', stateId: 'manual-review',
    assessmentId: 'assessment-1', packId: 'demo-g', assessmentJson: assessment, evidence: { befund: 'MISSING', arztbrief: 'PRESENT' },
    allowedActions: ['ACCEPT_SYSTEM_RESULT'], auditRevision: '1',
    audit: [{ sequence: '1', kind: 'ASSESSMENT_CREATED', occurredAtUtc: '2026-10-04T08:00:00Z', actorId: 'system', disposition: null, reason: null, overrideOutcome: null }],
    future: { anything: true }, ...overrides,
  });
}

describe('parseCaseDetail', () => {
  it('keeps revisions as exact strings and reads only display fields', () => {
    const detail = parseCaseDetail(body(), 'demo-g-review')!;
    expect(detail.caseRevision).toBe('90071992547409931234');
    expect(detail.evidence).toEqual([['befund', 'MISSING'], ['arztbrief', 'PRESENT']]);
    expect(detail.assessment).toEqual({ outcome: 'HUMAN_REVIEW', knowledgeRelease: 'demo-g@1.0.0', missingRequiredFields: ['befund'] });
    expect(detail.audit[0].disposition).toBeNull();
  });
  it('rejects a response for another case or with the wrong wire types', () => {
    expect(parseCaseDetail(body(), 'demo-g-supported')).toBeNull();
    expect(parseCaseDetail(body({ caseRevision: 9007199254740993 }), 'demo-g-review')).toBeNull();
    expect(parseCaseDetail(body({ evidence: { befund: null } }), 'demo-g-review')).toBeNull();
    expect(parseCaseDetail(body({ evidence: [] }), 'demo-g-review')).toBeNull();
    expect(parseCaseDetail(body({ allowedActions: [1] }), 'demo-g-review')).toBeNull();
    expect(parseCaseDetail(body({ audit: [{ sequence: '1' }] }), 'demo-g-review')).toBeNull();
    expect(parseCaseDetail(body({ stateId: undefined }), 'demo-g-review')).toBeNull();
    expect(parseCaseDetail('not json', 'demo-g-review')).toBeNull();
    expect(parseCaseDetail('[]', 'demo-g-review')).toBeNull();
    expect(parseCaseDetail(' '.repeat(1_048_577), 'demo-g-review')).toBeNull();
  });
  it('separates a missing assessment from an unreadable one', () => {
    expect(parseCaseDetail(body({ assessmentJson: '' }), 'demo-g-review')!.assessment).toBeNull();
    expect(parseCaseDetail(body({ assessmentJson: '{broken' }), 'demo-g-review')!.assessment).toBeUndefined();
    expect(parseCaseDetail(body({ assessmentJson: '{"other":1}' }), 'demo-g-review')!.assessment).toBeUndefined();
  });
  it('does not treat prototype keys as known evidence', () => {
    const detail = parseCaseDetail('{"caseId":"demo-g-review","caseRevision":"1","processRevision":"1","stateId":"x","assessmentId":"a","packId":"p","assessmentJson":"","auditRevision":"1","evidence":{"__proto__":"PRESENT"},"allowedActions":[],"audit":[]}', 'demo-g-review')!;
    expect(detail.evidence).toEqual([['__proto__', 'PRESENT']]);
  });
});
