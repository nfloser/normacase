import { Component, computed, input, output, signal } from '@angular/core';
import { NgTemplateOutlet } from '@angular/common';
import { de, germanDate, germanDateTime, label } from '../de';
import { readAssessmentDocument, displayValue, ValueView } from '../shared/assessment';
import { DecisionTrace, OutputLabel } from '../shared/decision-trace';
import { ExactNumber, germanDecimal, isObject } from '../shared/json';
import { Presentation, ProcessDetail, ReleaseSummary } from './api';
import { DocumentReader } from './document-reader';

type Tab = 'review' | 'documents' | 'history' | 'evidence';
export type CaseAction =
  | { kind: 'confirm' }
  | { kind: 'approve' }
  | { kind: 'reject'; reason: string }
  | { kind: 'clarify'; caseInputJson: string };

interface FactRow { id: string; label: string; value: string; kind: string }
interface FormField { id: string; label: string; type: 'truth' | 'number'; required: boolean; value: string; error?: string }
interface FormEvidence { id: string; label: string; value: string }

/** Fallakte: Ergebnis, Begründung, Angaben, Dokumente, Verlauf und Nachweis eines Vorgangs. */
@Component({
  selector: 'nc-case-file',
  imports: [DecisionTrace, DocumentReader, NgTemplateOutlet],
  templateUrl: './case-file.html',
  host: { class: 'case-detail-host', '(document:keydown.escape)': 'closeDialogs()' },
})
export class CaseFile {
  readonly detail = input.required<ProcessDetail>();
  readonly presentation = input<Presentation>({});
  readonly release = input<ReleaseSummary | null>(null);
  readonly username = input.required<string>();
  readonly busy = input(false);
  /** Vollansicht: Begründung und Dokumente nebeneinander. */
  readonly wide = input(false);
  readonly act = output<CaseAction>();

  protected readonly t = de.caseFile;
  protected readonly de = de;
  protected readonly tabs: Tab[] = ['review', 'documents', 'history', 'evidence'];
  protected readonly tab = signal<Tab>('review');
  protected readonly rejecting = signal(false);
  protected readonly rejectReason = signal('CRITERIA_UNCLEAR');
  protected readonly clarifying = signal(false);
  protected readonly form = signal<FormField[]>([]);
  protected readonly formEvidence = signal<FormEvidence[]>([]);
  protected readonly hasNumberField = computed(() => this.form().some((f) => f.type === 'number'));
  protected readonly reasons = Object.keys(de.rejectionReasons);

  protected readonly assessment = computed(() => readAssessmentDocument(this.detail().assessment) ?? null);
  protected readonly fieldLabels = computed(() => labels(this.presentation().fields));
  protected readonly evidenceLabels = computed(() => labels(this.presentation().evidenceRequirements));
  protected readonly outputLabels = computed(() => (this.presentation().outputs ?? {}) as Record<string, OutputLabel>);

  protected readonly step = computed(() => {
    const d = this.detail();
    const can = (a: string) => d.actions.includes(a);
    if (d.state === 'CLOSED') return 'closed';
    if (d.state === 'DISPATCHED') return 'dispatched';
    if (can('approve')) return 'approve';
    if (d.state === 'AWAITING_APPROVAL' && d.confirmedBy === this.username()) return 'ownConfirmation';
    if (can('confirm')) return 'confirm';
    if (can('clarify')) return 'clarify';
    return 'waiting';
  });

  protected readonly facts = computed<FactRow[]>(() => {
    const input = this.detail().caseInput;
    if (!isObject(input) || !isObject(input['facts'])) return [];
    return Object.entries(input['facts']).map(([id, value]) => ({
      id, label: label(this.fieldLabels(), id, id), value: displayValue(toValueView(value), de.trace.values),
      kind: isObject(value) ? String(value['kind']) : 'UNKNOWN',
    }));
  });

  protected readonly evidence = computed(() => {
    const input = this.detail().caseInput;
    if (!isObject(input) || !isObject(input['evidence'])) return [];
    return Object.entries(input['evidence']).map(([id, status]) => ({
      id, label: label(this.evidenceLabels(), id, id), status: String(status),
    }));
  });

  protected closeDialogs(): void {
    this.rejecting.set(false);
    this.clarifying.set(false);
  }

  protected select(tab: Tab): void {
    this.tab.set(tab);
  }

  protected tabKeys(event: KeyboardEvent): void {
    const index = this.tabs.indexOf(this.tab());
    const next = event.key === 'ArrowRight' ? index + 1 : event.key === 'ArrowLeft' ? index - 1 : null;
    if (next === null) return;
    event.preventDefault();
    const tab = this.tabs[(next + this.tabs.length) % this.tabs.length];
    this.tab.set(tab);
    document.getElementById('tab-' + tab)?.focus();
  }

  protected can(action: string): boolean {
    return this.detail().actions.includes(action);
  }

  protected stateLabel(): string {
    return label(de.states, this.detail().state, de.unknown);
  }

  protected outcomeLabel(): string {
    return label(de.outcomes, this.detail().outcome, de.unknown);
  }

  protected historyLabel(action: string): string {
    return label(de.historyActions, action, action);
  }

  protected reasonLabel(reason: string | null): string {
    return reason ? label(de.rejectionReasons, reason, reason) : '';
  }

  protected readonly dateTime = germanDateTime;
  protected readonly date = germanDate;

  protected startClarify(): void {
    const release = this.release();
    const input = this.detail().caseInput;
    const facts = isObject(input) && isObject(input['facts']) ? input['facts'] : {};
    const evidence = isObject(input) && isObject(input['evidence']) ? input['evidence'] : {};
    this.form.set((release?.fields ?? []).map((f) => ({
      id: f.id, label: label(this.fieldLabels(), f.id, f.id), type: f.type, required: f.required,
      value: formValue(facts[f.id]),
    })));
    this.formEvidence.set((release?.evidenceRequirements ?? []).map((id) => ({
      id, label: label(this.evidenceLabels(), id, id), value: typeof evidence[id] === 'string' ? (evidence[id] as string) : 'MISSING',
    })));
    this.clarifying.set(true);
  }

  protected setField(id: string, value: string): void {
    this.form.update((fields) => fields.map((f) => (f.id === id ? { ...f, value, error: undefined } : f)));
  }

  protected setEvidence(id: string, value: string): void {
    this.formEvidence.update((list) => list.map((e) => (e.id === id ? { ...e, value } : e)));
  }

  /** Baut die Falleingabe als JSON-Text; Zahlen werden exakt aus der Eingabe übernommen. */
  protected submitClarify(): void {
    let valid = true;
    const facts: string[] = [];
    const fields = this.form().map((f) => {
      if (f.type === 'number' && f.value.trim() !== '' && !/^-?\d+(,\d+)?$/.test(f.value.trim())) {
        valid = false;
        return { ...f, error: this.t.invalidNumber };
      }
      return f;
    });
    this.form.set(fields);
    if (!valid) return;
    for (const f of fields) {
      const v = f.value.trim();
      if (f.type === 'truth' && v && v !== 'UNKNOWN') facts.push(`${JSON.stringify(f.id)}:{"kind":"TRUTH","truth":${JSON.stringify(v)}}`);
      else if (f.type === 'number' && v) facts.push(`${JSON.stringify(f.id)}:{"kind":"NUMBER","number":${v.replace(',', '.')}}`);
      else facts.push(`${JSON.stringify(f.id)}:{"kind":"UNKNOWN"}`);
    }
    const evidence = this.formEvidence().map((e) => `${JSON.stringify(e.id)}:${JSON.stringify(e.value)}`);
    const input = this.detail().caseInput;
    const date = isObject(input) && typeof input['assessmentDate'] === 'string' ? input['assessmentDate'] : new Date().toISOString().slice(0, 10);
    this.act.emit({
      kind: 'clarify',
      caseInputJson: `{"formatVersion":1,"assessmentDate":${JSON.stringify(date)},"facts":{${facts.join(',')}},"evidence":{${evidence.join(',')}}}`,
    });
    this.clarifying.set(false);
  }

  protected submitReject(): void {
    this.act.emit({ kind: 'reject', reason: this.rejectReason() });
    this.rejecting.set(false);
  }
}

function labels(source: Record<string, { label: string }> | undefined): Record<string, string> {
  return Object.fromEntries(Object.entries(source ?? {}).map(([k, v]) => [k, v.label]));
}

function toValueView(value: unknown): ValueView | null {
  if (!isObject(value)) return null;
  switch (value['kind']) {
    case 'TRUTH': return { kind: 'TRUTH', code: String(value['truth']) };
    case 'NUMBER': return value['number'] instanceof ExactNumber
      ? { kind: 'NUMBER', code: value['number'].text, number: value['number'] } : { kind: 'OTHER', code: '' };
    case 'UNKNOWN': return { kind: 'UNKNOWN', code: 'UNKNOWN' };
    default: return { kind: 'OTHER', code: '' };
  }
}

function formValue(value: unknown): string {
  if (!isObject(value)) return '';
  if (value['kind'] === 'TRUTH') return String(value['truth']);
  if (value['kind'] === 'NUMBER' && value['number'] instanceof ExactNumber) return germanDecimal(value['number']);
  return value['kind'] === 'UNKNOWN' ? 'UNKNOWN' : '';
}
