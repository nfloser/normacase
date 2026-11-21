import { afterNextRender, Component, computed, effect, ElementRef, inject, Injector, input, OnDestroy, signal, untracked, viewChild } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Subscription } from 'rxjs';
import { wb } from '../de';
import { DemoDocument } from './document-case';

export interface ReaderTarget { documentId: string; page: number; label?: string; token: number }
type ZoomMode = 'width' | 'page' | number;
const ZOOM_STEPS = [50, 67, 80, 100, 125, 150, 200, 300];
const A4 = 1.4142;

/**
 * Local document reader for the synthetic case file. Shows the server-rendered page
 * previews of each PDF continuously, scans as images and text files as text.
 */
@Component({
  selector: 'nc-pdf-reader',
  templateUrl: './pdf-reader.html',
  host: { class: 'reader' },
})
export class PdfReader implements OnDestroy {
  readonly caseId = input.required<string>();
  readonly documents = input.required<DemoDocument[]>();
  /** Jump request from provenance links; a new token re-triggers the same page. */
  readonly target = input<ReaderTarget | null>(null);
  protected readonly text = wb;
  private readonly http = inject(HttpClient);
  private readonly injector = inject(Injector);
  private readonly stage = viewChild<ElementRef<HTMLElement>>('stage');
  private observer?: IntersectionObserver;
  private resize?: ResizeObserver;
  private pendingText?: Subscription;

  protected readonly selectedId = signal<string | null>(null);
  protected readonly page = signal(1);
  protected readonly zoom = signal<ZoomMode>('width');
  protected readonly rotation = signal(0);
  protected readonly stageSize = signal({ width: 800, height: 600 });
  protected readonly ratios = signal<Record<number, number>>({});
  protected readonly failed = signal<Record<number, boolean>>({});
  protected readonly highlight = signal<{ page: number; label: string } | null>(null);
  protected readonly documentText = signal<string | null>(null);
  protected readonly textState = signal<'idle' | 'loading' | 'error'>('idle');
  protected readonly pageInput = signal('1');

  protected readonly selected = computed(() => this.documents().find(doc => doc.id === this.selectedId()) ?? null);
  protected readonly pages = computed(() => {
    const doc = this.selected();
    if (!doc) return [];
    if (doc.mediaType === 'image/png') return [{ page: 1, url: this.url(doc) }];
    if (doc.mediaType !== 'application/pdf') return [];
    return doc.previewPages.map(page => ({ page, url: `${this.url(doc)}/pages/${page}` }));
  });
  protected readonly pageCount = computed(() => this.selected()?.pages ?? 0);
  protected readonly zoomLabel = computed(() => { const z = this.zoom(); return z === 'width' ? wb.fitWidth : z === 'page' ? wb.fitPage : `${z} %`; });

  constructor() {
    effect(() => {
      const docs = this.documents();
      untracked(() => { if (!docs.some(doc => doc.id === this.selectedId())) this.select(docs[0]?.id ?? null); });
    });
    effect(() => {
      const target = this.target();
      if (target) untracked(() => this.jump(target));
    });
    afterNextRender(() => this.observeStage(), { injector: this.injector });
  }

  ngOnDestroy(): void { this.observer?.disconnect(); this.resize?.disconnect(); this.pendingText?.unsubscribe(); }

  protected url(doc: DemoDocument): string {
    return `/api/document-cases/${encodeURIComponent(this.caseId())}/documents/${encodeURIComponent(doc.id)}`;
  }
  protected icon(doc: DemoDocument): string { return doc.mediaType === 'application/pdf' ? 'PDF' : doc.mediaType === 'image/png' ? 'BILD' : 'TXT'; }
  protected kind(doc: DemoDocument): string { return doc.mediaType === 'application/pdf' ? wb.pdf : doc.mediaType === 'image/png' ? wb.scan : wb.textFile; }
  protected meta(doc: DemoDocument): string { return doc.mediaType === 'application/pdf' ? `${wb.pdf} · ${doc.pages} ${doc.pages === 1 ? wb.page : wb.pages}` : this.kind(doc); }

  select(id: string | null, page = 1): void {
    this.pendingText?.unsubscribe();
    this.selectedId.set(id);
    this.ratios.set({}); this.failed.set({}); this.rotation.set(0); this.highlight.set(null);
    this.documentText.set(null); this.textState.set('idle');
    this.setPage(page);
    const doc = this.selected();
    if (doc?.mediaType === 'text/plain') {
      this.textState.set('loading');
      this.pendingText = this.http.get(this.url(doc), { responseType: 'text', cache: 'no-store', credentials: 'omit' }).subscribe({
        next: text => { this.documentText.set(text.length > 200_000 ? text.slice(0, 200_000) : text); this.textState.set('idle'); },
        error: () => this.textState.set('error'),
      });
    }
    afterNextRender(() => { this.observePages(); this.scrollTo(this.page(), 'auto'); }, { injector: this.injector });
  }

  private jump(target: ReaderTarget): void {
    if (target.documentId !== this.selectedId()) this.select(target.documentId, target.page);
    else { this.setPage(target.page); afterNextRender(() => this.scrollTo(target.page, 'smooth'), { injector: this.injector }); }
    this.highlight.set(target.label ? { page: target.page, label: target.label } : null);
  }

  protected go(delta: number): void { const next = this.clamp(this.page() + delta); this.setPage(next); this.scrollTo(next, 'smooth'); }
  protected commitPageInput(): void {
    const value = Number.parseInt(this.pageInput(), 10);
    const next = Number.isFinite(value) ? this.clamp(value) : this.page();
    this.setPage(next); this.scrollTo(next, 'smooth');
  }
  private clamp(page: number): number { return Math.min(Math.max(page, 1), Math.max(1, this.pages().length || this.pageCount())); }
  private setPage(page: number): void { const next = this.clamp(page); this.page.set(next); this.pageInput.set(String(next)); }

  protected setZoom(value: string): void {
    this.zoom.set(value === 'width' || value === 'page' ? value : Number(value));
    afterNextRender(() => this.scrollTo(this.page(), 'auto'), { injector: this.injector });
  }
  protected stepZoom(direction: -1 | 1): void {
    const current = this.currentPercent();
    const next = direction > 0 ? ZOOM_STEPS.find(step => step > current + 1) : [...ZOOM_STEPS].reverse().find(step => step < current - 1);
    if (next) this.setZoom(String(next));
  }
  protected canZoom(direction: -1 | 1): boolean {
    const current = this.currentPercent();
    return direction > 0 ? current < ZOOM_STEPS[ZOOM_STEPS.length - 1] - 1 : current > ZOOM_STEPS[0] + 1;
  }
  protected rotate(): void { this.rotation.update(value => (value + 90) % 360); }
  protected zoomOptions(): number[] { return ZOOM_STEPS; }
  protected zoomValue(): string { const z = this.zoom(); return typeof z === 'number' ? String(z) : z; }

  /** Current width relative to „fit width“ (100 %). */
  private currentPercent(): number {
    const z = this.zoom();
    if (typeof z === 'number') return z;
    if (z === 'width') return 100;
    const page = this.pages()[0]?.page ?? 1;
    return Math.round((this.box(page, 'page').imageWidth / Math.max(1, this.box(page, 'width').imageWidth)) * 100);
  }
  private ratio(page: number): number { return this.ratios()[page] ?? A4; }
  /** Laid-out box (after rotation) and the image size inside it. */
  protected box(page: number, percent?: ZoomMode): { width: number; height: number; imageWidth: number; imageHeight: number } {
    const { width } = this.stageSize();
    const ratio = this.ratio(page);
    const turned = this.rotation() % 180 !== 0;
    const fitWidth = Math.max(160, width - 48);
    const z = percent ?? this.zoom();
    let imageWidth: number;
    if (z === 'width') imageWidth = turned ? fitWidth / ratio : fitWidth;
    else if (z === 'page') {
      const height = this.stageSize().height - 32;
      imageWidth = turned ? Math.min(height, fitWidth / ratio) : Math.min(fitWidth, height / ratio);
    } else imageWidth = ((turned ? fitWidth / ratio : fitWidth) * z) / 100;
    const imageHeight = imageWidth * ratio;
    return turned ? { width: imageHeight, height: imageWidth, imageWidth, imageHeight } : { width: imageWidth, height: imageHeight, imageWidth, imageHeight };
  }
  protected loaded(page: number, event: Event): void {
    const image = event.target as HTMLImageElement;
    if (image.naturalWidth > 0) this.ratios.update(r => ({ ...r, [page]: image.naturalHeight / image.naturalWidth }));
  }
  protected broken(page: number): void { this.failed.update(f => ({ ...f, [page]: true })); }

  protected keys(event: KeyboardEvent): void {
    if (event.ctrlKey || event.metaKey || event.altKey) return;
    const handled: Record<string, () => void> = {
      PageDown: () => this.go(1), ArrowRight: () => this.go(1), PageUp: () => this.go(-1), ArrowLeft: () => this.go(-1),
      '+': () => this.stepZoom(1), '=': () => this.stepZoom(1), '-': () => this.stepZoom(-1), '0': () => this.setZoom('width'),
      r: () => this.rotate(), R: () => this.rotate(), Home: () => { this.setPage(1); this.scrollTo(1, 'smooth'); },
      End: () => { const last = this.clamp(Number.MAX_SAFE_INTEGER); this.setPage(last); this.scrollTo(last, 'smooth'); },
    };
    const action = handled[event.key];
    if (action) { action(); event.preventDefault(); }
  }

  private scrollTo(page: number, behavior: ScrollBehavior): void {
    const stage = this.stage()?.nativeElement;
    const element = stage?.querySelector<HTMLElement>(`[data-page="${page}"]`);
    if (!stage || !element) return;
    const top = Math.max(0, element.offsetTop - 16);
    if (typeof stage.scrollTo === 'function') stage.scrollTo({ top, behavior }); else stage.scrollTop = top;
  }
  private observeStage(): void {
    const stage = this.stage()?.nativeElement;
    if (!stage || typeof ResizeObserver === 'undefined') return;
    this.resize = new ResizeObserver(entries => {
      const rect = entries[0]?.contentRect;
      if (rect && rect.width > 0) this.stageSize.set({ width: rect.width, height: rect.height });
    });
    this.resize.observe(stage);
  }
  /** Updates the page indicator while scrolling through a multi-page document. */
  private observePages(): void {
    this.observer?.disconnect();
    const stage = this.stage()?.nativeElement;
    if (!stage || typeof IntersectionObserver === 'undefined') return;
    this.observer = new IntersectionObserver(entries => {
      const visible = entries.filter(entry => entry.isIntersecting).sort((a, b) => b.intersectionRatio - a.intersectionRatio)[0];
      const page = Number((visible?.target as HTMLElement | undefined)?.dataset['page']);
      if (page) { this.page.set(page); this.pageInput.set(String(page)); }
    }, { root: stage, threshold: [0.25, 0.5, 0.75] });
    stage.querySelectorAll('[data-page]').forEach(element => this.observer!.observe(element));
  }
}
