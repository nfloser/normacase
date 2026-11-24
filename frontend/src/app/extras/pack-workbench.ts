import { Component, DestroyRef, computed, effect, inject, input, signal, untracked } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { HttpErrorResponse } from '@angular/common/http';
import { catchError, of } from 'rxjs';
import { de, label } from '../de';
import { Presentation, ReleaseSummary, WorkbenchApi } from '../workbench/api';
import { DecisionTrace, OutputLabel } from '../shared/decision-trace';
import { readAssessmentDocument } from '../shared/assessment';
import { exactJson, isObject, parseExact } from '../shared/json';
import { AssessmentResponse, ExtrasApi, Verification } from './extras-api';
import { CaseForm, caseJson, emptyForm, formFromCase, invalidNumbers, saveJson } from './case-form';

const MAX_SNAPSHOT = 1024 * 1024;

/**
 * Prüfwerkstatt wie im bisherigen NormaCase: ein Regelwerk direkt ausprobieren, Beispiele laden, Ergebnis und
 * Snapshot exportieren und einen Snapshot später gegen den gespeicherten Nachweis nachprüfen.
 * Bewertet wird ausschließlich im Backend; die Oberfläche baut nur die Falleingabe.
 */
@Component({
  selector: 'nc-pack-workbench',
  imports: [DecisionTrace],
  template: `
    <div class="tool-grid">
      <section class="tool-card" [attr.aria-label]="x.facts">
        <h3>{{ x.facts }}</h3>
        <form class="tool-form" (submit)="$event.preventDefault(); check()">
          <label class="field"><span>{{ x.release }}</span>
            <select [attr.aria-label]="x.release" [value]="releaseId()" (change)="selectRelease($any($event.target).value)">
              @for (r of releases(); track r.releaseId) { <option [value]="r.releaseId" [selected]="r.releaseId === releaseId()">{{ releaseName(r.releaseId) }}</option> }
            </select>
          </label>
          <div class="field-inline">
            <label class="field grow"><span>{{ x.example }}</span>
              <select [attr.aria-label]="x.example" [value]="exampleId()" (change)="exampleId.set($any($event.target).value)">
                <option value="" [selected]="!exampleId()">{{ x.noExample }}</option>
                @for (e of presentation().examples ?? []; track e.id) { <option [value]="e.id" [selected]="e.id === exampleId()">{{ e.label }}</option> }
              </select>
            </label>
            <button type="button" class="button" [disabled]="!exampleId() || busy()" (click)="loadExample()">{{ x.loadExample }}</button>
          </div>
          <label class="field"><span>{{ x.date }}</span><input type="date" required [attr.aria-label]="x.date" [value]="form().date" (input)="patch({ date: $any($event.target).value })"></label>
          @for (f of form().fields; track f.id) {
            <div class="field">
              <span [id]="'pw-l-' + f.id">{{ fieldLabel(f.id) }} <small class="muted">{{ f.required ? x.required : x.optional }}</small></span>
              @if (f.type === 'truth') {
                <select [attr.aria-labelledby]="'pw-l-' + f.id" [value]="f.value" (change)="setField(f.id, $any($event.target).value)">
                  @for (v of truthValues; track v) { <option [value]="v" [selected]="v === f.value">{{ truth[v] }}</option> }
                </select>
              } @else {
                <div class="field-inline">
                  <select [attr.aria-label]="fieldLabel(f.id) + ' – ' + x.numberValue" [value]="f.value === 'UNKNOWN' ? 'UNKNOWN' : 'VALUE'" (change)="setField(f.id, $any($event.target).value === 'UNKNOWN' ? 'UNKNOWN' : '')">
                    <option value="UNKNOWN" [selected]="f.value === 'UNKNOWN'">{{ x.numberUnknown }}</option>
                    <option value="VALUE" [selected]="f.value !== 'UNKNOWN'">{{ x.numberValue }}</option>
                  </select>
                  @if (f.value !== 'UNKNOWN') {
                    <input class="grow" inputmode="decimal" [attr.aria-label]="fieldLabel(f.id)" [placeholder]="x.numberPlaceholder" [value]="f.value"
                      [attr.aria-invalid]="invalid().includes(f.id)" (input)="setField(f.id, $any($event.target).value)">
                  }
                </div>
              }
            </div>
          }
          @if (form().evidence.length) {
            <h4 class="tool-subhead">{{ x.evidence }}</h4>
            @for (e of form().evidence; track e.id) {
              <label class="field"><span>{{ evidenceLabel(e.id) }}</span>
                <select [attr.aria-label]="evidenceLabel(e.id)" [value]="e.value" (change)="setEvidence(e.id, $any($event.target).value)">
                  @for (s of evidenceValues; track s) { <option [value]="s" [selected]="s === e.value">{{ evidenceStatus[s] }}</option> }
                </select>
              </label>
            }
          }
          @if (error()) { <p class="banner error" role="alert">{{ error() }}</p> }
          <div class="dialog-actions">
            <button type="button" class="button ghost" (click)="reset()">{{ x.reset }}</button>
            <span class="spacer"></span>
            <button type="submit" class="button primary" [disabled]="busy() || !canAssess()">{{ busy() ? x.checking : x.check }}</button>
          </div>
        </form>
      </section>

      <section class="tool-card" aria-live="polite" [attr.aria-label]="x.resultHeading">
        <h3>{{ x.resultHeading }}</h3>
        @if (result(); as r) {
          @if (view(); as a) {
            <nc-decision-trace [assessment]="a" [fieldLabels]="fieldLabels()" [evidenceLabels]="evidenceLabels()" [outputLabels]="outputLabels()" [numericLabels]="numericLabels()" />
            <dl class="facts compact">
              <dt>{{ x.platform }}</dt><dd>{{ a.platformVersion }}</dd>
              <dt>{{ x.evidenceId }}</dt><dd><code>{{ r.response.evidenceId }}</code></dd>
              <dt>{{ x.inputHash }}</dt><dd><code class="hash">{{ r.response.inputSha256 }}</code></dd>
              <dt>{{ x.knowledgeHash }}</dt><dd><code class="hash">{{ r.response.knowledgeSha256 }}</code></dd>
            </dl>
          }
          <details class="raw"><summary>{{ x.raw }}</summary><pre>{{ r.raw }}</pre></details>
          <div class="dialog-actions">
            <button type="button" class="button" (click)="exportResult()">⤓ {{ x.exportResult }}</button>
            <button type="button" class="button" (click)="exportSnapshot()">⤓ {{ x.exportSnapshot }}</button>
          </div>
        } @else {
          <p class="tool-empty">{{ x.noResult }}</p>
        }
      </section>
    </div>

    <section class="tool-card snapshot" aria-live="polite" [attr.aria-label]="x.snapshotHeading">
      <h3>{{ x.snapshotHeading }}</h3>
      <input #file type="file" class="sr-only" accept=".json,application/json" [attr.aria-label]="x.chooseFile" (change)="verifySnapshot($any($event.target))">
      <button type="button" class="button" [disabled]="busy() || !canAssess()" (click)="file.click()">{{ x.chooseFile }}</button>
      @if (snapshotError()) { <p class="banner error" role="alert">{{ snapshotError() }}</p> }
      @if (verification(); as v) {
        <div class="verification" [attr.data-ok]="v.verified">
          <strong>{{ v.verified ? x.verified : x.notVerified }}</strong>
          <ul>
            <li [attr.data-ok]="v.inputMatches">{{ v.inputMatches ? '✓' : '✕' }} {{ x.inputMatches }}</li>
            <li [attr.data-ok]="v.knowledgeMatches">{{ v.knowledgeMatches ? '✓' : '✕' }} {{ x.knowledgeMatches }}</li>
            <li [attr.data-ok]="v.outcomeMatches">{{ v.outcomeMatches ? '✓' : '✕' }} {{ x.outcomeMatches }}: {{ outcome(v.recordedOutcome) }} ({{ x.recorded }}) · {{ outcome(v.replayedOutcome) }} ({{ x.replayed }})</li>
          </ul>
        </div>
      }
    </section>
  `,
})
export class PackWorkbench {
  readonly releases = input.required<ReleaseSummary[]>();
  readonly canAssess = input(true);
  /** Vorauswahl aus den Referenzfällen. */
  readonly request = input<{ releaseId: string; exampleId: string } | null>(null);

  protected readonly x = de.extras;
  protected readonly truth = de.caseFile.truth;
  protected readonly evidenceStatus = de.evidenceStatuses;
  protected readonly truthValues = ['UNKNOWN', 'YES', 'NO', 'NOT_APPLICABLE'];
  protected readonly evidenceValues = ['MISSING', 'PRESENT', 'CONFLICTING'];
  private readonly api = inject(ExtrasApi);
  private readonly workbench = inject(WorkbenchApi);
  private readonly destroy = inject(DestroyRef);

  protected readonly releaseId = signal('');
  protected readonly exampleId = signal('');
  protected readonly presentation = signal<Presentation>({});
  private readonly names = signal<ReadonlyMap<string, string>>(new Map());
  protected readonly form = signal<CaseForm>({ date: '', fields: [], evidence: [] });
  protected readonly invalid = signal<string[]>([]);
  protected readonly busy = signal(false);
  protected readonly error = signal('');
  protected readonly result = signal<{ response: AssessmentResponse; raw: string; caseJson: string } | null>(null);
  protected readonly snapshotError = signal('');
  protected readonly verification = signal<Verification | null>(null);

  protected readonly view = computed(() => readAssessmentDocument(this.result()?.response.document) ?? null);
  protected readonly fieldLabels = computed(() => labels(this.presentation().fields));
  protected readonly evidenceLabels = computed(() => labels(this.presentation().evidenceRequirements));
  protected readonly outputLabels = computed(() => (this.presentation().outputs ?? {}) as Record<string, OutputLabel>);
  protected readonly numericLabels = computed(() => this.presentation().numerics ?? {});

  constructor() {
    effect(() => {
      const list = this.releases();
      const wanted = this.request();
      untracked(() => {
        for (const r of list) this.loadName(r.releaseId);
        if (wanted && list.some((r) => r.releaseId === wanted.releaseId)) {
          this.selectRelease(wanted.releaseId, wanted.exampleId);
        } else if (!this.releaseId() && list.length) {
          this.selectRelease(preferred(list));
        }
      });
    });
  }

  protected releaseName(id: string): string {
    return this.names().get(id) ?? id;
  }

  protected fieldLabel(id: string): string {
    return label(this.fieldLabels(), id, id);
  }

  protected evidenceLabel(id: string): string {
    return label(this.evidenceLabels(), id, id);
  }

  protected outcome(code: string): string {
    return label(de.outcomes, code, de.unknown);
  }

  protected selectRelease(id: string, exampleId = ''): void {
    this.releaseId.set(id);
    this.exampleId.set(exampleId);
    this.clear();
    this.form.set(emptyForm(this.release()));
    this.workbench.presentation(id).pipe(catchError(() => of({} as Presentation)), takeUntilDestroyed(this.destroy)).subscribe((p) => {
      this.presentation.set(p);
      if (exampleId) this.loadExample();
    });
  }

  protected loadExample(): void {
    const id = this.exampleId();
    if (!id) return;
    this.clear();
    this.busy.set(true);
    this.api.example(this.releaseId(), id).pipe(takeUntilDestroyed(this.destroy)).subscribe({
      next: (text) => { this.busy.set(false); this.form.set(formFromCase(this.release(), parseExact(text))); },
      error: () => { this.busy.set(false); this.error.set(this.x.failed); },
    });
  }

  protected patch(change: Partial<CaseForm>): void {
    this.clear();
    this.form.update((f) => ({ ...f, ...change }));
  }

  protected setField(id: string, value: string): void {
    this.patch({ fields: this.form().fields.map((f) => (f.id === id ? { ...f, value } : f)) });
    this.invalid.update((list) => list.filter((x) => x !== id));
  }

  protected setEvidence(id: string, value: string): void {
    this.patch({ evidence: this.form().evidence.map((e) => (e.id === id ? { ...e, value } : e)) });
  }

  protected reset(): void {
    this.exampleId.set('');
    this.clear();
    this.invalid.set([]);
    this.form.set(emptyForm(this.release()));
  }

  protected check(): void {
    const form = this.form();
    this.clear();
    if (!form.date) { this.error.set(this.x.invalidDate); return; }
    const invalid = invalidNumbers(form);
    this.invalid.set(invalid);
    if (invalid.length) { this.error.set(this.x.invalidNumber); return; }
    const json = caseJson(form);
    this.busy.set(true);
    this.api.assess(this.releaseId(), json).pipe(takeUntilDestroyed(this.destroy)).subscribe({
      next: (response) => {
        this.busy.set(false);
        this.result.set({ response, raw: exactJson(response.document, 2), caseJson: json });
      },
      error: (e: unknown) => { this.busy.set(false); this.error.set(problem(e, this.x.failed)); },
    });
  }

  protected exportResult(): void {
    const r = this.result();
    if (r) saveJson(exactJson(r.response.document, 2), 'normacase-bewertung.json');
  }

  /** Snapshot: Eingabe plus Nachweis-Kennung und Prüfsummen – genug, um die Bewertung später nachzuprüfen. */
  protected exportSnapshot(): void {
    const r = this.result();
    if (!r) return;
    const head = {
      format: 'normacase-snapshot', version: 1, knowledgeRelease: this.releaseId(), evidenceId: r.response.evidenceId,
      inputSha256: r.response.inputSha256, knowledgeSha256: r.response.knowledgeSha256, outcome: this.view()?.outcome ?? null,
    };
    const text = JSON.stringify(head, null, 2).replace(/\n}$/, `,\n  "case": ${exactJson(parseExact(r.caseJson), 2).replace(/\n/g, '\n  ')}\n}`);
    saveJson(text, `normacase-snapshot-${r.response.evidenceId.slice(0, 8)}.json`);
  }

  protected verifySnapshot(input: HTMLInputElement): void {
    const file = input.files?.[0];
    input.value = '';
    if (!file) return;
    this.snapshotError.set('');
    this.verification.set(null);
    if (file.size > MAX_SNAPSHOT) { this.snapshotError.set(this.x.snapshotTooLarge); return; }
    this.busy.set(true);
    file.arrayBuffer().then((bytes) => {
      let snapshot: unknown;
      try { snapshot = parseExact(new TextDecoder('utf-8', { fatal: true }).decode(bytes)); } catch { snapshot = null; }
      if (!isObject(snapshot) || snapshot['format'] !== 'normacase-snapshot' || typeof snapshot['evidenceId'] !== 'string' || !isObject(snapshot['case'])) {
        this.busy.set(false);
        this.snapshotError.set(this.x.snapshotInvalid);
        return;
      }
      this.api.verify(snapshot['evidenceId'], exactJson(snapshot['case'])).pipe(takeUntilDestroyed(this.destroy)).subscribe({
        next: (v) => { this.busy.set(false); this.verification.set(v); },
        error: (e: unknown) => { this.busy.set(false); this.snapshotError.set(problem(e, this.x.failed)); },
      });
    }, () => { this.busy.set(false); this.snapshotError.set(this.x.snapshotInvalid); });
  }

  private release(): ReleaseSummary | undefined {
    return this.releases().find((r) => r.releaseId === this.releaseId());
  }

  private clear(): void {
    this.error.set('');
    this.result.set(null);
  }

  private loadName(id: string): void {
    if (this.names().has(id)) return;
    this.workbench.presentation(id).pipe(catchError(() => of({} as Presentation)), takeUntilDestroyed(this.destroy))
      .subscribe((p) => this.names.update((m) => new Map(m).set(id, p.name ?? id)));
  }
}

/** Startet mit dem ersten Regelwerk, das Beispiele hat (demo-g bevorzugt). */
function preferred(list: readonly ReleaseSummary[]): string {
  return (list.find((r) => r.releaseId.startsWith('demo-g')) ?? list[0]).releaseId;
}

function labels(source: Record<string, { label: string }> | undefined): Record<string, string> {
  return Object.fromEntries(Object.entries(source ?? {}).map(([k, v]) => [k, v.label]));
}

function problem(e: unknown, fallback: string): string {
  return e instanceof HttpErrorResponse && typeof e.error?.detail === 'string' ? e.error.detail : fallback;
}

