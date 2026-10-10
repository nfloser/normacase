import React, { useEffect, useRef, useState } from 'react';
import { createRoot } from 'react-dom/client';
import { parse } from 'lossless-json';
import de from './de.json';
import { exampleValues, normalizeCatalog, requestJson, type Pack } from './model';
import './style.css';
import { ReferenceDocumentCases } from './DocumentCaseFile';
import {CaseExplorer} from './CaseExplorer';
import {WorkspaceMenu} from './WorkspaceMenu';
import { CaseWorkQueues } from './CaseWorkQueues';
import { ReviewedCaseWorkQueues } from './ReviewedCaseWorkQueues';
import { WorkflowWorkbench } from './WorkflowWorkbench';
import { DecisionTrace } from './DecisionTrace';
import type { RuleTrace, OutputTrace } from './trace';

type Result = {platformVersion:string; assessment:{outcome:string; assessmentDate:string; knowledgeRelease:string; missingRequiredFields:string[]; domainOutputs?:OutputTrace[]; ruleTrace?:RuleTrace}};
const outcomes: Record<string,string> = {SUPPORTED:de.supported,NOT_SUPPORTED:de.notSupported,INCOMPLETE:de.incomplete,HUMAN_REVIEW:de.review,NOT_APPLICABLE:de.na};

function App() {
  const views=['work-queues','reference-cases','workbench','review','workflow','volume-cases'];
  const readView=()=>views.includes(window.location.hash.slice(1))?window.location.hash.slice(1):'work-queues';
  const [view,setView]=useState(readView);
  useEffect(()=>{const changed=()=>setView(readView());window.addEventListener('hashchange',changed);return()=>window.removeEventListener('hashchange',changed);},[]);
  const [packs,setPacks]=useState<Pack[]>([]);
  const [selected,setSelected]=useState('');
  const [date,setDate]=useState('');
  const [values,setValues]=useState<Record<string,string>>({});
  const [evidence,setEvidence]=useState<Record<string,string>>({});
  const [example,setExample]=useState('');
  const [result,setResult]=useState<Result|null>(null);
  const [raw,setRaw]=useState('');
  const [snapshot,setSnapshot]=useState('');
  const [replayed,setReplayed]=useState<Result|null>(null);
  const [replayRaw,setReplayRaw]=useState('');
  const [replayError,setReplayError]=useState('');
  const [error,setError]=useState('');
  const [busy,setBusy]=useState(false);
  const pending=useRef<AbortController|null>(null);
  const snapshotFile=useRef<HTMLInputElement|null>(null);
  const pack=packs.find(item=>item.packId===selected);
  useEffect(()=>{
    const controller=new AbortController();
    fetch('/api/packs',{signal:controller.signal}).then(response=>{if(!response.ok)throw new Error();return response.json();})
      .then(data=>{const catalog=normalizeCatalog(data);setPacks(catalog);setSelected(catalog[0]?.packId??'');})
      .catch(()=>{if(!controller.signal.aborted)setError(de.networkError);});
    return()=>{controller.abort();pending.current?.abort();};
  },[]);
  function clear() { pending.current?.abort();setBusy(false);setResult(null);setRaw('');setSnapshot('');setReplayed(null);setReplayRaw('');setReplayError('');setError(''); }
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
      const response=await fetch('/api/snapshots/'+encodeURIComponent(pack.packId),{method:'POST',headers:{'Content-Type':'application/json'},body,signal:controller.signal});
      const text=await response.text();
      if(controller.signal.aborted)return;
      if(!response.ok){setError((JSON.parse(text) as {message?:string}).message??de.inputError);return;}
      const captured=JSON.parse(text) as {snapshotJson:string;assessmentJson:string};
      if(typeof captured.snapshotJson!=='string'||typeof captured.assessmentJson!=='string')throw new Error();
      if(!controller.signal.aborted){setSnapshot(captured.snapshotJson);setRaw(captured.assessmentJson);setResult(parse(captured.assessmentJson) as Result);}
    } catch {if(!controller.signal.aborted)setError(de.networkError);}
    finally {if(!controller.signal.aborted)setBusy(false);}
  }
  function saveFile(content:string,name:string) {
    const url=URL.createObjectURL(new Blob([content],{type:'application/json'}));
    const link=document.createElement('a');link.href=url;link.download=name;link.click();
    setTimeout(()=>URL.revokeObjectURL(url),1000);
  }
  function download() {saveFile(raw,'normacase-assessment.json');}
  async function verifySnapshot(event:React.ChangeEvent<HTMLInputElement>) {
    const file=event.currentTarget.files?.[0];
    event.currentTarget.value='';
    if(!file)return;
    clear();
    if(file.size>1024*1024){setReplayError(de.snapshotTooLarge);return;}
    const controller=new AbortController();pending.current=controller;setBusy(true);
    try {
      const bytes=await file.arrayBuffer();
      if(controller.signal.aborted)return;
      let body:string;
      try {body=new TextDecoder('utf-8',{fatal:true}).decode(bytes);}
      catch {setReplayError(de.snapshotEncodingError);return;}
      const response=await fetch('/api/snapshots/replay',{method:'POST',headers:{'Content-Type':'application/json'},body,signal:controller.signal});
      const text=await response.text();
      if(controller.signal.aborted)return;
      if(!response.ok){setReplayError((JSON.parse(text) as {message?:string}).message??de.inputError);return;}
      const verified=JSON.parse(text) as {assessmentJson:string};
      if(typeof verified.assessmentJson!=='string')throw new Error();
      setReplayRaw(verified.assessmentJson);setReplayed(parse(verified.assessmentJson) as Result);
    } catch {if(!controller.signal.aborted)setReplayError(de.networkError);}
    finally {if(!controller.signal.aborted)setBusy(false);}
  }
  const status=result?.assessment.outcome??'';
  const title=pack?.presentation?.title??selected;
  return <><a className="skip-link" href="#main-content">{de.documents.skip}</a><main id="main-content" tabIndex={-1} className="application-workspace"><h1 className="visually-hidden">{de.app}</h1><WorkspaceMenu view={view}/><div className="workspace-view" hidden={view!=='work-queues'}><CaseExplorer/></div><div className="workspace-view" hidden={view!=='volume-cases'}><CaseWorkQueues packs={packs}/></div>
    <div className="workspace-view" hidden={view!=='reference-cases'}><ReferenceDocumentCases/></div>
    <div className="workspace-view tool-view" hidden={view!=='workbench'}><div id="workbench" className="workspace"><section className="card inputs"><div className="section-head"><span className="step">01</span><div><h2>{de.pack}</h2><p>{pack?.presentation?.description??de.loading}</p></div></div>
      <form onSubmit={evaluate}>
        <label className="field">{de.pack}<select aria-label={de.pack} value={selected} onChange={event=>changePack(event.target.value)}>{packs.map(item=><option key={item.packId} value={item.packId}>{item.presentation?.title??item.packId}</option>)}</select></label>
        <div className="example"><label className="field">{de.example}<select value={example} onChange={event=>{clear();setExample(event.target.value);}}><option value="">{de.emptyExample}</option>{pack?.presentation?.examples.map(item=><option key={item.file} value={item.file}>{item.label}</option>)}</select></label><button type="button" className="secondary" onClick={loadExample} disabled={!example||busy}>{de.loadExample}</button></div>
        <label className="field">{de.date}<input type="date" value={date} required onChange={event=>{clear();setDate(event.target.value);}}/></label>
        <h3>{de.facts}</h3><div className="fields">{pack?.fields.map(field=>{
          const label=pack.presentation?.fields[field.id]??de.fieldReference;
          const current=values[field.id]??'UNKNOWN';
          return <div className="field" key={field.id}>
            <span id={'field-'+field.id+'-label'}>{label}<small>{field.required?de.required:de.optional}</small></span>
            {field.type==='truth'
              ? <select aria-labelledby={'field-'+field.id+'-label'} value={current} onChange={event=>{clear();setValues({...values,[field.id]:event.target.value});}}>
                  <option value="UNKNOWN">{de.unknown}</option><option value="YES">{de.yes}</option><option value="NO">{de.no}</option><option value="NOT_APPLICABLE">{de.na}</option>
                </select>
              : <div className="numeric-input">
                  <select aria-label={label+' – '+de.numberStatus} value={current==='UNKNOWN'?'UNKNOWN':'VALUE'} onChange={event=>{clear();setValues({...values,[field.id]:event.target.value==='UNKNOWN'?'UNKNOWN':''});}}>
                    <option value="UNKNOWN">{de.unknown}</option><option value="VALUE">{de.numberValue}</option>
                  </select>
                  {current!=='UNKNOWN'?<input aria-label={label} type="text" inputMode="decimal" placeholder={de.emptyNumber} value={current} onChange={event=>{clear();setValues({...values,[field.id]:event.target.value});}}/>:null}
                </div>}
          </div>;
        })}</div>
        {!!pack?.evidenceRequirements.length&&<><h3>{de.evidence}</h3>{pack.evidenceRequirements.map(id=><label className="field" key={id}>{pack.presentation?.evidence[id]??de.evidenceReference}<select value={evidence[id]??'MISSING'} onChange={event=>{clear();setEvidence({...evidence,[id]:event.target.value});}}><option value="MISSING">{de.missing}</option><option value="PRESENT">{de.present}</option><option value="CONFLICTING">{de.conflicting}</option></select></label>)}</>}
        {error&&<div className="error" role="alert"><strong>{de.errorHeading}</strong><p>{error}</p></div>}
        <div className="actions"><button className="primary" disabled={busy||!pack}>{busy?de.checking:de.check}</button><button type="button" className="text-button" onClick={()=>{clear();setValues({});setEvidence({});setExample('');setDate('');}}>{de.reset}</button></div>
      </form>
    </section><section className="card result" aria-live="polite"><div className="section-head"><span className="step">02</span><div><h2>{de.result}</h2><p>{title}</p></div></div>
      {!result?<div className="empty"><div className="empty-symbol">✓</div><h3>{de.noResult}</h3><p>{de.noResultText}</p></div>:<><div className={'outcome '+status.toLowerCase()}><span className="eyebrow">{de.result}</span><h3>{outcomes[status]??de.unknown}</h3></div>
        <dl><dt>{de.date}</dt><dd>{result.assessment.assessmentDate.split('-').reverse().join('.')}</dd><dt>{de.release}</dt><dd>{result.assessment.knowledgeRelease}</dd><dt>{de.platform}</dt><dd>{result.platformVersion}</dd></dl>
        {!!result.assessment.missingRequiredFields.length&&<div className="missing"><h4>{de.missingFields}</h4><ul>{result.assessment.missingRequiredFields.map(id=><li key={id}>{pack?.presentation?.fields[id]??de.fieldReference}</li>)}</ul></div>}
        {!!result.assessment.domainOutputs?.length&&<div className="domain-outputs"><h4>{de.domainOutputs}</h4><dl>{result.assessment.domainOutputs.map(output=><React.Fragment key={output.outputId}><dt>{pack?.presentation?.outputs?.[output.outputId]?.label??de.outputReference}</dt><dd>{output.value.kind==='UNKNOWN'?de.unknown:(pack?.presentation?.outputs?.[output.outputId]?.choices[output.value.choice??'']??de.unknown)}<small>{output.source.title} · {output.source.version??'—'}</small></dd></React.Fragment>)}</dl></div>}
        {result.assessment.ruleTrace?<div className="source"><h4>{de.source}</h4><p>{result.assessment.ruleTrace.source.title}</p><span>{result.assessment.ruleTrace.source.authority}</span><dl><dt>{de.sourceRevision}</dt><dd>{result.assessment.ruleTrace.source.version??'—'}</dd><dt>{de.rule}</dt><dd>{result.assessment.ruleTrace.ruleId}</dd><dt>{de.sourceLocation}</dt><dd>{result.assessment.ruleTrace.source.sourceLocation??'—'}</dd></dl></div>:<p>{de.noSource}</p>}
        <DecisionTrace rule={result.assessment.ruleTrace} outputs={result.assessment.domainOutputs} presentation={pack?.presentation}/><details><summary>{de.trace}</summary><pre>{raw}</pre></details><button className="secondary export" onClick={download}>{de.export}</button><button className="secondary export" onClick={()=>saveFile(snapshot,'normacase-snapshot.json')} disabled={!snapshot}>{de.snapshotExport}</button>
      </>}
    </section></div>
    <section className="card snapshot-tools" aria-live="polite">
      <h2>{de.snapshotHeading}</h2><p>{de.snapshotHelp}</p>
      <div className="field"><span>{de.snapshotSelect}</span>
        <input ref={snapshotFile} aria-label={de.snapshotSelect} type="file" hidden accept=".json,application/json" onChange={verifySnapshot} disabled={busy}/>
        <button type="button" className="secondary file-button" onClick={()=>snapshotFile.current?.click()} disabled={busy}>{de.snapshotChooseFile}</button>
      </div>
      {busy&&<p>{de.checking}</p>}
      {replayError&&<div className="error" role="alert"><strong>{de.errorHeading}</strong><p>{replayError}</p></div>}
      {replayed&&<><h3>{de.replayVerified}</h3><p>{de.replayReadOnly}</p>
        <dl><dt>{de.result}</dt><dd>{outcomes[replayed.assessment.outcome]??de.unknown}</dd>
        <dt>{de.date}</dt><dd>{replayed.assessment.assessmentDate.split('-').reverse().join('.')}</dd>
        <dt>{de.release}</dt><dd>{replayed.assessment.knowledgeRelease}</dd>
        <dt>{de.platform}</dt><dd>{replayed.platformVersion}</dd></dl>
        <details><summary>{de.replayTrace}</summary><pre>{replayRaw}</pre></details>
        <button className="secondary export" onClick={()=>saveFile(replayRaw,'normacase-replayed-assessment.json')}>{de.replayExport}</button>
      </>}
    </section></div><div className="workspace-view tool-view" hidden={view!=='review'}><ReviewedCaseWorkQueues packs={packs}/></div><div className="workspace-view tool-view" hidden={view!=='workflow'}><label className="field workflow-pack">{de.workspace.workflowPack}<select value={selected} onChange={event=>changePack(event.target.value)}>{packs.map(item=><option key={item.packId} value={item.packId}>{item.presentation?.title??item.packId}</option>)}</select></label>{pack && <WorkflowWorkbench key={pack.packId} pack={pack} />}</div></main></>;
}
createRoot(document.getElementById('root')!).render(<React.StrictMode><App/></React.StrictMode>);
