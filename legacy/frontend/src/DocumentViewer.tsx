import {useEffect,useLayoutEffect,useRef,useState,type CSSProperties} from 'react';
import de from './de.json';
const ui=de.clinicalWorkspace,t=de.documents;
let sessionZoom='width';
type Document={id:string;title:string;mediaType:string;pages:number};
export function DocumentViewer({document,url,page,onPage}:{document:Document;url:string;page:number;onPage:(page:number)=>void}){
 const stage=useRef<HTMLDivElement|null>(null),pageRefs=useRef<Record<number,HTMLDivElement|null>>({});
 const [size,setSize]=useState({width:600,height:500}),[mode,setMode]=useState(sessionZoom),[rotation,setRotation]=useState(0),[plain,setPlain]=useState(''),[failed,setFailed]=useState(false);
 const resizing=useRef(true),requestedPage=useRef(page);
 const currentPage=useRef(page);currentPage.current=page;
 useEffect(()=>{const element=stage.current;if(!element)return;const observer=new ResizeObserver(([entry])=>{resizing.current=true;requestedPage.current=currentPage.current;setSize({width:entry.contentRect.width,height:entry.contentRect.height});});observer.observe(element);return()=>observer.disconnect();},[document.id]);
 useEffect(()=>{setRotation(0);setPlain('');setFailed(false);const controller=new AbortController();if(document.mediaType==='text/plain')fetch(url,{signal:controller.signal}).then(r=>{if(!r.ok)throw new Error();return r.text();}).then(text=>{if(!controller.signal.aborted)setPlain(text);}).catch(()=>{if(!controller.signal.aborted)setFailed(true);});return()=>controller.abort();},[url,document.mediaType]);
 function scrollToPage(number:number){const container=stage.current,target=pageRefs.current[number];if(container&&target)container.scrollTop+=target.getBoundingClientRect().top-container.getBoundingClientRect().top-12;}
 useLayoutEffect(()=>{
  resizing.current=true;scrollToPage(requestedPage.current);onPage(requestedPage.current);
  const frame=requestAnimationFrame(()=>{resizing.current=false;});return()=>cancelAnimationFrame(frame);
 },[url,size.width,size.height,mode,rotation]);
 function trackPage(){if(resizing.current||!stage.current)return;const top=stage.current.getBoundingClientRect().top;const visible=Object.entries(pageRefs.current).find(([,element])=>element&&element.getBoundingClientRect().bottom>top+24);if(visible){const number=Number(visible[0]);if(number!==currentPage.current)onPage(number);}}
 function zoom(next:string){if(next===mode)return;requestedPage.current=currentPage.current;resizing.current=true;sessionZoom=next;setMode(next);}
 function jump(next:number){const bounded=Math.max(1,Math.min(document.pages,next));requestedPage.current=bounded;currentPage.current=bounded;onPage(bounded);scrollToPage(bounded);}
 const available=Math.max(1,size.width-32),sideways=rotation%180!==0;
 const pageWidth=sideways?842:595,pageHeight=sideways?595:842;
 const width=mode==='width'?Math.max(320,Math.min(1000,available)):mode==='page'?Math.max(120,Math.min(1000,available,(size.height-24)*pageWidth/pageHeight)):595*Number(mode)/100;
 const scale=Math.round(width/595*100);
 useEffect(()=>{const element=stage.current;if(!element)return;const wheel=(event:WheelEvent)=>{if(event.ctrlKey){event.preventDefault();zoom(String(Math.max(50,Math.min(300,scale+(event.deltaY<0?25:-25)))));}};element.addEventListener('wheel',wheel,{passive:false});return()=>element.removeEventListener('wheel',wheel);},[url,scale,mode]);
 return <div className="document-reader"><div className="document-toolbar page-controls" aria-label={t.preview}>
  {document.mediaType==='application/pdf'&&<><button aria-label={t.previousPage} disabled={page<=1} onClick={()=>jump(page-1)}>‹</button><label className="viewer-page-field"><input aria-label={ui.pageNumber} type="number" min={1} max={document.pages} value={page} onChange={e=>{const v=Number(e.target.value);if(Number.isInteger(v)&&v>=1&&v<=document.pages)jump(v);}}/><span>/ {document.pages}</span></label><button aria-label={t.nextPage} disabled={page>=document.pages} onClick={()=>jump(page+1)}>›</button></>}
  {document.mediaType!=='text/plain'&&<><button aria-label="Verkleinern" onClick={()=>zoom(String(Math.max(50,scale-25)))}>−</button><select aria-label={ui.zoom} value={mode} onChange={e=>zoom(e.target.value)}><option value="width">{ui.fitWidth}</option><option value="page">{ui.wholePage}</option>{[50,75,100,125,150,200,250,300,...(![50,75,100,125,150,200,250,300].includes(Number(mode))&&mode!=='width'&&mode!=='page'?[Number(mode)]:[])].map(v=><option key={v} value={v}>{v} %</option>)}</select><button aria-label="Vergrößern" onClick={()=>zoom(String(Math.min(300,scale+25)))}>+</button><button aria-label={ui.rotate} onClick={()=>{requestedPage.current=currentPage.current;resizing.current=true;setRotation(v=>(v+90)%360);}}>↻</button></>}
  <a aria-label={t.newWindow} title={t.newWindow} href={url} target="_blank" rel="noopener noreferrer">↗</a><a aria-label={t.download} title={t.download} href={url+'?download=true'} download>↓</a><span className="viewer-filename" title={document.title}>{document.title}</span><span title={ui.viewerInfo} aria-label={ui.viewerInfo}>ⓘ</span>
 </div><div ref={stage} className={'page-stage'+(mode!=='width'&&mode!=='page'?' zoomed':'')} data-zoom-mode={mode} onScroll={trackPage} tabIndex={0} onKeyDown={e=>{if((e.ctrlKey||e.metaKey)&&e.key==='0'){e.preventDefault();zoom('width');}}}>
  {failed?<p role="alert">{t.previewFailed}</p>:document.mediaType==='text/plain'?<pre className="document-text">{plain||t.loading}</pre>:<div className={document.mediaType==='image/png'?'scan-preview':'continuous-pages'}>{Array.from({length:document.mediaType==='application/pdf'?document.pages:1},(_,index)=>{const number=index+1;return <div key={url+'-'+number} ref={element=>{pageRefs.current[number]=element;}} data-page={number} className="viewer-page" style={{width:width+'px',aspectRatio:pageWidth+'/'+pageHeight} as CSSProperties}><img className={document.mediaType==='application/pdf'?'pdf-page-image':'scan-image'} style={{width:sideways?width*pageHeight/pageWidth+'px':'100%',height:sideways?width+'px':'100%',transform:'translate(-50%, -50%) rotate('+rotation+'deg)'}} src={document.mediaType==='application/pdf'?url+'/pages/'+number:url} alt={t.preview+': '+document.title+' · '+t.page+' '+number} onError={()=>setFailed(true)}/></div>;})}</div>}
 </div></div>;
}
