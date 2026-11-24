import { Component, DestroyRef, computed, effect, inject, input, signal, untracked } from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { HttpErrorResponse } from '@angular/common/http';
import { Observable, catchError, forkJoin, map, of, switchMap } from 'rxjs';
import { AuthService, AuthUser } from '../auth/auth.service';
import { de, germanDateTime, label } from '../de';
import { SystemService } from '../system/system.service';
import { Folder, Mark, MarkCommand, Presentation, ProcessDetail, ProcessSummary, ReleaseSummary, WorkbenchApi, Workspace } from './api';
import { AblageDialog, COLOR_PRESETS, DEFAULT_QUEUE_COLORS, FOLDER_PREFIX, folderTree, isPersonalView } from './ablage';
import { CaseAction, CaseFile } from './case-file';
import { ThemeChoice, ThemeStore } from '../shared/theme';
import { exactJson } from '../shared/json';
import { PackWorkbench } from '../extras/pack-workbench';
import { ReferenceExamples } from '../extras/reference-examples';
import { VolumeOverview } from '../extras/volume-overview';
import { EvidenceSearch } from '../extras/evidence-search';
import {
  COLUMN_IDS, COLUMN_WIDTHS, ColumnId, DEFAULT_COLUMNS, Filters, SELECT_COLUMNS, SavedQuery, ViewSettings,
  ViewStore, DEFAULT_VIEW, DENSITIES, PLATFORM_SYSTEM, rowTone, storableFilters,
  MAX_COLUMN_WIDTH, minColumnWidth, clampColumnWidth,
} from './case-view';

import { PANE_POSITIONS, PanePosition, PaneResize } from './pane-resize';

type Load<T> = { state: 'loading' } | { state: 'ready'; value: T } | { state: 'error' };
/** Arbeitslisten wie im bisherigen Fallexplorer; Bestätigt und Versendet stehen unter „Meine Ablage“. */
const WORK_LISTS = ['review', 'clarification', 'conflict', 'technical', 'documents'];
const PERSONAL = ['approval', 'done'];
const QUEUES = [...WORK_LISTS, ...PERSONAL];
const ALL = 'all';
const EMPTY_WORKSPACE: Workspace = { folders: [], marks: [], queueColors: {} };

/**
 * Prüfwerkstatt nach dem bewährten Fallexplorer: Körbe wie Ordner, Vorgänge wie ein Postfach, darunter oder daneben
 * die Vorschau der Fallakte; Doppelklick öffnet den Vorgang in voller Größe.
 * Alle Daten kommen aus der API; die Oberfläche enthält keine Fachlogik.
 */
@Component({
  selector: 'nc-workbench',
  imports: [CaseFile, PackWorkbench, ReferenceExamples, VolumeOverview, EvidenceSearch, PaneResize],
  templateUrl: './workbench.html',
  host: {
    '(document:keydown.f4)': 'toggleDetails($event)',
    '(document:keydown)': 'keys($event)',
    '(document:click)': 'closeMenus($event)',
    '(window:resize)': 'closeContextMenus()',
  },
})
export class Workbench {
  readonly user = input.required<AuthUser>();
  protected readonly t = de;
  protected readonly auth = inject(AuthService);
  private readonly api = inject(WorkbenchApi);
  private readonly destroy = inject(DestroyRef);
  private readonly store = inject(ViewStore);
  protected readonly theme = inject(ThemeStore);
  protected readonly queueIds = WORK_LISTS;
  protected readonly personalIds = PERSONAL;

  protected readonly menuExpanded = signal(!globalThis.matchMedia?.('(max-width: 860px)').matches);
  protected readonly panePositions = PANE_POSITIONS;
  protected readonly helpPage = signal<'usage' | 'shortcuts' | null>(null);
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
  protected readonly tableMinWidth = computed(() => 38 + this.columns().reduce((sum, c) => sum + this.columnSize(c), 0));
  private readonly flexibleColumn = computed(() => this.columns().find((c) => this.view().columnSizes[c] === undefined));
  protected readonly fixedWidths = computed(() => this.flexibleColumn() === undefined);
  protected readonly columnMin = minColumnWidth;
  protected readonly columnMax = MAX_COLUMN_WIDTH;
  protected columnSize(c: ColumnId): number {
    return this.view().columnSizes[c] ?? COLUMN_WIDTHS[c];
  }
  protected width(c: ColumnId): number | null {
    return c === this.flexibleColumn() ? null : this.columnSize(c);
  }
  protected readonly rowTone = rowTone;
  protected readonly tones = ['ready', 'missing', 'conflict', 'technical', 'documents', 'approval', 'done'];
  protected readonly selection = signal<ReadonlySet<string>>(new Set());
  protected readonly active = signal<string | null>(null);
  protected readonly busy = signal(false);
  protected readonly notice = signal('');
  protected readonly error = signal('');
  private readonly revision = signal(0);

  /** Meine Ablage des Kontos: eigene Ordner, Markierungen, Arbeitslistenfarben (serverseitig je Konto). */
  protected readonly workspace = signal<Workspace>(EMPTY_WORKSPACE);
  protected readonly marks = computed(() => new Map(this.workspace().marks.map((m) => [m.processId, m] as const)));
  protected readonly folders = computed(() => folderTree(this.workspace().folders));
  protected readonly colorPresets = COLOR_PRESETS;
  protected readonly contextMenu = signal<{ x: number; y: number; ids: string[] } | null>(null);
  protected readonly navMenu = signal<{ x: number; y: number; kind: 'queue' | 'folder'; id: string } | null>(null);
  protected readonly ablageDialog = signal<AblageDialog | null>(null);
  private returnFocus: HTMLElement | null = null;

  protected readonly system = toSignal(inject(SystemService).state(), { initialValue: { status: 'loading' as const } });
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
    const filtered = this.inQueue().filter((p) =>
      (!q || this.searchText(p).includes(q)) &&
      filters.every(([id, v]) => SELECT_COLUMNS.has(id)
        ? this.cellText(p, id).toLocaleLowerCase('de-DE') === v
        : this.cellText(p, id).toLocaleLowerCase('de-DE').includes(v)));
    const { key, asc } = this.sort();
    return [...filtered].sort((a, b) => this.compare(a, b, key) * (asc ? 1 : -1));
  });
  /** Vorgänge im Blick: Plattformfälle nur, wenn eingeblendet (wie im bisherigen Fallexplorer). */
  protected readonly scope = computed(() => {
    const l = this.list();
    if (l.state !== 'ready') return [];
    return this.view().includePlatform ? l.value : l.value.filter((p) => p.externalReference.system !== PLATFORM_SYSTEM);
  });
  /** Vorgänge der gewählten Arbeitsliste (oder alle). */
  protected readonly inQueue = computed(() => {
    const q = this.queue();
    return this.scope().filter((p) => this.inView(p, q));
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
      untracked(() => {
        const saved = this.store.load(name);
        const narrow = globalThis.matchMedia?.('(max-width: 600px)').matches;
        this.view.set(narrow && saved === DEFAULT_VIEW ? { ...saved, showNav: false, navPosition: 'top', navSize: 30, documentPosition: 'bottom', filesPosition: 'top' } : saved);
      });
    });
    // Alle Arbeitslisten auf einmal laden: Zähler, „Alle Vorgänge“ und Plattformfälle entstehen in der Oberfläche.
    effect((cleanup) => {
      this.revision();
      untracked(() => { if (this.list().state !== 'ready') this.list.set({ state: 'loading' }); });
      const source = forkJoin(QUEUES.map((q) => this.api.processes(q).pipe(map((rows) => rows.map((r) => ({ ...r, queue: q }))))))
        .pipe(map((lists) => lists.flat()));
      const ws = this.api.workspace().pipe(catchError(() => of(null))).subscribe((w) => { if (w) this.workspace.set(w); });
      cleanup(() => ws.unsubscribe());
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
        switchMap((d) => d.state === 'ready' && d.value.knowledgeRelease
          ? this.api.presentation(d.value.knowledgeRelease).pipe(catchError(() => of({} as Presentation)),
            switchMap((p) => { this.presentation.set(p); return of(d); }))
          : (this.presentation.set({}), of(d))),
      ).subscribe((d) => this.detail.set(d));
      cleanup(() => sub.unsubscribe());
    });
  }

  protected count(id: string): number {
    return this.scope().filter((p) => this.inView(p, id)).length;
  }

  /**
   * Zuordnung wie im bisherigen Fallexplorer: „Alle Vorgänge“ und „Ohne eigenen Ordner“ zeigen, was nicht in einem
   * eigenen Ordner liegt; Arbeitslisten zeigen immer alles ihres Bearbeitungsstands.
   */
  private inView(p: ProcessSummary, view: string): boolean {
    const mark = this.marks().get(p.processId);
    if (view === ALL || view === 'unfiled') return !mark?.folderId;
    if (view === 'bookmarks') return !!mark?.bookmark;
    if (view.startsWith(FOLDER_PREFIX)) return mark?.folderId === view.slice(FOLDER_PREFIX.length);
    return p.queue === view;
  }

  protected mark(id: string): Mark | undefined {
    return this.marks().get(id);
  }

  protected queueColor(q: string): string | null {
    return this.workspace().queueColors[q] ?? null;
  }

  /** Farbe am Zeilenrand: eigene Markierung vor eigener Arbeitslistenfarbe; sonst die Farbe des Bearbeitungsstands. */
  protected caseColor(p: ProcessSummary): string | null {
    return this.mark(p.processId)?.color ?? (p.queue ? this.queueColor(p.queue) : null);
  }

  protected folderView(f: Folder): string {
    return FOLDER_PREFIX + f.id;
  }

  protected setIncludePlatform(on: boolean): void {
    this.updateView({ includePlatform: on });
  }

  protected openQueue(id: string): void {
    this.extras.set(null);
    this.queue.set(id);
    const list = document.querySelector('.list-scroll');
    if (list) { list.scrollLeft = 0; list.scrollTop = 0; }
    setTimeout(() => this.active.set(this.visible()[0]?.processId ?? null));
    this.selection.set(new Set());
    this.active.set(null);
    this.opened.set(null);
    this.anchor = null;
  }

  protected queueLabel(id: string): string {
    if (id === ALL) return de.wb.allCases;
    if (id === 'bookmarks') return de.wb.ablage.bookmarks;
    if (id === 'unfiled') return de.wb.ablage.unfiled;
    if (id.startsWith(FOLDER_PREFIX)) return this.workspace().folders.find((f) => FOLDER_PREFIX + f.id === id)?.label ?? '';
    return de.queues[id] ?? id;
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
    setTimeout(() => focusRow(document.querySelector<HTMLElement>(`tr[data-id="${id}"]`)));
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
    const index = PANE_POSITIONS.indexOf(this.view().previewPosition);
    this.updateView({ previewPosition: PANE_POSITIONS[(index + 1) % PANE_POSITIONS.length] });
  }

  protected setDock(kind: 'previewPosition' | 'navPosition', value: string): void {
    const position = PANE_POSITIONS.find((p): p is PanePosition => p === value);
    if (position) this.updateView({ [kind]: position });
  }

  protected setPaneSize(kind: 'previewSize' | 'navSize', size: number): void {
    this.updateView({ [kind]: size });
  }

  protected nextStep(p: ProcessSummary): string {
    const steps = de.wb.nextSteps;
    const step = p.state === 'IN_CLARIFICATION' ? steps['clarify']
      : p.state === 'IN_CONFLICT_REVIEW' ? steps['conflict']
        : p.state === 'TECHNICAL_CLARIFICATION' ? steps['technical']
          : p.state === 'DOCUMENT_REVIEW' ? steps['documents']
            : p.state === 'IN_REVIEW' ? (p.outcome === 'NOT_SUPPORTED' ? steps['reviewNotSupported'] : p.outcome === 'SUPPORTED' ? steps['reviewSupported'] : steps['review'])
        : p.state === 'AWAITING_APPROVAL' ? steps['approval']
          : p.state === 'DISPATCHED' ? steps['dispatched'] : steps['closed'];
    return p.missingFieldCount > 0 ? `${step} · ${de.wb.openPoints(p.missingFieldCount)}` : step;
  }

  private loadRequestNames(list: readonly ProcessSummary[]): void {
    const known = this.requestNames();
    for (const release of new Set(list.map((p) => p.knowledgeRelease).filter((r): r is string => !!r))) {
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
    { id: 'cases', items: ['confirm', 'approve', 'new-folder', 'move', 'mark', 'bookmark', 'note', 'package', 'save-query'] },
    { id: 'extras', items: ['x-pruefwerkstatt', 'x-referenzfaelle', 'x-massenfaelle', 'x-nachweise'] },
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
      case 'approve': return this.canApprove(this.targetIds()) && !this.busy();
      case 'move': case 'mark': case 'bookmark': return this.targetIds().length > 0 && !this.busy();
      case 'note': return this.targetIds().length === 1 && this.canNote(this.targetIds()) && !this.busy();
      case 'package': return this.canPackage(this.targetIds()) && !this.busy();
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
      case 'approve': this.begin('approve', this.targetIds()); break;
      case 'new-folder': this.begin('folder', []); break;
      case 'move': this.begin('move', this.targetIds()); break;
      case 'mark': this.begin('color', this.targetIds()); break;
      case 'bookmark': this.toggleBookmark(this.targetIds()); break;
      case 'note': this.begin('note', this.targetIds()); break;
      case 'package': this.downloadPackage(this.targetIds()); break;
      case 'save-query': this.openQueryDialog(); break;
      case 'x-pruefwerkstatt': case 'x-referenzfaelle': case 'x-massenfaelle': case 'x-nachweise':
        this.openExtras(action.slice(2)); break;
    }
  }

  /** Nur ein Menü gleichzeitig offen; Klick außerhalb schließt. */
  protected menuToggled(event: Event): void {
    const opened = event.target as HTMLDetailsElement;
    if (!opened.open) return;
    document.querySelectorAll<HTMLDetailsElement>('.menubar details[open]').forEach((d) => { if (d !== opened) d.open = false; });
  }

  protected openHelp(page: 'usage' | 'shortcuts', origin: HTMLElement, dialog: HTMLDialogElement): void {
    this.helpPage.set(page);
    const details = origin.closest('details');
    details?.removeAttribute('open');
    requestAnimationFrame(() => { if (dialog.isConnected && !dialog.open) dialog.showModal(); });
    dialog.addEventListener('close', () => {
      this.helpPage.set(null);
      details?.querySelector('summary')?.focus();
    }, { once: true });
  }

  protected switchHelpTab(page: 'usage' | 'shortcuts', event: KeyboardEvent): void {
    event.preventDefault();
    this.helpPage.set(page);
    document.getElementById('help-' + page)?.focus();
  }

  protected closeMenus(event: MouseEvent): void {
    if (!(event.target instanceof Element && event.target.closest('.context-menu'))) this.closeContextMenus();
    if (event.target instanceof Element && event.target.closest('.menubar')) return;
    document.querySelectorAll<HTMLDetailsElement>('.menubar details[open]').forEach((d) => (d.open = false));
  }

  protected copyNumber(id: string, event?: MouseEvent): void {
    event?.stopPropagation();
    this.copy(id, de.wb.caseNumberCopied);
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

  private columnDrag: { pointerId: number; start: number; width: number } | null = null;

  protected columnDown(event: PointerEvent): void {
    if (event.button !== 0 || this.columnDrag) return;
    const handle = event.currentTarget as HTMLElement;
    handle.setPointerCapture(event.pointerId);
    this.columnDrag = { pointerId: event.pointerId, start: event.clientX, width: handle.parentElement!.getBoundingClientRect().width };
  }

  protected columnMove(event: PointerEvent, column: ColumnId): void {
    const drag = this.columnDrag;
    if (drag?.pointerId !== event.pointerId || event.clientX === drag.start && this.view().columnSizes[column] === undefined) return;
    this.setColumnSize(column, drag.width + event.clientX - drag.start, false);
  }

  protected columnUp(event: PointerEvent): void {
    if (this.columnDrag?.pointerId !== event.pointerId) return;
    this.columnDrag = null;
    this.store.save(this.user().username, this.view());
  }

  protected columnKeys(event: KeyboardEvent, column: ColumnId): void {
    if (event.key !== 'ArrowLeft' && event.key !== 'ArrowRight') return;
    event.preventDefault();
    const width = (event.currentTarget as HTMLElement).parentElement!.getBoundingClientRect().width || this.columnSize(column);
    this.setColumnSize(column, width + (event.key === 'ArrowRight' ? 10 : -10));
  }

  protected resetColumnSize(column: ColumnId): void {
    const columnSizes = { ...this.view().columnSizes };
    delete columnSizes[column];
    this.updateView({ columnSizes });
  }

  private setColumnSize(column: ColumnId, width: number, persist = true): void {
    const columnSizes = { ...this.view().columnSizes, [column]: clampColumnWidth(column, width) };
    if (persist) this.updateView({ columnSizes });
    else this.view.update((v) => ({ ...v, columnSizes }));
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
    if (event.target !== event.currentTarget) return;
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
      focusRow(document.querySelector<HTMLElement>(`tr[data-id="${target.processId}"]`));
    };
    if (event.key === 'ContextMenu' || (event.shiftKey && event.key === 'F10')) { this.rowContext(event, p); return; }
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
      case 'request': return p.knowledgeRelease
        ? (this.requestNames().get(p.knowledgeRelease) ?? p.knowledgeRelease) : de.queues['documents'];
      case 'next': return this.nextStep(p);
      case 'number': return p.externalReference.caseId;
      case 'status': return this.stateLabel(p.state);
      case 'result': return this.outcome(p.outcome);
      case 'received': return germanDateTime(p.receivedAt);
      case 'expires': return germanDateTime(p.expiresAt);
      case 'missing': return String(p.missingFieldCount);
      case 'documents': return String(p.documentCount);
      case 'confirmedBy': return p.confirmedBy ?? '';
      case 'release': return p.knowledgeRelease ?? '–';
      case 'system': return p.externalReference.system;
      case 'folder': {
        const id = this.mark(p.processId)?.folderId;
        return id ? (this.workspace().folders.find((f) => f.id === id)?.label ?? '') : '';
      }
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
    const dialog = !!document.querySelector('.dialog-backdrop, dialog[open]');
    const mod = event.ctrlKey || event.metaKey;
    if (mod && event.key.toLowerCase() === 'k') { event.preventDefault(); document.querySelector<HTMLInputElement>('.appbar input[type=search]')?.focus(); return; }
    if (event.key === 'F5' && !mod) { event.preventDefault(); this.refresh(); return; }
    if (event.key === 'Escape' && (this.contextMenu() || this.navMenu())) { this.closeContextMenus(true); return; }
    if (dialog || editing) return;
    if (event.key === 'Escape' && this.extras()) { this.closeExtras(); return; }
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

  /** Reihenfolge per Ziehen: die Zeile folgt dem Zeiger (Maus, Stift, Touch) und rückt an die Stelle darunter. */
  protected readonly dragging = signal<ColumnId | null>(null);
  protected readonly columnAnnouncement = signal('');

  protected dragStart(event: PointerEvent, id: ColumnId): void {
    if (event.button !== 0 || (event.target as HTMLElement).closest('.icon-button')) return;
    event.preventDefault();
    (event.currentTarget as HTMLElement).setPointerCapture?.(event.pointerId);
    this.dragging.set(id);
  }

  protected dragMove(event: PointerEvent): void {
    const id = this.dragging();
    const draft = this.columnDialog();
    if (!id || !draft) return;
    const rows = [...((event.currentTarget as HTMLElement).parentElement?.children ?? [])] as HTMLElement[];
    const over = rows.findIndex((row) => {
      const r = row.getBoundingClientRect();
      return event.clientY >= r.top && event.clientY < r.bottom;
    });
    const from = draft.indexOf(id);
    if (over >= 0 && over !== from) this.moveColumnTo(from, over);
  }

  protected dragEnd(): void {
    const id = this.dragging();
    if (!id) return;
    this.dragging.set(null);
    this.announcePosition(id);
  }

  /** Tastatur am Griff: Pfeil hoch/runter, Pos1/Ende. */
  protected gripKeys(event: KeyboardEvent, index: number): void {
    const draft = this.columnDialog();
    if (!draft) return;
    const target = { ArrowUp: index - 1, ArrowDown: index + 1, Home: 0, End: draft.length - 1 }[event.key];
    if (target === undefined) return;
    event.preventDefault();
    const id = draft[index];
    if (!this.moveColumnTo(index, target)) return;
    this.announcePosition(id);
    setTimeout(() => document.querySelector<HTMLElement>(`.column-list li[data-column="${id}"] .grip`)?.focus());
  }

  private moveColumnTo(from: number, to: number): boolean {
    const d = this.columnDialog();
    if (!d || from === to || to < 0 || to >= d.length) return false;
    const next = [...d];
    const [moved] = next.splice(from, 1);
    next.splice(to, 0, moved);
    this.columnDialog.set(next);
    return true;
  }

  private announcePosition(id: ColumnId): void {
    const draft = this.columnDialog() ?? [];
    this.columnAnnouncement.set(this.t.wb.columnMoved(this.columnLabel(id), draft.indexOf(id) + 1, draft.length));
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
    const query: SavedQuery = { name: trimmed, queue: this.queue(), columns: this.columns(), columnSizes: this.view().columnSizes, filters: storableFilters(this.filters()), sort: this.sort() };
    this.updateView({ queries: [...this.view().queries.filter((q) => q.name !== trimmed), query].slice(-20) });
    this.queryDialog.set(false);
    this.flash(de.wb.querySaved(trimmed));
  }

  protected openQuery(q: SavedQuery): void {
    this.openQueue(q.queue === ALL || QUEUES.includes(q.queue) || isPersonalView(q.queue, this.workspace().folders) ? q.queue : ALL);
    this.updateView({ columns: q.columns, columnSizes: q.columnSizes, sort: q.sort, showFilters: this.view().showFilters || Object.keys(q.filters).length > 0 });
    this.filters.set({ ...q.filters });
  }

  protected deleteQuery(name: string): void {
    this.updateView({ queries: this.view().queries.filter((q) => q.name !== name) });
  }

  // ---- Meine Ablage: Kontextmenü, Ordner, Markierungen, Lesezeichen, Notizen, Freigabe, Versandpaket ----

  /** Ziel von Aktionen: geöffneter Vorgang, sonst Auswahl, sonst die aktive Zeile. */
  protected targetIds(): string[] {
    const opened = this.opened();
    if (opened) return [opened];
    const selected = this.selectedVisible().map((p) => p.processId);
    if (selected.length) return selected;
    const active = this.active();
    return active ? [active] : [];
  }

  private summaries(ids: readonly string[]): ProcessSummary[] {
    const l = this.list();
    if (l.state !== 'ready') return [];
    const set = new Set(ids);
    return l.value.filter((p) => set.has(p.processId));
  }

  protected canConfirm(ids: readonly string[]): boolean {
    const items = this.summaries(ids);
    return this.user().roles.includes('gutachter') && items.length > 0 && items.every((p) => p.state === 'IN_REVIEW');
  }

  protected canApprove(ids: readonly string[]): boolean {
    const items = this.summaries(ids);
    return this.user().roles.includes('freigabe') && items.length > 0
      && items.every((p) => p.state === 'AWAITING_APPROVAL' && p.confirmedBy !== this.user().username);
  }

  protected canPackage(ids: readonly string[]): boolean {
    const items = this.summaries(ids);
    return items.length > 0 && items.every((p) => p.state === 'DISPATCHED');
  }

  protected canNote(ids: readonly string[]): boolean {
    return this.summaries(ids).every((p) => p.state !== 'CLOSED');
  }

  protected allBookmarked(ids: readonly string[]): boolean {
    return ids.length > 0 && ids.every((id) => this.mark(id)?.bookmark);
  }

  protected anyInFolder(ids: readonly string[]): boolean {
    return ids.some((id) => !!this.mark(id)?.folderId);
  }

  /** Rechtsklick oder Umschalt+F10/Kontextmenütaste auf einer Zeile. */
  protected rowContext(event: Event, p: ProcessSummary): void {
    event.preventDefault();
    if (!this.selection().has(p.processId)) {
      this.selection.set(new Set([p.processId]));
      this.anchor = p.processId;
    }
    this.active.set(p.processId);
    this.returnFocus = event.currentTarget as HTMLElement;
    const box = (event.currentTarget as HTMLElement).getBoundingClientRect();
    const mouse = event instanceof MouseEvent && event.clientX > 0;
    const x = mouse ? event.clientX : box.left + 24;
    const y = mouse ? event.clientY : box.top + 20;
    this.navMenu.set(null);
    this.contextMenu.set({
      x: Math.max(8, Math.min(x, window.innerWidth - 280)),
      y: Math.max(8, Math.min(y, window.innerHeight - 420)),
      ids: [...this.selection()],
    });
    setTimeout(() => document.querySelector<HTMLElement>('.context-menu [role=menuitem]:not(:disabled)')?.focus());
  }

  protected navContext(event: Event, kind: 'queue' | 'folder', id: string): void {
    event.preventDefault();
    this.returnFocus = event.currentTarget as HTMLElement;
    const box = (event.currentTarget as HTMLElement).getBoundingClientRect();
    this.contextMenu.set(null);
    this.navMenu.set({ x: Math.min(box.right - 40, window.innerWidth - 260), y: Math.min(box.top + 8, window.innerHeight - 120), kind, id });
    setTimeout(() => document.querySelector<HTMLElement>('.context-menu [role=menuitem]')?.focus());
  }

  protected closeContextMenus(restoreFocus = false): void {
    if (!this.contextMenu() && !this.navMenu()) return;
    this.contextMenu.set(null);
    this.navMenu.set(null);
    if (restoreFocus) focusRow(this.returnFocus);
  }

  /** Pfeiltasten im Kontextmenü wie in einem Desktop-Menü. */
  protected menuKeys(event: KeyboardEvent): void {
    if (event.key === 'Tab') { this.closeContextMenus(); return; }
    if (!['ArrowDown', 'ArrowUp', 'Home', 'End'].includes(event.key)) return;
    event.preventDefault();
    const root = event.currentTarget as HTMLElement;
    const items = Array.from(root.querySelectorAll<HTMLElement>('button:not(:disabled), input'))
      .filter((b) => b.offsetParent !== null);
    const i = items.indexOf(document.activeElement as HTMLElement);
    const next = event.key === 'Home' ? 0 : event.key === 'End' ? items.length - 1
      : (i + (event.key === 'ArrowDown' ? 1 : items.length - 1)) % items.length;
    items[next]?.focus();
  }

  protected begin(kind: AblageDialog['kind'], ids: string[], folder?: Folder, parentId: string | null = null): void {
    this.closeContextMenus();
    this.returnFocus ??= document.activeElement as HTMLElement | null;
    const note = ids.length === 1 ? (this.mark(ids[0])?.note ?? '') : '';
    const queue = kind === 'queueColor' && folder ? folder.id : '';
    this.ablageDialog.set({
      kind, ids,
      folderId: folder?.id ?? (kind === 'move' ? (this.workspace().folders[0]?.id ?? '') : ''),
      parentId: folder?.parentId ?? parentId,
      label: kind === 'queueColor' ? '' : (folder?.label ?? ''),
      color: kind === 'queueColor'
        ? (this.queueColor(queue) ?? DEFAULT_QUEUE_COLORS[queue] ?? COLOR_PRESETS[0].hex)
        : kind === 'color' ? (ids.length === 1 ? (this.mark(ids[0])?.color ?? COLOR_PRESETS[0].hex) : COLOR_PRESETS[0].hex)
          : (folder?.color ?? COLOR_PRESETS[0].hex),
      note,
      queue,
    });
    setTimeout(() => ['input[type=text]', 'textarea', 'select', 'input', 'button[type=submit]']
      .map((q) => document.querySelector<HTMLElement>(`.ablage-dialog ${q}`)).find((e) => !!e)?.focus());
  }

  protected editFolder(f: Folder): void {
    this.begin('rename', [], f);
  }

  protected newSubfolder(f: Folder): void {
    this.begin('folder', [], undefined, f.id);
  }

  protected editQueueColor(queue: string): void {
    this.begin('queueColor', [], { id: queue, parentId: null, label: '', color: '' });
  }

  protected patchDialog(change: Partial<AblageDialog>): void {
    this.ablageDialog.update((d) => (d ? { ...d, ...change } : d));
  }

  protected closeDialog(): void {
    this.ablageDialog.set(null);
    focusRow(this.returnFocus);
    this.returnFocus = null;
  }

  /** Mögliche übergeordnete Ordner: höchstens vierte Ebene, beim Bearbeiten nicht der Ordner selbst oder darunter. */
  protected parentOptions(d: AblageDialog): { id: string; label: string }[] {
    const tree = this.folders();
    const excluded = new Set<string>();
    if (d.kind === 'rename' && d.folderId) {
      excluded.add(d.folderId);
      for (const f of tree) if (f.parentId && excluded.has(f.parentId)) excluded.add(f.id);
    }
    return tree.filter((f) => f.depth < 4 && !excluded.has(f.id)).map((f) => ({ id: f.id, label: '— '.repeat(f.depth) + f.label }));
  }

  protected submitDialog(): void {
    const d = this.ablageDialog();
    if (!d || this.busy()) return;
    const request = { label: d.label.trim(), color: d.color, parentId: d.parentId || null };
    switch (d.kind) {
      case 'folder': this.save(this.api.createFolder(request)); break;
      case 'rename': this.save(this.api.updateFolder(d.folderId, request)); break;
      case 'delete': this.save(this.api.deleteFolder(d.folderId), () => {
        if (this.queue() === FOLDER_PREFIX + d.folderId) this.openQueue(ALL);
      }); break;
      case 'move': this.command(d.ids, { command: 'MOVE', folderId: d.folderId || null }); break;
      case 'color': this.command(d.ids, { command: 'COLOR', color: d.color }); break;
      case 'note': this.command(d.ids, { command: 'NOTE', note: d.note.trim() || null }); break;
      case 'queueColor': this.save(this.api.queueColor(d.queue, d.color)); break;
      case 'approve': this.approveAll(d.ids); break;
    }
  }

  protected resetQueueColor(): void {
    const d = this.ablageDialog();
    if (d?.kind === 'queueColor') this.save(this.api.queueColor(d.queue, null));
  }

  protected command(ids: string[], command: MarkCommand): void {
    if (!ids.length || this.busy()) return;
    this.closeContextMenus();
    this.busy.set(true);
    this.api.mark(ids, command).pipe(takeUntilDestroyed(this.destroy)).subscribe({
      next: (w) => {
        this.busy.set(false);
        this.workspace.set(w);
        if (this.ablageDialog()) this.closeDialog();
        // Was aus der aktuellen Ansicht verschwindet, ist nicht mehr ausgewählt.
        const shown = new Set(this.visible().map((p) => p.processId));
        this.selection.update((s) => new Set([...s].filter((id) => shown.has(id))));
        this.flash(de.wb.ablage.saved);
      },
      error: () => { this.busy.set(false); this.error.set(de.wb.ablage.failed); },
    });
  }

  protected toggleBookmark(ids: string[]): void {
    this.command(ids, { command: 'BOOKMARK', bookmark: !this.allBookmarked(ids) });
  }

  protected setColor(ids: string[], color: string | null): void {
    this.command(ids, { command: 'COLOR', color });
  }

  protected moveTo(ids: string[], folderId: string | null): void {
    this.command(ids, { command: 'MOVE', folderId });
  }

  private save(call: Observable<unknown>, after?: () => void): void {
    this.busy.set(true);
    call.pipe(switchMap(() => this.api.workspace()), takeUntilDestroyed(this.destroy)).subscribe({
      next: (w) => {
        this.busy.set(false);
        this.workspace.set(w);
        this.closeDialog();
        after?.();
        this.flash(de.wb.ablage.saved);
      },
      error: (e: unknown) => {
        this.busy.set(false);
        this.error.set(e instanceof HttpErrorResponse && typeof e.error?.detail === 'string' ? e.error.detail : de.wb.ablage.failed);
      },
    });
  }

  protected confirmTargets(ids: string[]): void {
    this.closeContextMenus();
    if (!this.canConfirm(ids)) return;
    this.selection.set(new Set(ids));
    this.confirmSelected();
  }

  /** Freigabe je Vorgang einzeln, damit das Vier-Augen-Prinzip für jeden Vorgang geprüft und protokolliert wird. */
  private approveAll(ids: string[]): void {
    this.busy.set(true);
    forkJoin(ids.map((id) => this.api.approve(id).pipe(map(() => true), catchError(() => of(false)))))
      .pipe(takeUntilDestroyed(this.destroy)).subscribe((results) => {
        this.busy.set(false);
        this.closeDialog();
        this.selection.set(new Set());
        this.flash(de.wb.ablage.approveResult(results.filter(Boolean).length, results.length));
        this.refresh();
      });
  }

  /** Versandpaket wie im bisherigen Fallexplorer: das übergebene Ergebnis als JSON-Datei. */
  protected downloadPackage(ids: string[]): void {
    this.closeContextMenus();
    if (!this.canPackage(ids)) return;
    this.busy.set(true);
    forkJoin(ids.map((id) => this.api.process(id))).pipe(takeUntilDestroyed(this.destroy)).subscribe({
      next: (details) => {
        this.busy.set(false);
        const items = details.map((d) => ({
          processId: d.processId, externalReference: d.externalReference, knowledgeRelease: d.knowledgeRelease,
          outcome: d.outcome, evidenceId: d.evidenceId, confirmedBy: d.confirmedBy,
          history: d.history, assessment: '__ASSESSMENT_' + d.processId + '__',
        }));
        // Bewertung als exakter JSON-Text: Dezimalzahlen bleiben unverändert.
        let text = JSON.stringify({ format: 'normacase-versandpaket', version: 1, items }, null, 2);
        for (const d of details) text = text.replace(`"__ASSESSMENT_${d.processId}__"`, () => exactJson(d.assessment));
        const name = details.length === 1 ? details[0].externalReference.caseId : `${details.length}-vorgaenge`;
        const url = URL.createObjectURL(new Blob([text], { type: 'application/json' }));
        const a = document.createElement('a');
        a.href = url;
        a.download = `normacase-versand-${name.replace(/[^A-Za-z0-9._-]/g, '_')}.json`;
        a.click();
        setTimeout(() => URL.revokeObjectURL(url));
        this.flash(de.wb.ablage.packageSaved);
      },
      error: (e: unknown) => this.fail(e),
    });
  }

  protected innerWidth(): number {
    return globalThis.innerWidth ?? 1280;
  }

  protected dialogTitle(d: AblageDialog): string {
    const a = de.wb.ablage;
    switch (d.kind) {
      case 'folder': return a.newFolder;
      case 'rename': return a.editFolderTitle;
      case 'delete': return a.deleteFolder;
      case 'move': return d.ids.length > 1 ? `${a.move} (${d.ids.length})` : a.move;
      case 'color': return a.mark;
      case 'note': return a.note;
      case 'queueColor': return a.queueColorTitle(de.queues[d.queue] ?? d.queue);
      case 'approve': return a.approve;
    }
  }

  protected dialogSubmit(d: AblageDialog): string {
    switch (d.kind) {
      case 'delete': return de.wb.ablage.deleteFolder;
      case 'move': return de.wb.ablage.moveTo.replace(' nach', '');
      case 'approve': return de.wb.ablage.approve;
      default: return de.wb.save;
    }
  }

  // ---- Extras: Prüfwerkstatt, Referenzfälle, Massenfälle, Nachweise ----

  protected readonly extras = signal<string | null>(null);
  protected readonly extrasRequest = signal<{ releaseId: string; exampleId: string } | null>(null);
  protected readonly evidenceRequest = signal<{ system: string; caseId: string } | null>(null);
  protected readonly releaseList = signal<ReleaseSummary[]>([]);
  protected readonly allProcesses = computed(() => {
    const l = this.list();
    return l.state === 'ready' ? l.value : [];
  });

  protected isAssessor(): boolean {
    return this.user().roles.includes('gutachter');
  }

  protected openExtras(view: string, request: { releaseId: string; exampleId: string } | null = null): void {
    this.closeContextMenus();
    this.opened.set(null);
    this.extrasRequest.set(request);
    if (view === 'nachweise') {
      const active = this.summaries(this.active() ? [this.active()!] : [])[0];
      this.evidenceRequest.set(active ? { system: active.externalReference.system, caseId: active.externalReference.caseId } : null);
    }
    if (!this.releaseList().length) {
      this.api.releases().pipe(catchError(() => of([] as ReleaseSummary[])), takeUntilDestroyed(this.destroy))
        .subscribe((r) => this.releaseList.set(r));
    }
    this.extras.set(view);
    setTimeout(() => document.getElementById('tool-title')?.focus());
  }

  protected closeExtras(): void {
    this.extras.set(null);
    setTimeout(() => document.querySelector<HTMLElement>('.case-table tbody tr[tabindex="0"], .case-table tbody tr')?.focus());
  }

  protected showPlatformCases(): void {
    if (!this.view().includePlatform) this.updateView({ includePlatform: true });
    this.openQueue(ALL);
    this.query.set('PF-');
  }

  protected updateView(change: Partial<ViewSettings>): void {
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
    return [p.displayName, p.externalReference.caseId, this.outcome(p.outcome), this.stateLabel(p.state), this.cellText(p, 'request'),
      this.mark(p.processId)?.note ?? '']
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

/** Fokus auf eine Zeile ohne seitliches Mitscrollen: breite Zeilen würden sonst die erste Spalte aus dem Bild schieben. */
function focusRow(el: HTMLElement | null): void {
  if (!el) return;
  const scroller = el.closest<HTMLElement>('.list-scroll');
  const left = scroller?.scrollLeft ?? 0;
  el.focus({ preventScroll: true });
  el.scrollIntoView?.({ block: 'nearest' });
  if (scroller) scroller.scrollLeft = left;
}
