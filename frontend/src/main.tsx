import React, { useEffect, useRef, useState } from 'react';
import { createRoot } from 'react-dom/client';
import { parse } from 'lossless-json';
import de from './de.json';
import { exampleValues, normalizeCatalog, requestJson, type Pack } from './model';
import './style.css';

type Result = {platformVersion:string; assessment:{outcome:string; assessmentDate:string; knowledgeRelease:string; missingRequiredFields:string[]; ruleTrace?:{ruleId:string; ruleVersion:unknown; source:{title:string; authority:string; version?:string; sourceLocation?:string}}}};
const outcomes: Record<string,string> = {SUPPORTED:de.supported,NOT_SUPPORTED:de.notSupported,INCOMPLETE:de.incomplete,HUMAN_REVIEW:de.review,NOT_APPLICABLE:de.na};

function App() {
  const [packs,setPacks]=useState<Pack[]>([]);
  const [selected,setSelected]=useState('');
  const [date,setDate]=useState('');
  const [values,setValues]=useState<Record<string,string>>({});
  const [evidence,setEvidence]=useState<Record<string,string>>({});
  const [example,setExample]=useState('');
  const [result,setResult]=useState<Result|null>(null);
  const [raw,setRaw]=useState('');
  const [error,setError]=useState('');
  const [busy,setBusy]=useState(false);
  const pending=useRef<AbortController|null>(null);
  const pack=packs.find(item=>item.packId===selected);
  useEffect(()=>{
    const controller=new AbortController();
    fetch('/api/packs',{signal:controller.signal}).then(response=>{if(!response.ok)throw new Error();return response.json();})
      .then(data=>{const catalog=normalizeCatalog(data);setPacks(catalog);setSelected(catalog[0]?.packId??'');})
      .catch(()=>{if(!controller.signal.aborted)setError(de.networkError);});
    return()=>{controller.abort();pending.current?.abort();};
  },[]);
  function clear() { pending.current?.abort();setBusy(false);setResult(null);setRaw('');setError(''); }
  function changePack(id:string) {clear();setSelected(id);setValues({});setEvidence({});setExample('');setDate('');}
  async function loadExample() {
    clear();if(!example)return;
    const controller=new AbortController();pending.current=controller;setBusy(true);
    try {
      const response=await fetch('/api/packs/'+encodeURIComponent(selected)+'/examples/'+encodeURIComponent(example),{signal:controller.signal});
      if(!response.ok)throw new Error();
      const next=exampleValues(await response.text());
      if(!controller.signal.aborted){setDate(next.date);setValues(next.values);setEvidence(next.evidence);}
    } catch {if(!controller.signal.aborted)setError(de.networkError);}
    finally {if(!controller.signal.aborted)setBusy(false);}
  }
  async function evaluate(event:React.FormEvent) {
    event.preventDefault();clear();if(!pack)return;
    let body:string;
    try {body=requestJson(pack,date,values,evidence);}
    catch(exception){setError(exception instanceof Error && exception.message==='missing_date'?de.dateError:de.numberError);return;}
    const controller=new AbortController();pending.current=controller;setBusy(true);
    try {
      const response=await fetch('/api/assessments/'+encodeURIComponent(pack.packId),{method:'POST',headers:{'Content-Type':'application/json'},body,signal:controller.signal});
      const text=await response.text();
      if(!response.ok){setError((JSON.parse(text) as {message?:string}).message??de.inputError);return;}
      if(!controller.signal.aborted){setRaw(text);setResult(parse(text) as Result);}
    } catch {if(!controller.signal.aborted)setError(de.networkError);}
    finally {if(!controller.signal.aborted)setBusy(false);}
  }
  function download() {
    const url=URL.createObjectURL(new Blob([raw],{type:'application/json'}));
    const link=document.createElement('a');link.href=url;link.download='normacase-assessment.json';link.click();
    setTimeout(()=>URL.revokeObjectURL(url),1000);
  }
  const status=result?.assessment.outcome??'';
  const title=pack?.presentation?.title??selected;
  return <><header className="top"><a className="brand" href="/"><span className="brandmark">N</span>{de.app}<span className="brand-divider">/</span><span className="sub">{de.subtitle}</span></a><span className="local"><span/>{de.local}</span></header>
    <main><div className="intro"><p className="eyebrow">{de.kicker}</p><h1>{de.hero}</h1><p>{de.intro}</p><div className="notice">{de.notice}</div></div>
    <div className="workspace"><section className="card inputs"><div className="section-head"><span className="step">01</span><div><h2>{de.pack}</h2><p>{pack?.presentation?.description??de.loading}</p></div></div>
      <form onSubmit={evaluate}>
        <label className="field">{de.pack}<select value={selected} onChange={event=>changePack(event.target.value)}>{packs.map(item=><option key={item.packId} value={item.packId}>{item.presentation?.title??item.packId}</option>)}</select></label>
        <div className="example"><label className="field">{de.example}<select value={example} onChange={event=>{clear();setExample(event.target.value);}}><option value="">{de.emptyExample}</option>{pack?.presentation?.examples.map(item=><option key={item.file} value={item.file}>{item.label}</option>)}</select></label><button type="button" className="secondary" onClick={loadExample} disabled={!example||busy}>{de.loadExample}</button></div>
        <label className="field">{de.date}<input type="date" value={date} required onChange={event=>{clear();setDate(event.target.value);}}/></label>
        <h3>{de.facts}</h3><div className="fields">{pack?.fields.map(field=><label className="field" key={field.id}><span>{pack.presentation?.fields[field.id]??de.fieldReference}<small>{field.required?de.required:de.optional}</small></span>{field.type==='truth'?<select value={values[field.id]??'UNKNOWN'} onChange={event=>{clear();setValues({...values,[field.id]:event.target.value});}}><option value="UNKNOWN">{de.unknown}</option><option value="YES">{de.yes}</option><option value="NO">{de.no}</option><option value="NOT_APPLICABLE">{de.na}</option></select>:<input type="text" inputMode="decimal" placeholder={de.emptyNumber} value={values[field.id]??''} onChange={event=>{clear();setValues({...values,[field.id]:event.target.value});}}/>}</label>)}</div>
        {!!pack?.evidenceRequirements.length&&<><h3>{de.evidence}</h3>{pack.evidenceRequirements.map(id=><label className="field" key={id}>{pack.presentation?.evidence[id]??de.evidenceReference}<select value={evidence[id]??'MISSING'} onChange={event=>{clear();setEvidence({...evidence,[id]:event.target.value});}}><option value="MISSING">{de.missing}</option><option value="PRESENT">{de.present}</option><option value="CONFLICTING">{de.conflicting}</option></select></label>)}</>}
        {error&&<div className="error" role="alert"><strong>{de.errorHeading}</strong><p>{error}</p></div>}
        <div className="actions"><button className="primary" disabled={busy||!pack}>{busy?de.checking:de.check}</button><button type="button" className="text-button" onClick={()=>{clear();setValues({});setEvidence({});setExample('');setDate('');}}>{de.reset}</button></div>
      </form>
    </section><section className="card result" aria-live="polite"><div className="section-head"><span className="step">02</span><div><h2>{de.result}</h2><p>{title}</p></div></div>
      {!result?<div className="empty"><div className="empty-symbol">✓</div><h3>{de.noResult}</h3><p>{de.noResultText}</p></div>:<><div className={'outcome '+status.toLowerCase()}><span className="eyebrow">{de.result}</span><h3>{outcomes[status]??de.unknown}</h3></div>
        <dl><dt>{de.date}</dt><dd>{result.assessment.assessmentDate.split('-').reverse().join('.')}</dd><dt>{de.release}</dt><dd>{result.assessment.knowledgeRelease}</dd><dt>{de.platform}</dt><dd>{result.platformVersion}</dd></dl>
        {!!result.assessment.missingRequiredFields.length&&<div className="missing"><h4>{de.missingFields}</h4><ul>{result.assessment.missingRequiredFields.map(id=><li key={id}>{pack?.presentation?.fields[id]??de.fieldReference}</li>)}</ul></div>}
        {result.assessment.ruleTrace?<div className="source"><h4>{de.source}</h4><p>{result.assessment.ruleTrace.source.title}</p><span>{result.assessment.ruleTrace.source.authority}</span><dl><dt>{de.sourceRevision}</dt><dd>{result.assessment.ruleTrace.source.version??'—'}</dd><dt>{de.rule}</dt><dd>{result.assessment.ruleTrace.ruleId}</dd><dt>{de.sourceLocation}</dt><dd>{result.assessment.ruleTrace.source.sourceLocation??'—'}</dd></dl></div>:<p>{de.noSource}</p>}
        <details><summary>{de.trace}</summary><pre>{raw}</pre></details><button className="secondary export" onClick={download}>{de.export}</button>
      </>}
    </section></div><footer>{de.foot}</footer></main></>;
}
createRoot(document.getElementById('root')!).render(<React.StrictMode><App/></React.StrictMode>);
