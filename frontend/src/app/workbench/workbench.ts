import { Component, DestroyRef, computed, effect, inject, input, signal, untracked } from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { HttpErrorResponse } from '@angular/common/http';
import { Observable, Subject, catchError, forkJoin, map, of, startWith, switchMap } from 'rxjs';
import { AuthService, AuthUser } from '../auth/auth.service';
import { de, germanDateTime, label } from '../de';
import { SystemService } from '../system/system.service';
import { Presentation, ProcessDetail, ProcessSummary, QueueCount, ReleaseSummary, WorkbenchApi } from './api';
import { CaseAction, CaseFile } from './case-file';
import { ThemeChoice, ThemeStore } from '../shared/theme';
import {
  COLUMN_IDS, COLUMN_WIDTHS, ColumnId, DEFAULT_COLUMNS, Filters, PREVIEW_MAX, PREVIEW_MIN, SELECT_COLUMNS, SavedQuery, ViewSettings,
  ViewStore, DEFAULT_VIEW, DENSITIES, clampPreview, rowTone, storableFilters,
} from './case-view';

type Load<T> = { state: 'loading' } | { state: 'ready'; value: T } | { state: 'error' };
const QUEUES = ['clarification', 'review', 'approval', 'done'];
const ALL = 'all';

/**
 * Prüfwerkstatt nach dem bewährten Fallexplorer: Körbe wie Ordner, Vorgänge wie ein Postfach, darunter oder daneben
 * die Vorschau der Fallakte; Doppelklick öffnet den Vorgang in voller Größe.
 * Alle Daten kommen aus der API; die Oberfläche enthält keine Fachlogik.
 */
@Component({
  selector: 'nc-workbench',
  imports: [CaseFile],
  templateUrl: './workbench.html',
  host: {
    '(document:keydown.f4)': 'toggleDetails($event)',
    '(document:keydown)': 'keys($event)',
    '(document:click)': 'closeMenus($event)',
    '[class.nav-collapsed]': '!view().showNav',
  },
})
export class Workbench {
  readonly user = input.required<AuthUser>();
  protected readonly t = de;
  protected readonly auth = inject(AuthService);
  private readonly api = inject(WorkbenchApi);
  private readonly destroy = inject(DestroyRef);
  private readonly reload$ = new Subject<void>();
  private readonly store = inject(ViewStore);
  protected readonly theme = inject(ThemeStore);
  protected readonly queueIds = QUEUES;

  protected readonly queue = signal(ALL);
  /** Vorgang in voller Größe (Doppelklick, Eingabe, „Vorgang öffnen“); sonst Liste mit Vorschau. */
  protected readonly opened = signal<string | null>(null);
  /** Prüfanlass je Wissensstand, aus den Darstellungsdaten des Knowledge Packs. */
  private readonly requestNames = signal<ReadonlyMap<string, string>>(new Map());
  private anchor: string | null = null;
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
      const source = queue === ALL
        ? forkJoin(QUEUES.map((q) => this.api.processes(q))).pipe(map((lists) => lists.flat()))
        : this.api.processes(queue);
      const sub = this.guard(source).subscribe((l) => {
        this.list.set(l);
        if (l.state === 'ready') {
          if (!l.value.some((p) => p.processId === this.active())) this.active.set(this.visible()[0]?.processId ?? null);
          if (this.opened() && !l.value.some((p) => p.processId === this.opened())) this.opened.set(null);
          this.loadRequestNames(l.value);
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
    if (id === ALL) return this.queues().reduce((sum, q) => sum + q.count, 0);
    return this.queues().find((q) => q.id === id)?.count ?? 0;
  }

  protected openQueue(id: string): void {
    this.queue.set(id);
    this.selection.set(new Set());
    this.active.set(null);
    this.opened.set(null);
    this.anchor = null;
  }

  protected queueLabel(id: string): string {
    return id === ALL ? de.wb.allCases : (de.queues[id] ?? id);
  }

  protected queueHelp(id: string): string {
    return id === ALL ? de.wb.allCasesHelp : (de.queueHelp[id] ?? '');
  }

  /** Öffnet den Vorgang in voller Größe. */
  protected open(id: string | null): void {
    if (!id) return;
    this.active.set(id);
    this.opened.set(id);
    setTimeout(() => document.getElementById('case-title')?.focus());
  }

  protected back(): void {
    const id = this.opened();
    this.opened.set(null);
    setTimeout(() => document.querySelector<HTMLElement>(`tr[data-id="${id}"]`)?.focus());
  }

  /** Klick wie im Explorer: einfach wählt aus, Strg ergänzt, Umschalt wählt einen Bereich. */
  protected rowClick(event: MouseEvent, p: ProcessSummary): void {
    if (event.target instanceof Element && event.target.closest('input, button, a')) return;
    this.active.set(p.processId);
    if (event.shiftKey && this.anchor) {
      this.selectRange(this.anchor, p.processId);
    } else if (event.ctrlKey || event.metaKey) {
      this.toggle(p.processId);
      this.anchor = p.processId;
    } else {
      this.selection.set(new Set([p.processId]));
      this.anchor = p.processId;
    }
  }

  private selectRange(from: string, to: string): void {
    const ids = this.visible().map((r) => r.processId);
    const a = ids.indexOf(from);
    const b = ids.indexOf(to);
    if (a < 0 || b < 0) return;
    this.selection.set(new Set(ids.slice(Math.min(a, b), Math.max(a, b) + 1)));
  }

  protected setTheme(choice: string): void {
    if (choice === 'system' || choice === 'light' || choice === 'dark') this.theme.set(choice as ThemeChoice);
  }

  protected togglePreviewPosition(): void {
    this.updateView({ previewPosition: this.view().previewPosition === 'bottom' ? 'right' : 'bottom' });
  }

  private resizing = false;

  protected splitterDown(event: PointerEvent): void {
    this.resizing = true;
    (event.target as HTMLElement).setPointerCapture?.(event.pointerId);
  }

  protected splitterMove(event: PointerEvent, container: HTMLElement): void {
    if (!this.resizing) return;
    const box = container.getBoundingClientRect();
    const share = this.view().previewPosition === 'bottom'
      ? (box.bottom - event.clientY) / Math.max(1, box.height)
      : (box.right - event.clientX) / Math.max(1, box.width);
    this.view.update((v) => ({ ...v, previewSize: clampPreview(share * 100) }));
  }

  protected splitterUp(): void {
    if (!this.resizing) return;
    this.resizing = false;
    this.store.save(this.user().username, this.view());
  }

  protected splitterKeys(event: KeyboardEvent): void {
    const grow = this.view().previewPosition === 'bottom' ? 'ArrowUp' : 'ArrowLeft';
    const shrink = this.view().previewPosition === 'bottom' ? 'ArrowDown' : 'ArrowRight';
    if (event.key !== grow && event.key !== shrink) return;
    event.preventDefault();
    this.updateView({ previewSize: clampPreview(this.view().previewSize + (event.key === grow ? 5 : -5)) });
  }

  protected readonly previewMin = PREVIEW_MIN;
  protected readonly previewMax = PREVIEW_MAX;

  protected nextStep(p: ProcessSummary): string {
    const steps = de.wb.nextSteps;
    const step = p.state === 'IN_CLARIFICATION' ? (p.outcome === 'HUMAN_REVIEW' ? steps['conflict'] : steps['clarify'])
      : p.state === 'IN_REVIEW' ? steps['review']
        : p.state === 'AWAITING_APPROVAL' ? steps['approval']
          : p.state === 'DISPATCHED' ? steps['dispatched'] : steps['closed'];
    return p.missingFieldCount > 0 ? `${step} · ${de.wb.openPoints(p.missingFieldCount)}` : step;
  }

  private loadRequestNames(list: readonly ProcessSummary[]): void {
    const known = this.requestNames();
    for (const release of new Set(list.map((p) => p.knowledgeRelease))) {
      if (known.has(release)) continue;
      this.api.presentation(release).pipe(catchError(() => of({} as Presentation)), takeUntilDestroyed(this.destroy))
        .subscribe((pr) => this.requestNames.update((m) => new Map(m).set(release, pr.name ?? release)));
    }
  }

  protected clearAllFilters(): void {
    this.filters.set({});
    this.query.set('');
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
    this.selection.set(checked ? new Set(this.visible().slice(0, 100).map((p) => p.processId)) : new Set());
    if (checked && this.visible().length > 100) this.flash(de.wb.selectionLimit);
  }

  /** Menüleiste wie im bisherigen Fallexplorer. */
  protected readonly menus: readonly { id: string; items: readonly string[] }[] = [
    { id: 'file', items: ['open', 'copy-id', 'copy-result'] },
    { id: 'edit', items: ['select-all', 'clear'] },
    { id: 'view', items: ['detail', 'orientation', 'navigation', 'density', 'columns', 'filters', 'colors', 'reset', 'refresh'] },
    { id: 'cases', items: ['confirm', 'save-query'] },
  ];

  protected menuLabel(id: string): string {
    return (de.wb.menu as unknown as Record<string, string>)[id] ?? id;
  }

  protected available(action: string): boolean {
    const inList = !this.opened();
    switch (action) {
      case 'open': return inList && !!this.active();
      case 'copy-id': return !!this.active();
      case 'copy-result': return this.detail()?.state === 'ready';
      case 'select-all': return inList && this.visible().length > 0;
      case 'clear': return this.selection().size > 0;
      case 'confirm': return this.canBatchConfirm() && !this.busy();
      case 'detail': case 'orientation': case 'columns': case 'filters': case 'save-query': return inList;
      default: return true;
    }
  }

  protected menu(action: string, event?: Event): void {
    (event?.target as HTMLElement | undefined)?.closest('details')?.removeAttribute('open');
    if (!this.available(action)) return;
    switch (action) {
      case 'open': this.open(this.active()); break;
      case 'copy-id': this.copyId(); break;
      case 'copy-result': this.copyResult(); break;
      case 'select-all': this.toggleVisible(true); break;
      case 'clear': this.selection.set(new Set()); this.anchor = null; break;
      case 'detail': this.toggleDetails(); break;
      case 'orientation': this.togglePreviewPosition(); break;
      case 'navigation': this.updateView({ showNav: !this.view().showNav }); break;
      case 'density': {
        const next = DENSITIES[(DENSITIES.indexOf(this.view().density) + 1) % DENSITIES.length];
        this.updateView({ density: next });
        break;
      }
      case 'columns': this.openColumnOptions(); break;
      case 'filters': this.toggleFilters(); break;
      case 'colors': this.setRowColors(!this.view().rowColors); break;
      case 'reset':
        this.updateView({ ...DEFAULT_VIEW, queries: this.view().queries });
        this.clearAllFilters();
        this.flash(de.wb.viewReset);
        break;
      case 'refresh': this.refresh(); break;
      case 'confirm': this.confirmSelected(); break;
      case 'save-query': this.openQueryDialog(); break;
    }
  }

  /** Nur ein Menü gleichzeitig offen; Klick außerhalb schließt. */
  protected menuToggled(event: Event): void {
    const opened = event.target as HTMLDetailsElement;
    if (!opened.open) return;
    document.querySelectorAll<HTMLDetailsElement>('.menubar details[open]').forEach((d) => { if (d !== opened) d.open = false; });
  }

  protected closeMenus(event: MouseEvent): void {
    if (event.target instanceof Element && event.target.closest('.menubar')) return;
    document.querySelectorAll<HTMLDetailsElement>('.menubar details[open]').forEach((d) => (d.open = false));
  }

  private copyId(): void {
    const p = this.list().state === 'ready' ? (this.list() as { value: ProcessSummary[] }).value.find((r) => r.processId === this.active()) : undefined;
    if (p) this.copy(p.externalReference.caseId, de.wb.caseNumberCopied);
  }

  private copyResult(): void {
    const d = this.detail();
    if (d?.state !== 'ready') return;
    const v = d.value;
    const lines = [
      `${v.displayName ?? v.externalReference.caseId} (${v.externalReference.caseId})`,
      `${de.trace.result}: ${this.outcome(v.outcome)}`,
      `${de.wb.columns['status']}: ${this.stateLabel(v.state)}`,
      `${de.caseFile.release}: ${v.knowledgeRelease}`,
    ];
    this.copy(lines.join('\n'), de.wb.resultText);
  }

  private copy(text: string, what: string): void {
    const clipboard = globalThis.navigator?.clipboard;
    if (!clipboard) { this.flash(de.wb.copyFailed); return; }
    clipboard.writeText(text).then(() => this.flash(de.wb.copied(what)), () => this.flash(de.wb.copyFailed));
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
      if (event.shiftKey) {
        this.anchor ??= p.processId;
        this.selectRange(this.anchor, target.processId);
      }
      document.querySelector<HTMLElement>(`tr[data-id="${target.processId}"]`)?.focus();
    };
    switch (event.key) {
      case 'ArrowDown': event.preventDefault(); move(index + 1); break;
      case 'ArrowUp': event.preventDefault(); move(index - 1); break;
      case 'Home': event.preventDefault(); move(0); break;
      case 'End': event.preventDefault(); move(rows.length - 1); break;
      case ' ': event.preventDefault(); this.toggle(p.processId); break;
      case 'Enter': event.preventDefault(); this.open(p.processId); break;
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
      case 'request': return this.requestNames().get(p.knowledgeRelease) ?? p.knowledgeRelease;
      case 'next': return this.nextStep(p);
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

  protected toggleDetails(event?: Event): void {
    event?.preventDefault();
    if (this.opened()) return;
    this.updateView({ showDetails: !this.view().showDetails });
  }

  protected keys(event: KeyboardEvent): void {
    const editing = event.target instanceof Element ? event.target.closest('input, textarea, select') : null;
    const dialog = !!document.querySelector('.dialog-backdrop');
    const mod = event.ctrlKey || event.metaKey;
    if (mod && event.key.toLowerCase() === 'k') { event.preventDefault(); document.querySelector<HTMLInputElement>('.appbar input[type=search]')?.focus(); return; }
    if (event.key === 'F5' && !mod) { event.preventDefault(); this.refresh(); return; }
    if (dialog || editing) return;
    if (event.altKey && event.key === 'ArrowLeft' && this.opened()) { event.preventDefault(); this.back(); return; }
    if (mod && event.key.toLowerCase() === 'a' && !this.opened()) { event.preventDefault(); this.toggleVisible(true); return; }
    if (event.key === 'Escape') {
      if (this.opened()) this.back();
      else { this.selection.set(new Set()); this.anchor = null; }
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
    this.openQueue(q.queue === ALL || QUEUES.includes(q.queue) ? q.queue : ALL);
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
    return [p.displayName, p.externalReference.caseId, this.outcome(p.outcome), this.stateLabel(p.state), this.cellText(p, 'request')]
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
