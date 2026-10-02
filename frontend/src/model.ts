import { LosslessNumber, parse, stringify } from 'lossless-json';

export interface Field { id: string; type: string; required: boolean }
export interface Presentation {
  title: string; description: string; outputs?: Record<string,string>; fields: Record<string,string>; evidence: Record<string,string>;
  examples: { label: string; file: string }[];
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
    if (field.type === 'number') return [field.id, value.trim() === '' ? { kind: 'UNKNOWN' } : { kind: 'NUMBER', number: decimal(value) }];
    if (field.type !== 'truth') throw new Error('unsupported_field');
    if (value === '' || value === 'UNKNOWN') return [field.id, { kind: 'UNKNOWN' }];
    if (!['YES','NO','NOT_APPLICABLE'].includes(value)) throw new Error('invalid_truth');
    return [field.id, { kind: 'TRUTH', truth: value }];
  }));
  return stringify({ formatVersion: 1, assessmentDate: date, facts,
    evidence: Object.fromEntries(pack.evidenceRequirements.map(id => [id, evidence[id] ?? 'MISSING'])) })!;
}
export function exampleValues(json: string): {date: string; values: Record<string,string>; evidence: Record<string,string>} {
  const input = parse(json) as {assessmentDate:string; facts:Record<string,{kind:string; number?:LosslessNumber; truth?:string}>; evidence?:Record<string,string>};
  return {date:input.assessmentDate, evidence:input.evidence ?? {}, values:Object.fromEntries(Object.entries(input.facts).map(([id,value]) =>
    [id, value.kind === 'NUMBER' ? value.number!.value.replace('.', ',') : value.kind === 'TRUTH' ? value.truth! : '']))};
}

export function normalizeCatalog(data: {packId:string;releaseId:string;fields:(Field & {label:string})[];evidenceRequirements:{id:string;label:string}[];presentation:{name:string;description:string;outputs:{id:string;label:string}[];examples:{id:string;label:string}[]}}[]): Pack[] {
 return data.map(p=>({packId:p.packId,releaseId:p.releaseId,fields:p.fields,evidenceRequirements:p.evidenceRequirements.map(e=>e.id),presentation:{title:p.presentation.name,description:p.presentation.description,fields:Object.fromEntries(p.fields.map(f=>[f.id,f.label])),evidence:Object.fromEntries(p.evidenceRequirements.map(e=>[e.id,e.label])),outputs:Object.fromEntries(p.presentation.outputs.map(o=>[o.id,o.label])),examples:p.presentation.examples.map(e=>({file:e.id,label:e.label}))}}));
}
