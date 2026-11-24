import { Component, DestroyRef, ElementRef, OnDestroy, computed, effect, inject, input, signal, untracked } from '@angular/core';
import { Subscription, from, mergeMap } from 'rxjs';
import { de, germanDate, label } from '../de';
import { DocumentMeta, WorkbenchApi } from './api';

interface PageState { url?: string; failed?: boolean }
const ZOOM_STEPS = [50, 67, 80, 100, 125, 150, 200, 300];
type Zoom = number | 'width' | 'page';
const CATEGORIES = ['ORDER', 'MEDICAL', 'CARE', 'PROOF', 'OTHER'] as const;
const EXTENSIONS: Record<string, string> = {
  'application/pdf': '.pdf', 'image/png': '.png', 'image/jpeg': '.jpg', 'image/tiff': '.tiff', 'text/plain': '.txt',
};

/** Sprungziel aus der Begründung: Dokument und Seite, aus der eine Angabe stammt. {@code seq} löst erneut aus. */
export interface ReaderTarget { readonly documentId: string; readonly page: number; readonly seq: number }
/** Nachweis, den das Regelwerk verlangt und der in der Akte fehlt oder widersprüchlich ist. */
export interface MissingProof { readonly id: string; readonly label: string; readonly status: string }

/** Seiten mit eigener Vorschau: PDF über die Vorschau des Fachsystems, TIFF über die serverseitig erzeugten Seiten. */
function paged(doc: DocumentMeta): boolean {
  return doc.mediaType === 'application/pdf' || doc.mediaType === 'image/tiff';
}

/**
 * Dokumentleser der Fallakte. Seiten werden mit dem Access Token geladen und nur als Blob-URL im Speicher
 * gehalten; beim Wechsel oder Schließen werden sie freigegeben. NormaCase interpretiert die Dokumente nicht.
 *
 * Großansicht und eigenes Fenster zeigen dieselben Blob-URLs. Das Fenster wird aus dieser Seite heraus
 * aufgebaut (gleiche Herkunft, gleiche CSP, keine Skripte im Fenster) und schließt sich mit dem Vorgang.
 */
@Component({
  selector: 'nc-pdf-reader',
  templateUrl: './document-reader.html',
  host: { '[class.reader-max]': 'maximized()', '(keydown.escape)': 'restore($event)' },
})
export class DocumentReader implements OnDestroy {
  readonly processId = input.required<string>();
  readonly documents = input.required<readonly DocumentMeta[]>();
  readonly fieldLabels = input<Record<string, string>>({});
  readonly missing = input<readonly MissingProof[]>([]);
  readonly target = input<ReaderTarget | null>(null);
  protected readonly t = de.reader;
  private readonly api = inject(WorkbenchApi);
  private loading?: Subscription;
  private download?: string;
  private readonly windows = new Set<Window>();
  private readonly host = (inject(ElementRef) as ElementRef<HTMLElement>).nativeElement;

  protected readonly selectedId = signal<string | null>(null);
  protected readonly pages = signal<Record<number, PageState>>({});
  protected readonly text = signal<string | null>(null);
  protected readonly page = signal(1);
  protected readonly zoom = signal<Zoom>('width');
  protected readonly rotation = signal(0);
  protected readonly maximized = signal(false);
  protected readonly highlight = signal<number | null>(null);
  protected readonly notice = signal<string | null>(null);
  protected readonly zoomSteps = ZOOM_STEPS;

  protected readonly selected = computed(() => this.documents().find((d) => d.id === this.selectedId()) ?? null);
  protected readonly pageNumbers = computed(() => {
    const doc = this.selected();
    if (!doc || doc.mediaType === 'text/plain') return [];
    return Array.from({ length: paged(doc) ? doc.pageCount : 1 }, (_, i) => i + 1);
  });
  protected readonly imageWidth = computed(() => {
    const z = this.zoom();
    return z === 'width' ? '100%' : z === 'page' ? null : `${z}%`;
  });
  /** Akte nach Dokumentart, innerhalb der Gruppe nach Ausstellungsdatum (ohne Datum zuletzt). */
  protected readonly groups = computed(() => CATEGORIES
    .map((category) => ({
      category, label: this.t.categories[category],
      docs: this.documents().filter((d) => (CATEGORIES as readonly string[]).includes(d.category ?? 'OTHER')
        ? (d.category ?? 'OTHER') === category : category === 'OTHER')
        .sort((a, b) => (a.issuedOn ?? '9999').localeCompare(b.issuedOn ?? '9999') || a.title.localeCompare(b.title, 'de')),
    }))
    .filter((g) => g.docs.length));

  constructor() {
    effect(() => {
      const docs = this.documents();
      untracked(() => {
        if (!docs.some((d) => d.id === this.selectedId())) this.select(this.groups()[0]?.docs[0]?.id ?? null);
      });
    });
    effect(() => {
      const target = this.target();
      if (target) untracked(() => this.jump(target.documentId, target.page));
    });
    // Ein anderer Vorgang: Fenster mit Unterlagen des alten Vorgangs schließen.
    effect(() => {
      this.processId();
      untracked(() => this.closeWindows());
    });
    inject(DestroyRef).onDestroy(() => this.release());
  }

  ngOnDestroy(): void {
    this.release();
    this.closeWindows();
  }

  protected select(id: string | null, page = 1): void {
    this.release();
    this.selectedId.set(id);
    this.page.set(page);
    this.rotation.set(0);
    this.highlight.set(null);
    this.notice.set(null);
    const doc = this.documents().find((d) => d.id === id);
    if (!doc) return;
    if (doc.mediaType === 'text/plain') {
      this.loading = this.api.documentBlob(this.processId(), doc.id).subscribe({
        next: (blob) => void blob.text().then((value) => this.text.set(value)),
        error: () => this.text.set(this.t.failed),
      });
      return;
    }
    this.loading = this.loadPages(doc, (n, state) => {
      if (this.selectedId() !== doc.id) {
        if (state.url) URL.revokeObjectURL(state.url);
        return;
      }
      this.pages.update((p) => ({ ...p, [n]: state }));
      if (n === this.highlight()) this.scrollTo(n);
    });
  }

  /** Springt aus der Begründung an die Fundstelle einer Angabe und hebt die Seite hervor. */
  protected jump(documentId: string, page: number): void {
    const doc = this.documents().find((d) => d.id === documentId);
    if (!doc) return;
    const target = Math.min(Math.max(1, page), Math.max(1, this.pageNumbersOf(doc).length));
    if (this.selectedId() !== documentId) this.select(documentId, target);
    this.page.set(target);
    this.highlight.set(target);
    this.scrollTo(target);
  }

  protected icon(doc: DocumentMeta): string {
    return label(this.t.kinds, doc.mediaType, '?');
  }

  protected meta(doc: DocumentMeta): string {
    const parts = [doc.issuedOn ? germanDate(doc.issuedOn) : this.t.noDate];
    if (paged(doc) && doc.pageCount > 0) parts.push(`${doc.pageCount} ${doc.pageCount === 1 ? this.t.onePage : this.t.pages}`);
    return parts.join(' · ');
  }

  /** Welche Angaben der Bewertung aus diesem Dokument stammen, für Hinweis und Screenreader. */
  protected usedFor(doc: DocumentMeta): string {
    const fields = [...new Set((doc.references ?? []).map((r) => label(this.fieldLabels(), r.field, r.field)))];
    return fields.length ? `${this.t.usedFor}: ${fields.join(', ')}` : '';
  }

  protected missingLabel(status: string): string {
    return label(this.t.proofStatus, status, status);
  }

  protected go(delta: number): void {
    const target = Math.min(Math.max(1, this.page() + delta), this.pageNumbers().length || 1);
    this.page.set(target);
    this.scrollTo(target);
  }

  protected pageAnchor(n: number): string {
    return `nc-page-${this.selectedId()}-${n}`;
  }

  protected stepZoom(direction: 1 | -1): void {
    const current = this.zoom();
    const value = typeof current === 'number' ? current : 100;
    const next = direction > 0 ? ZOOM_STEPS.find((s) => s > value) : [...ZOOM_STEPS].reverse().find((s) => s < value);
    if (next) this.zoom.set(next);
  }

  protected setZoom(value: string): void {
    this.zoom.set(value === 'width' || value === 'page' ? value : Number(value));
  }

  protected rotate(): void {
    this.rotation.update((r) => (r + 90) % 360);
  }

  protected toggleMaximized(): void {
    this.maximized.update((m) => !m);
    queueMicrotask(() => this.host.querySelector<HTMLElement>('.reader-stage')?.focus({ preventScroll: true }));
  }

  protected restore(event: Event): void {
    if (!this.maximized()) return;
    event.stopPropagation();
    this.maximized.set(false);
  }

  protected keys(event: KeyboardEvent): void {
    const actions: Record<string, () => void> = {
      ArrowRight: () => this.go(1), PageDown: () => this.go(1), ArrowLeft: () => this.go(-1), PageUp: () => this.go(-1),
      '+': () => this.stepZoom(1), '-': () => this.stepZoom(-1), '0': () => this.zoom.set('width'), r: () => this.rotate(),
      f: () => this.toggleMaximized(),
    };
    const action = actions[event.key];
    if (action) {
      event.preventDefault();
      action();
    }
  }

  protected saveOriginal(): void {
    const doc = this.selected();
    if (!doc) return;
    this.api.documentBlob(this.processId(), doc.id).subscribe((blob) => {
      if (this.download) URL.revokeObjectURL(this.download);
      this.download = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = this.download;
      link.download = doc.id + (EXTENSIONS[doc.mediaType] ?? '');
      link.click();
    });
  }

  /** PDF ohne Seitenvorschau: im PDF-Betrachter des Browsers öffnen. Nur für geprüfte PDFs (Signatur beim Eingang). */
  protected openInBrowser(): void {
    const doc = this.selected();
    if (!doc || doc.mediaType !== 'application/pdf') return;
    this.api.documentBlob(this.processId(), doc.id).subscribe((blob) => {
      const url = URL.createObjectURL(new Blob([blob], { type: 'application/pdf' }));
      const win = window.open(url, '_blank', 'noopener');
      if (!win) this.notice.set(this.t.popupBlocked);
      setTimeout(() => URL.revokeObjectURL(url), 60_000);
    });
  }

  /**
   * Öffnet das Dokument in einem eigenen Fenster, z. B. auf einem zweiten Bildschirm neben der Begründung.
   * Das Fenster lädt seine Seiten selbst nach und gibt sie frei, sobald es geschlossen wird.
   */
  protected openWindow(): void {
    const doc = this.selected();
    if (!doc) return;
    const win = window.open('', `nc-doc-${this.processId()}-${doc.id}`, 'popup,width=980,height=1100');
    if (!win) {
      this.notice.set(this.t.popupBlocked);
      return;
    }
    this.windows.add(win);
    const view = new DocumentWindow(win, doc, this.t, this.meta(doc));
    const pages = doc.mediaType === 'text/plain'
      ? this.api.documentBlob(this.processId(), doc.id).subscribe({
        next: (blob) => void blob.text().then((value) => view.showText(value)),
        error: () => view.showText(this.t.failed),
      })
      : this.loadPages(doc, (n, state) => view.showPage(n, state));
    win.addEventListener('pagehide', () => {
      pages.unsubscribe();
      view.release();
      this.windows.delete(win);
    });
  }

  private loadPages(doc: DocumentMeta, done: (n: number, state: PageState) => void): Subscription {
    const process = this.processId();
    return from(this.pageNumbersOf(doc))
      .pipe(mergeMap((n) => {
        const request = paged(doc) ? this.api.pageBlob(process, doc.id, n) : this.api.documentBlob(process, doc.id);
        return new Promise<[number, PageState]>((resolve) => request.subscribe({
          next: (blob) => resolve([n, { url: URL.createObjectURL(blob) }]),
          error: () => resolve([n, { failed: true }]),
        }));
      }, 3))
      .subscribe(([n, state]) => done(n, state));
  }

  private pageNumbersOf(doc: DocumentMeta): number[] {
    if (doc.mediaType === 'text/plain') return [];
    return Array.from({ length: paged(doc) ? doc.pageCount : 1 }, (_, i) => i + 1);
  }

  /** Scrollt nur innerhalb des Lesers; die Arbeitsfläche selbst bleibt stehen. */
  private scrollTo(n: number): void {
    setTimeout(() => {
      const sheet = this.host.querySelector<HTMLElement>(`#${CSS.escape(this.pageAnchor(n))}`);
      const stage = sheet?.closest<HTMLElement>('.reader-stage');
      if (sheet && stage) stage.scrollTop = sheet.offsetTop - 16;
      const item = this.host.querySelector<HTMLElement>('.file-item[aria-pressed="true"]');
      const list = item?.closest<HTMLElement>('.reader-files');
      if (item && list && (item.offsetTop < list.scrollTop || item.offsetTop > list.scrollTop + list.clientHeight - 60)) {
        list.scrollTop = item.offsetTop - 40;
      }
    });
  }

  private closeWindows(): void {
    this.windows.forEach((w) => w.close());
    this.windows.clear();
  }

  private release(): void {
    this.loading?.unsubscribe();
    Object.values(this.pages()).forEach((p) => p.url && URL.revokeObjectURL(p.url));
    this.pages.set({});
    this.text.set(null);
    if (this.download) {
      URL.revokeObjectURL(this.download);
      this.download = undefined;
    }
  }
}

/**
 * Inhalt des eigenen Fensters. Aufbau nur über DOM-Methoden und textContent: Titel und Text der Dokumente
 * werden nie als HTML interpretiert. Stylesheets werden als Verweis übernommen (CSP style-src 'self').
 */
class DocumentWindow {
  private readonly urls: string[] = [];
  private readonly stage: HTMLElement;
  private readonly figures = new Map<number, HTMLElement>();
  private zoom = 100;
  private rotation = 0;

  constructor(private readonly win: Window, doc: DocumentMeta, t: typeof de.reader, meta: string) {
    const d = win.document;
    d.open();
    d.close();
    d.documentElement.lang = 'de';
    const theme = document.documentElement.getAttribute('data-theme');
    if (theme) d.documentElement.setAttribute('data-theme', theme);
    d.title = `${doc.title} – NormaCase`;
    document.querySelectorAll<HTMLLinkElement>('link[rel="stylesheet"]').forEach((link) => {
      const copy = d.createElement('link');
      copy.rel = 'stylesheet';
      copy.href = link.href;
      d.head.append(copy);
    });
    d.body.className = 'reader-window';
    const header = d.createElement('header');
    header.className = 'reader-toolbar';
    const title = d.createElement('strong');
    title.className = 'reader-window-title';
    title.textContent = doc.title;
    const info = d.createElement('span');
    info.className = 'muted small';
    info.textContent = meta;
    const spacer = d.createElement('span');
    spacer.className = 'spacer';
    header.append(title, info, spacer);
    if (doc.mediaType !== 'text/plain') {
      header.append(
        this.button(t.zoomOut, '−', () => this.setZoom(Math.max(50, this.zoom - 25))),
        this.button(t.fitWidth, '↔', () => this.setZoom(100)),
        this.button(t.zoomIn, '+', () => this.setZoom(Math.min(300, this.zoom + 25))),
        this.button(t.rotate, '⟳', () => this.rotate()),
      );
    }
    header.append(this.button(t.closeWindow, '✕', () => win.close()));
    this.stage = d.createElement('main');
    this.stage.className = 'reader-stage';
    this.stage.tabIndex = 0;
    this.stage.setAttribute('aria-label', `${doc.title} – ${t.title}`);
    d.body.append(header, this.stage);
    for (let n = 1; doc.mediaType !== 'text/plain' && n <= (paged(doc) ? doc.pageCount : 1); n++) {
      const figure = d.createElement('figure');
      figure.className = 'sheet simple';
      const wait = d.createElement('p');
      wait.className = 'muted';
      wait.textContent = t.loading;
      const caption = d.createElement('figcaption');
      caption.textContent = `${t.page} ${n}`;
      figure.append(wait, caption);
      this.stage.append(figure);
      this.figures.set(n, figure);
    }
    this.alt = (n) => `${doc.title}, ${t.page} ${n}`;
    this.failed = t.failed;
    this.stage.focus();
  }

  private readonly alt: (n: number) => string;
  private readonly failed: string;

  showPage(n: number, state: PageState): void {
    const figure = this.figures.get(n);
    if (!figure || this.win.closed) {
      if (state.url) URL.revokeObjectURL(state.url);
      return;
    }
    const d = this.win.document;
    figure.firstChild?.remove();
    if (state.url) {
      this.urls.push(state.url);
      const img = d.createElement('img');
      img.src = state.url;
      img.alt = this.alt(n);
      img.style.transform = `rotate(${this.rotation}deg)`;
      figure.prepend(img);
    } else {
      const error = d.createElement('p');
      error.className = 'error-text';
      error.textContent = this.failed;
      figure.prepend(error);
    }
  }

  showText(value: string): void {
    if (this.win.closed) return;
    const pre = this.win.document.createElement('pre');
    pre.className = 'reader-text';
    pre.textContent = value;
    this.stage.append(pre);
  }

  release(): void {
    this.urls.forEach((u) => URL.revokeObjectURL(u));
    this.urls.length = 0;
  }

  private setZoom(value: number): void {
    this.zoom = value;
    this.figures.forEach((f) => (f.style.width = `${value}%`));
  }

  private rotate(): void {
    this.rotation = (this.rotation + 90) % 360;
    this.stage.querySelectorAll('img').forEach((img) => (img.style.transform = `rotate(${this.rotation}deg)`));
  }

  private button(text: string, symbol: string, action: () => void): HTMLButtonElement {
    const b = this.win.document.createElement('button');
    b.type = 'button';
    b.className = 'icon-button';
    b.title = text;
    b.setAttribute('aria-label', text);
    b.textContent = symbol;
    b.addEventListener('click', action);
    return b;
  }
}
