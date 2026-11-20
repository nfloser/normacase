/**
 * Read-only view of the existing synthetic case-detail contract.
 *
 * The browser only checks the wire shape and reads display fields. It never
 * re-evaluates, re-serializes or stores the assessment; revisions stay the
 * exact strings sent by the host.
 */
export interface AuditEntry {
  sequence: string;
  kind: string;
  occurredAtUtc: string;
  actorId: string;
  disposition: string | null;
  reason: string | null;
  overrideOutcome: string | null;
}

export interface AssessmentSummary {
  outcome: string | null;
  knowledgeRelease: string | null;
  missingRequiredFields: string[];
}

export interface CaseDetail {
  caseId: string;
  caseRevision: string;
  processRevision: string;
  stateId: string;
  assessmentId: string;
  packId: string;
  auditRevision: string;
  evidence: [field: string, status: string][];
  allowedActions: string[];
  audit: AuditEntry[];
  /** Exact assessment document as sent; only read for display. */
  assessmentJson: string;
  /** `null` when the host sent no assessment; `undefined` when it could not be read. */
  assessment: AssessmentSummary | null | undefined;
}

const MAX_RESPONSE_LENGTH = 1_048_576;
const REQUIRED_TEXT = ['caseRevision', 'processRevision', 'stateId', 'assessmentId', 'packId', 'assessmentJson', 'auditRevision'] as const;

type Json = Record<string, unknown>;
const isObject = (value: unknown): value is Json => typeof value === 'object' && value !== null && !Array.isArray(value);
const isText = (value: unknown): value is string => typeof value === 'string';
const isNullableText = (value: unknown): value is string | null => value === null || typeof value === 'string';

/** Returns `null` for anything that does not match the contract or belongs to another case. */
export function parseCaseDetail(body: unknown, expectedCaseId: string): CaseDetail | null {
  if (!isText(body) || body.length > MAX_RESPONSE_LENGTH || !expectedCaseId) return null;
  let root: unknown;
  try { root = JSON.parse(body); } catch { return null; }
  if (!isObject(root) || root['caseId'] !== expectedCaseId) return null;
  if (!REQUIRED_TEXT.every(field => isText(root[field]))) return null;

  const evidence = root['evidence'];
  if (!isObject(evidence) || !Object.values(evidence).every(isText)) return null;
  const actions = root['allowedActions'];
  if (!Array.isArray(actions) || !actions.every(isText)) return null;
  const audit = root['audit'];
  if (!Array.isArray(audit)) return null;
  const entries: AuditEntry[] = [];
  for (const event of audit) {
    if (!isObject(event)) return null;
    const { sequence, kind, occurredAtUtc, actorId, disposition, reason, overrideOutcome } = event;
    if (!isText(sequence) || !isText(kind) || !isText(occurredAtUtc) || !isText(actorId)) return null;
    if (!isNullableText(disposition) || !isNullableText(reason) || !isNullableText(overrideOutcome)) return null;
    entries.push({ sequence, kind, occurredAtUtc, actorId, disposition, reason, overrideOutcome });
  }

  return {
    caseId: expectedCaseId,
    caseRevision: root['caseRevision'] as string,
    processRevision: root['processRevision'] as string,
    stateId: root['stateId'] as string,
    assessmentId: root['assessmentId'] as string,
    packId: root['packId'] as string,
    auditRevision: root['auditRevision'] as string,
    evidence: Object.entries(evidence) as [string, string][],
    allowedActions: [...actions],
    audit: entries,
    assessmentJson: root['assessmentJson'] as string,
    assessment: summarize(root['assessmentJson'] as string),
  };
}

/** Reads only the display fields; numbers inside the assessment are never shown or rewritten. */
function summarize(assessmentJson: string): AssessmentSummary | null | undefined {
  if (assessmentJson === '') return null;
  let parsed: unknown;
  try { parsed = JSON.parse(assessmentJson); } catch { return undefined; }
  const assessment = isObject(parsed) ? parsed['assessment'] : undefined;
  if (!isObject(assessment)) return undefined;
  const missing = assessment['missingRequiredFields'];
  return {
    outcome: isText(assessment['outcome']) ? assessment['outcome'] : null,
    knowledgeRelease: isText(assessment['knowledgeRelease']) ? assessment['knowledgeRelease'] : null,
    missingRequiredFields: Array.isArray(missing) ? missing.filter(isText) : [],
  };
}
