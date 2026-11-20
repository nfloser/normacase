import { afterNextRender, Component, computed, ElementRef, inject, Injector, input, OnDestroy, OnInit, output, signal, viewChild } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Subscription } from 'rxjs';
import { de, demo, label } from '../de';
import { DecisionTrace } from '../shared/decision-trace';
import { readAssessment } from '../shared/assessment';
import { DemoDocument, DocumentCase, Observation, parseDocumentCase, safeExternalUrl } from './demo-data';

const ZOOM_STEPS = [50, 67, 80, 100, 125, 150, 200];

/** One synthetic case file: documents on the left, result, reasoning and provenance on the right. */
@Component({
  selector: 'nc-case-file',
  imports: [DecisionTrace],
  templateUrl: './case-file.html',
  host: { '(keydown.escape)': 'back.emit()' },
})
export class CaseFile implements OnInit, OnDestroy {
  readonly caseId = input.required<string>();
  readonly stateLabel = input<string>('');
  readonly back = output<void>();
  protected readonly text = demo;
  protected readonly values = demo.values;
  private readonly http = inject(HttpClient);
  private readonly injector = inject(Injector);
  private readonly heading = viewChild<ElementRef<HTMLHeadingElement>>('heading');
  private pending?: Subscription;
  private pendingText?: Subscription;

  protected readonly loading = signal(true);
  protected readonly error = signal('');
  protected readonly file = signal<DocumentCase | null>(null);
  protected readonly selectedDocument = signal<DemoDocument | null>(null);
  protected readonly page = signal(1);
  protected readonly zoom = signal(100);
  protected readonly documentText = signal<string | null>(null);
  protected readonly textState = signal<'idle' | 'loading' | 'error'>('idle');
  protected readonly assessment = computed(() => readAssessment(this.file()?.assessmentJson));
  protected readonly pageUrl = computed(() => {
    const doc = this.selectedDocument();
    if (!doc) return null;
    if (doc.mediaType === 'image/png') return this.documentUrl(doc);
    return doc.previewPages.includes(this.page()) ? `${this.documentUrl(doc)}/pages/${this.page()}` : null;
  });

  ngOnInit(): void {
    this.pending = this.http.get(`/api/document-cases/${encodeURIComponent(this.caseId())}`, { responseType: 'text', cache: 'no-store', credentials: 'omit' })
      .subscribe({
        next: body => {
          this.loading.set(false);
          const parsed = parseDocumentCase(body, this.caseId());
          if (!parsed) { this.error.set(demo.invalid); return; }
          this.file.set(parsed);
          if (parsed.documents.length) this.select(parsed.documents[0], 1);
        },
        error: (err: unknown) => {
          this.loading.set(false);
          this.error.set(err instanceof HttpErrorResponse && err.status === 404 ? demo.caseMissing : demo.caseError);
        },
      });
    afterNextRender(() => this.heading()?.nativeElement.focus(), { injector: this.injector });
  }
  ngOnDestroy(): void { this.pending?.unsubscribe(); this.pendingText?.unsubscribe(); }

  protected documentUrl(doc: DemoDocument): string {
    return `/api/document-cases/${encodeURIComponent(this.caseId())}/documents/${encodeURIComponent(doc.id)}`;
  }
  protected select(doc: DemoDocument, page = 1): void {
    this.pendingText?.unsubscribe();
    this.selectedDocument.set(doc);
    this.page.set(Math.min(Math.max(page, 1), doc.pages));
    this.documentText.set(null);
    this.textState.set('idle');
    if (doc.mediaType === 'text/plain') {
      this.textState.set('loading');
      this.pendingText = this.http.get(this.documentUrl(doc), { responseType: 'text', cache: 'no-store', credentials: 'omit' }).subscribe({
        next: text => { this.documentText.set(text.length > 200_000 ? text.slice(0, 200_000) : text); this.textState.set('idle'); },
        error: () => this.textState.set('error'),
      });
    }
  }
  protected openObservation(observation: Observation): void {
    const doc = this.file()?.documents.find(item => item.id === observation.documentId);
    if (doc) this.select(doc, observation.page);
  }
  protected turn(delta: number): void {
    const doc = this.selectedDocument();
    if (doc) this.page.update(page => Math.min(Math.max(page + delta, 1), doc.pages));
  }
  protected changeZoom(direction: -1 | 0 | 1): void {
    if (direction === 0) { this.zoom.set(100); return; }
    const index = ZOOM_STEPS.indexOf(this.zoom());
    this.zoom.set(ZOOM_STEPS[Math.min(Math.max(index + direction, 0), ZOOM_STEPS.length - 1)]);
  }
  protected canZoom(direction: -1 | 1): boolean {
    const index = ZOOM_STEPS.indexOf(this.zoom());
    return direction < 0 ? index > 0 : index < ZOOM_STEPS.length - 1;
  }
  protected viewerKeys(event: KeyboardEvent): void {
    if (event.key === 'ArrowRight' || event.key === 'PageDown') { this.turn(1); event.preventDefault(); }
    else if (event.key === 'ArrowLeft' || event.key === 'PageUp') { this.turn(-1); event.preventDefault(); }
    else if (event.key === '+') { this.changeZoom(1); event.preventDefault(); }
    else if (event.key === '-') { this.changeZoom(-1); event.preventDefault(); }
  }

  protected documentTitle(id: string): string { return this.file()?.documents.find(doc => doc.id === id)?.title ?? id; }
  protected fieldLabel(field: string): string { return label(this.file()?.fieldLabels ?? {}, field, field); }
  protected evidenceLabel(id: string): string { return label(this.file()?.evidenceLabels ?? {}, id, id); }
  protected evidenceStatus(status: string): string { return label(de.evidenceStatuses, status, de.unknown); }
  protected observedValue(value: string): string {
    const truth: Record<string, string> = { YES: demo.values.yes, NO: demo.values.no, NOT_APPLICABLE: demo.values.na, UNKNOWN: demo.values.unknown };
    return Object.hasOwn(truth, value) ? truth[value] : /^-?\d+(\.\d+)?$/.test(value) ? value.replace('.', ',') : value;
  }
  protected sourceUrl(url: string): string | null { return safeExternalUrl(url); }
  protected outcomeTone(): string {
    const assessment = this.assessment();
    const outcome = assessment ? assessment.outcome : null;
    return outcome !== null && Object.hasOwn(de.outcomes, outcome) ? outcome : 'NONE';
  }
  protected outcomeText(): string {
    const assessment = this.assessment();
    if (assessment === null) return demo.noAssessment;
    if (assessment === undefined) return demo.unreadable;
    return label(de.outcomes, assessment.outcome, de.unknown);
  }
}
