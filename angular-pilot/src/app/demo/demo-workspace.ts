import { afterNextRender, Component, computed, ElementRef, inject, Injector, OnDestroy, OnInit, output, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { forkJoin, Subscription } from 'rxjs';
import { demo, label } from '../de';
import { CaseFile } from './case-file';
import { DemoQueues, parseDemoQueues, parseReferenceCases, ReferenceCase } from './demo-data';

const PREVIEW_ROWS = 6;

/** Anonymous, read-only synthetic workspace served by the local preview host. */
@Component({
  selector: 'nc-demo-workspace',
  imports: [CaseFile],
  templateUrl: './demo-workspace.html',
})
export class DemoWorkspace implements OnInit, OnDestroy {
  readonly leave = output<void>();
  protected readonly text = demo;
  private readonly http = inject(HttpClient);
  private readonly injector = inject(Injector);
  private readonly host: ElementRef<HTMLElement> = inject(ElementRef);
  private pending?: Subscription;

  protected readonly loading = signal(true);
  protected readonly error = signal('');
  protected readonly queues = signal<DemoQueues | null>(null);
  protected readonly references = signal<ReferenceCase[]>([]);
  protected readonly query = signal('');
  protected readonly expanded = signal<Record<string, boolean>>({});
  protected readonly openCase = signal<{ caseId: string; state: string } | null>(null);

  private readonly needle = computed(() => this.query().trim().toLocaleLowerCase('de-DE'));
  protected readonly visibleQueues = computed(() => {
    const queues = this.queues();
    if (!queues) return [];
    const needle = this.needle();
    return queues.queues.map(queue => {
      const matches = needle ? queue.items.filter(item => item.caseId.toLocaleLowerCase('de-DE').includes(needle)) : queue.items;
      const all = this.expanded()[queue.queueId] || needle.length > 0;
      return { queueId: queue.queueId, total: queue.items.length, matches: matches.length, items: all ? matches : matches.slice(0, PREVIEW_ROWS) };
    });
  });
  protected readonly visibleReferences = computed(() => {
    const needle = this.needle();
    return needle
      ? this.references().filter(item => item.caseId.includes(needle) || item.title.toLocaleLowerCase('de-DE').includes(needle))
      : this.references();
  });

  ngOnInit(): void {
    const options = { responseType: 'text' as const, cache: 'no-store' as const, credentials: 'omit' as const };
    this.pending = forkJoin([this.http.get('/api/work-queues', options), this.http.get('/api/document-cases', options)]).subscribe({
      next: ([queueBody, referenceBody]) => {
        this.loading.set(false);
        const queues = parseDemoQueues(queueBody);
        const references = parseReferenceCases(referenceBody);
        if (!queues || !references) { this.error.set(demo.invalid); return; }
        this.queues.set(queues);
        this.references.set(references);
      },
      error: () => { this.loading.set(false); this.error.set(demo.unavailable); },
    });
  }
  ngOnDestroy(): void { this.pending?.unsubscribe(); }

  protected queueLabel(id: string): string { return label(demo.queues, id, id); }
  protected queueHelp(id: string): string { return label(demo.queueHelp, id, ''); }
  protected stateLabel(id: string): string { return label(demo.states, id, id); }
  protected toggle(queueId: string): void { this.expanded.update(value => ({ ...value, [queueId]: !value[queueId] })); }
  protected search(value: string): void { this.query.set(value.slice(0, 100)); }

  protected open(caseId: string, state: string): void { this.openCase.set({ caseId, state }); }
  protected close(): void {
    const caseId = this.openCase()?.caseId;
    this.openCase.set(null);
    if (!caseId) return;
    afterNextRender(() => {
      const target = [...this.host.nativeElement.querySelectorAll<HTMLButtonElement>('button[data-case-id]')]
        .find(button => button.dataset['caseId'] === caseId);
      target?.focus();
    }, { injector: this.injector });
  }
}
