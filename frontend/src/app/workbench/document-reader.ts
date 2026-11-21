import { Component, DestroyRef, OnDestroy, computed, effect, inject, input, signal, untracked } from '@angular/core';
import { Subscription, from, mergeMap } from 'rxjs';
import { de } from '../de';
import { DocumentMeta, WorkbenchApi } from './api';

interface PageState { url?: string; failed?: boolean }
const ZOOM_STEPS = [50, 67, 80, 100, 125, 150, 200];

/**
 * Dokumentleser der Fallakte. Seiten werden mit dem Access Token geladen und nur als Blob-URL im Speicher
 * gehalten; beim Wechsel oder Schließen werden sie freigegeben. NormaCase interpretiert die Dokumente nicht.
 */
@Component({
  selector: 'nc-pdf-reader',
  templateUrl: './document-reader.html',
})
export class DocumentReader implements OnDestroy {
  readonly processId = input.required<string>();
  readonly documents = input.required<readonly DocumentMeta[]>();
  protected readonly t = de.reader;
  private readonly api = inject(WorkbenchApi);
  private loading?: Subscription;
  private download?: string;

  protected readonly selectedId = signal<string | null>(null);
  protected readonly pages = signal<Record<number, PageState>>({});
  protected readonly text = signal<string | null>(null);
  protected readonly page = signal(1);
  protected readonly zoom = signal<number | 'width'>('width');
  protected readonly rotation = signal(0);
  protected readonly zoomSteps = ZOOM_STEPS;

  protected readonly selected = computed(() => this.documents().find((d) => d.id === this.selectedId()) ?? null);
  protected readonly pageNumbers = computed(() => {
    const doc = this.selected();
    if (!doc || doc.mediaType === 'text/plain') return [];
    return Array.from({ length: doc.mediaType === 'application/pdf' ? doc.pageCount : 1 }, (_, i) => i + 1);
  });
  protected readonly imageWidth = computed(() => {
    const z = this.zoom();
    return z === 'width' ? '100%' : `${z}%`;
  });

  constructor() {
    effect(() => {
      const docs = this.documents();
      untracked(() => {
        if (!docs.some((d) => d.id === this.selectedId())) this.select(docs[0]?.id ?? null);
      });
    });
    inject(DestroyRef).onDestroy(() => this.release());
  }

  ngOnDestroy(): void {
    this.release();
  }

  protected select(id: string | null): void {
    this.release();
    this.selectedId.set(id);
    this.page.set(1);
    this.rotation.set(0);
    const doc = this.documents().find((d) => d.id === id);
    if (!doc) return;
    const process = this.processId();
    if (doc.mediaType === 'text/plain') {
      this.loading = this.api.documentBlob(process, doc.id).subscribe({
        next: (blob) => void blob.text().then((value) => this.text.set(value)),
        error: () => this.text.set(this.t.failed),
      });
      return;
    }
    const numbers = doc.mediaType === 'application/pdf' ? this.pageNumbersFor(doc) : [1];
    this.loading = from(numbers)
      .pipe(mergeMap((n) => {
        const request = doc.mediaType === 'application/pdf' ? this.api.pageBlob(process, doc.id, n) : this.api.documentBlob(process, doc.id);
        return new Promise<[number, PageState]>((resolve) => request.subscribe({
          next: (blob) => resolve([n, { url: URL.createObjectURL(blob) }]),
          error: () => resolve([n, { failed: true }]),
        }));
      }, 3))
      .subscribe(([n, state]) => {
        if (this.selectedId() !== doc.id) {
          if (state.url) URL.revokeObjectURL(state.url);
          return;
        }
        this.pages.update((p) => ({ ...p, [n]: state }));
      });
  }

  protected icon(doc: DocumentMeta): string {
    return this.t.kinds[doc.mediaType] ?? '?';
  }

  protected meta(doc: DocumentMeta): string {
    const size = new Intl.NumberFormat('de-DE', { maximumFractionDigits: 1 }).format(doc.sizeBytes / 1024);
    const pages = doc.mediaType === 'application/pdf' ? ` · ${doc.pageCount} ${doc.pageCount === 1 ? 'Seite' : 'Seiten'}` : '';
    return `${this.icon(doc)} · ${size} KB${pages}`;
  }

  protected go(delta: number): void {
    const target = Math.min(Math.max(1, this.page() + delta), this.pageNumbers().length || 1);
    this.page.set(target);
    document.getElementById(this.pageAnchor(target))?.scrollIntoView({ block: 'start' });
  }

  protected pageAnchor(n: number): string {
    return `nc-page-${this.selectedId()}-${n}`;
  }

  protected stepZoom(direction: 1 | -1): void {
    const current = this.zoom();
    const value = current === 'width' ? 100 : current;
    const next = direction > 0 ? ZOOM_STEPS.find((s) => s > value) : [...ZOOM_STEPS].reverse().find((s) => s < value);
    if (next) this.zoom.set(next);
  }

  protected setZoom(value: string): void {
    this.zoom.set(value === 'width' ? 'width' : Number(value));
  }

  protected rotate(): void {
    this.rotation.update((r) => (r + 90) % 360);
  }

  protected keys(event: KeyboardEvent): void {
    const actions: Record<string, () => void> = {
      ArrowRight: () => this.go(1), PageDown: () => this.go(1), ArrowLeft: () => this.go(-1), PageUp: () => this.go(-1),
      '+': () => this.stepZoom(1), '-': () => this.stepZoom(-1), '0': () => this.zoom.set('width'), r: () => this.rotate(),
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
      link.download = doc.id + (doc.mediaType === 'application/pdf' ? '.pdf' : '');
      link.click();
    });
  }

  private pageNumbersFor(doc: DocumentMeta): number[] {
    return Array.from({ length: doc.pageCount }, (_, i) => i + 1);
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
