import { Component, input } from '@angular/core';
import { NgTemplateOutlet } from '@angular/common';
import { de, label } from '../de';

const demo = de.trace;
import { AssessmentView, ConditionView, displayValue, NumericView, ValueView } from './assessment';
import { germanDecimal, ExactNumber } from './json';
export interface OutputLabel { label: string; choices: Record<string, string> }

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
      <div class="trace-block"><h3>{{ text.reasoning }}</h3>
        <ul class="tree" role="tree" [attr.aria-label]="text.reasoning">
          <ng-container *ngTemplateOutlet="node; context: { $implicit: r.condition, level: 1 }" />
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
        <ul class="output-list">@for (o of a.outputs; track o.outputId) {
          <li class="cond" [attr.data-result]="o.conditionResult">
            <span class="cond-mark" aria-hidden="true">{{ o.conditionResult === 'MATCHED' ? '✓' : o.conditionResult === 'NOT_MATCHED' ? '✕' : '?' }}</span>
            <span class="cond-text">{{ outputName(o.outputId) }}</span>
            <span class="cond-result"><strong>{{ outputValue(o.outputId, o.value) }}</strong></span>
          </li>
        }</ul>
      </div>
    }

    <ng-template #node let-c let-level="level">
      <li role="treeitem" aria-selected="false" [attr.aria-level]="level" [attr.aria-expanded]="c.children.length ? true : null">
        <div class="cond" [attr.data-result]="c.result">
          <span class="cond-mark" aria-hidden="true">{{ c.result === 'MATCHED' ? '✓' : c.result === 'NOT_MATCHED' ? '✕' : '?' }}</span>
          <span class="cond-text">{{ describe(c) }}</span>
          <span class="cond-result">{{ conditionLabel(c.result) }}</span>
        </div>
        @if (c.numeric) {
          <ul class="numeric-list">@for (line of numericLines(c.numeric); track $index) {
            <li [class.numeric-total]="$last">{{ line }}</li>
          }</ul>
        }
        @if (c.children.length) {
          <ul role="group">@for (child of c.children; track $index) {
            <ng-container *ngTemplateOutlet="node; context: { $implicit: child, level: level + 1 }" />
          }</ul>
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
  protected readonly text = de.trace;

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
}
