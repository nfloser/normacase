import { LosslessNumber, parse, stringify } from 'lossless-json';

export interface WorkflowPresentation { id:string; version:number; label:string; states:Record<string,string>; transitions:Record<string,string> }

export interface Field { id: string; type: string; required: boolean }
export interface Presentation {
  title: string; description: string; outputs?: Record<string,{label:string;choices:Record<string,string>}>; fields: Record<string,string>; evidence: Record<string,string>;
  examples: { label: string; file: string }[];
  workflows?: WorkflowPresentation[];
}
export interface Pack {
  packId: string; releaseId: string; fields: Field[]; evidenceRequirements: string[]; presentation?: Presentation;
}
export function decimal(text: string): LosslessNumber {
  const value = text.trim().replace(',', '.');
  if (!/^-?(0|[1-9]\d*)(\.\d+)?$/.test(value)) throw new Error('invalid_number');
  return new LosslessNumber(value);
}
export function requestJson(pack: Pack, date: string, values: Record<string,string>, evidence: Record<string,string>): string {
  if (!/^\d{4}-\d{2}-\d{2}$/.test(date)) throw new Error('missing_date');
  const facts = Object.fromEntries(pack.fields.map(field => {
    const value = values[field.id] ?? '';
    if (field.type === 'number') return [field.id, value.trim() === '' || value === 'UNKNOWN' ? { kind: 'UNKNOWN' } : { kind: 'NUMBER', number: decimal(value) }];
    if (field.type !== 'truth') throw new Error('unsupported_field');
    if (value === '' || value === 'UNKNOWN') return [field.id, { kind: 'UNKNOWN' }];
    if (!['YES','NO','NOT_APPLICABLE'].includes(value)) throw new Error('invalid_truth');
    return [field.id, { kind: 'TRUTH', truth: value }];
  }));
  return stringify({ formatVersion: 1, assessmentDate: date, facts,
    evidence: Object.fromEntries(pack.evidenceRequirements.map(id => [id, evidence[id] ?? 'MISSING'])) })!;
}
export function exampleValues(json: string): {date: string; values: Record<string,string>; evidence: Record<string,string>} {
  const loaded = parse(json) as {assessmentDate:string;values?:Record<string,string>;evidence?:Record<string,string>};
  if (loaded.values) return {date:loaded.assessmentDate,values:Object.fromEntries(Object.entries(loaded.values).map(([id,value])=>[id,value === 'UNKNOWN' ? 'UNKNOWN' : value.replace('.', ',')])),evidence:loaded.evidence??{}};
  const input = parse(json) as {assessmentDate:string; facts:Record<string,{kind:string; number?:LosslessNumber; truth?:string}>; evidence?:Record<string,string>};
  return {date:input.assessmentDate, evidence:input.evidence ?? {}, values:Object.fromEntries(Object.entries(input.facts).map(([id,value]) =>
    [id, value.kind === 'NUMBER' ? value.number!.value.replace('.', ',') : value.kind === 'TRUTH' ? value.truth! : 'UNKNOWN']))};
}

export function normalizeCatalog(data: {packId:string;releaseId:string;fields:(Field & {label:string})[];evidenceRequirements:{id:string;label:string}[];presentation:{name:string;description:string;workflows?:WorkflowPresentation[];outputs:{id:string;label:string;choices:Record<string,string>}[];examples:{id:string;label:string}[]}}[]): Pack[] {
 return data.map(p=>({packId:p.packId,releaseId:p.releaseId,fields:p.fields,evidenceRequirements:p.evidenceRequirements.map(e=>e.id),presentation:{title:p.presentation.name,description:p.presentation.description,workflows:p.presentation.workflows??[],fields:Object.fromEntries(p.fields.map(f=>[f.id,f.label])),evidence:Object.fromEntries(p.evidenceRequirements.map(e=>[e.id,e.label])),outputs:Object.fromEntries(p.presentation.outputs.map(o=>[o.id,{label:o.label,choices:o.choices}])),examples:p.presentation.examples.map(e=>({file:e.id,label:e.label}))}}));
}
