import { Component, DestroyRef, computed, effect, inject, input, output, signal, untracked } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { catchError, concatMap, forkJoin, from, map, of, switchMap } from 'rxjs';
import { de, label } from '../de';
import { Presentation, ReleaseSummary, WorkbenchApi } from '../workbench/api';
import { readAssessmentDocument } from '../shared/assessment';
import { ExtrasApi } from './extras-api';

interface ExampleRow {
  readonly key: string;
  readonly releaseId: string;
  readonly releaseName: string;
  readonly exampleId: string;
  readonly label: string;
  readonly expected: string | null;
}

/** Referenzfälle: die Beispiele aller Regelwerke mit dokumentiertem Ergebnis; „Alle prüfen“ bewertet neu und gleicht ab. */
@Component({
  selector: 'nc-reference-examples',
  template: `
    <section class="tool-card wide">
      <div class="tool-toolbar">
        <p class="muted" aria-live="polite">{{ done() ? x.summary(okCount(), rows().length) : '' }}</p>
        <span class="spacer"></span>
        <button type="button" class="button primary" [disabled]="running() || !rows().length || !canAssess()" (click)="checkAll()">{{ running() ? x.checking : x.checkAll }}</button>
      </div>
      <div class="table-wrap">
        <table class="plain-table">
          <caption class="sr-only">{{ x.views['referenzfaelle'] }}</caption>
          <thead><tr><th scope="col">{{ x.release }}</th><th scope="col">{{ x.example }}</th><th scope="col">{{ x.expected }}</th><th scope="col">{{ x.actual }}</th><th scope="col">{{ x.match }}</th><th scope="col"><span class="sr-only">{{ x.openInWorkbench }}</span></th></tr></thead>
          <tbody>
            @for (r of rows(); track r.key) {
              <tr>
                <td>{{ r.releaseName }}</td>
                <td>{{ r.label }}</td>
                <td>@if (r.expected) { <span class="result-pill ampel" [attr.data-outcome]="r.expected">{{ outcome(r.expected) }}</span> }</td>
                <td>@if (results()[r.key]; as a) { @if (a === 'ERROR') { <span class="muted">{{ x.failed }}</span> } @else { <span class="result-pill ampel" [attr.data-outcome]="a">{{ outcome(a) }}</span> } } @else { <span class="muted">{{ x.pending }}</span> }</td>
                <td>@if (results()[r.key]; as a) { <span class="match" [attr.data-ok]="a === r.expected">{{ a === r.expected ? '✓ ' + x.matches : '✕ ' + x.differs }}</span> }</td>
                <td><button type="button" class="link" (click)="openExample.emit({ releaseId: r.releaseId, exampleId: r.exampleId })">{{ x.openInWorkbench }}</button></td>
              </tr>
            }
          </tbody>
        </table>
      </div>
    </section>
  `,
})
export class ReferenceExamples {
  readonly releases = input.required<ReleaseSummary[]>();
  readonly canAssess = input(true);
  readonly openExample = output<{ releaseId: string; exampleId: string }>();

  protected readonly x = de.extras;
  private readonly api = inject(ExtrasApi);
  private readonly workbench = inject(WorkbenchApi);
  private readonly destroy = inject(DestroyRef);
  protected readonly rows = signal<ExampleRow[]>([]);
  protected readonly results = signal<Record<string, string>>({});
  protected readonly running = signal(false);
  protected readonly done = computed(() => this.rows().length > 0 && this.rows().every((r) => this.results()[r.key]));
  protected readonly okCount = computed(() => this.rows().filter((r) => this.results()[r.key] === r.expected).length);

  constructor() {
    effect(() => {
      const list = this.releases();
      untracked(() => {
        if (!list.length) return;
        forkJoin(list.map((r) => this.workbench.presentation(r.releaseId).pipe(catchError(() => of({} as Presentation)),
          map((p) => (p.examples ?? []).map((e) => ({
            key: `${r.releaseId}/${e.id}`, releaseId: r.releaseId, releaseName: p.name ?? r.releaseId,
            exampleId: e.id, label: e.label, expected: e.expectedOutcome ?? null,
          }))))))
          .pipe(takeUntilDestroyed(this.destroy))
          .subscribe((groups) => this.rows.set(groups.flat()));
      });
    });
  }

  protected outcome(code: string): string {
    return label(de.outcomes, code, de.unknown);
  }

  /** Nacheinander, damit die Reihenfolge der Nachweise nachvollziehbar bleibt und der Server nicht geflutet wird. */
  protected checkAll(): void {
    this.running.set(true);
    this.results.set({});
    from(this.rows()).pipe(
      concatMap((r) => this.api.example(r.releaseId, r.exampleId).pipe(
        switchMap((json) => this.api.assess(r.releaseId, json)),
        map((res) => readAssessmentDocument(res.document)?.outcome ?? 'ERROR'),
        catchError(() => of('ERROR')),
        map((outcome) => ({ key: r.key, outcome })),
      )),
      takeUntilDestroyed(this.destroy),
    ).subscribe({
      next: ({ key, outcome }) => this.results.update((m) => ({ ...m, [key]: outcome })),
      complete: () => this.running.set(false),
    });
  }
}
