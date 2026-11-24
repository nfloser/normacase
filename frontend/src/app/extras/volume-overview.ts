import { Component, computed, input, output } from '@angular/core';
import { de } from '../de';
import { ProcessSummary } from '../workbench/api';
import { PLATFORM_SYSTEM } from '../workbench/case-view';

const OUTCOMES = ['SUPPORTED', 'NOT_SUPPORTED', 'INCOMPLETE', 'HUMAN_REVIEW', 'NOT_ASSESSED'];

/** Massenfälle: wie viele Plattformfälle in welcher Arbeitsliste mit welchem Prüfergebnis liegen. */
@Component({
  selector: 'nc-volume-overview',
  template: `
    <section class="tool-card wide">
      @if (total()) {
        <div class="tool-toolbar">
          <p class="muted">{{ x.platformCount(total()) }}</p>
          <span class="spacer"></span>
          <button type="button" class="button primary" (click)="showInList.emit()">{{ x.showInList }}</button>
        </div>
        <div class="table-wrap">
          <table class="plain-table matrix">
            <caption class="sr-only">{{ x.views['massenfaelle'] }}</caption>
            <thead><tr><th scope="col">{{ x.worklist }}</th>@for (o of outcomes; track o) { <th scope="col"><span class="result-pill ampel" [attr.data-outcome]="o">{{ outcomeLabel(o) }}</span></th> }<th scope="col">{{ x.total }}</th></tr></thead>
            <tbody>
              @for (r of matrix(); track r.queue) {
                <tr>
                  <th scope="row"><span class="nav-dot" [attr.data-queue]="r.queue" aria-hidden="true"></span> {{ queueLabel(r.queue) }}</th>
                  @for (c of r.counts; track $index) { <td class="num">{{ c || '' }}</td> }
                  <td class="num"><span class="bar" [style.--share]="r.total / max()" aria-hidden="true"></span><strong>{{ r.total }}</strong></td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      } @else {
        <p class="muted">{{ x.noPlatformCases }}</p>
      }
    </section>
  `,
})
export class VolumeOverview {
  readonly processes = input.required<ProcessSummary[]>();
  readonly showInList = output<void>();
  protected readonly x = de.extras;
  protected readonly outcomes = OUTCOMES;

  private readonly platform = computed(() => this.processes().filter((p) => p.externalReference.system === PLATFORM_SYSTEM));
  protected readonly total = computed(() => this.platform().length);
  protected readonly matrix = computed(() => {
    const queues = ['review', 'clarification', 'conflict', 'technical', 'documents', 'approval', 'done'];
    return queues.map((queue) => {
      const items = this.platform().filter((p) => p.queue === queue);
      return { queue, counts: OUTCOMES.map((o) => items.filter((p) => (p.outcome ?? 'NOT_ASSESSED') === o).length), total: items.length };
    }).filter((r) => r.total > 0);
  });
  protected readonly max = computed(() => Math.max(1, ...this.matrix().map((r) => r.total)));

  protected outcomeLabel(o: string): string {
    return de.outcomes[o] ?? o;
  }

  protected queueLabel(q: string): string {
    return de.queues[q] ?? q;
  }
}
