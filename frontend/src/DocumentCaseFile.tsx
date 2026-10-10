import { useEffect, useState } from 'react';
import { parse } from 'lossless-json';
import de from './de.json';
import type { Pack } from './model';
import { DecisionTrace } from './DecisionTrace';
import type { RuleTrace, OutputTrace } from './trace';

const text=de.documents;
type Document={id:string;title:string;mediaType:string;pages:number;sha256:string};
type Observation={id:string;page:number;field:string;value:string;method:string};
type File={caseId:string;title:string;scope:string;validationLevel:string;documents:Document[];observations:Observation[];findings:string[];fieldLabels:Record<string,string>;source:{title:string;version:string;url:string};assessmentJson:string|null};
type Assessment={assessment:{outcome:string;ruleTrace?:RuleTrace;domainOutputs?:OutputTrace[]}};
const outcomes:Record<string,string>={SUPPORTED:de.supported,NOT_SUPPORTED:de.notSupported,INCOMPLETE:de.incomplete,HUMAN_REVIEW:de.review,NOT_APPLICABLE:de.na};
const values:Record<string,string>={YES:text.yes,NO:text.no,UNKNOWN:text.unknown,NOT_APPLICABLE:text.notApplicable};

export function DocumentCaseFile({caseId,presentation,showAssessment=false}:{caseId:string;presentation?:Pack['presentation'];showAssessment?:boolean}) {
  const [file,setFile]=useState<File|null>(null);
  const [selected,setSelected]=useState('');
  const [error,setError]=useState('');
  const [plain,setPlain]=useState('');const [sourcePage,setSourcePage]=useState(1);const [zoom,setZoom]=useState(false);
  useEffect(()=>{
    const controller=new AbortController();setFile(null);setSelected('');setError('');setPlain('');setSourcePage(1);setZoom(false);
    fetch('/api/document-cases/'+encodeURIComponent(caseId),{signal:controller.signal})
      .then(response=>{if(!response.ok)throw new Error();return response.json();})
      .then((data:File)=>{if(!controller.signal.aborted){setFile(data);setSelected(data.documents[0]?.id??'');}})
      .catch(()=>{if(!controller.signal.aborted)setError(de.networkError);});
    return()=>controller.abort();
  },[caseId]);
  const document=file?.documents.find(item=>item.id===selected);
  const url='/api/document-cases/'+encodeURIComponent(caseId)+'/documents/'+encodeURIComponent(selected);
  useEffect(()=>{
    const controller=new AbortController();setPlain('');
    if(document?.mediaType==='text/plain')fetch(url,{signal:controller.signal}).then(response=>{if(!response.ok)throw new Error();return response.text();}).then(value=>{if(!controller.signal.aborted)setPlain(value);}).catch(()=>{if(!controller.signal.aborted)setError(de.networkError);});
    return()=>controller.abort();
  },[url,document?.mediaType]);
  const resolvedPresentation=presentation??(file?{title:file.title,description:file.scope,fields:file.fieldLabels,evidence:{severe_disability_card:de.documents.disabilityProof,care_grade_notice:de.documents.careProof,care_transition_classification_proof:de.documents.transitionProof},examples:[]}:undefined);
  const assessment=file?.assessmentJson?parse(file.assessmentJson) as Assessment:null;
  // Ignore an old result immediately on case switch, before the effect cleans it up.
  if(file&&file.caseId!==caseId)return <p>{text.loading}</p>;
  return <section className="document-case-file" aria-label={text.heading}>
    <h4>{text.heading}</h4><p>{text.help}</p>
    {error?<p role="alert">{error}</p>:!file?<p role="status">{text.loading}</p>:<>
      <p className="review-notice">{file.validationLevel==='PUBLIC_REFERENCE'?text.publicReference:text.synthetic}</p>
      <p><strong>{text.scope}: </strong>{file.scope}</p>
      <div className="document-layout"><ul className="document-list">{file.documents.map(item=><li key={item.id}>
        <button type="button" className="secondary" aria-pressed={selected===item.id} onClick={()=>{setSelected(item.id);setSourcePage(1);setZoom(false);}}>{text.choose}: {item.title}</button>
        <span>{item.mediaType==='application/pdf'?'PDF':item.mediaType==='text/plain'?text.textFile:text.scan} · {item.pages} {text.page}</span>
      </li>)}</ul>
      {document&&<div className="document-viewer"><h5>{document.title}</h5>
        <div className="document-actions"><a href={url} target="_blank" rel="noopener noreferrer">{text.newWindow}</a><a href={url+'?download=true'} download>{text.download}</a></div>
        {document.mediaType==='application/pdf'?<iframe key={url+sourcePage} title={text.preview+': '+document.title} src={url+'#page='+sourcePage}/>:document.mediaType==='text/plain'?<pre className="document-text">{plain||text.loading}</pre>:<><button type="button" className="secondary" aria-pressed={zoom} onClick={()=>setZoom(!zoom)}>{zoom?text.zoomOut:text.zoomIn}</button><div className={zoom?'scan-preview zoomed':'scan-preview'}><img src={url} alt={document.title}/></div></>}
      </div>}</div>
      {!!file.findings.length&&<div className="missing"><h4>{text.findings}</h4><ul>{file.findings.map((finding,i)=><li key={i}>{finding}</li>)}</ul></div>}
      <details className="document-observations"><summary>{text.observations}</summary><p>{text.observationsHelp}</p>
        <ul>{file.observations.map((item,i)=><li key={i}><strong>{resolvedPresentation?.fields[item.field]??de.fieldReference}</strong>: {values[item.value]??item.value.replace('.',',')} — <button type="button" className="secondary" onClick={()=>{setSelected(item.id);setSourcePage(item.page);setZoom(false);}}>{file.documents.find(d=>d.id===item.id)?.title}, {text.page} {item.page}</button></li>)}</ul>
      </details>
      <details><summary>{text.source}</summary><p>{file.source.title}</p><p>{text.sourceVersion}: {file.source.version}</p><a href={file.source.url} target="_blank" rel="noopener noreferrer">{text.source}</a></details>
      {showAssessment&&(assessment?<><h4>{de.result}: {outcomes[assessment.assessment.outcome]??de.unknown}</h4><DecisionTrace rule={assessment.assessment.ruleTrace} outputs={assessment.assessment.domainOutputs} presentation={resolvedPresentation}/><details><summary>{de.trace}</summary><pre>{file.assessmentJson}</pre></details></>:<p>{text.noRule}</p>)}
    </>}
  </section>;
}

export function ReferenceDocumentCases() {
  const [cases,setCases]=useState<{caseId:string;title:string;scope:string}[]>([]);
  const [selected,setSelected]=useState('');const [error,setError]=useState('');
  useEffect(()=>{const controller=new AbortController();fetch('/api/document-cases',{signal:controller.signal}).then(response=>{if(!response.ok)throw new Error();return response.json();}).then(data=>{if(!controller.signal.aborted)setCases(data.cases);}).catch(()=>{if(!controller.signal.aborted)setError(de.networkError);});return()=>controller.abort();},[]);
  return <section id="reference-cases" className="card reference-document-cases" aria-label={text.referenceHeading}><h2>{text.referenceHeading}</h2><p>{text.referenceHelp}</p>
    {error&&<p role="alert">{error}</p>}<ul className="reference-list">{cases.map(item=><li key={item.caseId}><button type="button" className="secondary" aria-pressed={selected===item.caseId} onClick={()=>setSelected(item.caseId)}>{item.title}</button></li>)}</ul>
    {selected&&<DocumentCaseFile key={selected} caseId={selected} showAssessment/>}
  </section>;
}
