import { ProcessDetail, ProcessSummary } from './api';

export const summary = (id: string, overrides: Partial<ProcessSummary> = {}): ProcessSummary => ({
  processId: id,
  externalReference: { system: 'host-a', caseId: 'VG-' + id },
  knowledgeRelease: 'demo-g-2026.1',
  state: 'IN_REVIEW',
  outcome: 'SUPPORTED',
  receivedAt: '2026-11-21T10:00:00Z',
  expiresAt: '2026-12-21T10:00:00Z',
  displayName: 'Synthetische Person ' + id,
  confirmedBy: null,
  documentCount: 1,
  missingFieldCount: 0,
  ...overrides,
});

/** Fallakte als JSON-Text, wie sie die API liefert; Zahlen mit Nachkommastellen bleiben exakt. */
export const detailJson = (id: string, overrides: Partial<Record<keyof ProcessDetail, unknown>> = {}): string => JSON.stringify({
  processId: id,
  externalReference: { system: 'host-a', caseId: 'VG-' + id },
  displayName: 'Synthetische Person ' + id,
  knowledgeRelease: 'demo-g-2026.1',
  state: 'IN_REVIEW',
  queue: 'review',
  outcome: 'SUPPORTED',
  evidenceId: '00000000-0000-0000-0000-000000000001',
  confirmedBy: null,
  receivedAt: '2026-11-21T10:00:00Z',
  expiresAt: '2026-12-21T10:00:00Z',
  assessment: {
    formatVersion: 2, platformVersion: 'test',
    assessment: {
      knowledgeRelease: 'demo-g-2026.1', assessmentDate: '2026-10-02', outcome: 'SUPPORTED', missingRequiredFields: [],
      ruleTrace: {
        ruleId: 'DEMO-G-DECISION', ruleVersion: 1, sourceId: 'S', conditionResult: 'MATCHED', outcome: 'SUPPORTED',
        condition: { kind: 'field_equals', result: 'MATCHED', field: 'request_complete', expected: { kind: 'TRUTH', truth: 'YES' },
          actual: { kind: 'TRUTH', truth: 'YES' }, minimum: null, maximum: null, children: [], evidenceRequirementId: null,
          evidenceStatus: null, numericExpression: null },
        source: { id: 'S', version: '1', sourceLocation: null, authority: 'Test', title: 'Fiktive Regelquelle', documentType: 'SYNTHETIC',
          status: 'ACTIVE', publicationDate: null, validFrom: null, validUntil: null, retrievedAt: null, contentHash: null },
      },
      domainOutputs: [],
    },
  },
  caseInput: { formatVersion: 1, assessmentDate: '2026-10-02', facts: { request_complete: { kind: 'TRUTH', truth: 'YES' } }, evidence: { supporting_document: 'PRESENT' } },
  documents: [{ id: 'auftrag', title: 'Prüfauftrag', mediaType: 'application/pdf', sha256: 'sha256:x', sizeBytes: 2048, pageCount: 2 }],
  history: [{ at: '2026-11-21T10:00:00Z', actor: 'host-a', action: 'RECEIVED', reason: null }],
  actions: ['clarify', 'confirm'],
  ...overrides,
});
