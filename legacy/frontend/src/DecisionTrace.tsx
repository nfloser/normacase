import de from './de.json';
import type { Presentation } from './model';
import { displayDecimal, displayValue, type ConditionTrace, type NumericTrace, type RuleTrace, type OutputTrace, type TraceSource } from './trace';
const text=de.decisionTrace;
const valueLabels={...de,unrecorded:text.unrecorded};
const conditionResult=(result:string)=>(text.results as Record<string,string>)[result]??text.unrecorded;
function Source({source}:{source:TraceSource}) {
  return <p className="trace-source">{de.source}: {source.title} · {source.version??text.unrecorded}<br/>{source.authority}<br/>{de.sourceLocation}: {source.sourceLocation??text.unrecorded}</p>;
}
function Numeric({trace,presentation}:{trace:NumericTrace;presentation?:Presentation}) {
  return <li><strong>{(text.expressions as Record<string,string>)[trace.kind]??text.unrecorded}</strong>
    {trace.field&&<p>{presentation?.fields[trace.field]??de.fieldReference}</p>}
    <p>{text.calculatedValue}: {displayValue(trace.value,valueLabels)}</p>
    {trace.selectedValue!=null&&<dl><dt>{text.selectedRange}</dt><dd>{displayDecimal(trace.selectedMinimum,text.unrecorded)} – {displayDecimal(trace.selectedMaximum,text.unrecorded)}</dd><dt>{text.selectedValue}</dt><dd>{displayDecimal(trace.selectedValue,text.unrecorded)}</dd></dl>}
    {!!trace.children.length&&<ol>{trace.children.map((child,index)=><Numeric key={index} trace={child} presentation={presentation}/>)}</ol>}
  </li>;
}
function Condition({trace,presentation}:{trace:ConditionTrace;presentation?:Presentation}) {
  const evidenceStatuses:Record<string,string>={MISSING:de.missing,PRESENT:de.present,CONFLICTING:de.conflicting};
  return <li><strong>{(text.conditions as Record<string,string>)[trace.kind]??text.unrecorded}</strong>
    <p className="trace-result">{text.conditionResult}: {conditionResult(trace.result)}</p>
    {trace.field&&<p>{presentation?.fields[trace.field]??de.fieldReference}</p>}
    <dl>
      {trace.actual!=null&&<><dt>{text.actual}</dt><dd>{displayValue(trace.actual,valueLabels)}</dd></>}
      {trace.expected!=null&&<><dt>{text.expected}</dt><dd>{displayValue(trace.expected,valueLabels)}</dd></>}
      {trace.minimum!=null&&<><dt>{text.minimum}</dt><dd>{displayDecimal(trace.minimum,text.unrecorded)}</dd></>}
      {trace.maximum!=null&&<><dt>{text.maximum}</dt><dd>{displayDecimal(trace.maximum,text.unrecorded)}</dd></>}
      {trace.evidenceRequirementId&&<><dt>{presentation?.evidence[trace.evidenceRequirementId]??de.evidenceReference}</dt><dd>{evidenceStatuses[trace.evidenceStatus??'']??text.unrecorded}</dd></>}
    </dl>
    {trace.numericExpression&&<div className="numeric-trace"><h5>{text.calculation}</h5><ol><Numeric trace={trace.numericExpression} presentation={presentation}/></ol></div>}
    {!!trace.children.length&&<ol>{trace.children.map((child,index)=><Condition key={index} trace={child} presentation={presentation}/>)}</ol>}
  </li>;
}
export function DecisionTrace({rule,outputs,presentation}:{rule?:RuleTrace;outputs?:OutputTrace[];presentation?:Presentation}) {
  if (!rule&&!outputs?.length) return null;
  return <details className="decision-trace"><summary>{text.heading}</summary><p>{text.help}</p>
    {rule&&<section><h4>{text.entry}</h4><p>{de.rule}: {rule.ruleId}</p><p>{text.conditionResult}: {conditionResult(rule.conditionResult)}</p><ol><Condition trace={rule.condition} presentation={presentation}/></ol><Source source={rule.source}/></section>}
    {outputs?.map(output=><section key={output.outputId}><h4>{text.output}: {presentation?.outputs?.[output.outputId]?.label??de.outputReference}</h4><p>{text.conditionResult}: {conditionResult(output.conditionResult)}</p><ol><Condition trace={output.condition} presentation={presentation}/></ol><Source source={output.source}/></section>)}
  </details>;
}
