import { useEffect, useState, useRef, type ReactNode } from 'react';
import { parse } from 'lossless-json';
import de from './de.json';
import type { Pack } from './model';
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
  'Aktenzeichen: '+file.caseId,
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


export function DocumentCaseFile({caseId,presentation,showAssessment=false,analysis,initialView='result'}:{caseId:string;initialView?:'result'|'documents';analysis?:ReactNode;presentation?:Pack['presentation'];showAssessment?:boolean}) {
  const [view,setView]=useState<'result'|'documents'>(initialView);
  const [file,setFile]=useState<File|null>(null);
  const [selected,setSelected]=useState('');
  const [error,setError]=useState('');const [copyNotice,setCopyNotice]=useState('');
  const [plain,setPlain]=useState('');const [sourcePage,setSourcePage]=useState(1);const [zoom,setZoom]=useState(100);const [previewFailed,setPreviewFailed]=useState(false);
  useEffect(()=>{
    const controller=new AbortController();setView(initialView);setFile(null);setSelected('');setError('');setCopyNotice('');setPlain('');setSourcePage(1);setZoom(100);
    fetch('/api/document-cases/'+encodeURIComponent(caseId),{signal:controller.signal})
      .then(response=>{if(!response.ok)throw new Error();return response.json();})
      .then((data:File)=>{if(!controller.signal.aborted){setFile(data);setSelected(data.documents[0]?.id??'');}})
      .catch(()=>{if(!controller.signal.aborted)setError(de.networkError);});
    return()=>controller.abort();
  },[caseId,initialView]);
  const document=file?.documents.find(item=>item.id===selected);
  const url='/api/document-cases/'+encodeURIComponent(caseId)+'/documents/'+encodeURIComponent(selected);
  useEffect(()=>{
    const controller=new AbortController();setPlain('');
    if(document?.mediaType==='text/plain')fetch(url,{signal:controller.signal}).then(response=>{if(!response.ok)throw new Error();return response.text();}).then(value=>{if(!controller.signal.aborted)setPlain(value);}).catch(()=>{if(!controller.signal.aborted)setError(de.networkError);});
    return()=>controller.abort();
  },[url,document?.mediaType]);
  useEffect(()=>{setPreviewFailed(false);},[selected,sourcePage,caseId]);
  const resolvedPresentation=presentation??(file?{title:file.title,description:file.scope,fields:file.fieldLabels,evidence:file.evidenceLabels,outputs:file.outputLabels,examples:[]}:undefined);
  const assessment=file?.assessmentJson?parse(file.assessmentJson) as Assessment:null;
  async function copyText(content:string){
   try{
    if(!navigator.clipboard?.writeText)throw new Error('clipboard_unavailable');
    await navigator.clipboard.writeText(content);
    setCopyNotice('In die Zwischenablage kopiert.');
   }catch{setCopyNotice('Kopieren nicht möglich. Bitte die Berechtigung für die Zwischenablage prüfen.');}
  }

  // Ignore an old result immediately on case switch, before the effect cleans it up.
  if(file&&file.caseId!==caseId)return <p>{text.loading}</p>;
  return <section className="document-case-file" aria-label={text.heading}>
    <nav className="case-view-navigation" aria-label={de.workspace.caseViews}>
      <button type="button" className="secondary" aria-pressed={view==='result'} onClick={()=>setView('result')}>{de.workspace.resultView}</button>
      <button type="button" className="secondary" aria-label={de.workspace.documentsView} aria-pressed={view==='documents'} onClick={()=>setView('documents')}>{de.workspace.documentsView}{file?' ('+file.documents.length+')':''}</button>
      <div className="case-copy-actions">
       <button type="button" className="secondary" disabled={!file} onClick={()=>file&&copyText(file.caseId)}>Aktenzeichen kopieren</button>
       <button type="button" className="secondary" disabled={!file||!assessment} title={!assessment?'Für diesen Vorgang liegt kein regelbasiertes Prüfergebnis vor.':undefined} onClick={()=>file&&assessment&&copyText(assessmentCopyText(file,assessment))}>Ergebnistext kopieren</button>
       {copyNotice&&<span role="status" className="case-copy-notice">{copyNotice}</span>}
      </div>
    </nav>
    {error?<div className="case-result-view"><p role="alert">{error}</p>{analysis}</div>:!file?<div className="case-result-view"><p role="status">{text.loading}</p>{analysis}</div>:<>
      {view==='result'?<div className="case-result-view">
        <p className="review-notice">{file.validationLevel==='PUBLIC_REFERENCE'?text.publicReference:file.assessmentJson?text.synthetic:text.documentOnly}</p>
        <div className="result-banner">{analysis}{showAssessment&&(assessment?<h4>{de.result}: {outcomes[assessment.assessment.outcome]??de.unknown}</h4>:<p>{text.noRule}</p>)}</div>

        {!!file.findings.length&&<div className="missing"><h4>{text.findings}</h4><ul>{file.findings.map((finding,i)=><li key={i}>{finding}</li>)}</ul></div>}
        {showAssessment&&!!assessment?.assessment.domainOutputs?.length&&<section className="case-domain-results"><h4>{de.domainOutputs}</h4><dl>{assessment.assessment.domainOutputs.map(output=><div key={output.outputId}><dt>{resolvedPresentation?.outputs?.[output.outputId]?.label??de.outputReference}</dt><dd>{output.value.kind==='UNKNOWN'?de.unknown:(resolvedPresentation?.outputs?.[output.outputId]?.choices[output.value.choice??'']??de.unknown)}</dd></div>)}</dl></section>}
        {file.context&&<section className="case-context" aria-label={de.workspace.context}><h4>{file.context.request}</h4><p><strong>{de.workspace.question}: </strong>{file.context.question}</p><details><summary>{de.workspace.background}</summary><p>{file.context.background}</p></details></section>}
        <p><strong>{text.scope}: </strong>{file.scope}</p>
        <details className="document-observations" open><summary>{text.observations}</summary><p>{text.observationsHelp}</p>
          <ul>{file.observations.map((item,i)=><li key={i}><strong>{resolvedPresentation?.fields[item.field]??de.fieldReference}</strong>: {values[item.value]??item.value.replace('.',',')} — <button type="button" className="secondary" onClick={()=>{setSelected(item.id);setSourcePage(item.page);setZoom(100);setView('documents');}}>{file.documents.find(d=>d.id===item.id)?.title}, {text.page} {item.page}</button></li>)}</ul>
        </details>
        {showAssessment&&assessment&&<><DecisionTrace rule={assessment.assessment.ruleTrace} outputs={assessment.assessment.domainOutputs} presentation={resolvedPresentation}/><details><summary>{de.trace}</summary><pre>{file.assessmentJson}</pre></details></>}
        <details><summary>{text.source}</summary><p>{file.source.title}</p><p>{text.sourceVersion}: {file.source.version}</p><a href={file.source.url} target="_blank" rel="noopener noreferrer">{text.source}</a></details>
      </div>:<div className="case-documents-view"><aside className="document-case-summary" aria-label={de.workspace.analysis}>
        <h4>{text.fileList}</h4><ul className="case-file-list">{file.documents.map(item=><li key={item.id}><button type="button" className="secondary" aria-pressed={selected===item.id} onClick={()=>{setSelected(item.id);setSourcePage(1);setZoom(100);}}>{item.title}<small>{item.mediaType==='application/pdf'?'PDF · '+item.pages+' '+text.page:item.mediaType==='image/png'?'PNG':text.textFile}</small></button></li>)}</ul>
        <div className="compact-case-result">{analysis}{showAssessment&&(assessment?<h4>{de.result}: {outcomes[assessment.assessment.outcome]??de.unknown}</h4>:<h4>{text.documentReviewResult}</h4>)}</div>
        {file.context&&<p>{file.context.question}</p>}
        {!!file.findings.length&&<div className="missing"><h4>{text.findings}</h4><ul>{file.findings.map((finding,i)=><li key={i}>{finding}</li>)}</ul></div>}
        <button type="button" className="secondary" onClick={()=>setView('result')}>{de.workspace.resultView}</button>
      </aside><div className="document-reader"><div className="document-toolbar">
        <label className="field document-choice">{text.select}<select value={selected} onChange={event=>{setSelected(event.target.value);setSourcePage(1);setZoom(100);}}>{file.documents.map(item=><option key={item.id} value={item.id}>{item.title}</option>)}</select></label>
        {document&&<div className="document-actions"><a href={url} target="_blank" rel="noopener noreferrer">{text.newWindow}</a><a href={url+'?download=true'} download>{text.download}</a></div>}
      </div>
      {document&&<div className="document-viewer">
        {document.mediaType==='application/pdf'?<>
          <div className="page-controls" aria-label={text.preview}>
            <button type="button" className="secondary" aria-label={text.previousPage} disabled={sourcePage<=1} onClick={()=>setSourcePage(sourcePage-1)}>‹</button>
            <span role="status">{text.page} {sourcePage} {text.of} {document.pages}</span>
            <button type="button" className="secondary" aria-label={text.nextPage} disabled={sourcePage>=document.pages} onClick={()=>setSourcePage(sourcePage+1)}>›</button>
            <button type="button" className="secondary" aria-label="Verkleinern" disabled={zoom<=50} onClick={()=>setZoom(v=>Math.max(50,v-10))}>−</button>
            <span aria-label="Zoomstufe" role="status">{zoom} %</span>
            <button type="button" className="secondary" aria-label="Vergrößern" disabled={zoom>=250} onClick={()=>setZoom(v=>Math.min(250,v+10))}>+</button>
            <button type="button" className="secondary" onClick={()=>setZoom(100)}>{text.fitPage}</button>
          </div>
          <div className={zoom>100?'page-stage zoomed':'page-stage'} key={url+sourcePage}>
            {previewFailed?<p role="alert">{text.previewFailed}</p>:<img className="pdf-page-image" style={{width:zoom+'%',height:zoom+'%',maxWidth:zoom>100?'none':undefined,maxHeight:zoom>100?'none':undefined}} src={url+'/pages/'+sourcePage} alt={text.preview+': '+document.title+' · '+text.page+' '+sourcePage} onError={()=>setPreviewFailed(true)}/>}
          </div><p className="reader-help">{text.readerHelp}</p>
        </>:document.mediaType==='text/plain'?<pre className="document-text">{plain||text.loading}</pre>:<><button type="button" className="secondary" aria-pressed={zoom>100} onClick={()=>setZoom(zoom>100?100:150)}>{zoom>100?text.zoomOut:text.zoomIn}</button><div className={zoom>100?'scan-preview zoomed':'scan-preview'}><img src={url} alt={document.title}/></div></>}
      </div>}
      </div></div>}
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
    </div>:<div className="opened-case"><header className="case-header"><button type="button" className="secondary" onClick={back}>{de.workspace.back}</button><h2>{cases.find(item=>item.caseId===selected)?.title}</h2></header><DocumentCaseFile key={selected} caseId={selected} showAssessment/></div>}
  </section>;
}
