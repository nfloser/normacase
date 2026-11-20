import { Component, computed, input, output, signal, effect, untracked } from '@angular/core';
import { de, label, wb } from '../de';
import { DecisionTrace } from '../shared/decision-trace';
import { readAssessment } from '../shared/assessment';
import { DemoDocument, DocumentCase, Observation, safeExternalUrl } from './document-case';
import { PdfReader, ReaderTarget } from './pdf-reader';
import { AuditEntry, caseIdentity, processingStep, WorkCase } from './workspace';

export type CaseTab = 'overview' | 'documents' | 'evidence' | 'data' | 'history';
const TABS: CaseTab[] = ['overview', 'documents', 'evidence', 'data', 'history'];

/** Case file: identity, next step, result and reasoning, documents, provenance, data and history. */
@Component({
  selector: 'nc-case-view',
  imports: [DecisionTrace, PdfReader],
  templateUrl: './case-view.html',
  host: { class: 'case-view', '[class.full]': "mode() === 'full'" },
})
export class CaseView {
  readonly item = input.required<WorkCase>();
  /** `undefined` while loading, `null` when the case has no document file. */
  readonly file = input<DocumentCase | null | undefined>(undefined);
  readonly audit = input<AuditEntry[]>([]);
  readonly folderLabel = input<string | null>(null);
  readonly busy = input(false);
  readonly mode = input<'pane' | 'full'>('pane');
  readonly initialTab = input<CaseTab>('overview');
  readonly back = output<void>();
  readonly expand = output<void>();
  readonly confirmCase = output<void>();
  readonly dispatchCase = output<void>();
  readonly saveNote = output<string>();
  protected readonly text = wb;
  protected readonly tabs = TABS;

  protected readonly tab = signal<CaseTab>('overview');
  protected readonly target = signal<ReaderTarget | null>(null);
  protected readonly copyNotice = signal('');
  protected readonly note = signal('');
  private token = 0;

  protected readonly identity = computed(() => caseIdentity(this.item().caseId, this.item().title, wb.platformCheck));
  protected readonly step = computed(() => processingStep(this.item()));
  protected readonly assessment = computed(() => readAssessment(this.file()?.assessmentJson));
  protected readonly outcome = computed(() => { const a = this.assessment(); return a ? a.outcome : null; });
  protected readonly observationGroups = computed(() => {
    const file = this.file();
    if (!file) return [];
    return file.documents
      .map(doc => ({ doc, items: file.observations.filter(o => o.documentId === doc.id) }))
      .filter(group => group.items.length > 0);
  });
  protected readonly caseAudit = computed(() => this.audit().filter(entry => entry.caseIds.includes(this.item().caseId)).slice().reverse());
  protected readonly kindLabel = computed(() => {
    const file = this.file();
    if (!file) return '';
    return file.validationLevel === 'PUBLIC_REFERENCE' ? wb.publicReference : file.assessmentJson ? wb.synthetic : wb.documentOnly;
  });

  constructor() {
    effect(() => { const tab = this.initialTab(); this.item(); untracked(() => { this.tab.set(tab); this.copyNotice.set(''); }); });
    effect(() => { const note = this.item().organization.note; untracked(() => this.note.set(note)); });
  }

  protected select(tab: CaseTab): void { this.tab.set(tab); }
  protected tabKeys(event: KeyboardEvent): void {
    const index = TABS.indexOf(this.tab());
    const next = event.key === 'ArrowRight' ? index + 1 : event.key === 'ArrowLeft' ? index - 1 : event.key === 'Home' ? 0 : event.key === 'End' ? TABS.length - 1 : -2;
    if (next === -2) return;
    event.preventDefault();
    const tab = TABS[(next + TABS.length) % TABS.length];
    this.tab.set(tab);
    (event.currentTarget as HTMLElement).parentElement?.querySelector<HTMLElement>(`[data-tab="${tab}"]`)?.focus();
  }
  protected tabLabel(tab: CaseTab): string {
    const file = this.file();
    const count = tab === 'documents' ? file?.documents.length : tab === 'evidence' ? file?.observations.length : undefined;
    return wb.tabs[tab] + (count ? ` (${count})` : '');
  }

  protected openObservation(o: Observation): void {
    this.target.set({ documentId: o.documentId, page: o.page, label: this.fieldLabel(o.field), token: ++this.token });
    this.tab.set('documents');
  }
  protected openDocument(doc: DemoDocument): void {
    this.target.set({ documentId: doc.id, page: 1, token: ++this.token });
    this.tab.set('documents');
  }

  protected outcomeLabel(): string {
    const a = this.assessment();
    if (a === undefined) return de.unknown;
    if (a === null) return this.file() ? wb.noRuleResult : wb.pending;
    return label(de.outcomes, a.outcome, de.unknown);
  }
  protected tone(): string { const o = this.outcome(); return o !== null && Object.hasOwn(de.outcomes, o) ? o : 'NONE'; }
  protected queueName(id: string): string { return label(wb.queueNames, id, de.unknown); }
  protected status(): string {
    const o = this.item().organization;
    return o.dispatched ? wb.dispatched : o.confirmed ? wb.confirmed : this.queueName(this.item().queueId);
  }
  protected fieldLabel(field: string): string { return label(this.file()?.fieldLabels ?? {}, field, field); }
  protected evidenceLabel(id: string): string { return label(this.file()?.evidenceLabels ?? {}, id, id); }
  protected evidenceStatus(status: string): string { return label(de.evidenceStatuses, status, de.unknown); }
  protected value(raw: string): string {
    const truth: Record<string, string> = { YES: wb.trace.values.yes, NO: wb.trace.values.no, NOT_APPLICABLE: wb.trace.values.na, UNKNOWN: wb.trace.values.unknown };
    return Object.hasOwn(truth, raw) ? truth[raw] : /^-?\d+(\.\d+)?$/.test(raw) ? raw.replace('.', ',') : raw;
  }
  protected actionLabel(action: string): string { return label(wb.actions, action, action); }
  protected sourceUrl(url: string): string | null { return safeExternalUrl(url); }
  protected germanDate(iso: string | null | undefined): string {
    const match = iso ? /^(\d{4})-(\d{2})-(\d{2})$/.exec(iso) : null;
    return match ? `${match[3]}.${match[2]}.${match[1]}` : iso ?? wb.none;
  }

  protected async copy(kind: 'number' | 'result'): Promise<void> {
    const file = this.file();
    const id = this.identity();
    const a = this.assessment();
    const textValue = kind === 'number' ? id.number : [
      `Fall: ${this.item().title}`, `Aktenzeichen: ${id.number}`, `Prüfergebnis: ${this.outcomeLabel()}`,
      ...(a?.assessmentDate ? [`Prüfdatum: ${this.germanDate(a.assessmentDate)}`] : []),
      ...(a?.knowledgeRelease ? [`Wissensstand: ${a.knowledgeRelease}`] : []),
      ...(a?.missingRequiredFields.length ? ['Fehlende Pflichtangaben:', ...a.missingRequiredFields.map(f => '– ' + this.fieldLabel(f))] : []),
      ...(file?.findings.length ? ['Offene Punkte:', ...file.findings.map(f => '– ' + f)] : []),
      'Synthetischer Schulungsfall · Kein verbindlicher medizinischer oder rechtlicher Prüfentscheid.',
    ].join('\n');
    try {
      if (!navigator.clipboard?.writeText) throw new Error('clipboard');
      await navigator.clipboard.writeText(textValue);
      this.copyNotice.set(wb.copied);
    } catch { this.copyNotice.set(wb.copyFailed); }
  }
}
