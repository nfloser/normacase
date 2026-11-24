import { ReleaseSummary } from '../workbench/api';
import { ExactNumber, germanDecimal, isObject } from '../shared/json';

/** Angaben eines Regelwerks als Formular; Werte als Text, Zahlen mit deutschem Komma. */
export interface CaseField { readonly id: string; readonly type: 'truth' | 'number'; readonly required: boolean; readonly value: string }
export interface CaseEvidence { readonly id: string; readonly value: string }
export interface CaseForm { readonly date: string; readonly fields: CaseField[]; readonly evidence: CaseEvidence[] }

const NUMBER = /^-?\d+(,\d+)?$/;

export function emptyForm(release: ReleaseSummary | undefined): CaseForm {
  return {
    date: '',
    fields: (release?.fields ?? []).map((f) => ({ id: f.id, type: f.type, required: f.required, value: 'UNKNOWN' })),
    evidence: (release?.evidenceRequirements ?? []).map((id) => ({ id, value: 'MISSING' })),
  };
}

/** Übernimmt eine exakt geparste Falleingabe in das Formular. */
export function formFromCase(release: ReleaseSummary | undefined, input: unknown): CaseForm {
  const form = emptyForm(release);
  if (!isObject(input)) return form;
  const facts = isObject(input['facts']) ? input['facts'] : {};
  const evidence = isObject(input['evidence']) ? input['evidence'] : {};
  return {
    date: typeof input['assessmentDate'] === 'string' ? input['assessmentDate'] : '',
    fields: form.fields.map((f) => {
      const v = facts[f.id];
      if (!isObject(v)) return f;
      if (v['kind'] === 'TRUTH' && typeof v['truth'] === 'string') return { ...f, value: v['truth'] };
      if (v['kind'] === 'NUMBER' && v['number'] instanceof ExactNumber) return { ...f, value: germanDecimal(v['number']) };
      return f;
    }),
    evidence: form.evidence.map((e) => (typeof evidence[e.id] === 'string' ? { ...e, value: evidence[e.id] as string } : e)),
  };
}

/** Fehlerhafte Zahlenfelder (IDs); leer bedeutet gültig. */
export function invalidNumbers(form: CaseForm): string[] {
  return form.fields.filter((f) => f.type === 'number' && f.value !== 'UNKNOWN' && !NUMBER.test(f.value.trim())).map((f) => f.id);
}

/** Falleingabe als JSON-Text; Zahlen werden exakt aus der Eingabe übernommen, nie über Gleitkomma. */
export function caseJson(form: CaseForm): string {
  const facts = form.fields.map((f) => {
    const v = f.value.trim();
    if (f.type === 'truth' && v && v !== 'UNKNOWN') return `${JSON.stringify(f.id)}:{"kind":"TRUTH","truth":${JSON.stringify(v)}}`;
    if (f.type === 'number' && v && v !== 'UNKNOWN') return `${JSON.stringify(f.id)}:{"kind":"NUMBER","number":${v.replace(',', '.')}}`;
    return `${JSON.stringify(f.id)}:{"kind":"UNKNOWN"}`;
  });
  const evidence = form.evidence.map((e) => `${JSON.stringify(e.id)}:${JSON.stringify(e.value)}`);
  return `{"formatVersion":1,"assessmentDate":${JSON.stringify(form.date)},"facts":{${facts.join(',')}},"evidence":{${evidence.join(',')}}}`;
}

export function saveJson(text: string, name: string): void {
  const url = URL.createObjectURL(new Blob([text], { type: 'application/json' }));
  const a = document.createElement('a');
  a.href = url;
  a.download = name;
  a.click();
  setTimeout(() => URL.revokeObjectURL(url), 1000);
}
