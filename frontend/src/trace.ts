import { isLosslessNumber, type LosslessNumber } from 'lossless-json';

export interface TraceValue { kind:string; truth?:string; number?:LosslessNumber }
export interface NumericTrace {
  kind:string; value:TraceValue; field:string|null;
  selectedMinimum:LosslessNumber|null; selectedMaximum:LosslessNumber|null; selectedValue:LosslessNumber|null;
  children:NumericTrace[];
}
export interface ConditionTrace {
  kind:string; result:string; field:string|null; expected:TraceValue|null; actual:TraceValue|null;
  minimum:LosslessNumber|null; maximum:LosslessNumber|null; children:ConditionTrace[];
  evidenceRequirementId?:string|null; evidenceStatus?:string|null; numericExpression?:NumericTrace|null;
}
export interface TraceSource { title:string; authority:string; version?:string; sourceLocation?:string }
export interface RuleTrace { ruleId:string; ruleVersion:unknown; source:TraceSource; conditionResult:string; outcome:string; condition:ConditionTrace }
export interface OutputTrace { outputId:string; value:{kind:string;choice?:string}; source:TraceSource; conditionResult:string; condition:ConditionTrace }
export function displayDecimal(value:unknown, absent:string):string {
  return isLosslessNumber(value) ? value.value.replace('.', ',') : absent;
}
export function displayValue(value:TraceValue|null|undefined, labels:{unknown:string;yes:string;no:string;na:string;unrecorded:string}):string {
  if (!value) return labels.unrecorded;
  if (value.kind==='UNKNOWN') return labels.unknown;
  if (value.kind==='NUMBER') return displayDecimal(value.number,labels.unrecorded);
  if (value.kind==='TRUTH') return ({YES:labels.yes,NO:labels.no,UNKNOWN:labels.unknown,NOT_APPLICABLE:labels.na} as Record<string,string>)[value.truth??'']??labels.unrecorded;
  return labels.unrecorded;
}
