import {useEffect,useRef,useState,type CSSProperties} from 'react';
import de from './de.json';
import {parse} from 'lossless-json';
import {createPortal} from 'react-dom';
import {WorkspaceIcon} from './WorkspaceIcon';
import {NavTreeItem} from './NavTreeItem';
import {processingStep} from './caseProcessing';
import {caseIdentity} from './casePresentation';
const ui=de.clinicalWorkspace;
import {browserWorkspaceHost,type WorkspaceHost} from './workspaceHost';
import {DocumentCaseFile} from './DocumentCaseFile';
import {selectCaseRange} from './caseSelection';
const t=de.explorer;
const processing=de.caseProcessing;
type Folder={id:string;label:string;color:string;parentId:string|null};
type Organization={folderId:string|null;color:string|null;bookmark:boolean;note:string;confirmed:boolean;dispatched:boolean};
type Case={caseId:string;title:string;queueId:string;canConfirm:boolean;organization:Organization};
type Workspace={revision:number;localSimulation:boolean;folders:Folder[];queueColors:Record<string,string>;cases:Case[];audit:{sequence:number;action:string;caseIds:string[]}[]};
type Dialog='folder'|'move'|'color'|'note'|'confirm'|'dispatch'|'queueColor'|'rename'|'delete';
const outcomes:Record<string,string>={SUPPORTED:de.supported,NOT_SUPPORTED:de.notSupported,INCOMPLETE:de.incomplete,HUMAN_REVIEW:de.review,NOT_APPLICABLE:de.na};
const queueNames:Record<string,string>={...de.workQueues.queues,documents:t.documentReview};
const colorPresets={blue:'#497ab7',teal:'#24708f',green:'#287554',amber:'#b7791f',purple:'#7956a3',rose:'#a54866'};
const defaultColors:Record<string,string>={approval:'#24708f',clarification:'#b7791f',review:'#a54866',technical:'#7956a3',documents:'#497ab7'};

export function CaseExplorer({host=browserWorkspaceHost}:{host?:WorkspaceHost}={}){
 const [workspace,setWorkspace]=useState<Workspace|null>(null),[error,setError]=useState(''),[notice,setNotice]=useState(''),[busy,setBusy]=useState(false);
 const [catalog,setCatalog]=useState<Record<string,{context:{request:string;question:string}|null}>>({});
 const [summaries,setSummaries]=useState<Record<string,{documents:{id:string}[];findings:string[];assessmentJson:string|null}>>({});
 const [extraFilters,setExtraFilters]=useState<Record<string,string>>({});
 const [platformCases,setPlatformCases]=useState(false),[navOpen,setNavOpen]=useState(true),[rightDetail,setRightDetail]=useState(false),[density,setDensity]=useState(28),[showFolder,setShowFolder]=useState(false),[sort,setSort]=useState<'title'|'number'|'status'>('number'),[descending,setDescending]=useState(false),[columnQuery,setColumnQuery]=useState(''),[statusFilter,setStatusFilter]=useState('');
 const searchRef=useRef<HTMLInputElement|null>(null);
 const [query,setQuery]=useState(''),[filter,setFilter]=useState('all'),[selection,setSelection]=useState<string[]>([]),[opened,setOpened]=useState('');
 const [openRevision,setOpenRevision]=useState(0);
 const [openedView,setOpenedView]=useState<'result'|'documents'>('result');
 const [activeCase,setActiveCase]=useState('');
 const [rowColors,setRowColors]=useState(true);
 const [detailOpen,setDetailOpen]=useState(true);
 const [detailPercent,setDetailPercent]=useState(55);
 const selectionAnchor=useRef<string|null>(null),rowRefs=useRef<Record<string,HTMLTableRowElement|null>>({});
 const resizing=useRef(false),explorerContentRef=useRef<HTMLDivElement|null>(null);

 const [coarsePointer]=useState(()=>typeof window!=='undefined'&&window.matchMedia?.('(pointer: coarse)').matches===true);
 const [navMenu,setNavMenu]=useState<{x:number;y:number;id:string;folder:boolean}|null>(null);
 const [subMenu,setSubMenu]=useState<'color'|'folder'|null>(null);
 const [menu,setMenu]=useState<{x:number;y:number;ids:string[]}|null>(null),[dialog,setDialog]=useState<Dialog|null>(null);
 const [targets,setTargets]=useState<string[]>([]),[label,setLabel]=useState(''),[color,setColor]=useState('#497ab7'),[folderId,setFolderId]=useState(''),[parentId,setParentId]=useState(''),[note,setNote]=useState('');
 const menuRef=useRef<HTMLDivElement|null>(null),dialogRef=useRef<HTMLDialogElement|null>(null),openButtons=useRef<Record<string,HTMLButtonElement|null>>({});
 const pending=useRef<AbortController|null>(null),returnFocus=useRef<HTMLElement|null>(null);
 async function refresh(signal?:AbortSignal){const response=await fetch('/api/demo-workspace',{signal});if(!response.ok)throw new Error();setWorkspace(await response.json());}
 useEffect(()=>{const controller=new AbortController();fetch('/api/document-cases',{signal:controller.signal}).then(r=>{if(!r.ok)throw new Error();return r.json();}).then(data=>{if(!controller.signal.aborted)setCatalog(Object.fromEntries(data.cases.map((c:{caseId:string})=>[c.caseId,c])));}).catch(()=>{if(!controller.signal.aborted)setError(de.networkError);});return()=>controller.abort();},[]);
 useEffect(()=>{
  const controller=new AbortController(),ids=Object.keys(catalog);let cursor=0;
  async function worker(){while(cursor<ids.length&&!controller.signal.aborted){const id=ids[cursor++];try{const response=await fetch('/api/document-cases/'+encodeURIComponent(id),{signal:controller.signal});if(!response.ok)throw new Error();const item=await response.json();if(!controller.signal.aborted)setSummaries(old=>({...old,[id]:item}));}catch{if(!controller.signal.aborted)setError(de.networkError);}}}
  Promise.all(Array.from({length:Math.min(4,ids.length)},worker));return()=>controller.abort();
 },[catalog]);
 useEffect(()=>{const controller=new AbortController();refresh(controller.signal).catch(()=>{if(!controller.signal.aborted)setError(de.networkError);});return()=>{controller.abort();pending.current?.abort();};},[]);
 useEffect(()=>{if(!menu)return;menuRef.current?.querySelector<HTMLButtonElement>('button:not(:disabled)')?.focus();const close=(event:PointerEvent)=>{if(!menuRef.current?.contains(event.target as Node))setMenu(null);};document.addEventListener('pointerdown',close);return()=>document.removeEventListener('pointerdown',close);},[menu]);
 useEffect(()=>{if(dialog&&!dialogRef.current?.open)dialogRef.current?.showModal();},[dialog]);
 useEffect(()=>{
  const handle=(event:KeyboardEvent)=>{
   if(document.querySelector('.case-explorer')?.closest('[hidden]'))return;
   if(event.key==='F4'&&!event.altKey&&!event.ctrlKey&&!event.metaKey){
    event.preventDefault();setDetailOpen(open=>!open);
   }
  };
  window.addEventListener('keydown',handle);
  return()=>window.removeEventListener('keydown',handle);
 },[]);
 function resizeDetail(event:React.PointerEvent<HTMLDivElement>){
  if(!resizing.current||!explorerContentRef.current)return;
  const bounds=explorerContentRef.current.getBoundingClientRect();
  const ratio=rightDetail?(bounds.right-event.clientX)/Math.max(1,bounds.width):(bounds.bottom-event.clientY)/Math.max(1,bounds.height);
  setDetailPercent(Math.max(25,Math.min(70,Math.round(ratio*100))));
 }

 useEffect(()=>{if(!notice)return;const timer=window.setTimeout(()=>setNotice(''),3000);return()=>window.clearTimeout(timer);},[notice]);
 function closeDialog(){dialogRef.current?.close();setDialog(null);returnFocus.current?.focus();}
 function begin(kind:Dialog,ids=selection,focus?:HTMLElement){setNavMenu(null);returnFocus.current=focus??document.activeElement as HTMLElement;setMenu(null);setTargets(ids);setLabel('');setNote(workspace?.cases.find(c=>c.caseId===ids[0])?.organization.note??'');setFolderId(workspace?.folders[0]?.id??'');setParentId('');setColor('#497ab7');setDialog(kind);}
 async function command(action:string,ids:string[],extra:Record<string,unknown>={}){
  if(!workspace||busy)return;setBusy(true);setError('');setNotice('');const controller=new AbortController();pending.current=controller;
  try{const response=await fetch('/api/demo-workspace/commands',{method:'POST',signal:controller.signal,headers:{'Content-Type':'application/json'},body:JSON.stringify({action,caseIds:ids,expectedRevision:workspace.revision,operationId:crypto.randomUUID(),...extra})});
   if(!response.ok){let message=de.networkError;try{message=(await response.json()).message??message;}catch{}setError(message);await refresh(controller.signal);return;}
   const result=await response.json();if(controller.signal.aborted)return;setWorkspace(result.workspace);
   if(action==='MOVE'){const remaining=new Set(result.workspace.cases.filter((item:Case)=>matches(item)).map((item:Case)=>item.caseId));setSelection(previous=>previous.filter(id=>remaining.has(id)));if(activeCase&&!remaining.has(activeCase))setActiveCase('');if(opened&&!remaining.has(opened))setOpened('');}
  setNotice(action==='DISPATCH'?t.dispatchedNotice:t.saved);if(dialog)closeDialog();
   if(result.packageJson)host.saveJson('normacase-versand-demo-'+result.workspace.revision+'.json',result.packageJson);
  }catch{if(!controller.signal.aborted)setError(t.completionUnknown);}finally{if(!controller.signal.aborted)setBusy(false);}
 }
 function open(id:string,view:'result'|'documents'='result'){setOpenRevision(value=>value+1);setMenu(null);setActiveCase(id);setOpenedView(view);setOpened(id);}
 function back(){const id=opened;setOpened('');requestAnimationFrame(()=>openButtons.current[id]?.focus());}
 function context(event:React.MouseEvent|React.KeyboardEvent,id:string){event.preventDefault();if(!selection.includes(id))selectionAnchor.current=id;setSubMenu(null);const ids=selection.includes(id)?selection:[id];setActiveCase(id);setSelection(ids);returnFocus.current=event.currentTarget as HTMLElement;const box=event.currentTarget.getBoundingClientRect();const mouse='clientX' in event;setMenu({x:Math.max(8,Math.min(mouse?event.clientX:box.left+20,window.innerWidth-280)),y:Math.max(8,Math.min(mouse?event.clientY:box.top+20,window.innerHeight-380)),ids});}
 const folders=[...(workspace?.folders??[])].sort((a,b)=>a.label.localeCompare(b.label,'de-DE',{sensitivity:'base'})||a.id.localeCompare(b.id)),cases=workspace?.cases??[],selected=cases.filter(c=>selection.includes(c.caseId));
 const orderedFolders:(Folder&{depth:number})[]=[];
 function appendFolders(parent:string|null,depth:number){for(const folder of folders.filter(f=>(f.parentId??null)===parent)){orderedFolders.push({...folder,depth});appendFolders(folder.id,depth+1);}}
 appendFolders(null,0);
 const groups=['approval','clarification','review','technical','documents'];
 function resultLabel(c:Case){const record=summaries[c.caseId];return record?record.assessmentJson?(outcomes[(parse(record.assessmentJson) as {assessment:{outcome:string}}).assessment.outcome]??de.unknown):ui.noTechnical:de.unknown;}
 function nextStep(c:Case){return c.organization.dispatched?processing.completeNext:c.organization.confirmed?processing.confirmedNext:(ui.nextSteps as Record<string,string>)[c.queueId]??de.unknown;}
 function extraText(c:Case,key:string){return key==='number'?caseIdentity(c.caseId,c.title).number:key==='question'?catalog[c.caseId]?.context?.request??ui.noTechnical:key==='next'?nextStep(c):key==='result'?resultLabel(c):key==='documents'?String(summaries[c.caseId]?.documents.length??''):folders.find(f=>f.id===c.organization.folderId)?.label??'';}
 function matches(c:Case){return Object.entries(extraFilters).every(([key,value])=>extraText(c,key).toLocaleLowerCase('de-DE').includes(value.toLocaleLowerCase('de-DE')))&& (platformCases||c.caseId.startsWith('reference-'))&&(!statusFilter||c.queueId===statusFilter)&&caseIdentity(c.caseId,c.title).subject.toLocaleLowerCase('de-DE').includes(columnQuery.toLocaleLowerCase('de-DE'))&&(filter==='all'&&!c.organization.folderId||filter==='bookmarks'&&c.organization.bookmark||filter==='confirmed'&&c.organization.confirmed&&!c.organization.dispatched||filter==='dispatched'&&c.organization.dispatched||filter==='unfiled'&&!c.organization.folderId||filter===c.queueId||filter===c.organization.folderId)&&(c.caseId+' '+c.title+' '+c.organization.note).toLocaleLowerCase('de-DE').includes(query.toLocaleLowerCase('de-DE'));}
 const visible=cases.filter(matches).sort((a,b)=>{const left=caseIdentity(a.caseId,a.title),right=caseIdentity(b.caseId,b.title);const compared=(sort==='title'?left.subject:sort==='status'?a.queueId:left.number).localeCompare(sort==='title'?right.subject:sort==='status'?b.queueId:right.number,'de-DE',{numeric:true});return (descending?-compared:compared)||a.caseId.localeCompare(b.caseId);});
 function selectRow(id:string,range:boolean,toggle:boolean){
  if(busy)return;
  if(range){
   const ids=selectCaseRange(visible.map(c=>c.caseId),selectionAnchor.current,id);
   if(ids===null){setNotice(t.selectionLimit);return;}
   setSelection(ids);if(!selectionAnchor.current||!visible.some(c=>c.caseId===selectionAnchor.current))selectionAnchor.current=id;
  }else{
   selectionAnchor.current=id;
   setSelection(previous=>toggle?(previous.includes(id)?previous.filter(value=>value!==id):previous.length<100?[...previous,id]:previous):[id]);
  }
 }
 function rowKey(event:React.KeyboardEvent<HTMLTableRowElement>,id:string){
  if(event.target!==event.currentTarget)return;
  if(event.key==='ContextMenu'||event.shiftKey&&event.key==='F10'){context(event,id);return;}
  if(event.key==='Enter'){event.preventDefault();open(id);return;}
  if(!['ArrowUp','ArrowDown','Home','End'].includes(event.key)||event.altKey||event.ctrlKey||event.metaKey)return;
  event.preventDefault();
  const index=visible.findIndex(c=>c.caseId===id);
  const next=event.key==='Home'?0:event.key==='End'?visible.length-1:Math.max(0,Math.min(visible.length-1,index+(event.key==='ArrowDown'?1:-1)));
  const target=visible[next];if(!target)return;
  if(event.shiftKey&&!selectionAnchor.current)selectionAnchor.current=id;
  selectRow(target.caseId,event.shiftKey,false);setActiveCase(target.caseId);rowRefs.current[target.caseId]?.focus();
 }
 const canConfirm=(items:Case[])=>items.length>0&&items.every(c=>c.canConfirm&&!c.organization.confirmed&&!c.organization.dispatched);
 const canDispatch=(items:Case[])=>items.length>0&&items.every(c=>c.organization.confirmed&&!c.organization.dispatched);
 const current=cases.find(c=>c.caseId===opened);
 const focused=cases.find(c=>c.caseId===activeCase);
 const status=(c:Case)=>c.organization.dispatched?t.dispatched:c.organization.confirmed?t.confirmed:queueNames[c.queueId]??de.unknown;
 const caseColor=(c:Case)=>c.organization.color??workspace?.queueColors[c.queueId]??defaultColors[c.queueId];
 const actionTargets=opened?[opened]:selection;
 const actionCases=cases.filter(c=>actionTargets.includes(c.caseId));
 useEffect(()=>{
  function action(event:Event){
   const name=(event as CustomEvent<string>).detail;
   if(name==='open'&&focused)open(focused.caseId);
   if(name==='copy-id'||name==='copy-result'){const region=document.getElementById(opened?'full-case-copy':'inline-case-copy');const label=name==='copy-id'?ui.copyId:ui.copyResult;Array.from(region?.querySelectorAll<HTMLButtonElement>('button')??[]).find(b=>b.textContent===label)?.click();}
   if(name==='folder')begin('folder',[]);
   if(name==='move'&&actionTargets.length)begin('move',actionTargets);
   if(name==='color'&&actionTargets.length)begin('color',actionTargets);
   if(name==='bookmark'&&actionTargets.length)command('BOOKMARK',actionTargets,{value:!actionCases.every(c=>c.organization.bookmark)});
   if(name==='confirm'&&canConfirm(actionCases))begin('confirm',actionTargets);
   if(name==='dispatch'&&canDispatch(actionCases))begin('dispatch',actionTargets);
   if(name==='select-all'){setSelection(visible.slice(0,100).map(c=>c.caseId));}
   if(name==='clear'){selectionAnchor.current=null;setSelection([]);}
   if(name==='detail')setDetailOpen(v=>!v);
   if(name==='orientation')setRightDetail(v=>!v);
   if(name==='navigation')setNavOpen(v=>!v);
   if(name==='density')setDensity(v=>v===28?24:v===24?34:28);
   if(name==='columns')setShowFolder(v=>!v);
   if(name==='refresh')refresh().catch(()=>setError(de.networkError));
   if(name==='reset'){setQuery('');setColumnQuery('');setExtraFilters({});setStatusFilter('');setFilter('all');setDensity(28);setDetailPercent(55);setNavOpen(true);setRightDetail(false);setShowFolder(false);setSort('number');setDescending(false);}
  }
  function key(event:KeyboardEvent){
   if(document.querySelector('.case-explorer')?.closest('[hidden]'))return;
   if((event.ctrlKey||event.metaKey)&&event.key.toLowerCase()==='k'){event.preventDefault();searchRef.current?.focus();}
   if(event.key==='F5'){event.preventDefault();action(new CustomEvent('action',{detail:'refresh'}));}
   const editing=(event.target as HTMLElement)?.closest('input,textarea,select,[contenteditable=true]');
   if(!editing&&!dialog&&!menu){
    if((event.ctrlKey||event.metaKey)&&event.key.toLowerCase()==='a'){event.preventDefault();action(new CustomEvent('action',{detail:'select-all'}));}
    if(event.key==='Escape')action(new CustomEvent('action',{detail:'clear'}));
    if(event.altKey&&event.key==='ArrowLeft'&&opened){event.preventDefault();back();}
   }
  }
  window.addEventListener('normacase-workspace-action',action);window.addEventListener('keydown',key);
  return()=>{window.removeEventListener('normacase-workspace-action',action);window.removeEventListener('keydown',key);};
 });
 useEffect(()=>{window.dispatchEvent(new CustomEvent('normacase-workspace-capabilities',{detail:{hasSelection:actionTargets.length>0,hasVisible:visible.length>0,focused:!!focused,canConfirm:canConfirm(actionCases)&&!busy,canDispatch:canDispatch(actionCases)&&!busy,canCopy:!!focused&&!!summaries[focused.caseId],canCopyResult:!!focused&&!!summaries[focused.caseId]?.assessmentJson,busy}}));},[workspace,selection,opened,activeCase,query,columnQuery,statusFilter,extraFilters,filter,platformCases,summaries,busy]);
 function sortBy(value:'title'|'number'|'status'){if(sort===value)setDescending(v=>!v);else{setSort(value);setDescending(false);}}
 function processingActions(item:Case){const step=processingStep(item);return <section className="case-processing" aria-label={processing.heading}>
  <div><strong>{processing.heading}</strong><p>{processing[step]}</p></div>
  <div className="case-processing-actions">
   <button type="button" className={step==='confirm'?'primary':'secondary'} disabled={busy||!canConfirm([item])} title={step==='blocked'?processing.blocked:undefined} onClick={()=>begin('confirm',[item.caseId])}><WorkspaceIcon name="confirmed"/>{processing.confirmAction}</button>
   <button type="button" className={step==='dispatch'?'primary':'secondary'} disabled={busy||!canDispatch([item])} onClick={()=>begin('dispatch',[item.caseId])}><WorkspaceIcon name="dispatch"/>{processing.dispatchAction}</button>
   {step==='blocked'&&<button type="button" className="secondary" onClick={()=>open(item.caseId,'result')}>{processing.openResult}</button>}
  </div>
 </section>;}
 const toolbar=<div className="compact-workplace-toolbar" role="toolbar" aria-label="Fallaktionen">
  <button type="button" className="secondary" onClick={()=>begin('folder',[])}><WorkspaceIcon name="folder"/>{t.newFolder}</button>
  <button type="button" className="secondary" disabled={!focused} onClick={()=>focused&&open(focused.caseId)}><WorkspaceIcon name="open"/>{ui.open}</button>
  <button type="button" className="secondary" onClick={()=>refresh().catch(()=>setError(de.networkError))} title={ui.refresh} aria-label={ui.refresh}><WorkspaceIcon name="refresh"/></button>
  <span className="toolbar-separator"/>
  <button type="button" className="secondary" title={!actionTargets.length?ui.noSelection:undefined} disabled={!actionTargets.length||busy} onClick={()=>begin('move',actionTargets)}><WorkspaceIcon name="folder"/>{t.moveSelection}{!!actionTargets.length&&<small> ({actionTargets.length})</small>}</button>
  <button type="button" className="secondary" title={!actionTargets.length?ui.noSelection:undefined} disabled={!actionTargets.length||busy} onClick={()=>begin('color',actionTargets)}><WorkspaceIcon name="mark"/>{ui.mark}</button>
  <button type="button" className="secondary" title={!actionTargets.length?ui.noSelection:undefined} disabled={!actionTargets.length||busy} onClick={()=>command('BOOKMARK',actionTargets,{value:!actionCases.every(c=>c.organization.bookmark)})}><WorkspaceIcon name="bookmark"/>{ui.bookmark}</button>
  <button type="button" className="secondary" disabled={!canConfirm(actionCases)||busy} onClick={()=>begin('confirm',actionTargets)}><WorkspaceIcon name="confirmed"/>{t.confirmSelection}</button>
  <button type="button" className="secondary" disabled={!canDispatch(actionCases)||busy} onClick={()=>begin('dispatch',actionTargets)}><WorkspaceIcon name="dispatch"/>{t.dispatchSelection}</button>
  <button type="button" className="secondary" aria-pressed={detailOpen} onClick={()=>setDetailOpen(!detailOpen)}>{detailOpen?"Details ausblenden (F4)":"Details anzeigen (F4)"}</button>
  <label className="compact-color-toggle"><input type="checkbox" checked={rowColors} onChange={e=>setRowColors(e.target.checked)}/> Zeilenfärbung</label>
 </div>;
 const menuCases=cases.filter(c=>menu?.ids.includes(c.caseId));
 return <section className="card case-explorer case-first" aria-label={t.heading} style={{"--row-height":density+"px"} as CSSProperties}>
 {document.getElementById("workspace-search-slot")&&createPortal(<input ref={searchRef} aria-label={t.search} value={query} onChange={e=>setQuery(e.target.value)} placeholder={ui.searchPlaceholder}/>,document.getElementById("workspace-search-slot")!)}
 {toolbar}
  {error&&<p className="error" role="alert">{error}</p>}{notice&&<p className="explorer-notice" role="status">{notice}</p>}
<div className={"explorer-grid"+(!navOpen?" navigation-hidden":"")}><div className="explorer-nav-resize" role="separator" aria-label="Explorerbreite" aria-orientation="vertical" tabIndex={0} onKeyDown={e=>{if(e.key==='ArrowLeft'||e.key==='ArrowRight'){e.preventDefault();e.currentTarget.parentElement?.style.setProperty('--nav-width',Math.max(200,Math.min(360,(parseFloat(getComputedStyle(e.currentTarget.parentElement!).getPropertyValue('--nav-width'))||240)+(e.key==='ArrowRight'?10:-10)))+'px');}}} onPointerDown={e=>e.currentTarget.setPointerCapture(e.pointerId)} onPointerMove={e=>{if(e.currentTarget.hasPointerCapture(e.pointerId))e.currentTarget.parentElement?.style.setProperty('--nav-width',Math.max(200,Math.min(360,e.clientX))+'px');}}/><aside className="explorer-tree" aria-label={t.tree}>
    <NavTreeItem label={t.all} count={cases.filter(c=>(platformCases||c.caseId.startsWith('reference-'))&&!c.organization.folderId).length} selected={filter==='all'} onSelect={()=>setFilter('all')}/>
    <h3>{t.queues}</h3>{groups.map(id=><NavTreeItem key={id} label={queueNames[id]} count={cases.filter(c=>c.queueId===id&&(platformCases||c.caseId.startsWith('reference-'))).length} selected={filter===id} color={workspace?.queueColors[id]??defaultColors[id]} onSelect={()=>setFilter(id)} onContext={e=>{e.preventDefault();const bounds=e.currentTarget.getBoundingClientRect();setNavMenu({x:bounds.right,y:bounds.top,id,folder:false});}}/>)}
    <p className="processing-list-help">{processing.listsHelp}</p><h3>{t.personal}<button className="text-button" aria-label="Neuer Ordner in meiner Ablage" title={t.newFolder} onClick={()=>begin('folder',[])}>+</button></h3>
    <NavTreeItem kind="bookmark" color="#d4a017" label={t.bookmarks} count={cases.filter(c=>c.organization.bookmark&&(platformCases||c.caseId.startsWith('reference-'))).length} selected={filter==='bookmarks'} onSelect={()=>setFilter('bookmarks')}/>
    <NavTreeItem kind="confirmed" color="#2e9e8f" label={t.confirmed} count={cases.filter(c=>c.organization.confirmed&&!c.organization.dispatched&&(platformCases||c.caseId.startsWith('reference-'))).length} selected={filter==='confirmed'} onSelect={()=>setFilter('confirmed')}/>
    <NavTreeItem kind="dispatch" label={t.dispatched} count={cases.filter(c=>c.organization.dispatched&&(platformCases||c.caseId.startsWith('reference-'))).length} selected={filter==='dispatched'} onSelect={()=>setFilter('dispatched')}/>
    {orderedFolders.map(f=><div key={f.id} className="custom-folder" style={{paddingLeft:f.depth*15}}><NavTreeItem kind="folder" label={f.label} count={cases.filter(c=>c.organization.folderId===f.id&&(platformCases||c.caseId.startsWith('reference-'))).length} selected={filter===f.id} color={f.color} onSelect={()=>setFilter(f.id)} onContext={e=>{e.preventDefault();const bounds=e.currentTarget.getBoundingClientRect();setNavMenu({x:bounds.right,y:bounds.top,id:f.id,folder:true});}}/><button type="button" className="text-button" title="Unterordner anlegen" aria-label={'Unterordner in '+f.label+' anlegen'} onClick={()=>{begin('folder',[]);setParentId(f.id);}}>＋</button><button type="button" className="text-button" aria-label={t.editFolder+': '+f.label} onClick={()=>{begin('rename',[]);setFolderId(f.id);setLabel(f.label);setColor(f.color);}}>⋯</button></div>)}
    <NavTreeItem kind="folder" label={t.unfiled} count={cases.filter(c=>!c.organization.folderId&&(platformCases||c.caseId.startsWith('reference-'))).length} selected={filter==='unfiled'} onSelect={()=>setFilter('unfiled')}/>
    <label className="platform-toggle"><input type="checkbox" checked={platformCases} onChange={e=>setPlatformCases(e.target.checked)}/>{ui.platformCases}</label>
   </aside><div ref={explorerContentRef} className={"explorer-content"+(rightDetail?" detail-right":"")}>{opened?<div className="opened-case"><header className="case-header"><button type="button" className="secondary" onClick={back}>{de.workspace.back}</button><h2>{current&&caseIdentity(opened,current.title).person+' · '+caseIdentity(opened,current.title).subject}</h2><span className="case-identifier">{current&&caseIdentity(opened,current.title).number}</span>{current&&<span className="opened-case-status" role="status">{status(current)}</span>}<div id="full-case-copy"/></header>{current&&processingActions(current)}<DocumentCaseFile key={opened+':'+openRevision} caseId={opened} initialView={openedView} showAssessment copyTargetId="full-case-copy"/></div>:<>

    <div className="explorer-table-scroll"><table className={"explorer-table"+(rowColors?" status-row-colors":"")}><thead><tr><th><input type="checkbox" aria-label={t.selectVisible} checked={visible.length>0&&visible.slice(0,100).every(c=>selection.includes(c.caseId))} disabled={!visible.length||busy} onChange={e=>setSelection(e.target.checked?visible.slice(0,100).map(c=>c.caseId):[])}/></th><th aria-sort={sort==='title'?(descending?'descending':'ascending'):'none'}><button onClick={()=>sortBy('title')}>{ui.subject}</button></th><th aria-sort={sort==='number'?(descending?'descending':'ascending'):'none'}><button onClick={()=>sortBy('number')}>{ui.number}</button></th><th aria-sort={sort==='status'?(descending?'descending':'ascending'):'none'}><button onClick={()=>sortBy('status')}>{ui.status}</button></th><th>{ui.question}</th><th>{ui.next}</th><th>{ui.result}</th><th>{ui.documents}</th>{showFolder&&<th>{t.folder}</th>}</tr><tr className="table-filter-row"><th/><th><input aria-label={ui.subjectFilter} placeholder={ui.contains} value={columnQuery} onChange={e=>setColumnQuery(e.target.value)}/></th><th><input aria-label={ui.number+ui.filterSuffix} placeholder={ui.contains} value={extraFilters.number??''} onChange={e=>setExtraFilters({...extraFilters,number:e.target.value})}/></th><th><select aria-label={ui.statusFilter} value={statusFilter} onChange={e=>setStatusFilter(e.target.value)}><option value="">{ui.all}</option>{groups.map(id=><option key={id} value={id}>{queueNames[id]}</option>)}</select></th>{[['question',ui.question],['next',ui.next],['result',ui.result],['documents',ui.documents],...(showFolder?[['folder',t.folder]]:[])].map(([key,label])=><th key={key}><input aria-label={label+ui.filterSuffix} placeholder={ui.contains} value={extraFilters[key]??''} onChange={e=>setExtraFilters({...extraFilters,[key]:e.target.value})}/></th>)}</tr></thead><tbody>{visible.map(c=><tr key={c.caseId} ref={node=>{rowRefs.current[c.caseId]=node;}} tabIndex={0} aria-selected={selection.includes(c.caseId)} style={{'--case-color':caseColor(c)} as CSSProperties} data-status={c.organization.dispatched?'complete':c.organization.confirmed?'complete':c.queueId} onClick={e=>{setActiveCase(c.caseId);if((e.target as HTMLElement).closest("button,input"))return;selectRow(c.caseId,e.shiftKey,e.ctrlKey||e.metaKey);e.currentTarget.focus();}} onDoubleClick={()=>open(c.caseId)} onContextMenu={e=>context(e,c.caseId)} onKeyDown={e=>rowKey(e,c.caseId)}>
     <td className="case-selection-cell"><input type="checkbox" aria-label={t.selectCase+': '+c.caseId} checked={selection.includes(c.caseId)} disabled={busy} onChange={()=>selectRow(c.caseId,false,true)}/>{c.organization.color&&<span className="personal-mark-chip" style={{backgroundColor:c.organization.color}} aria-label="Eigene Farbmarkierung"/>}</td>
     <td><button ref={node=>{openButtons.current[c.caseId]=node;}} type="button" className="case-title-button" aria-label={de.workQueues.select+': '+c.caseId} onClick={()=>open(c.caseId)}><span className="case-bookmark-slot">{c.organization.bookmark&&<span aria-label={t.bookmarked}>☆</span>}</span><span className="case-label">{caseIdentity(c.caseId,c.title).person&&caseIdentity(c.caseId,c.title).person+' · '}{caseIdentity(c.caseId,c.title).subject}</span></button>{c.organization.note&&<small className="case-note">{c.organization.note}</small>}</td><td className="case-identifier" title={c.caseId}>{caseIdentity(c.caseId,c.title).number}<span className="visually-hidden">{c.caseId}</span></td><td><span className="queue-badge">{status(c)}</span></td><td title={catalog[c.caseId]?.context?.question}>{catalog[c.caseId]?.context?.request??ui.noTechnical}</td><td>{nextStep(c)}{!!summaries[c.caseId]?.findings.length&&<span title={summaries[c.caseId].findings.join(' · ')}> · {summaries[c.caseId].findings.length} {ui.openPoints}</span>}</td><td>{resultLabel(c)}</td><td>{summaries[c.caseId]?.documents.length??''}</td>{showFolder&&<td>{folders.find(f=>f.id===c.organization.folderId)?.label??''}</td>}
    </tr>)}</tbody></table>{!workspace?<p role="status">{de.documents.loading}</p>:!visible.length&&<p>{t.noCases}</p>}</div>
    {detailOpen&&<>
     <div className="explorer-pane-splitter" role="separator" aria-label="Fallliste und Detailbereich vergrößern oder verkleinern" aria-orientation={rightDetail?"vertical":"horizontal"} aria-valuemin={25} aria-valuemax={70} aria-valuenow={detailPercent} tabIndex={0}
      onPointerDown={event=>{resizing.current=true;event.currentTarget.setPointerCapture(event.pointerId);}}
      onPointerMove={resizeDetail}
      onPointerUp={event=>{resizing.current=false;if(event.currentTarget.hasPointerCapture(event.pointerId))event.currentTarget.releasePointerCapture(event.pointerId);}}
      onPointerCancel={()=>{resizing.current=false;}}
      onKeyDown={event=>{if(event.key==='ArrowUp'||event.key==='ArrowDown'){event.preventDefault();setDetailPercent(value=>Math.max(25,Math.min(70,value+(event.key==='ArrowUp'?5:-5))));}}}>
      <span aria-hidden="true">⋮⋮</span>
     </div>
     <section className="explorer-detail-pane" style={{flexBasis:detailPercent+'%'}} aria-label="Fallakte und Dokumente">
      {focused?<><header className="explorer-detail-heading"><strong title={focused.title}>{caseIdentity(focused.caseId,focused.title).person+' · '+caseIdentity(focused.caseId,focused.title).subject}</strong><span className="case-identifier">{caseIdentity(focused.caseId,focused.title).number}</span><span className="opened-case-status">{status(focused)}</span><div className="explorer-detail-actions">
       <button type="button" className="secondary" onClick={()=>open(focused.caseId,'documents')}>Vollansicht</button>
      </div><div id="inline-case-copy"/></header>
      {processingActions(focused)}<DocumentCaseFile key={focused.caseId} caseId={focused.caseId} initialView="documents" showAssessment copyTargetId="inline-case-copy"/>
      </>:<p className="explorer-detail-empty">Fall in der Tabelle auswählen, um Dokumente und Prüfergebnis hier anzuzeigen.</p>}
     </section>
    </>}
   </>}</div></div>
  <footer className="explorer-footer" role="status"><span>{visible.length} von {cases.filter(c=>platformCases||c.caseId.startsWith('reference-')).length} Fällen · {selection.length} ausgewählt · {filter==='all'&&!query&&!columnQuery&&!statusFilter&&!Object.values(extraFilters).some(Boolean)?'Kein Filter':'Filter aktiv'}{(query||columnQuery||statusFilter||Object.values(extraFilters).some(Boolean)||filter!=='all')&&<button onClick={()=>{setQuery('');setColumnQuery('');setExtraFilters({});setStatusFilter('');setFilter('all');}}>{ui.resetFilters}</button>}</span><span title={ui.session}>{ui.clinicalScope}</span></footer>
  {navMenu&&<div className="case-context-menu" role="menu" aria-label="Exploreraktionen" style={{left:Math.min(navMenu.x,window.innerWidth-250),top:Math.min(navMenu.y,window.innerHeight-120)}} onKeyDown={e=>{if(e.key==='Escape')setNavMenu(null);}} onBlur={e=>{if(!e.currentTarget.contains(e.relatedTarget))setNavMenu(null);}} ref={element=>element?.querySelector<HTMLButtonElement>('button')?.focus()}>
   <button role="menuitem" onClick={()=>{const item=navMenu;if(item.folder){const f=folders.find(f=>f.id===item.id);begin('rename',[]);setFolderId(item.id);setLabel(f?.label??'');setColor(f?.color??'#497ab7');}else{begin('queueColor',[]);setFolderId(item.id);setColor(workspace?.queueColors[item.id]??defaultColors[item.id]);}}}>{navMenu.folder?ui.folderEdit:ui.queueColor}</button>
  </div>}
  {menu&&<div ref={menuRef} className={"case-context-menu"+(menu.x>window.innerWidth-530?" submenu-to-left":"")} role="menu" aria-label={t.caseActions} style={{left:menu.x,top:menu.y}} onKeyDown={e=>{if(e.key==='Escape'){setMenu(null);returnFocus.current?.focus();}if(e.key==='Tab')setMenu(null);if(['ArrowDown','ArrowUp','Home','End'].includes(e.key)){e.preventDefault();const buttons=Array.from(menuRef.current?.querySelectorAll<HTMLButtonElement>('button:not(:disabled)')??[]);const i=buttons.indexOf(document.activeElement as HTMLButtonElement);buttons[e.key==='Home'?0:e.key==='End'?buttons.length-1:(i+(e.key==='ArrowDown'?1:buttons.length-1))%buttons.length]?.focus();}}}>
    <button type="button" role="menuitem" onClick={()=>open(menu.ids[0])}>{de.workQueues.select}</button>
    <div className={"context-submenu"+(coarsePointer?" touch-submenu":"")}><button type="button" role="menuitem" aria-haspopup="true" aria-expanded={coarsePointer?subMenu==='color':undefined} onClick={()=>setSubMenu(prev=>prev==='color'?null:'color')}>Markieren <span aria-hidden="true">›</span></button><div className={"context-submenu-panel"+(coarsePointer&&subMenu==='color'?" is-open":"")} role="group" aria-label="Farbmarkierung">{Object.entries(colorPresets).map(([name,hex])=><button key={name} type="button" onClick={()=>{const ids=menu.ids;setMenu(null);command('COLOR',ids,{color:hex});}}><span className="color-swatch" style={{backgroundColor:hex}}/>{t.colors[name as keyof typeof t.colors]}</button>)}<label className="menu-custom-color">Eigene Farbe<input type="color" aria-label="Eigene Markierungsfarbe" defaultValue="#497ab7" onChange={e=>{const ids=menu.ids;setMenu(null);command('COLOR',ids,{color:e.target.value});}}/></label><button type="button" onClick={()=>{const ids=menu.ids;setMenu(null);command('COLOR',ids,{color:null});}}>Markierung entfernen</button></div></div>
    <div className={"context-submenu"+(coarsePointer?" touch-submenu":"")}><button type="button" role="menuitem" aria-haspopup="true" aria-expanded={coarsePointer?subMenu==='folder':undefined} onClick={()=>setSubMenu(prev=>prev==='folder'?null:'folder')}>{processing.moveTo} <span aria-hidden="true">›</span></button><div className={"context-submenu-panel"+(coarsePointer&&subMenu==='folder'?" is-open":"")} role="group" aria-label="Ordner wählen">{folders.map(f=><button type="button" key={f.id} onClick={()=>{const ids=menu.ids;setMenu(null);command('MOVE',ids,{folderId:f.id});}}><span className="color-swatch" style={{backgroundColor:f.color}}/>{f.label}</button>)}<button type="button" onClick={()=>begin('folder',[])}>{t.newFolder}</button></div></div>
    <button type="button" role="menuitem" disabled={busy} onClick={()=>{const ids=menu.ids;setMenu(null);command('BOOKMARK',ids,{value:!menuCases.every(c=>c.organization.bookmark)});}}>{menuCases.every(c=>c.organization.bookmark)?t.removeBookmark:t.addBookmark}</button>
    <button type="button" role="menuitem" disabled={busy||!menuCases.some(c=>c.organization.folderId)} onClick={()=>{const ids=menu.ids;setMenu(null);command('MOVE',ids,{folderId:null});}}>{t.removeFromFolder}</button><button type="button" role="menuitem" disabled={menu.ids.length!==1} onClick={()=>begin('note',menu.ids)}>{t.note}</button>
    <button type="button" role="menuitem" disabled={busy||!canConfirm(menuCases)} onClick={()=>begin('confirm',menu.ids)}>{t.confirmCase}</button><button type="button" role="menuitem" disabled={busy||!canDispatch(menuCases)} onClick={()=>begin('dispatch',menu.ids)}>{t.dispatchCases}</button>
  </div>}
  {dialog&&<dialog ref={dialogRef} className="explorer-dialog" aria-label={t.dialogTitles[dialog]} onCancel={e=>{if(busy)e.preventDefault();else closeDialog();}}><form onSubmit={e=>{e.preventDefault();if(dialog==='folder'||dialog==='rename')command(dialog==='folder'?'CREATE_FOLDER':'UPDATE_FOLDER',[],{folderId:dialog==='rename'?folderId:null,label,color,parentId:dialog==='folder'?parentId||null:null});else if(dialog==='delete')command('DELETE_FOLDER',[],{folderId});else if(dialog==='move')command('MOVE',targets,{folderId:folderId||null});else if(dialog==='color')command('COLOR',targets,{color});else if(dialog==='queueColor')command('QUEUE_COLOR',[],{folderId,color});else if(dialog==='note')command('NOTE',targets,{note});else command(dialog==='confirm'?'CONFIRM':'DISPATCH',targets);}}>
   <h2>{t.dialogTitles[dialog]}</h2>
   {dialog==='folder'&&<label className="field">Übergeordneter Ordner<select value={parentId} onChange={e=>setParentId(e.target.value)}><option value="">Meine Ablage</option>{orderedFolders.filter(f=>f.depth<4).map(f=><option key={f.id} value={f.id}>{'— '.repeat(f.depth)+f.label}</option>)}</select></label>}
   {(dialog==='folder'||dialog==='rename')&&<label className="field">{t.folderName}<input autoFocus required maxLength={80} value={label} onChange={e=>setLabel(e.target.value)}/></label>}
   {['folder','rename','color','queueColor'].includes(dialog)&&<label className="field">{dialog==='folder'||dialog==='rename'?t.folderColor:t.markColor}<input type="color" value={color} onChange={e=>setColor(e.target.value)}/></label>}
   {['folder','rename','color','queueColor'].includes(dialog)&&<div className="color-presets" role="group" aria-label={t.colorPresets}>{Object.entries(colorPresets).map(([key,value])=><button key={key} type="button" aria-pressed={color===value} onClick={()=>setColor(value)}><span style={{backgroundColor:value}} aria-hidden="true"/>{t.colors[key as keyof typeof t.colors]}</button>)}</div>}
   {dialog==='move'&&<label className="field">{t.targetFolder}<select autoFocus value={folderId} onChange={e=>setFolderId(e.target.value)}><option value="">{t.unfiled}</option>{folders.map(f=><option key={f.id} value={f.id}>{f.label}</option>)}</select></label>}
   {dialog==='note'&&<label className="field">{t.note}<textarea autoFocus maxLength={1000} value={note} onChange={e=>setNote(e.target.value)}/></label>}
   {dialog==='confirm'&&<p>{processing.confirmationHelp} ({targets.length} {t.cases})</p>}{dialog==='dispatch'&&<p>{processing.dispatchHelp} ({targets.length} {t.cases})</p>}{dialog==='delete'&&<p>{t.deleteHelp}</p>}
   <div className="actions"><button type="submit" className="primary" disabled={busy}>{dialog==='folder'||dialog==='rename'?t.saveFolder:dialog==='move'?t.move:dialog==='confirm'?t.confirm:dialog==='dispatch'?t.simulateDispatch:dialog==='delete'?t.delete:t.save}</button><button type="button" className="secondary" disabled={busy} onClick={closeDialog}>{t.cancel}</button>{dialog==='rename'&&<button type="button" className="text-button" onClick={()=>setDialog('delete')}>{t.deleteFolder}</button>}{dialog==='color'&&<button type="button" className="text-button" disabled={busy} onClick={()=>command('COLOR',targets,{color:null})}>{t.clearColor}</button>}</div>
  </form></dialog>}
 </section>;
}
