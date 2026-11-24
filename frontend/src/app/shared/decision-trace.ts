import { Component, effect, input, output, signal, untracked } from '@angular/core';
import { NgTemplateOutlet } from '@angular/common';
import { de, label } from '../de';

const demo = de.trace;
import { AssessmentView, ConditionView, displayValue, NumericView, ValueView } from './assessment';
import { germanDecimal, ExactNumber } from './json';
export interface OutputLabel { label: string; choices: Record<string, string> }
/** Fundstelle einer Angabe in der Fallakte (Dokument und Seite laut Fachsystem). */
export interface SourceRef { readonly field: string; readonly documentId: string; readonly title: string; readonly page: number }
interface ShownSource { readonly ref: SourceRef; readonly title: string; readonly page: number; readonly fields: string }

/** Read-only rendering of the recorded decision trace: input → condition → rule → source → outcome. */
@Component({
  selector: 'nc-decision-trace',
  imports: [NgTemplateOutlet],
  template: `
    @let a = assessment();
    <div class="verdict" [attr.data-outcome]="tone(a.outcome)">
      <span>{{ text.result }}</span>
      <strong>{{ outcomeLabel(a.outcome) }}</strong>
    </div>
    @if (a.missingRequiredFields.length) {
      <div class="trace-block"><h3>{{ text.missingFields }}</h3>
        <ul class="tags">@for (field of a.missingRequiredFields; track field) { <li>{{ fieldLabel(field) }}</li> }</ul>
      </div>
    }
    @if (a.rule; as r) {
      <div class="trace-block">
        <div class="trace-head">
          <h3>{{ text.reasoning }}</h3>
          <span class="spacer"></span>
          <button type="button" class="link small" (click)="expandAll(true)">{{ text.expandAll }}</button>
          <button type="button" class="link small" (click)="expandAll(false)">{{ text.collapseAll }}</button>
        </div>
        <ul class="tree">
          <ng-container *ngTemplateOutlet="node; context: { $implicit: r.condition, level: 1, path: 'r' }" />
        </ul>
      </div>
      <dl class="facts compact">
        <dt>{{ text.rule }}</dt><dd><code>{{ r.ruleId }}</code> · {{ text.ruleVersion }} {{ r.ruleVersion }}</dd>
        <dt>{{ text.ruleSource }}</dt><dd>{{ r.source.title }}@if (r.source.version) { · {{ text.sourceVersion }} {{ r.source.version }} }@if (r.source.authority) { <span class="muted"> · {{ r.source.authority }}</span> }</dd>
        @if (a.knowledgeRelease) { <dt>{{ text.release }}</dt><dd><code>{{ a.knowledgeRelease }}</code></dd> }
        @if (a.assessmentDate) { <dt>{{ text.assessedOn }}</dt><dd>{{ germanDate(a.assessmentDate) }}</dd> }
      </dl>
    }
    @if (a.outputs.length) {
      <div class="trace-block"><h3>{{ text.outputs }}</h3>
        <ul class="tree output-tree">@for (o of a.outputs; track o.outputId; let i = $index) {
          @let open = isOpen(o.condition, 0);
          <li>
            <div class="cond" [attr.data-result]="o.conditionResult">
              <button type="button" class="tree-toggle" [attr.aria-expanded]="open" [attr.aria-controls]="'tc-o' + i"
                [attr.aria-label]="(open ? text.collapse : text.expand) + ': ' + outputName(o.outputId)" (click)="toggle(o.condition, 0)">
                <span class="cond-mark" aria-hidden="true">{{ mark(o.conditionResult) }}</span>
              </button>
              <span class="cond-text">{{ outputName(o.outputId) }}</span>
              <span class="cond-result"><strong>{{ outputValue(o.outputId, o.value) }}</strong></span>
            </div>
            @if (open) {
              <ul class="tree" [id]="'tc-o' + i"><ng-container *ngTemplateOutlet="node; context: { $implicit: o.condition, level: 2, path: 'o' + i }" /></ul>
            }
          </li>
        }</ul>
      </div>
    }

    <ng-template #num let-n let-depth="depth">
      @let kids = numericChildren(n);
      <li [class.numeric-total]="depth === 0">
        @if (kids.length) {
          <details [open]="depth === 0">
            <summary>{{ numericText(n) }}</summary>
            <ul>@for (k of kids; track $index) { <ng-container *ngTemplateOutlet="num; context: { $implicit: k, depth: depth + 1 }" /> }</ul>
          </details>
        } @else {
          <span>{{ numericText(n) }}</span>
        }
      </li>
    </ng-template>

    <ng-template #node let-c let-level="level" let-path="path">
      @let expandable = !!c.children.length || !!c.numeric;
      @let open = expandable && isOpen(c, level);
      <li>
        <div class="cond" [attr.data-result]="c.result">
          @if (expandable) {
            <button type="button" class="tree-toggle" [attr.aria-expanded]="open" [attr.aria-controls]="'tc-' + path"
              [attr.aria-label]="(open ? text.collapse : text.expand) + ': ' + describe(c)" (click)="toggle(c, level)">
              <span class="chevron" aria-hidden="true"></span>
            </button>
          } @else { <span class="tree-toggle-space" aria-hidden="true"></span> }
          <span class="cond-mark" aria-hidden="true">{{ mark(c.result) }}</span>
          <span class="cond-text">{{ describe(c) }}</span>
          <span class="cond-result">{{ conditionLabel(c.result) }}</span>
        </div>
        @let refs = sourcesOf(c);
        @if (refs.length) {
          <div class="cond-sources">
            <span class="muted">{{ text.from }}</span>
            @for (s of refs; track s.ref.documentId + '#' + s.page) {
              <button type="button" class="source-link" (click)="openSource.emit(s.ref)" [title]="text.openSource + ' · ' + s.fields">
                {{ s.title }}, {{ text.page }} {{ s.page }}
              </button>
            }
          </div>
        }
        @if (open) {
          <div [id]="'tc-' + path">
            @if (c.numeric && named(c.numeric)) {
              <ul class="numeric-tree"><ng-container *ngTemplateOutlet="num; context: { $implicit: c.numeric, depth: 0 }" /></ul>
            } @else if (c.numeric) {
              <ul class="numeric-list">@for (line of numericLines(c.numeric); track $index) {
                <li [class.numeric-total]="$last">{{ line }}</li>
              }</ul>
            }
            @if (c.children.length) {
              <ul>@for (child of c.children; track $index) {
                <ng-container *ngTemplateOutlet="node; context: { $implicit: child, level: level + 1, path: path + '-' + $index }" />
              }</ul>
            }
          </div>
        }
      </li>
    </ng-template>
  `,
})
export class DecisionTrace {
  readonly assessment = input.required<AssessmentView>();
  readonly fieldLabels = input<Record<string, string>>({});
  readonly evidenceLabels = input<Record<string, string>>({});
  readonly outputLabels = input<Record<string, OutputLabel>>({});
  /** Fundstellen je Feld-ID; ohne Eintrag zeigt die Begründung keinen Verweis. */
  readonly sources = input<Record<string, readonly SourceRef[]>>({});
  /** Beschriftungen benannter Zwischenwerte (Präsentation „numerics“), z. B. Module. */
  readonly numericLabels = input<Record<string, string>>({});
  readonly openSource = output<SourceRef>();
  protected readonly text = de.trace;

  /** Auf- und Zuklappen: Voreinstellung zeigt den Weg zu allem, was nicht erfüllt oder offen ist. */
  private readonly mode = signal<'default' | 'all' | 'none'>('default');
  private readonly overrides = signal(new Map<ConditionView, boolean>());

  constructor() {
    effect(() => {
      this.assessment();
      untracked(() => {
        this.mode.set('default');
        this.overrides.set(new Map());
      });
    });
  }

  protected isOpen(c: ConditionView, level: number): boolean {
    const own = this.overrides().get(c);
    if (own !== undefined) return own;
    const mode = this.mode();
    if (mode !== 'default') return mode === 'all';
    if (level === 0) return false; // Teilbewertungen: erst auf Wunsch
    return level === 1 || !!c.numeric || c.result !== 'MATCHED';
  }

  protected toggle(c: ConditionView, level: number): void {
    const next = !this.isOpen(c, level);
    this.overrides.update((m) => new Map(m).set(c, next));
  }

  protected expandAll(open: boolean): void {
    this.overrides.set(new Map());
    this.mode.set(open ? 'all' : 'none');
  }

  protected mark(result: string): string {
    return result === 'MATCHED' ? '✓' : result === 'NOT_MATCHED' ? '✕' : '?';
  }

  /** Fundstellen aller Felder dieser Bedingung, auch der Felder in einer Berechnung. */
  protected sourcesOf(c: ConditionView): ShownSource[] {
    const fields = new Set<string>();
    if (c.field) fields.add(c.field);
    const walk = (n: NumericView | null): void => {
      if (!n) return;
      if (n.field) fields.add(n.field);
      n.children.forEach(walk);
    };
    walk(c.numeric);
    // Mehrere Felder aus derselben Seite (z. B. alle Module eines Erhebungsbogens) ergeben einen Verweis.
    const all = this.sources();
    const byPage = new Map<string, { ref: SourceRef; labels: string[] }>();
    for (const f of fields) {
      for (const ref of Object.hasOwn(all, f) ? all[f] : []) {
        const key = `${ref.documentId}#${ref.page}`;
        const entry = byPage.get(key) ?? { ref, labels: [] };
        entry.labels.push(this.fieldLabel(f));
        byPage.set(key, entry);
      }
    }
    return [...byPage.values()].map(({ ref, labels }) => ({ ref, title: ref.title, page: ref.page, fields: labels.join(', ') }));
  }

  protected outcomeLabel(outcome: string | null): string { return label(de.outcomes, outcome, de.unknown); }
  protected tone(outcome: string | null): string { return outcome !== null && Object.hasOwn(de.outcomes, outcome) ? outcome : 'NONE'; }
  protected conditionLabel(result: string): string { return label(demo.conditionResults, result, de.unknown); }
  protected fieldLabel(field: string): string { return label(this.fieldLabels(), field, field); }
  protected outputName(id: string): string { return Object.hasOwn(this.outputLabels(), id) ? this.outputLabels()[id].label : id; }
  protected outputValue(id: string, value: ValueView | null): string {
    if (value?.kind === 'CHOICE' && Object.hasOwn(this.outputLabels(), id)) return label(this.outputLabels()[id].choices, value.code, value.code);
    return displayValue(value, demo.values);
  }
  protected germanDate(iso: string): string {
    // de-DE: TT.MM.JJJJ
    const match = /^(\d{4})-(\d{2})-(\d{2})$/.exec(iso);
    return match ? `${match[3]}.${match[2]}.${match[1]}` : iso;
  }

  protected describe(c: ConditionView): string {
    const kind = label(demo.conditionKinds, c.kind, c.kind);
    const value = (v: ValueView | null) => displayValue(v, demo.values);
    const num = (n: ExactNumber | null) => (n ? germanDecimal(n) : '–');
    switch (c.kind) {
      case 'field_equals':
        return `${this.fieldLabel(c.field ?? '')}: ${demo.expected} ${value(c.expected)}, ${demo.actual} ${value(c.actual)}`;
      case 'number_gte':
        // Die Schwelle steht im Nachweis als erwarteter Zahlenwert; ohne Feld stammt der Wert aus einer Berechnung.
        return `${c.field ? this.fieldLabel(c.field) : kind}: ${demo.minimum} ${c.minimum ? num(c.minimum) : value(c.expected)}, ${demo.actual} ${value(c.actual)}`;
      case 'number_in_range':
        return `${c.field ? this.fieldLabel(c.field) : kind}: ${demo.range} ${num(c.minimum)} – ${num(c.maximum)}, ${demo.actual} ${value(c.actual)}`;
      case 'requires_evidence': {
        const id = c.evidenceRequirementId ?? '';
        const status = c.evidenceStatus ? label(de.evidenceStatuses, c.evidenceStatus, de.unknown) : demo.values.unrecorded;
        return `${kind}: ${label(this.evidenceLabels(), id, id)} – ${status}`;
      }
      default:
        return c.field ? `${kind}: ${this.fieldLabel(c.field)}` : kind;
    }
  }
  /** Zerlegt eine Berechnung in lesbare Zeilen: je Bestandteil eine Zeile, zuletzt das Ergebnis. */
  protected numericLines(n: NumericView): string[] {
    const v = (node: NumericView) => displayValue(node.value, demo.values);
    const known = (node: NumericView) => node.value?.kind === 'NUMBER';
    const points = (node: NumericView) => (known(node) ? `${v(node)} ${v(node) === '1' ? 'Punkt' : 'Punkte'}` : v(node).toLowerCase());
    const weighted = (node: NumericView): string | null => {
      if (node.kind !== 'range_lookup' || node.children.length !== 1 || node.children[0].kind !== 'field') return null;
      const f = node.children[0];
      return known(f) ? `${this.fieldLabel(f.field ?? '')}: ${points(f)} → ${v(node)} gewichtet` : `${this.fieldLabel(f.field ?? '')}: ${points(f)}`;
    };
    const lines = (node: NumericView): string[] => {
      const w = weighted(node);
      if (w) return [w];
      if (node.kind === 'field') return [`${this.fieldLabel(node.field ?? '')}: ${points(node)}`];
      if (node.kind === 'max') return [...node.children.flatMap(lines), `↳ davon zählt der höhere Wert: ${v(node)}`];
      const name = label(demo.numericKinds, node.kind, node.kind);
      return [...node.children.flatMap(lines), `${name}: ${v(node)}`];
    };
    return lines(n);
  }

  /** Regelwerke mit benannten Zwischenwerten werden als aufklappbarer Rechenweg gezeigt. */
  protected named(n: NumericView): boolean {
    return !!n.id || n.children.some((c) => this.named(c));
  }

  /** Kinder im Rechenweg: Summen mit Einzelpunkten werden übersprungen, Kriterien sind Blätter. */
  protected numericChildren(n: NumericView): NumericView[] {
    if (n.kind === 'field') return [];
    if ((n.kind === 'range_lookup' || n.kind === 'scale') && n.children[0]?.kind === 'field') return [];
    const only = n.children.length === 1 ? n.children[0] : null;
    if (n.kind === 'range_lookup' && only && only.kind === 'sum') return only.children;
    return n.children;
  }

  protected numericText(n: NumericView): string {
    const fmt = (node: NumericView | undefined) => (node ? displayValue(node.value, demo.values) : '');
    const own = n.id && Object.hasOwn(this.numericLabels(), n.id) ? this.numericLabels()[n.id] : null;
    const input = n.children[0];
    if (n.kind === 'field') return `${this.fieldLabel(n.field ?? '')}: ${fmt(n)}`;
    if (n.kind === 'range_lookup' && input?.kind === 'field') {
      const raw = fmt(input);
      return raw === fmt(n) ? `${this.fieldLabel(input.field ?? '')}: ${raw}` : `${this.fieldLabel(input.field ?? '')}: ${raw} → ${fmt(n)} ${demo.pointsWord}`;
    }
    if (n.kind === 'scale') {
      const dec = (text: string) => germanDecimal(new ExactNumber(text));
      const op = n.factor ? `× ${dec(n.factor)}` : `÷ ${dec(n.divisor ?? '')}`;
      const name = input?.kind === 'field' ? this.fieldLabel(input.field ?? '') : own ?? '';
      return `${name}: ${fmt(input)} ${op} = ${fmt(n)}`;
    }
    if (n.kind === 'range_lookup') return `${own ?? label(demo.numericKinds, n.kind, n.kind)}: ${fmt(input)} → ${fmt(n)}`;
    if (n.kind === 'max') return `${own ?? demo.higherValue}: ${fmt(n)}`;
    return `${own ?? label(demo.numericKinds, n.kind, n.kind)}: ${fmt(n)}`;
  }
}