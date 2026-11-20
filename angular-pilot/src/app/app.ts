import { afterNextRender, Component, computed, ElementRef, inject, Injector, OnDestroy, signal, viewChild } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { Subscription } from 'rxjs';
import { de, demo, label } from './de';
import { CaseDetail, parseCaseDetail } from './case-detail';
import { DemoWorkspace } from './demo/demo-workspace';
import { DecisionTrace } from './shared/decision-trace';
import { AssessmentView, readAssessment } from './shared/assessment';

interface QueueItem { caseId: string; }
interface Queue { queueId: string; items: QueueItem[]; }
interface QueuePage { queues: Queue[]; nextPageCursor: string | null; }

const dateFormat = new Intl.DateTimeFormat('de-DE', { dateStyle: 'medium', timeStyle: 'short' });

@Component({ selector: 'app-root', imports: [FormsModule, DemoWorkspace, DecisionTrace], templateUrl: './app.html' })
export class App implements OnDestroy {
  protected readonly text = de;
  protected readonly demoText = demo;
  /** Anonymous synthetic preview workspace; never combined with a review session. */
  protected readonly demoMode = signal(false);
  private readonly injector = inject(Injector);
  private readonly host: ElementRef<HTMLElement> = inject(ElementRef);
  private readonly logoutButton = viewChild<ElementRef<HTMLButtonElement>>('logoutButton');
  private readonly credentialField = viewChild<ElementRef<HTMLInputElement>>('credentialField');
  private readonly demoButton = viewChild<ElementRef<HTMLButtonElement>>('demoButton');
  private readonly caseTitle = viewChild<ElementRef<HTMLHeadingElement>>('caseTitle');
  protected keyInput = '';
  private key = '';
  private pending?: Subscription;
  private pendingCase?: Subscription;
  protected readonly queues = signal<Queue[]>([]);
  protected readonly cursor = signal<string | null>(null);
  protected readonly busy = signal(false);
  protected readonly error = signal('');
  protected readonly active = signal(false);
  protected readonly dark = signal(false);
  /** The case being shown; the queue page stays in memory so “back” needs no reload. */
  protected readonly openCaseId = signal<string | null>(null);
  protected readonly detail = signal<CaseDetail | null>(null);
  protected readonly caseBusy = signal(false);
  protected readonly caseError = signal('');
  /** Parsed once per loaded case, not on every render. */
  protected readonly reviewTrace = computed<AssessmentView | null>(() => {
    const detail = this.detail();
    return detail ? readAssessment(detail.assessmentJson) ?? null : null;
  });

  protected openDemo(): void { this.logout(); this.demoMode.set(true); }
  protected leaveDemo(): void { this.demoMode.set(false); afterNextRender(() => this.demoButton()?.nativeElement.focus(), { injector: this.injector }); }

  protected toggleTheme(): void { this.dark.update(value => !value); }

  constructor(private readonly http: HttpClient) {}

  protected login(): void {
    const key = this.keyInput;
    this.demoMode.set(false);
    this.logout();
    if (!key.trim()) { this.error.set(de.credentialRequired); return; }
    this.key = key;
    this.load(null);
  }

  protected load(cursor: string | null): void {
    if (!this.key || this.busy()) return;
    this.pending?.unsubscribe();
    this.clearCase();
    this.busy.set(true);
    this.error.set('');
    this.queues.set([]);
    this.cursor.set(null);
    const query = cursor === null ? '' : '?afterCaseId=' + encodeURIComponent(cursor);
    this.pending = this.http.get<QueuePage>('/api/review/work-queues' + query, {
      headers: { Authorization: 'Bearer ' + this.key }, credentials: 'omit', cache: 'no-store',
    }).subscribe({
      next: page => {
        if (!page || !Array.isArray(page.queues) || !(page.nextPageCursor === null || typeof page.nextPageCursor === 'string') ||
          !page.queues.every(q => q != null && typeof q.queueId === 'string' && Array.isArray(q.items) && q.items.every(i => i != null && typeof i.caseId === 'string'))) {
          this.active.set(false); this.key = ''; this.error.set(de.invalidResponse); this.busy.set(false); return;
        }
        const firstLogin = !this.active();
        this.queues.set(page.queues); this.cursor.set(page.nextPageCursor); this.active.set(true); this.busy.set(false);
        if (firstLogin) afterNextRender(() => this.logoutButton()?.nativeElement.focus(), { injector: this.injector });
      },
      error: (err: unknown) => {
        this.busy.set(false);
        if (err instanceof HttpErrorResponse && (err.status === 401 || err.status === 403)) {
          this.key = ''; this.active.set(false);
          this.error.set(err.status === 401 ? de.expired : de.forbidden);
        } else { this.error.set(de.networkError); this.key = ''; this.active.set(false); }
      },
    });
  }

  protected openCase(caseId: string): void {
    if (!this.key || !this.active()) return;
    this.pendingCase?.unsubscribe();
    this.openCaseId.set(caseId); this.detail.set(null); this.caseError.set(''); this.caseBusy.set(true);
    this.focusCaseTitle();
    this.pendingCase = this.http.get('/api/review/work-cases/' + encodeURIComponent(caseId), {
      headers: { Authorization: 'Bearer ' + this.key }, credentials: 'omit', cache: 'no-store', responseType: 'text',
    }).subscribe({
      next: body => {
        if (this.openCaseId() !== caseId) return;
        this.caseBusy.set(false);
        const parsed = parseCaseDetail(body, caseId);
        if (parsed) this.detail.set(parsed); else this.caseError.set(de.caseInvalid);
      },
      error: (err: unknown) => {
        if (this.openCaseId() !== caseId) return;
        this.caseBusy.set(false);
        const status = err instanceof HttpErrorResponse ? err.status : 0;
        if (status === 401) { this.logout(); this.error.set(de.expired); this.focusCredential(); return; }
        this.caseError.set(status === 404 ? de.caseNotFound : status === 403 ? de.caseForbidden : de.caseNetworkError);
      },
    });
  }

  protected closeCase(): void {
    const caseId = this.openCaseId();
    this.clearCase();
    if (caseId === null) return;
    afterNextRender(() => {
      const button = [...this.host.nativeElement.querySelectorAll<HTMLButtonElement>('button[data-case-id]')]
        .find(candidate => candidate.dataset['caseId'] === caseId);
      button?.focus();
    }, { injector: this.injector });
  }

  protected label(id: string): string { return label(de.queueLabels, id, de.unknownQueue); }
  protected stateLabel(id: string): string { return label(de.states, id, de.unknownState); }
  protected evidenceLabel(status: string): string { return label(de.evidenceStatuses, status, de.unknown); }
  protected auditLabel(kind: string): string { return label(de.auditKinds, kind, de.unknown); }
  protected dispositionLabel(id: string): string { return label(de.dispositions, id, de.unknown); }
  protected outcomeLabel(id: string | null | undefined): string { return label(de.outcomes, id, de.unknown); }
  /** Technical outcome key for styling only; unknown values get the neutral style. */
  protected outcomeTone(detail: CaseDetail): string {
    const outcome = detail.assessment?.outcome;
    return outcome != null && Object.hasOwn(de.outcomes, outcome) ? outcome : 'NONE';
  }
  protected assessmentText(detail: CaseDetail): string {
    if (detail.assessment === null) return de.noAssessment;
    if (detail.assessment === undefined) return de.unreadableAssessment;
    return this.outcomeLabel(detail.assessment.outcome);
  }
  protected formatTime(value: string): string {
    const time = Date.parse(value);
    return Number.isNaN(time) ? value : dateFormat.format(time);
  }

  protected logoutFromUi(): void {
    this.logout();
    this.focusCredential();
  }

  protected logout(): void {
    this.pending?.unsubscribe(); this.pending = undefined; this.key = ''; this.keyInput = '';
    this.clearCase();
    this.queues.set([]); this.cursor.set(null); this.busy.set(false); this.active.set(false); this.error.set('');
  }

  private clearCase(): void {
    this.pendingCase?.unsubscribe(); this.pendingCase = undefined;
    this.openCaseId.set(null); this.detail.set(null); this.caseError.set(''); this.caseBusy.set(false);
  }
  private focusCaseTitle(): void {
    afterNextRender(() => this.caseTitle()?.nativeElement.focus(), { injector: this.injector });
  }
  private focusCredential(): void {
    afterNextRender(() => this.credentialField()?.nativeElement.focus(), { injector: this.injector });
  }
  ngOnDestroy(): void { this.logout(); }
}
