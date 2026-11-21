import { ExactNumber, germanDecimal, isObject, isText, parseExact, textOr } from './json';

/**
 * Presentation model of a recorded assessment document (format v1/v2).
 * It only reshapes what the deterministic core already decided; nothing is evaluated here.
 */
export interface ValueView { kind: 'UNKNOWN' | 'TRUTH' | 'NUMBER' | 'CHOICE' | 'OTHER'; code: string; number?: ExactNumber }
export interface SourceView {
  id: string | null; title: string; authority: string | null; version: string | null;
  location: string | null; status: string | null; documentType: string | null;
}
export interface ConditionView {
  kind: string;
  result: string;
  field: string | null;
  expected: ValueView | null;
  actual: ValueView | null;
  minimum: ExactNumber | null;
  maximum: ExactNumber | null;
  evidenceRequirementId: string | null;
  evidenceStatus: string | null;
  numeric: NumericView | null;
  children: ConditionView[];
}
export interface NumericView { kind: string; field: string | null; value: ValueView | null; children: NumericView[] }
export interface RuleView { ruleId: string; ruleVersion: string; outcome: string; conditionResult: string; source: SourceView; condition: ConditionView }
export interface OutputView { outputId: string; value: ValueView | null; conditionResult: string; source: SourceView; condition: ConditionView }
export interface AssessmentView {
  outcome: string | null;
  knowledgeRelease: string | null;
  assessmentDate: string | null;
  platformVersion: string | null;
  missingRequiredFields: string[];
  rule: RuleView | null;
  outputs: OutputView[];
}

const MAX_DEPTH = 64;

/** `null` when absent, `undefined` when present but unreadable. */
export function readAssessment(json: string | null | undefined): AssessmentView | null | undefined {
  if (json === null || json === undefined || json === '') return null;
  let root: unknown;
  try { root = parseExact(json); } catch { return undefined; }
  return readAssessmentDocument(root);
}

/** Wie {@link readAssessment}, aber für ein bereits exakt geparstes Bewertungsdokument. */
export function readAssessmentDocument(root: unknown): AssessmentView | null | undefined {
  if (root === null || root === undefined) return null;
  if (!isObject(root) || !isObject(root['assessment'])) return undefined;
  const assessment = root['assessment'];
  try {
    const missing = assessment['missingRequiredFields'];
    const outputs = assessment['domainOutputs'];
    return {
      outcome: textOr(assessment['outcome']),
      knowledgeRelease: textOr(assessment['knowledgeRelease']),
      assessmentDate: textOr(assessment['assessmentDate']),
      platformVersion: textOr(root['platformVersion']),
      missingRequiredFields: Array.isArray(missing) ? missing.filter(isText) : [],
      rule: isObject(assessment['ruleTrace']) ? rule(assessment['ruleTrace']) : null,
      outputs: Array.isArray(outputs) ? outputs.filter(isObject).map(output) : [],
    };
  } catch { return undefined; }
}

function rule(value: Record<string, unknown>): RuleView {
  return {
    ruleId: required(value['ruleId']),
    ruleVersion: versionText(value['ruleVersion']),
    outcome: required(value['outcome']),
    conditionResult: required(value['conditionResult']),
    source: source(value['source']),
    condition: condition(value['condition'], 0),
  };
}

function output(value: Record<string, unknown>): OutputView {
  return {
    outputId: required(value['outputId']),
    value: caseValue(value['value']),
    conditionResult: required(value['conditionResult']),
    source: source(value['source']),
    condition: condition(value['condition'], 0),
  };
}

function condition(value: unknown, depth: number): ConditionView {
  if (!isObject(value) || depth > MAX_DEPTH) throw new Error('invalid condition');
  const children = value['children'];
  if (!Array.isArray(children)) throw new Error('invalid condition');
  return {
    kind: required(value['kind']),
    result: required(value['result']),
    field: textOr(value['field']),
    expected: caseValue(value['expected']),
    actual: caseValue(value['actual']),
    minimum: value['minimum'] instanceof ExactNumber ? value['minimum'] : null,
    maximum: value['maximum'] instanceof ExactNumber ? value['maximum'] : null,
    evidenceRequirementId: textOr(value['evidenceRequirementId']),
    evidenceStatus: textOr(value['evidenceStatus']),
    numeric: isObject(value['numericExpression']) ? numeric(value['numericExpression'], depth + 1) : null,
    children: children.map(child => condition(child, depth + 1)),
  };
}

function numeric(value: Record<string, unknown>, depth: number): NumericView {
  if (depth > MAX_DEPTH) throw new Error('invalid expression');
  const children = value['children'];
  return {
    kind: required(value['kind']),
    field: textOr(value['field']),
    value: caseValue(value['value']),
    children: Array.isArray(children) ? children.filter(isObject).map(child => numeric(child, depth + 1)) : [],
  };
}

function caseValue(value: unknown): ValueView | null {
  if (!isObject(value) || !isText(value['kind'])) return null;
  switch (value['kind']) {
    case 'UNKNOWN': return { kind: 'UNKNOWN', code: 'UNKNOWN' };
    case 'TRUTH': return { kind: 'TRUTH', code: textOr(value['truth'], 'UNKNOWN')! };
    case 'NUMBER': return value['number'] instanceof ExactNumber ? { kind: 'NUMBER', code: 'NUMBER', number: value['number'] } : null;
    case 'CHOICE': return { kind: 'CHOICE', code: textOr(value['choice'], '')! };
    default: return { kind: 'OTHER', code: value['kind'] };
  }
}

function source(value: unknown): SourceView {
  if (!isObject(value)) return { id: null, title: '', authority: null, version: null, location: null, status: null, documentType: null };
  return {
    id: textOr(value['id']), title: textOr(value['title'], '')!, authority: textOr(value['authority']),
    version: textOr(value['version']), location: textOr(value['sourceLocation']), status: textOr(value['status']),
    documentType: textOr(value['documentType']),
  };
}

function required(value: unknown): string {
  if (!isText(value)) throw new Error('missing text');
  return value;
}
function versionText(value: unknown): string {
  if (value instanceof ExactNumber) return value.text;
  return isText(value) ? value : '';
}

/** German display for a traced value; numbers keep every digit. */
export function displayValue(value: ValueView | null, labels: { yes: string; no: string; na: string; unknown: string; unrecorded: string }): string {
  if (!value) return labels.unrecorded;
  if (value.kind === 'UNKNOWN') return labels.unknown;
  if (value.kind === 'NUMBER' && value.number) return germanDecimal(value.number);
  if (value.kind === 'TRUTH') return value.code === 'YES' ? labels.yes : value.code === 'NO' ? labels.no : value.code === 'NOT_APPLICABLE' ? labels.na : labels.unknown;
  return value.code || labels.unrecorded;
}
