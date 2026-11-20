import { isNullableText, isObject, isText, Json } from '../shared/json';

/**
 * Read-only contracts of the anonymous synthetic preview host
 * (`/api/work-queues`, `/api/document-cases`, `/api/document-cases/{id}`).
 * Anything that does not match is rejected as a whole.
 */
export interface DemoQueueItem { caseId: string; stateId: string }
export interface DemoQueue { queueId: string; items: DemoQueueItem[] }
export interface DemoQueues { totalCases: number; queues: DemoQueue[] }
export interface CaseContext { request: string; question: string; background: string }
export interface ReferenceCase { caseId: string; title: string; scope: string; context: CaseContext | null }
export interface DemoSource { id: string; title: string; version: string; url: string; scope: string }
export interface DemoDocument { id: string; title: string; mediaType: string; pages: number; previewPages: number[] }
export interface Observation { documentId: string; page: number; field: string; value: string; method: string }
export interface OutputLabel { label: string; choices: Record<string, string> }
export interface DocumentCase {
  caseId: string;
  title: string;
  scope: string;
  context: CaseContext | null;
  validationLevel: string;
  documents: DemoDocument[];
  observations: Observation[];
  findings: string[];
  fieldLabels: Record<string, string>;
  evidenceLabels: Record<string, string>;
  outputLabels: Record<string, OutputLabel>;
  source: DemoSource | null;
  assessmentJson: string | null;
  evidence: [requirement: string, status: string][];
}

const MAX_BODY = 4 * 1024 * 1024;
const SAFE_ID = /^[a-z0-9-]{1,100}$/;

function parse(body: unknown): unknown {
  if (!isText(body) || body.length > MAX_BODY) return null;
  try { return JSON.parse(body); } catch { return null; }
}
function textMap(value: unknown): Record<string, string> | null {
  if (!isObject(value)) return null;
  const result: Record<string, string> = {};
  for (const [key, text] of Object.entries(value)) { if (!isText(text)) return null; result[key] = text; }
  return result;
}
function context(value: unknown): CaseContext | null | undefined {
  if (value === null || value === undefined) return null;
  if (!isObject(value) || !isText(value['request']) || !isText(value['question']) || !isText(value['background'])) return undefined;
  return { request: value['request'], question: value['question'], background: value['background'] };
}

export function parseDemoQueues(body: unknown): DemoQueues | null {
  const root = parse(body);
  if (!isObject(root) || typeof root['totalCases'] !== 'number' || !Array.isArray(root['queues'])) return null;
  const queues: DemoQueue[] = [];
  for (const queue of root['queues']) {
    if (!isObject(queue) || !isText(queue['queueId']) || !Array.isArray(queue['items'])) return null;
    const items: DemoQueueItem[] = [];
    for (const item of queue['items']) {
      if (!isObject(item) || !isText(item['caseId']) || !isText(item['stateId'])) return null;
      items.push({ caseId: item['caseId'], stateId: item['stateId'] });
    }
    queues.push({ queueId: queue['queueId'], items });
  }
  return { totalCases: root['totalCases'], queues };
}

export function parseReferenceCases(body: unknown): ReferenceCase[] | null {
  const root = parse(body);
  if (!isObject(root) || !Array.isArray(root['cases'])) return null;
  const cases: ReferenceCase[] = [];
  for (const item of root['cases']) {
    if (!isObject(item) || !isText(item['caseId']) || !SAFE_ID.test(item['caseId']) || !isText(item['title']) || !isText(item['scope'])) return null;
    const ctx = context(item['context']);
    if (ctx === undefined) return null;
    cases.push({ caseId: item['caseId'], title: item['title'], scope: item['scope'], context: ctx });
  }
  return cases;
}

export function parseDocumentCase(body: unknown, expectedCaseId: string): DocumentCase | null {
  const root = parse(body);
  if (!isObject(root) || root['caseId'] !== expectedCaseId || !isText(root['title']) || !isText(root['scope'])) return null;
  const ctx = context(root['context']);
  const fieldLabels = textMap(root['fieldLabels']);
  const evidenceLabels = textMap(root['evidenceLabels']);
  if (ctx === undefined || !fieldLabels || !evidenceLabels || !isNullableText(root['assessmentJson'] ?? null)) return null;
  if (!Array.isArray(root['documents']) || !Array.isArray(root['observations']) || !Array.isArray(root['findings'])) return null;

  const documents: DemoDocument[] = [];
  for (const doc of root['documents']) {
    if (!isObject(doc) || !isText(doc['id']) || !SAFE_ID.test(doc['id']) || !isText(doc['title']) || !isText(doc['mediaType'])
      || !Number.isInteger(doc['pages']) || (doc['pages'] as number) < 1) return null;
    const previews = Array.isArray(doc['previews']) ? doc['previews'] : [];
    const previewPages = previews.filter(isObject).map(p => p['page']).filter((p): p is number => Number.isInteger(p));
    documents.push({ id: doc['id'], title: doc['title'], mediaType: doc['mediaType'], pages: doc['pages'] as number, previewPages });
  }
  const observations: Observation[] = [];
  for (const item of root['observations']) {
    if (!isObject(item) || !isText(item['id']) || !Number.isInteger(item['page']) || !isText(item['field']) || !isText(item['value']) || !isText(item['method'])) return null;
    observations.push({ documentId: item['id'], page: item['page'] as number, field: item['field'], value: item['value'], method: item['method'] });
  }
  if (!root['findings'].every(isText)) return null;

  const outputLabels: Record<string, OutputLabel> = {};
  if (isObject(root['outputLabels'])) {
    for (const [key, value] of Object.entries(root['outputLabels'] as Json)) {
      const choices = isObject(value) ? textMap(value['choices']) : null;
      if (!isObject(value) || !isText(value['label']) || !choices) return null;
      outputLabels[key] = { label: value['label'], choices };
    }
  }
  const src = root['source'];
  const source = isObject(src) && ['id', 'title', 'version', 'url', 'scope'].every(key => isText(src[key]))
    ? { id: src['id'] as string, title: src['title'] as string, version: src['version'] as string, url: src['url'] as string, scope: src['scope'] as string }
    : null;

  return {
    caseId: expectedCaseId,
    title: root['title'],
    scope: root['scope'],
    context: ctx,
    validationLevel: isText(root['validationLevel']) ? root['validationLevel'] : '',
    documents,
    observations,
    findings: [...root['findings']] as string[],
    fieldLabels,
    evidenceLabels,
    outputLabels,
    source,
    assessmentJson: (root['assessmentJson'] as string | null | undefined) ?? null,
    evidence: inputEvidence(root['inputJson']),
  };
}

/** Evidence statuses exactly as recorded in the normalized input; never derived from documents. */
function inputEvidence(inputJson: unknown): [string, string][] {
  if (!isText(inputJson)) return [];
  try {
    const input = JSON.parse(inputJson);
    const evidence = isObject(input) ? input['evidence'] : null;
    return isObject(evidence) ? Object.entries(evidence).filter((entry): entry is [string, string] => isText(entry[1])) : [];
  } catch { return []; }
}

/** Only https links to public sources are offered; anything else stays plain text. */
export function safeExternalUrl(url: string): string | null {
  try { return new URL(url).protocol === 'https:' ? url : null; } catch { return null; }
}
