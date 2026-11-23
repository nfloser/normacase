import { Component, DestroyRef, computed, effect, inject, input, signal, untracked } from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { HttpErrorResponse } from '@angular/common/http';
import { Observable, Subject, catchError, of, startWith, switchMap } from 'rxjs';
import { AuthService, AuthUser } from '../auth/auth.service';
import { de, germanDateTime, label } from '../de';
import { SystemService } from '../system/system.service';
import { Presentation, ProcessDetail, ProcessSummary, QueueCount, ReleaseSummary, WorkbenchApi } from './api';
import { CaseAction, CaseFile } from './case-file';
import {
  COLUMN_IDS, COLUMN_WIDTHS, ColumnId, DEFAULT_COLUMNS, Filters, SELECT_COLUMNS, SavedQuery, ViewSettings, ViewStore,
  rowTone, storableFilters,
} from './case-view';

type Load<T> = { state: 'loading' } | { state: 'ready'; value: T } | { state: 'error' };
const QUEUES = ['clarification', 'review', 'approval', 'done'];

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
  private readonly store = inject(ViewStore);

  protected readonly queue = signal('review');
  protected readonly query = signal('');
  /** Ansicht: Spalten, Färbung, Sortierung, gespeicherte Abfragen. Wird je Konto im Browser gehalten. */
  protected readonly view = signal<ViewSettings>(this.store.load(''));
  protected readonly filters = signal<Filters>({});
  protected readonly columnDialog = signal<ColumnId[] | null>(null);
  protected readonly queryDialog = signal(false);
  protected readonly columns = computed(() => this.view().columns);
  protected readonly sort = computed(() => this.view().sort);
  protected readonly selectColumns = SELECT_COLUMNS;
  /** Mindestbreite der Tabelle aus den gewählten Spalten; reicht der Platz nicht, scrollt die Liste waagerecht. */
  protected readonly tableMinWidth = computed(() => 38 + this.columns().reduce((sum, c) => sum + COLUMN_WIDTHS[c], 0));
  protected width(c: ColumnId): number | null {
    return c === 'subject' ? null : COLUMN_WIDTHS[c];
  }
  protected readonly rowTone = rowTone;
  protected readonly tones = ['ready', 'missing', 'conflict', 'done'];
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

  protected readonly activeFilters = computed(() => Object.values(this.filters()).filter((v) => !!v?.trim()).length);
  protected readonly visible = computed(() => {
    const l = this.list();
    if (l.state !== 'ready') return [];
    const q = this.query().trim().toLocaleLowerCase('de-DE');
    const filters = Object.entries(this.filters())
      .filter((e): e is [ColumnId, string] => !!e[1]?.trim())
      .map(([id, v]) => [id, v.trim().toLocaleLowerCase('de-DE')] as const);
    const filtered = l.value.filter((p) =>
      (!q || this.searchText(p).includes(q)) &&
      filters.every(([id, v]) => SELECT_COLUMNS.has(id)
        ? this.cellText(p, id).toLocaleLowerCase('de-DE') === v
        : this.cellText(p, id).toLocaleLowerCase('de-DE').includes(v)));
    const { key, asc } = this.sort();
    return [...filtered].sort((a, b) => this.compare(a, b, key) * (asc ? 1 : -1));
  });
  /** Auswahlwerte je Spalte aus den geladenen Vorgängen. */
  protected readonly options = computed(() => {
    const l = this.list();
    const values = l.state === 'ready' ? l.value : [];
    const out: Partial<Record<ColumnId, string[]>> = {};
    for (const id of SELECT_COLUMNS) out[id] = [...new Set(values.map((p) => this.cellText(p, id)))].sort((a, b) => a.localeCompare(b, 'de-DE'));
    return out;
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
    effect(() => {
      const name = this.user().username;
      untracked(() => this.view.set(this.store.load(name)));
    });
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

  protected sortBy(key: ColumnId): void {
    const s = this.sort();
    this.updateView({ sort: { key, asc: s.key === key ? !s.asc : true } });
  }

  protected ariaSort(key: ColumnId): string | null {
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

  protected columnLabel(id: ColumnId): string {
    return de.wb.columns[id] ?? id;
  }

  /** Anzeigetext einer Zelle; zugleich Grundlage für Filter. */
  protected cellText(p: ProcessSummary, id: ColumnId): string {
    switch (id) {
      case 'subject': return p.displayName ?? '';
      case 'number': return p.externalReference.caseId;
      case 'status': return this.stateLabel(p.state);
      case 'result': return this.outcome(p.outcome);
      case 'received': return germanDateTime(p.receivedAt);
      case 'expires': return germanDateTime(p.expiresAt);
      case 'missing': return String(p.missingFieldCount);
      case 'documents': return String(p.documentCount);
      case 'confirmedBy': return p.confirmedBy ?? '';
      case 'release': return p.knowledgeRelease;
      case 'system': return p.externalReference.system;
    }
  }

  private compare(a: ProcessSummary, b: ProcessSummary, id: ColumnId): number {
    switch (id) {
      case 'received': return a.receivedAt.localeCompare(b.receivedAt);
      case 'expires': return a.expiresAt.localeCompare(b.expiresAt);
      case 'missing': return a.missingFieldCount - b.missingFieldCount;
      case 'documents': return a.documentCount - b.documentCount;
      default: return this.cellText(a, id).localeCompare(this.cellText(b, id), 'de-DE', { numeric: true });
    }
  }

  protected setRowColors(on: boolean): void {
    this.updateView({ rowColors: on });
  }

  protected toggleFilters(): void {
    const show = !this.view().showFilters;
    this.updateView({ showFilters: show });
    if (!show) this.filters.set({});
  }

  protected setFilter(id: ColumnId, value: string): void {
    this.filters.update((f) => ({ ...f, [id]: value.slice(0, 100) }));
  }

  protected clearFilters(): void {
    this.filters.set({});
  }

  protected openColumnOptions(): void {
    this.columnDialog.set([...this.columns()]);
    this.focusLater('.column-options');
  }

  protected openQueryDialog(): void {
    this.queryDialog.set(true);
    this.focusLater('#query-name');
  }

  private focusLater(selector: string): void {
    setTimeout(() => document.querySelector<HTMLElement>(selector)?.focus());
  }

  protected availableColumns(draft: readonly ColumnId[]): ColumnId[] {
    return COLUMN_IDS.filter((id) => !draft.includes(id));
  }

  protected addColumn(id: string): void {
    const col = COLUMN_IDS.find((c) => c === id);
    if (col) this.columnDialog.update((d) => (d && !d.includes(col) ? [...d, col] : d));
  }

  protected removeColumn(id: ColumnId): void {
    this.columnDialog.update((d) => (d && d.length > 1 ? d.filter((c) => c !== id) : d));
  }

  protected moveColumn(index: number, delta: -1 | 1): void {
    this.columnDialog.update((d) => {
      if (!d || index + delta < 0 || index + delta >= d.length) return d;
      const next = [...d];
      [next[index], next[index + delta]] = [next[index + delta], next[index]];
      return next;
    });
  }

  protected resetColumns(): void {
    this.columnDialog.set([...DEFAULT_COLUMNS]);
  }

  protected applyColumns(): void {
    const draft = this.columnDialog();
    if (!draft?.length) return;
    const sort = draft.includes(this.sort().key) ? this.sort() : { key: draft[0], asc: true };
    this.updateView({ columns: draft, sort });
    this.filters.update((f) => Object.fromEntries(Object.entries(f).filter(([k]) => draft.includes(k as ColumnId))));
    this.columnDialog.set(null);
  }

  protected saveQuery(name: string): void {
    const trimmed = name.trim().slice(0, 60);
    if (!trimmed) return;
    const query: SavedQuery = { name: trimmed, queue: this.queue(), columns: this.columns(), filters: storableFilters(this.filters()), sort: this.sort() };
    this.updateView({ queries: [...this.view().queries.filter((q) => q.name !== trimmed), query].slice(-20) });
    this.queryDialog.set(false);
    this.flash(de.wb.querySaved(trimmed));
  }

  protected openQuery(q: SavedQuery): void {
    this.openQueue(QUEUES.includes(q.queue) ? q.queue : 'review');
    this.updateView({ columns: q.columns, sort: q.sort, showFilters: this.view().showFilters || Object.keys(q.filters).length > 0 });
    this.filters.set({ ...q.filters });
  }

  protected deleteQuery(name: string): void {
    this.updateView({ queries: this.view().queries.filter((q) => q.name !== name) });
  }

  private updateView(change: Partial<ViewSettings>): void {
    this.view.update((v) => ({ ...v, ...change }));
    this.store.save(this.user().username, this.view());
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
