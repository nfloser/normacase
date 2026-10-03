export type ReviewSession = { actorId: string };

export type ReviewQueueItem = {
  caseId: string;
  caseRevision: string;
  processRevision: string;
  stateId: string;
  assessmentId: string;
};

export type ReviewQueues = {
  configurationId: string;
  configurationVersion: number;
  queues: { queueId: string; items: ReviewQueueItem[] }[];
};

export type ReviewAuditEntry = {
  sequence: string;
  kind: 'ASSESSMENT_CREATED' | 'HUMAN_REVIEW_RECORDED';
  occurredAtUtc: string;
  actorId: string;
  disposition: 'ACCEPT_SYSTEM_RESULT' | 'OVERRIDE' | null;
  reason: string | null;
  overrideOutcome: string | null;
};

export type ReviewDetail = ReviewQueueItem & {
  packId: string;
  assessmentJson: string;
  evidence: Record<string,string>;
  allowedActions: ('ACCEPT_SYSTEM_RESULT' | 'OVERRIDE')[];
  auditRevision: string;
  audit: ReviewAuditEntry[];
};

export type ReviewDisposition = 'ACCEPT_SYSTEM_RESULT' | 'OVERRIDE';

export type ReviewApiResult<T> =
  | { kind: 'ok'; value: T }
  | { kind: 'unauthorized' }
  | { kind: 'forbidden' }
  | { kind: 'conflict' }
  | { kind: 'disabled' }
  | { kind: 'error' };

export function bearerHeaders(credential: string): HeadersInit {
  return { Authorization: `Bearer ${credential}` };
}

export function reviewCommandJson(
  detail: ReviewDetail,
  disposition: ReviewDisposition,
  reason: string,
  overrideOutcome?: string
): string {
  const trimmed = reason.trim();
  if (!trimmed) throw new Error('missing_reason');
  if (disposition === 'OVERRIDE' && !overrideOutcome) throw new Error('missing_override');
  return JSON.stringify({
    expectedCaseRevision: detail.caseRevision,
    expectedProcessRevision: detail.processRevision,
    expectedAuditRevision: detail.auditRevision,
    disposition,
    reason: trimmed,
    overrideOutcome: disposition === 'OVERRIDE' ? overrideOutcome : null
  });
}

export async function reviewFetch<T>(
  path: string,
  credential: string,
  init: RequestInit = {}
): Promise<ReviewApiResult<T>> {
  try {
    const headers = new Headers(init.headers);
    headers.set('Authorization', `Bearer ${credential}`);
    const response = await fetch(path, { ...init, headers });
    if (response.status === 401) return { kind: 'unauthorized' };
    if (response.status === 403) return { kind: 'forbidden' };
    if (response.status === 409) return { kind: 'conflict' };
    if (response.status === 404) return { kind: 'disabled' };
    if (!response.ok) return { kind: 'error' };
    return { kind: 'ok', value: await response.json() as T };
  } catch {
    return { kind: 'error' };
  }
}
