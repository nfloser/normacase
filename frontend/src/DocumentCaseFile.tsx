import { useEffect, useState, useRef, type ReactNode } from 'react';
import {createPortal} from 'react-dom';
import { parse } from 'lossless-json';
import de from './de.json';
import type { Pack } from './model';
import {WorkspaceIcon} from './WorkspaceIcon';
import {DocumentViewer} from './DocumentViewer';
import {RecordedCriteria} from './RecordedCriteria';
import {caseIdentity} from './casePresentation';
const ui=de.clinicalWorkspace;
import { DecisionTrace } from './DecisionTrace';
import type { RuleTrace, OutputTrace } from './trace';

const text=de.documents;
type Document={id:string;title:string;mediaType:string;pages:number;sha256:string};
type Observation={id:string;page:number;field:string;value:string;method:string};
type CaseContext={request:string;question:string;background:string};
type File={context:CaseContext|null;caseId:string;title:string;scope:string;validationLevel:string;documents:Document[];observations:Observation[];findings:string[];fieldLabels:Record<string,string>;evidenceLabels:Record<string,string>;outputLabels:Record<string,{label:string;choices:Record<string,string>}>;source:{title:string;version:string;url:string};assessmentJson:string|null};
type Assessment={platformVersion?:string;assessment:{outcome:string;assessmentDate?:string;knowledgeRelease?:string;missingRequiredFields?:string[];ruleTrace?:RuleTrace;domainOutputs?:OutputTrace[]}};
const outcomes:Record<string,string>={SUPPORTED:de.supported,NOT_SUPPORTED:de.notSupported,INCOMPLETE:de.incomplete,HUMAN_REVIEW:de.review,NOT_APPLICABLE:de.na};
const values:Record<string,string>={YES:text.yes,NO:text.no,UNKNOWN:text.unknown,NOT_APPLICABLE:text.notApplicable};

function assessmentCopyText(file:File,record:Assessment):string{
 const result=record.assessment;
 const lines=[
  'Fall: '+file.title,
  'Aktenzeichen: '+caseIdentity(file.caseId,file.title).number,
  'Prüfergebnis: '+(outcomes[result.outcome]??de.unknown)
 ];
 if(result.assessmentDate)lines.push('Prüfdatum: '+result.assessmentDate);
 if(result.knowledgeRelease)lines.push('Wissensstand: '+result.knowledgeRelease);
 if(record.platformVersion)lines.push('Plattformstand: '+record.platformVersion);
 if(result.missingRequiredFields?.length){
  lines.push('Fehlende Pflichtangaben:');
  for(const field of result.missingRequiredFields)lines.push('– '+(file.fieldLabels[field]??de.fieldReference));
 }
 lines.push('Synthetischer Schulungsfall · Kein verbindlicher medizinischer oder rechtlicher Prüfentscheid.');
 return lines.join('\n');
}


export function DocumentCaseFile({caseId,presentation,showAssessment=false,analysis,initialView='result',copyTargetId}:{caseId:string;copyTargetId?:string;initialView?:'result'|'documents';analysis?:ReactNode;presentation?:Pack['presentation'];showAssessment?:boolean}) {
  const [view,setView]=useState<'result'|'documents'|'data'|'history'|'notes'>(initialView);
  const [file,setFile]=useState<File|null>(null);
  const [selected,setSelected]=useState('');
  const [error,setError]=useState('');const [copyNotice,setCopyNotice]=useState('');
  const [sourcePage,setSourcePage]=useState(1);
  useEffect(()=>{
    const controller=new AbortController();setView(initialView);setFile(null);setSelected('');setError('');setCopyNotice('');setSourcePage(1);
    fetch('/api/document-cases/'+encodeURIComponent(caseId),{signal:controller.signal})
      .then(response=>{if(!response.ok)throw new Error();return response.json();})
      .then((data:File)=>{if(!controller.signal.aborted){setFile(data);setSelected(data.documents[0]?.id??'');}})
      .catch(()=>{if(!controller.signal.aborted)setError(de.networkError);});
    return()=>controller.abort();
  },[caseId,initialView]);
  const document=file?.documents.find(item=>item.id===selected);
  const url='/api/document-cases/'+encodeURIComponent(caseId)+'/documents/'+encodeURIComponent(selected);
  const resolvedPresentation=presentation??(file?{title:file.title,description:file.scope,fields:file.fieldLabels,evidence:file.evidenceLabels,outputs:file.outputLabels,examples:[]}:undefined);
  const assessment=file?.assessmentJson?parse(file.assessmentJson) as Assessment:null;
  async function copyText(content:string){
   try{
    if(!navigator.clipboard?.writeText)throw new Error('clipboard_unavailable');
    await navigator.clipboard.writeText(content);
    setCopyNotice('In die Zwischenablage kopiert.');
   }catch{setCopyNotice('Kopieren nicht möglich. Bitte die Berechtigung für die Zwischenablage prüfen.');}
  }

  const copyActions=<div className="case-copy-actions">
       <button type="button" className="secondary" disabled={!file} onClick={()=>file&&copyText(caseIdentity(file.caseId,file.title).number)}><WorkspaceIcon name="copy"/>{ui.copyId}</button>
       <button type="button" className="secondary" disabled={!file||!assessment} title={!assessment?'Für diesen Vorgang liegt kein regelbasiertes Prüfergebnis vor.':undefined} onClick={()=>file&&assessment&&copyText(assessmentCopyText(file,assessment))}><WorkspaceIcon name="copy"/>{ui.copyResult}</button>
       {copyNotice&&<span role="status" className="case-copy-notice">{copyNotice}</span>}
      </div>;
  // Ignore an old result immediately on case switch, before the effect cleans it up.
  if(file&&file.caseId!==caseId)return <p>{text.loading}</p>;
  return <section className="document-case-file" aria-label={text.heading}>
    <nav className="case-view-navigation" aria-label={de.workspace.caseViews}>
      <button type="button" className="secondary" aria-pressed={view==='result'} onClick={()=>setView('result')}>{de.workspace.resultView}</button>
      <button type="button" className="secondary" aria-label={de.workspace.documentsView} aria-pressed={view==='documents'} onClick={()=>setView('documents')}>{de.workspace.documentsView}{file?' ('+file.documents.length+')':''}</button>
      <button type="button" className="secondary" aria-pressed={view==='data'} onClick={()=>setView('data')}>{ui.fileData}</button><button type="button" className="secondary" aria-pressed={view==='history'} onClick={()=>setView('history')}>{ui.history}</button><button type="button" className="secondary" aria-pressed={view==='notes'} onClick={()=>setView('notes')}>{ui.notes}</button>{copyTargetId&&globalThis.document.getElementById(copyTargetId)?createPortal(copyActions,globalThis.document.getElementById(copyTargetId)!):copyActions}
    </nav>
    {error?<div className="case-result-view"><p role="alert">{error}</p>{analysis}</div>:!file?<div className="case-result-view"><p role="status">{text.loading}</p>{analysis}</div>:<>
      {view==='result'?<div className="case-result-view"><div className="case-result-summary">
        <p className="review-notice">{file.validationLevel==='PUBLIC_REFERENCE'?text.publicReference:file.assessmentJson?text.synthetic:text.documentOnly}</p>
        <div className="result-banner">{analysis}{showAssessment&&(assessment?<h4>{de.result}: {outcomes[assessment.assessment.outcome]??de.unknown}</h4>:<p>{text.noRule}</p>)}</div>

        {!!file.findings.length&&<div className="missing"><h4>{text.findings}</h4><ul>{file.findings.map((finding,i)=><li key={i}>{finding}</li>)}</ul></div>}
        {file.context&&<section className="case-context" aria-label={de.workspace.context}><h4>{file.context.request}</h4><p><strong>{de.workspace.question}: </strong>{file.context.question}</p><details><summary>{de.workspace.background}</summary><p>{file.context.background}</p></details></section>}
        <p><strong>{text.scope}: </strong>{file.scope}</p><details><summary>{ui.check}</summary>        {showAssessment&&!!assessment?.assessment.domainOutputs?.length&&<section className="case-domain-results"><h4>{de.domainOutputs}</h4><dl>{assessment.assessment.domainOutputs.map(output=><div key={output.outputId}><dt>{resolvedPresentation?.outputs?.[output.outputId]?.label??de.outputReference}</dt><dd>{output.value.kind==='UNKNOWN'?de.unknown:(resolvedPresentation?.outputs?.[output.outputId]?.choices[output.value.choice??'']??de.unknown)}</dd></div>)}</dl></section>}
{showAssessment&&assessment&&<><p>{de.release}: {assessment.assessment.knowledgeRelease} · {de.date}: {assessment.assessment.assessmentDate}</p><RecordedCriteria rule={assessment.assessment.ruleTrace} outputs={assessment.assessment.domainOutputs} presentation={resolvedPresentation}/><DecisionTrace rule={assessment.assessment.ruleTrace} outputs={assessment.assessment.domainOutputs} presentation={resolvedPresentation}/><details><summary>{de.trace}</summary><pre>{file.assessmentJson}</pre></details></>}</details></div>
        <details className="document-observations" open><summary>{text.observations}</summary><p>{text.observationsHelp}</p>
          {file.documents.filter(document=>file.observations.some(item=>item.id===document.id)).map(document=><div key={document.id} className="observation-source-group"><p className="observation-document">{ui.source}: {document.title}</p><table><thead><tr><th>{ui.criterion}</th><th>{ui.value}</th><th>{ui.source}</th></tr></thead><tbody>{file.observations.filter(item=>item.id===document.id).map((item,i)=><tr key={i}><td>{resolvedPresentation?.fields[item.field]??de.fieldReference}</td><td>{values[item.value]??item.value.replace('.',',')}</td><td><button type="button" className="source-page-link" aria-label={document.title+', '+text.page+' '+item.page} onClick={()=>{setSelected(item.id);setSourcePage(item.page);setView('documents');}}>{text.page} {item.page}</button></td></tr>)}</tbody></table></div>)}

        </details>
        <details><summary>{text.source}</summary><p>{file.source.title}</p><p>{text.sourceVersion}: {file.source.version}</p><a href={file.source.url} target="_blank" rel="noopener noreferrer">{text.source}</a></details>
      </div>:view!=='documents'?<div className="case-result-view">{view==='data'?<><h4>{ui.fileData}</h4><dl><dt>{ui.number}</dt><dd>{caseIdentity(file.caseId,file.title).number}</dd><dt>{ui.subject}</dt><dd>{caseIdentity(file.caseId,file.title).subject}</dd><dt>{ui.owner}</dt><dd>{ui.unassigned}</dd></dl><p>{file.context?.background}</p><p>{file.context?.question}</p></>:<p>{view==='history'?ui.noHistory:ui.noNotes}</p>}</div>:<div className="case-documents-view"><aside className="document-case-summary" aria-label={de.workspace.analysis}>
        <h4>{text.fileList}</h4><ul className="case-file-list">{file.documents.map(item=><li key={item.id}><button type="button" className="secondary" aria-pressed={selected===item.id} onClick={()=>{setSelected(item.id);setSourcePage(1);}}><WorkspaceIcon name="document"/>{item.title}<small>{item.mediaType==='application/pdf'?'PDF · '+item.pages+' '+text.page:item.mediaType==='image/png'?'PNG':text.textFile}</small></button></li>)}</ul>
      </aside>{document&&<DocumentViewer key={url} document={document} url={url} page={sourcePage} onPage={setSourcePage}/>}</div>}

    </>}
  </section>;
}

export function ReferenceDocumentCases() {
  const [cases,setCases]=useState<{caseId:string;title:string;scope:string;context:CaseContext|null}[]>([]);
  const [selected,setSelected]=useState('');const [error,setError]=useState('');
  useEffect(()=>{const controller=new AbortController();fetch('/api/document-cases',{signal:controller.signal}).then(response=>{if(!response.ok)throw new Error();return response.json();}).then(data=>{if(!controller.signal.aborted)setCases(data.cases);}).catch(()=>{if(!controller.signal.aborted)setError(de.networkError);});return()=>controller.abort();},[]);
  const buttons=useRef<Record<string,HTMLButtonElement|null>>({});
  function back(){const previous=selected;setSelected('');requestAnimationFrame(()=>buttons.current[previous]?.focus());}
  return <section id="reference-cases" className="card reference-document-cases case-first" aria-label={text.referenceHeading}>
    {!selected?<div className="case-overview"><h2>{text.referenceHeading}</h2><p>{text.referenceHelp}</p>
      {error&&<p role="alert">{error}</p>}<ul className="reference-list">{cases.map(item=><li key={item.caseId}><button ref={node=>{buttons.current[item.caseId]=node;}} type="button" className="secondary" onClick={()=>setSelected(item.caseId)}>{item.title}</button><p>{item.context?.question??item.scope}</p></li>)}</ul>
    </div>:<div className="opened-case"><header className="case-header"><button type="button" className="secondary" onClick={back}>{de.workspace.back}</button><h2>{cases.find(item=>item.caseId===selected)?.title}</h2><div id="reference-case-copy"/></header><DocumentCaseFile key={selected} caseId={selected} showAssessment copyTargetId="reference-case-copy"/></div>}
  </section>;
}
