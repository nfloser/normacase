import { Component, DestroyRef, effect, inject, input, signal, untracked } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { de, germanDate, germanDateTime, label } from '../de';
import { EvidenceRecord, ExtrasApi } from './extras-api';

/** Nachweise eines Vorgangs: nur Prüfsummen, Versionen, Regel und Ergebnis – keine Fallinhalte. */
@Component({
  selector: 'nc-evidence-search',
  template: `
    <section class="tool-card wide">
      <form class="tool-toolbar" (submit)="$event.preventDefault(); search()">
        <label class="field"><span>{{ x.system }}</span><input type="text" required [value]="system()" (input)="system.set($any($event.target).value)"></label>
        <label class="field"><span>{{ x.caseNumber }}</span><input type="text" required [value]="caseId()" (input)="caseId.set($any($event.target).value)"></label>
        <button type="submit" class="button primary" [disabled]="busy() || !system().trim() || !caseId().trim()">{{ x.search }}</button>
      </form>
      <p class="muted">{{ x.retentionNote }}</p>
      @if (error()) { <p class="banner error" role="alert">{{ error() }}</p> }
      @if (records(); as list) {
        @if (list.length) {
          <div class="table-wrap">
            <table class="plain-table">
              <caption class="sr-only">{{ x.views['nachweise'] }}</caption>
              <thead><tr><th scope="col">{{ x.recordedAt }}</th><th scope="col">{{ x.actual }}</th><th scope="col">{{ x.release }}</th><th scope="col">{{ x.date }}</th><th scope="col">{{ x.rule }}</th><th scope="col">{{ x.actor }}</th><th scope="col">{{ x.inputHash }}</th></tr></thead>
              <tbody>
                @for (r of list; track r.evidenceId) {
                  <tr>
                    <td>{{ dateTime(r.recordedAt) }}</td>
                    <td><span class="result-pill ampel" [attr.data-outcome]="r.outcome">{{ outcome(r.outcome) }}</span></td>
                    <td><code>{{ r.knowledgeRelease }}</code></td>
                    <td>{{ date(r.assessmentDate) }}</td>
                    <td>{{ r.ruleId ? r.ruleId + ' v' + r.ruleVersion : '–' }}</td>
                    <td>{{ r.actor }}</td>
                    <td><code class="hash" [title]="r.inputSha256">{{ r.inputSha256.slice(0, 19) }}…</code></td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        } @else {
          <p class="muted" role="status">{{ x.noEvidence }}</p>
        }
      }
    </section>
  `,
})
export class EvidenceSearch {
  /** Vorbelegung mit dem aktiven Vorgang der Liste. */
  readonly initial = input<{ system: string; caseId: string } | null>(null);
  protected readonly x = de.extras;
  protected readonly date = germanDate;
  protected readonly dateTime = germanDateTime;
  private readonly api = inject(ExtrasApi);
  private readonly destroy = inject(DestroyRef);
  protected readonly system = signal('fachsystem-demo');
  protected readonly caseId = signal('');
  protected readonly records = signal<EvidenceRecord[] | null>(null);
  protected readonly busy = signal(false);
  protected readonly error = signal('');

  constructor() {
    effect(() => {
      const ref = this.initial();
      untracked(() => {
        if (!ref) return;
        this.system.set(ref.system);
        this.caseId.set(ref.caseId);
        this.search();
      });
    });
  }

  protected outcome(code: string): string {
    return label(de.outcomes, code, de.unknown);
  }

  protected search(): void {
    if (!this.system().trim() || !this.caseId().trim()) return;
    this.busy.set(true);
    this.error.set('');
    this.api.evidence(this.system().trim(), this.caseId().trim()).pipe(takeUntilDestroyed(this.destroy)).subscribe({
      next: (list) => { this.busy.set(false); this.records.set(list); },
      error: () => { this.busy.set(false); this.records.set(null); this.error.set(this.x.failed); },
    });
  }
}
