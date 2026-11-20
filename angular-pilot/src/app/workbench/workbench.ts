import { afterNextRender, Component, computed, ElementRef, HostListener, inject, Injector, input, OnInit, output, signal, viewChild } from '@angular/core';
import { de, label, wb } from '../de';
import { readAssessment } from '../shared/assessment';
import { CaseTab, CaseView } from './case-view';
import { WorkbenchStore } from './workbench-store';
import { canConfirm, canDispatch, caseIdentity, folderTree, MAX_SELECTION, processingStep, selectRange, WorkCase } from './workspace';

type DialogKind = 'folder' | 'rename' | 'delete' | 'move' | 'color' | 'queueColor' | 'note' | 'confirm' | 'dispatch';
type SortKey = 'subject' | 'number' | 'status' | 'result';
type ColumnKey = 'subject' | 'number' | 'next' | 'result';
interface Dialog { kind: DialogKind; targets: string[]; label: string; color: string; folderId: string; parentId: string; note: string }
interface Menu { x: number; y: number; ids: string[]; sub: 'color' | 'folder' | null }

export const QUEUES = ['approval', 'clarification', 'review', 'technical', 'documents'];
export const COLOR_PRESETS: Record<string, string> = { blue: '#3f6fb5', teal: '#1f7890', green: '#2a7a55', amber: '#b7791f', purple: '#7652a8', rose: '#b04870' };
const DEFAULT_QUEUE_COLORS: Record<string, string> = { approval: '#1f7890', clarification: '#b7791f', review: '#b04870', technical: '#7652a8', documents: '#3f6fb5' };

/**
 * Case workbench: explorer tree, case list and reading pane in the style of a mail client
 * combined with a file explorer. Organisation actions are local rehearsal only.
 */
@Component({
  selector: 'nc-workbench',
  imports: [CaseView],
  providers: [WorkbenchStore],
  templateUrl: './workbench.html',
  host: { class: 'workbench', '[class.compact]': "density() === 'compact'", '[class.nav-collapsed]': '!navOpen()' },
})
export class Workbench implements OnInit {
  readonly openReview = output<void>();
  readonly theme = input<'system' | 'light' | 'dark'>('system');
  readonly themeChange = output<'system' | 'light' | 'dark'>();
  protected readonly themes = ['system', 'light', 'dark'] as const;
  protected readonly store = inject(WorkbenchStore);
  protected readonly text = wb;
  protected readonly queues = QUEUES;
  protected readonly presets = Object.entries(COLOR_PRESETS);
  private readonly injector = inject(Injector);
  private readonly host: ElementRef<HTMLElement> = inject(ElementRef);
  private readonly searchField = viewChild<ElementRef<HTMLInputElement>>('searchField');
  private readonly dialogElement = viewChild<ElementRef<HTMLDialogElement>>('dialog');
  private readonly body = viewChild<ElementRef<HTMLElement>>('body');
  private returnFocus: HTMLElement | null = null;
  private anchor: string | null = null;
  private resizing = false;

  // view state (session only; never persisted)
  protected readonly filter = signal('all');
  protected readonly query = signal('');
  protected readonly columns = signal<Record<ColumnKey, string>>({ subject: '', number: '', next: '', result: '' });
  protected readonly statusFilter = signal('');
  protected readonly showFilters = signal(false);
  protected readonly sort = signal<{ key: SortKey; descending: boolean }>({ key: 'number', descending: false });
  protected readonly selection = signal<string[]>([]);
  protected readonly active = signal<string | null>(null);
  protected readonly opened = signal<{ id: string; tab: CaseTab } | null>(null);
  protected readonly pane = signal<'right' | 'bottom' | 'off'>('right');
  protected readonly panePercent = signal(44);
  protected readonly density = signal<'compact' | 'comfortable'>('comfortable');
  protected readonly platformCases = signal(false);
  protected readonly navOpen = signal(!this.narrow());
  protected readonly menu = signal<Menu | null>(null);
  protected readonly navMenu = signal<{ x: number; y: number; id: string; folder: boolean } | null>(null);
  protected readonly dialog = signal<Dialog | null>(null);
  protected readonly notice = signal('');
  protected readonly error = signal('');
  protected readonly showShortcuts = signal(false);

  protected readonly workspace = this.store.workspace;
  protected readonly cases = computed(() => (this.workspace()?.cases ?? []).filter(c => this.platformCases() || c.caseId.startsWith('reference-')));
  protected readonly folders = computed(() => folderTree(this.workspace()?.folders ?? []));
  protected readonly outcomes = computed(() => {
    const result: Record<string, string | null | undefined> = {};
    for (const [id, file] of Object.entries(this.store.files())) { const a = readAssessment(file.assessmentJson); result[id] = a ? a.outcome : a; }
    return result;
  });
  protected readonly visible = computed(() => {
    const needle = this.query().trim().toLocaleLowerCase('de-DE');
    const columns = this.columns();
    const filter = this.filter();
    const { key, descending } = this.sort();
    const contains = (value: string, part: string) => !part || value.toLocaleLowerCase('de-DE').includes(part.toLocaleLowerCase('de-DE'));
    return this.cases().filter(c => {
      const o = c.organization;
      const inView = filter === 'all' ? !o.folderId : filter === 'bookmarks' ? o.bookmark : filter === 'confirmed' ? o.confirmed && !o.dispatched
        : filter === 'dispatched' ? o.dispatched : filter === 'unfiled' ? !o.folderId : QUEUES.includes(filter) ? c.queueId === filter : o.folderId === filter;
      const id = this.identity(c);
      return inView && (!this.statusFilter() || c.queueId === this.statusFilter())
        && contains(`${c.caseId} ${c.title} ${o.note}`, needle) && contains(`${id.person} ${id.subject}`, columns.subject)
        && contains(id.number, columns.number) && contains(this.nextStep(c), columns.next) && contains(this.resultLabel(c.caseId), columns.result);
    }).sort((a, b) => {
      const left = this.sortValue(a, key), right = this.sortValue(b, key);
      const compared = left.localeCompare(right, 'de-DE', { numeric: true });
      return (descending ? -compared : compared) || a.caseId.localeCompare(b.caseId);
    });
  });
  protected readonly selectedCases = computed(() => this.cases().filter(c => this.selection().includes(c.caseId)));
  protected readonly targets = computed(() => { const opened = this.opened(); return opened ? [opened.id] : this.selection(); });
  protected readonly targetCases = computed(() => (this.workspace()?.cases ?? []).filter(c => this.targets().includes(c.caseId)));
  protected readonly focused = computed(() => (this.workspace()?.cases ?? []).find(c => c.caseId === this.active()) ?? null);
  protected readonly openedCase = computed(() => { const o = this.opened(); return o ? (this.workspace()?.cases ?? []).find(c => c.caseId === o.id) ?? null : null; });
  protected readonly filtering = computed(() => this.filter() !== 'all' || !!this.query() || !!this.statusFilter() || Object.values(this.columns()).some(Boolean));
  protected readonly canConfirmTargets = computed(() => canConfirm(this.targetCases()));
  protected readonly canDispatchTargets = computed(() => canDispatch(this.targetCases()));

  ngOnInit(): void { this.store.load(); }

  // ---- presentation helpers
  protected identity(c: WorkCase) { return caseIdentity(c.caseId, c.title, wb.platformCheck); }
  protected queueName(id: string): string { return label(wb.queueNames, id, de.unknown); }
  protected status(c: WorkCase): string { return c.organization.dispatched ? wb.dispatched : c.organization.confirmed ? wb.confirmed : this.queueName(c.queueId); }
  protected statusKey(c: WorkCase): string { return c.organization.dispatched || c.organization.confirmed ? 'complete' : c.queueId; }
  protected nextStep(c: WorkCase): string {
    const step = processingStep(c);
    return step === 'complete' ? wb.completeNext : step === 'dispatch' ? wb.confirmedNext : label(wb.nextSteps, c.queueId, de.unknown);
  }
  protected request(c: WorkCase): string { return this.store.files()[c.caseId]?.context?.request ?? ''; }
  protected findings(c: WorkCase): number { return this.store.files()[c.caseId]?.findings.length ?? 0; }
  protected documentCount(c: WorkCase): string { const n = this.store.files()[c.caseId]?.documents.length; return n === undefined ? '' : String(n); }
  protected resultLabel(caseId: string): string {
    const outcome = this.outcomes()[caseId];
    if (!(caseId in this.outcomes())) return wb.pending;
    if (outcome === null) return wb.noRuleResult;
    return label(de.outcomes, outcome, de.unknown);
  }
  protected resultTone(caseId: string): string { const o = this.outcomes()[caseId]; return o && Object.hasOwn(de.outcomes, o) ? o : 'NONE'; }
  protected queueColor(id: string): string { return this.workspace()?.queueColors[id] ?? DEFAULT_QUEUE_COLORS[id] ?? '#64748b'; }
  protected caseColor(c: WorkCase): string { return c.organization.color ?? this.queueColor(c.queueId); }
  protected folderLabel(id: string | null): string | null { return id ? this.workspace()?.folders.find(f => f.id === id)?.label ?? null : null; }
  protected count(kind: string): number {
    const cases = this.cases();
    switch (kind) {
      case 'all': case 'unfiled': return cases.filter(c => !c.organization.folderId).length;
      case 'bookmarks': return cases.filter(c => c.organization.bookmark).length;
      case 'confirmed': return cases.filter(c => c.organization.confirmed && !c.organization.dispatched).length;
      case 'dispatched': return cases.filter(c => c.organization.dispatched).length;
      default: return QUEUES.includes(kind) ? cases.filter(c => c.queueId === kind).length : cases.filter(c => c.organization.folderId === kind).length;
    }
  }
  private sortValue(c: WorkCase, key: SortKey): string {
    const id = this.identity(c);
    return key === 'subject' ? `${id.person} ${id.subject}` : key === 'status' ? this.status(c) : key === 'result' ? this.resultLabel(c.caseId) : id.number;
  }
  protected sortBy(key: SortKey): void { this.sort.update(s => s.key === key ? { key, descending: !s.descending } : { key, descending: false }); }
  protected ariaSort(key: SortKey): string { const s = this.sort(); return s.key === key ? (s.descending ? 'descending' : 'ascending') : 'none'; }
  protected setColumn(key: ColumnKey, value: string): void { this.columns.update(c => ({ ...c, [key]: value.slice(0, 100) })); }
  protected resetFilters(): void { this.query.set(''); this.columns.set({ subject: '', number: '', next: '', result: '' }); this.statusFilter.set(''); this.filter.set('all'); }
  protected setFilter(id: string): void { this.filter.set(id); this.opened.set(null); this.menu.set(null); if (this.narrow()) this.navOpen.set(false); }
  private narrow(): boolean { return typeof window !== 'undefined' && typeof window.matchMedia === 'function' && window.matchMedia('(max-width: 860px)').matches; }

  // ---- selection and navigation
  protected rowClick(event: MouseEvent, c: WorkCase): void {
    if ((event.target as HTMLElement).closest('input')) return;
    this.choose(c.caseId, event.shiftKey, event.ctrlKey || event.metaKey);
    (event.currentTarget as HTMLElement).focus();
  }
  protected choose(id: string, range: boolean, toggle: boolean): void {
    this.active.set(id);
    if (range) {
      const ids = selectRange(this.visible().map(c => c.caseId), this.anchor, id);
      if (ids === null) { this.flash(wb.selectionLimit); return; }
      this.selection.set(ids);
      if (!this.anchor) this.anchor = id;
      return;
    }
    this.anchor = id;
    this.selection.update(current => toggle
      ? (current.includes(id) ? current.filter(v => v !== id) : current.length < MAX_SELECTION ? [...current, id] : current)
      : [id]);
  }
  protected toggleVisible(checked: boolean): void { this.selection.set(checked ? this.visible().slice(0, MAX_SELECTION).map(c => c.caseId) : []); }
  protected allVisibleSelected(): boolean { const v = this.visible().slice(0, MAX_SELECTION); return v.length > 0 && v.every(c => this.selection().includes(c.caseId)); }
  protected rowKeys(event: KeyboardEvent, c: WorkCase): void {
    if (event.target !== event.currentTarget) return;
    if (event.key === 'ContextMenu' || (event.shiftKey && event.key === 'F10')) { this.openMenu(event, c.caseId); return; }
    if (event.key === 'Enter') { event.preventDefault(); this.open(c.caseId); return; }
    if (event.key === ' ') { event.preventDefault(); this.choose(c.caseId, false, true); return; }
    if (!['ArrowUp', 'ArrowDown', 'Home', 'End'].includes(event.key) || event.altKey || event.ctrlKey || event.metaKey) return;
    event.preventDefault();
    const list = this.visible();
    const index = list.findIndex(v => v.caseId === c.caseId);
    const next = event.key === 'Home' ? 0 : event.key === 'End' ? list.length - 1 : Math.max(0, Math.min(list.length - 1, index + (event.key === 'ArrowDown' ? 1 : -1)));
    const target = list[next];
    if (!target) return;
    if (event.shiftKey && !this.anchor) this.anchor = c.caseId;
    this.choose(target.caseId, event.shiftKey, false);
    this.focusRow(target.caseId);
  }
  private focusRow(id: string): void {
    afterNextRender(() => {
      const row = [...this.host.nativeElement.querySelectorAll<HTMLElement>('tr[data-case-id]')].find(r => r.dataset['caseId'] === id);
      row?.focus(); row?.scrollIntoView({ block: 'nearest' });
    }, { injector: this.injector });
  }
  protected open(id: string, tab: CaseTab = 'overview'): void { this.menu.set(null); this.active.set(id); this.opened.set({ id, tab }); }
  protected closeCase(): void { const id = this.opened()?.id; this.opened.set(null); if (id) this.focusRow(id); }

  // ---- context menus
  protected openMenu(event: MouseEvent | KeyboardEvent, id: string): void {
    event.preventDefault();
    const ids = this.selection().includes(id) ? this.selection() : [id];
    if (!this.selection().includes(id)) this.anchor = id;
    this.selection.set(ids); this.active.set(id);
    this.returnFocus = event.currentTarget as HTMLElement;
    const box = (event.currentTarget as HTMLElement).getBoundingClientRect();
    const mouse = event instanceof MouseEvent && event.clientX > 0;
    this.menu.set({ x: Math.max(8, Math.min(mouse ? event.clientX : box.left + 24, window.innerWidth - 280)), y: Math.max(8, Math.min(mouse ? event.clientY : box.top + 24, window.innerHeight - 380)), ids, sub: null });
    afterNextRender(() => this.host.nativeElement.querySelector<HTMLButtonElement>('.context-menu button:not(:disabled)')?.focus(), { injector: this.injector });
  }
  protected menuKeys(event: KeyboardEvent): void {
    const menu = event.currentTarget as HTMLElement;
    if (event.key === 'Escape') { this.closeMenu(); return; }
    if (event.key === 'Tab') { this.menu.set(null); return; }
    if (!['ArrowDown', 'ArrowUp', 'Home', 'End'].includes(event.key)) return;
    event.preventDefault();
    const items = [...menu.querySelectorAll<HTMLButtonElement>('button:not(:disabled)')];
    const i = items.indexOf(document.activeElement as HTMLButtonElement);
    const next = event.key === 'Home' ? 0 : event.key === 'End' ? items.length - 1 : (i + (event.key === 'ArrowDown' ? 1 : items.length - 1)) % items.length;
    items[next]?.focus();
  }
  protected closeMenu(): void { this.menu.set(null); this.navMenu.set(null); this.returnFocus?.focus(); }
  protected toggleSub(sub: 'color' | 'folder'): void { this.menu.update(m => m ? { ...m, sub: m.sub === sub ? null : sub } : m); }
  protected menuCases(): WorkCase[] { const ids = this.menu()?.ids ?? []; return (this.workspace()?.cases ?? []).filter(c => ids.includes(c.caseId)); }
  protected allBookmarked(cases: WorkCase[]): boolean { return cases.length > 0 && cases.every(c => c.organization.bookmark); }
  protected anyFiled(cases: WorkCase[]): boolean { return cases.some(c => c.organization.folderId); }
  protected canConfirmCases(cases: WorkCase[]): boolean { return canConfirm(cases); }
  protected canDispatchCases(cases: WorkCase[]): boolean { return canDispatch(cases); }
  protected openNavMenu(event: MouseEvent, id: string, folder: boolean): void {
    event.preventDefault();
    this.returnFocus = event.currentTarget as HTMLElement;
    const box = (event.currentTarget as HTMLElement).getBoundingClientRect();
    this.navMenu.set({ x: Math.min(box.right - 40, window.innerWidth - 240), y: Math.min(box.top + 8, window.innerHeight - 120), id, folder });
    afterNextRender(() => this.host.nativeElement.querySelector<HTMLButtonElement>('.nav-menu button')?.focus(), { injector: this.injector });
  }
  protected editNavItem(): void {
    const item = this.navMenu();
    if (!item) return;
    this.navMenu.set(null);
    if (item.folder) { const f = this.workspace()?.folders.find(x => x.id === item.id); this.begin('rename', [], { folderId: item.id, label: f?.label ?? '', color: f?.color ?? COLOR_PRESETS['blue'] }); }
    else this.begin('queueColor', [], { folderId: item.id, color: this.queueColor(item.id) });
  }

  // ---- dialogs and commands
  protected begin(kind: DialogKind, targets: string[] = this.targets(), extra: Partial<Dialog> = {}): void {
    this.returnFocus = document.activeElement as HTMLElement;
    this.menu.set(null); this.navMenu.set(null);
    const first = (this.workspace()?.cases ?? []).find(c => c.caseId === targets[0]);
    this.dialog.set({ kind, targets, label: '', color: COLOR_PRESETS['blue'], folderId: this.workspace()?.folders[0]?.id ?? '', parentId: '', note: first?.organization.note ?? '', ...extra });
    afterNextRender(() => {
      const element = this.dialogElement()?.nativeElement;
      if (element && !element.open) element.showModal?.();
      element?.querySelector<HTMLElement>('input:not([type=color]),select,textarea,button[type=submit]')?.focus();
    }, { injector: this.injector });
  }
  protected patchDialog(patch: Partial<Dialog>): void { this.dialog.update(d => d ? { ...d, ...patch } : d); }
  protected closeDialog(): void { this.dialogElement()?.nativeElement.close?.(); this.dialog.set(null); this.returnFocus?.focus(); }
  protected async submitDialog(event: Event): Promise<void> {
    event.preventDefault();
    const d = this.dialog();
    if (!d) return;
    const done = (() => {
      switch (d.kind) {
        case 'folder': return this.run('CREATE_FOLDER', [], { folderId: null, label: d.label, color: d.color, parentId: d.parentId || null });
        case 'rename': return this.run('UPDATE_FOLDER', [], { folderId: d.folderId, label: d.label, color: d.color });
        case 'delete': return this.run('DELETE_FOLDER', [], { folderId: d.folderId });
        case 'move': return this.run('MOVE', d.targets, { folderId: d.folderId || null });
        case 'color': return this.run('COLOR', d.targets, { color: d.color });
        case 'queueColor': return this.run('QUEUE_COLOR', [], { folderId: d.folderId, color: d.color });
        case 'note': return this.run('NOTE', d.targets, { note: d.note });
        case 'confirm': return this.run('CONFIRM', d.targets);
        case 'dispatch': return this.run('DISPATCH', d.targets);
      }
    })();
    if (await done) this.closeDialog();
  }
  async run(action: string, ids: string[], extra: Record<string, unknown> = {}): Promise<boolean> {
    this.menu.set(null);
    this.error.set('');
    const result = await this.store.command(action, ids, extra);
    if (!result.ok) { this.error.set(result.message ?? wb.completionUnknown); return false; }
    if (action === 'MOVE') {
      const remaining = new Set(this.visible().map(c => c.caseId));
      this.selection.update(s => s.filter(id => remaining.has(id)));
    }
    if (action === 'DELETE_FOLDER' && this.filter() === extra['folderId']) this.filter.set('all');
    if (result.packageJson) this.download(`normacase-versand-demo-${this.workspace()?.revision ?? 0}.json`, result.packageJson);
    this.flash(action === 'DISPATCH' ? wb.dispatchedNotice : wb.saved);
    return true;
  }
  protected bookmark(ids: string[]): void {
    const cases = (this.workspace()?.cases ?? []).filter(c => ids.includes(c.caseId));
    void this.run('BOOKMARK', ids, { value: !this.allBookmarked(cases) });
  }
  private download(name: string, content: string): void {
    const url = URL.createObjectURL(new Blob([content], { type: 'application/json' }));
    const link = document.createElement('a');
    link.href = url; link.download = name; link.click();
    setTimeout(() => URL.revokeObjectURL(url), 1000);
  }
  private flash(message: string): void { this.notice.set(message); setTimeout(() => { if (this.notice() === message) this.notice.set(''); }, 3500); }
  protected async refresh(): Promise<void> { if (!(await this.store.refresh())) this.error.set(wb.invalid); }

  // ---- reading pane
  protected startResize(event: PointerEvent): void { this.resizing = true; (event.currentTarget as HTMLElement).setPointerCapture(event.pointerId); }
  protected moveResize(event: PointerEvent): void {
    const body = this.body()?.nativeElement;
    if (!this.resizing || !body) return;
    const r = body.getBoundingClientRect();
    const ratio = this.pane() === 'right' ? (r.right - event.clientX) / Math.max(1, r.width) : (r.bottom - event.clientY) / Math.max(1, r.height);
    this.panePercent.set(Math.max(25, Math.min(75, Math.round(ratio * 100))));
  }
  protected endResize(): void { this.resizing = false; }
  protected resizeKeys(event: KeyboardEvent): void {
    const grow = this.pane() === 'right' ? 'ArrowLeft' : 'ArrowUp', shrink = this.pane() === 'right' ? 'ArrowRight' : 'ArrowDown';
    if (event.key !== grow && event.key !== shrink) return;
    event.preventDefault();
    this.panePercent.update(v => Math.max(25, Math.min(75, v + (event.key === grow ? 5 : -5))));
  }
  protected cyclePane(): void { this.pane.update(p => p === 'off' ? 'right' : 'off'); }

  // ---- global shortcuts (only while the workbench is shown)
  @HostListener('document:keydown', ['$event'])
  protected shortcuts(event: KeyboardEvent): void {
    if (this.dialog()) return;
    const editing = (event.target as HTMLElement | null)?.closest?.('input,textarea,select,[contenteditable=true]');
    if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 'k') { event.preventDefault(); this.searchField()?.nativeElement.focus(); return; }
    if (event.key === 'F4') { event.preventDefault(); this.cyclePane(); return; }
    if (event.key === 'F5') { event.preventDefault(); void this.refresh(); return; }
    if (editing || this.menu() || this.navMenu()) return;
    if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 'a' && !(event.target as HTMLElement)?.closest?.('.case-view')) { event.preventDefault(); this.toggleVisible(true); return; }
    if (event.altKey && event.key === 'ArrowLeft' && this.opened()) { event.preventDefault(); this.closeCase(); return; }
    if (event.key === 'Escape' && !this.opened() && !(event.target as HTMLElement)?.closest?.('.case-view')) { this.anchor = null; this.selection.set([]); }
  }
  @HostListener('document:pointerdown', ['$event'])
  protected outside(event: PointerEvent): void {
    if (!(event.target as HTMLElement)?.closest?.('.context-menu,.nav-menu')) { this.menu.set(null); this.navMenu.set(null); }
  }
}
