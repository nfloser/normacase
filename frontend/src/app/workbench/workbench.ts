import { Component, DestroyRef, computed, effect, inject, input, signal, untracked } from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { HttpErrorResponse } from '@angular/common/http';
import { Observable, Subject, catchError, of, startWith, switchMap } from 'rxjs';
import { AuthService, AuthUser } from '../auth/auth.service';
import { de, germanDateTime, label } from '../de';
import { SystemService } from '../system/system.service';
import { Presentation, ProcessDetail, ProcessSummary, QueueCount, ReleaseSummary, WorkbenchApi } from './api';
import { CaseAction, CaseFile } from './case-file';

type Load<T> = { state: 'loading' } | { state: 'ready'; value: T } | { state: 'error' };
type SortKey = 'received' | 'subject' | 'number' | 'result';

/**
 * Prüfwerkstatt nach dem bewährten Konzept: Körbe wie Ordner, Vorgänge wie ein Postfach, rechts die Fallakte.
 * Alle Daten kommen aus der API; die Oberfläche enthält keine Fachlogik.
 */
@Component({
  selector: 'nc-workbench',
  imports: [CaseFile],
  templateUrl: './workbench.html',
})
export class Workbench {
  readonly user = input.required<AuthUser>();
  protected readonly t = de;
  protected readonly auth = inject(AuthService);
  private readonly api = inject(WorkbenchApi);
  private readonly destroy = inject(DestroyRef);
  private readonly reload$ = new Subject<void>();

  protected readonly queue = signal('review');
  protected readonly query = signal('');
  protected readonly sort = signal<{ key: SortKey; asc: boolean }>({ key: 'received', asc: true });
  protected readonly selection = signal<ReadonlySet<string>>(new Set());
  protected readonly active = signal<string | null>(null);
  protected readonly busy = signal(false);
  protected readonly notice = signal('');
  protected readonly error = signal('');
  private readonly revision = signal(0);

  protected readonly system = toSignal(inject(SystemService).state(), { initialValue: { status: 'loading' as const } });
  protected readonly queues = signal<QueueCount[]>([]);
  protected readonly list = signal<Load<ProcessSummary[]>>({ state: 'loading' });
  protected readonly detail = signal<Load<ProcessDetail> | null>(null);
  protected readonly presentation = signal<Presentation>({});
  protected readonly releases = toSignal(this.api.releases().pipe(catchError(() => of([] as ReleaseSummary[]))), { initialValue: [] });

  protected readonly visible = computed(() => {
    const l = this.list();
    if (l.state !== 'ready') return [];
    const q = this.query().trim().toLocaleLowerCase('de-DE');
    const filtered = q ? l.value.filter((p) => this.searchText(p).includes(q)) : l.value;
    const { key, asc } = this.sort();
    const value = (p: ProcessSummary) =>
      key === 'subject' ? (p.displayName ?? '') : key === 'number' ? p.externalReference.caseId
        : key === 'result' ? this.outcome(p.outcome) : p.receivedAt;
    return [...filtered].sort((a, b) => value(a).localeCompare(value(b), 'de-DE') * (asc ? 1 : -1));
  });
  protected readonly selectedVisible = computed(() => this.visible().filter((p) => this.selection().has(p.processId)));
  protected readonly allVisibleSelected = computed(() => this.visible().length > 0 && this.selectedVisible().length === this.visible().length);
  protected readonly canBatchConfirm = computed(() =>
    this.user().roles.includes('gutachter') && this.selectedVisible().some((p) => p.state === 'IN_REVIEW'));
  protected readonly activeRelease = computed(() => {
    const d = this.detail();
    return d?.state === 'ready' ? (this.releases().find((r) => r.releaseId === d.value.knowledgeRelease) ?? null) : null;
  });

  constructor() {
    this.reload$.pipe(
      startWith(undefined),
      switchMap(() => this.guard(this.api.queues())),
      takeUntilDestroyed(),
    ).subscribe((q) => q.state === 'ready' && this.queues.set(q.value));

    effect((cleanup) => {
      const queue = this.queue();
      this.revision();
      untracked(() => { if (this.list().state !== 'ready') this.list.set({ state: 'loading' }); });
      const sub = this.guard(this.api.processes(queue)).subscribe((l) => {
        this.list.set(l);
        if (l.state === 'ready' && !l.value.some((p) => p.processId === this.active())) {
          this.active.set(l.value[0]?.processId ?? null);
        }
      });
      cleanup(() => sub.unsubscribe());
    });

    effect((cleanup) => {
      const id = this.active();
      this.revision();
      if (!id) {
        this.detail.set(null);
        return;
      }
      untracked(() => { if (this.detail()?.state !== 'ready') this.detail.set({ state: 'loading' }); });
      const sub = this.guard(this.api.process(id)).pipe(
        switchMap((d) => d.state === 'ready'
          ? this.api.presentation(d.value.knowledgeRelease).pipe(catchError(() => of({} as Presentation)),
            switchMap((p) => { this.presentation.set(p); return of(d); }))
          : of(d)),
      ).subscribe((d) => this.detail.set(d));
      cleanup(() => sub.unsubscribe());
    });
  }

  protected count(id: string): number {
    return this.queues().find((q) => q.id === id)?.count ?? 0;
  }

  protected openQueue(id: string): void {
    this.queue.set(id);
    this.selection.set(new Set());
    this.active.set(null);
  }

  protected refresh(): void {
    this.reload$.next();
    this.revision.update((r) => r + 1);
  }

  protected toggle(id: string): void {
    this.selection.update((s) => {
      const next = new Set(s);
      if (next.has(id)) next.delete(id); else next.add(id);
      return next;
    });
  }

  protected toggleVisible(checked: boolean): void {
    this.selection.set(checked ? new Set(this.visible().map((p) => p.processId)) : new Set());
  }

  protected sortBy(key: SortKey): void {
    this.sort.update((s) => ({ key, asc: s.key === key ? !s.asc : true }));
  }

  protected ariaSort(key: SortKey): string | null {
    const s = this.sort();
    return s.key === key ? (s.asc ? 'ascending' : 'descending') : null;
  }

  protected rowKeys(event: KeyboardEvent, p: ProcessSummary): void {
    const rows = this.visible();
    const index = rows.findIndex((r) => r.processId === p.processId);
    const move = (to: number) => {
      const target = rows[Math.min(Math.max(0, to), rows.length - 1)];
      if (!target) return;
      this.active.set(target.processId);
      document.querySelector<HTMLElement>(`tr[data-id="${target.processId}"]`)?.focus();
    };
    switch (event.key) {
      case 'ArrowDown': event.preventDefault(); move(index + 1); break;
      case 'ArrowUp': event.preventDefault(); move(index - 1); break;
      case 'Home': event.preventDefault(); move(0); break;
      case 'End': event.preventDefault(); move(rows.length - 1); break;
      case ' ': event.preventDefault(); this.toggle(p.processId); break;
      case 'Enter': event.preventDefault(); this.active.set(p.processId); document.getElementById('case-title')?.focus(); break;
    }
  }

  protected confirmSelected(): void {
    const ids = this.selectedVisible().filter((p) => p.state === 'IN_REVIEW').map((p) => p.processId);
    if (!ids.length) return;
    this.busy.set(true);
    this.api.confirmBatch(ids).pipe(takeUntilDestroyed(this.destroy)).subscribe({
      next: (results) => {
        this.busy.set(false);
        this.selection.set(new Set());
        this.flash(de.wb.batchResult(results.filter((r) => r.result === 'CONFIRMED').length, results.length));
        this.refresh();
      },
      error: (e: unknown) => this.fail(e),
    });
  }

  protected act(id: string, action: CaseAction): void {
    const call: Observable<unknown> =
      action.kind === 'confirm' ? this.api.confirm(id)
        : action.kind === 'approve' ? this.api.approve(id)
          : action.kind === 'reject' ? this.api.reject(id, action.reason)
            : this.api.clarify(id, action.caseInputJson);
    this.busy.set(true);
    call.pipe(takeUntilDestroyed(this.destroy)).subscribe({
      next: () => {
        this.busy.set(false);
        this.flash(de.caseFile.done[action.kind]);
        this.refresh();
      },
      error: (e: unknown) => this.fail(e),
    });
  }

  protected stateLabel(state: string): string {
    return label(de.states, state, de.unknown);
  }

  protected outcome(code: string): string {
    return label(de.outcomes, code, de.unknown);
  }

  protected readonly dateTime = germanDateTime;

  protected roleLabels(): string {
    return this.user().roles.map((r) => de.roles[r]).filter((r): r is string => !!r).join(', ');
  }

  private searchText(p: ProcessSummary): string {
    return [p.displayName, p.externalReference.caseId, this.outcome(p.outcome), this.stateLabel(p.state)]
      .join(' ').toLocaleLowerCase('de-DE');
  }

  private guard<T>(source: Observable<T>): Observable<Load<T>> {
    return source.pipe(
      switchMap((value) => of<Load<T>>({ state: 'ready', value })),
      catchError(() => of<Load<T>>({ state: 'error' })),
    );
  }

  private fail(error: unknown): void {
    this.busy.set(false);
    const type = error instanceof HttpErrorResponse && typeof error.error?.type === 'string'
      ? String(error.error.type).split('/').pop() ?? '' : '';
    this.error.set(label(de.caseFile.errors, type, de.caseFile.errors['generic']));
    this.refresh();
  }

  private flash(message: string): void {
    this.notice.set(message);
    setTimeout(() => this.notice.set(''), 4000);
  }
}
