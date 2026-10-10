import {useEffect,useRef,useState,type CSSProperties} from 'react';
import de from './de.json';
import {browserWorkspaceHost,type WorkspaceHost} from './workspaceHost';
import {DocumentCaseFile} from './DocumentCaseFile';
const t=de.explorer;
type Folder={id:string;label:string;color:string;parentId:string|null};
type Organization={folderId:string|null;color:string|null;bookmark:boolean;note:string;confirmed:boolean;dispatched:boolean};
type Case={caseId:string;title:string;queueId:string;canConfirm:boolean;organization:Organization};
type Workspace={revision:number;localSimulation:boolean;folders:Folder[];queueColors:Record<string,string>;cases:Case[];audit:{sequence:number;action:string;caseIds:string[]}[]};
type Dialog='folder'|'move'|'color'|'note'|'confirm'|'dispatch'|'queueColor'|'rename'|'delete';
const queueNames:Record<string,string>={...de.workQueues.queues,documents:t.documentReview};
const colorPresets={blue:'#497ab7',teal:'#24708f',green:'#287554',amber:'#b7791f',purple:'#7956a3',rose:'#a54866'};
const defaultColors:Record<string,string>={approval:'#24708f',clarification:'#b7791f',review:'#a54866',technical:'#7956a3',documents:'#497ab7'};

export function CaseExplorer({host=browserWorkspaceHost}:{host?:WorkspaceHost}={}){
 const [workspace,setWorkspace]=useState<Workspace|null>(null),[error,setError]=useState(''),[notice,setNotice]=useState(''),[busy,setBusy]=useState(false);
 const [query,setQuery]=useState(''),[filter,setFilter]=useState('all'),[selection,setSelection]=useState<string[]>([]),[opened,setOpened]=useState('');
 const [openedView,setOpenedView]=useState<'result'|'documents'>('result');
 const [activeCase,setActiveCase]=useState('');
 const [rowColors,setRowColors]=useState(true);
 const [detailOpen,setDetailOpen]=useState(true);
 const [detailPercent,setDetailPercent]=useState(45);
 const resizing=useRef(false),explorerContentRef=useRef<HTMLDivElement|null>(null);
 const [moreActions,setMoreActions]=useState(false);
 const [coarsePointer]=useState(()=>typeof window!=='undefined'&&window.matchMedia?.('(pointer: coarse)').matches===true);
 const [subMenu,setSubMenu]=useState<'color'|'folder'|null>(null);
 const [menu,setMenu]=useState<{x:number;y:number;ids:string[]}|null>(null),[dialog,setDialog]=useState<Dialog|null>(null);
 const [targets,setTargets]=useState<string[]>([]),[label,setLabel]=useState(''),[color,setColor]=useState('#497ab7'),[folderId,setFolderId]=useState(''),[parentId,setParentId]=useState(''),[note,setNote]=useState('');
 const menuRef=useRef<HTMLDivElement|null>(null),dialogRef=useRef<HTMLDialogElement|null>(null),openButtons=useRef<Record<string,HTMLButtonElement|null>>({});
 const pending=useRef<AbortController|null>(null),returnFocus=useRef<HTMLElement|null>(null);
 async function refresh(signal?:AbortSignal){const response=await fetch('/api/demo-workspace',{signal});if(!response.ok)throw new Error();setWorkspace(await response.json());}
 useEffect(()=>{const controller=new AbortController();refresh(controller.signal).catch(()=>{if(!controller.signal.aborted)setError(de.networkError);});return()=>{controller.abort();pending.current?.abort();};},[]);
 useEffect(()=>{if(!menu)return;menuRef.current?.querySelector<HTMLButtonElement>('button:not(:disabled)')?.focus();const close=(event:PointerEvent)=>{if(!menuRef.current?.contains(event.target as Node))setMenu(null);};document.addEventListener('pointerdown',close);return()=>document.removeEventListener('pointerdown',close);},[menu]);
 useEffect(()=>{if(dialog&&!dialogRef.current?.open)dialogRef.current?.showModal();},[dialog]);
 useEffect(()=>{
  const handle=(event:KeyboardEvent)=>{
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
  const ratio=(bounds.bottom-event.clientY)/Math.max(1,bounds.height);
  setDetailPercent(Math.max(25,Math.min(70,Math.round(ratio*100))));
 }

 function closeDialog(){dialogRef.current?.close();setDialog(null);returnFocus.current?.focus();}
 function begin(kind:Dialog,ids=selection,focus?:HTMLElement){returnFocus.current=focus??document.activeElement as HTMLElement;setMenu(null);setTargets(ids);setLabel('');setNote(workspace?.cases.find(c=>c.caseId===ids[0])?.organization.note??'');setFolderId(workspace?.folders[0]?.id??'');setParentId('');setColor('#497ab7');setDialog(kind);}
 async function command(action:string,ids:string[],extra:Record<string,unknown>={}){
  if(!workspace||busy)return;setBusy(true);setError('');setNotice('');const controller=new AbortController();pending.current=controller;
  try{const response=await fetch('/api/demo-workspace/commands',{method:'POST',signal:controller.signal,headers:{'Content-Type':'application/json'},body:JSON.stringify({action,caseIds:ids,expectedRevision:workspace.revision,operationId:crypto.randomUUID(),...extra})});
   if(!response.ok){let message=de.networkError;try{message=(await response.json()).message??message;}catch{}setError(message);await refresh(controller.signal);return;}
   const result=await response.json();if(controller.signal.aborted)return;setWorkspace(result.workspace);setNotice(action==='DISPATCH'?t.dispatchedNotice:t.saved);if(dialog)closeDialog();
   if(result.packageJson)host.saveJson('normacase-versand-demo-'+result.workspace.revision+'.json',result.packageJson);
  }catch{if(!controller.signal.aborted)setError(t.completionUnknown);}finally{if(!controller.signal.aborted)setBusy(false);}
 }
 function open(id:string,view:'result'|'documents'='result'){setMenu(null);setActiveCase(id);setOpenedView(view);setOpened(id);}
 function back(){const id=opened;setOpened('');requestAnimationFrame(()=>openButtons.current[id]?.focus());}
 function context(event:React.MouseEvent|React.KeyboardEvent,id:string){event.preventDefault();setSubMenu(null);const ids=selection.includes(id)?selection:[id];setActiveCase(id);setSelection(ids);returnFocus.current=event.currentTarget as HTMLElement;const box=event.currentTarget.getBoundingClientRect();const mouse='clientX' in event;setMenu({x:Math.max(8,Math.min(mouse?event.clientX:box.left+20,window.innerWidth-280)),y:Math.max(8,Math.min(mouse?event.clientY:box.top+20,window.innerHeight-380)),ids});}
 const folders=[...(workspace?.folders??[])].sort((a,b)=>a.label.localeCompare(b.label,'de-DE',{sensitivity:'base'})||a.id.localeCompare(b.id)),cases=workspace?.cases??[],selected=cases.filter(c=>selection.includes(c.caseId));
 const orderedFolders:(Folder&{depth:number})[]=[];
 function appendFolders(parent:string|null,depth:number){for(const folder of folders.filter(f=>(f.parentId??null)===parent)){orderedFolders.push({...folder,depth});appendFolders(folder.id,depth+1);}}
 appendFolders(null,0);
 const groups=['approval','clarification','review','technical','documents'];
 function matches(c:Case){return (filter==='all'||filter==='bookmarks'&&c.organization.bookmark||filter==='confirmed'&&c.organization.confirmed&&!c.organization.dispatched||filter==='dispatched'&&c.organization.dispatched||filter==='unfiled'&&!c.organization.folderId||filter===c.queueId||filter===c.organization.folderId)&&(c.caseId+' '+c.title+' '+c.organization.note).toLocaleLowerCase('de-DE').includes(query.toLocaleLowerCase('de-DE'));}
 const visible=cases.filter(matches).sort((a,b)=>Number(b.caseId.startsWith('reference-'))-Number(a.caseId.startsWith('reference-'))||a.caseId.localeCompare(b.caseId));
 const canConfirm=(items:Case[])=>items.length>0&&items.every(c=>c.canConfirm&&!c.organization.confirmed&&!c.organization.dispatched);
 const canDispatch=(items:Case[])=>items.length>0&&items.every(c=>c.organization.confirmed&&!c.organization.dispatched);
 const current=cases.find(c=>c.caseId===opened);
 const focused=cases.find(c=>c.caseId===activeCase);
 const status=(c:Case)=>c.organization.dispatched?t.dispatched:c.organization.confirmed?t.confirmed:queueNames[c.queueId]??de.unknown;
 const caseColor=(c:Case)=>c.organization.color??workspace?.queueColors[c.queueId]??defaultColors[c.queueId];
 const menuCases=cases.filter(c=>menu?.ids.includes(c.caseId));
 return <section className="card case-explorer case-first" aria-label={t.heading}>
  {error&&<p className="error" role="alert">{error}</p>}{notice&&<p className="explorer-notice" role="status">{notice}</p>}
  {opened?<div className="opened-case"><header className="case-header"><button type="button" className="secondary" onClick={back}>{de.workspace.back}</button><h2>{current?.title}</h2><span className="case-identifier">{opened}</span>{current&&<span className="opened-case-status" role="status">{status(current)}</span>}<div className="opened-case-quick-actions"><button type="button" className="secondary" onClick={()=>begin('move',[opened])}>Verschieben nach …</button><button type="button" className="secondary" onClick={()=>begin('color',[opened])}>Markieren</button><button type="button" className="secondary" disabled={busy} onClick={()=>command('BOOKMARK',[opened],{value:!current?.organization.bookmark})}>{current?.organization.bookmark?'Lesezeichen entfernen':'Lesezeichen setzen'}</button><button type="button" className="secondary" disabled={!current?.canConfirm||current.organization.confirmed||busy} onClick={()=>begin('confirm',[opened])}>Bestätigen</button></div></header><DocumentCaseFile key={opened} caseId={opened} initialView={openedView} showAssessment/></div>:<>
   <header className="explorer-header"><div><h2>{t.heading}</h2><p className="workplace-case-context" aria-live="polite">{focused?`Fall: ${focused.caseId} · ${focused.title}`:'Kein Fall ausgewählt'}</p></div><label className="field explorer-search">{t.search}<input value={query} onChange={e=>setQuery(e.target.value)} placeholder={t.searchPlaceholder}/></label></header>
   <nav className="explorer-navigation compact-explorer-navigation" aria-label={t.navigation}><button type="button" className="secondary" aria-expanded={moreActions} onClick={()=>setMoreActions(!moreActions)}>Ansicht ▾</button>{moreActions&&<><button type="button" className="secondary" aria-pressed={filter==='all'} onClick={()=>setFilter('all')}>{t.all}</button><button type="button" className="secondary" aria-pressed={filter==='bookmarks'} onClick={()=>setFilter('bookmarks')}>{t.bookmarks}</button><button type="button" className="secondary" aria-pressed={filter==='unfiled'} onClick={()=>setFilter('unfiled')}>{t.unfiled}</button><button type="button" className="secondary" disabled={busy} onClick={()=>refresh().catch(()=>setError(de.networkError))}>{t.refresh}</button></>}</nav>
   <div className="compact-workplace-toolbar" role="toolbar" aria-label="Fallaktionen"><button type="button" className="secondary" onClick={()=>begin('folder',[])}>{t.newFolder}</button><button type="button" className="secondary" disabled={!focused} onClick={()=>focused&&open(focused.caseId)}>Fall öffnen</button><button type="button" className="secondary" aria-pressed={detailOpen} onClick={()=>setDetailOpen(!detailOpen)}>{detailOpen?"Details ausblenden (F4)":"Details anzeigen (F4)"}</button><button type="button" className="secondary" disabled={!canConfirm(selected)||busy} onClick={()=>begin('confirm')}>Bestätigen</button><label className="compact-color-toggle"><input type="checkbox" checked={rowColors} onChange={e=>setRowColors(e.target.checked)}/> Zeilenfärbung</label></div><div className="explorer-grid"><aside className="explorer-tree" aria-label={t.tree}>
    <button type="button" className="tree-filter" aria-pressed={filter==='all'} onClick={()=>setFilter('all')}>{t.all} <span>{cases.length}</span></button>
    <h3>{t.queues}</h3>{groups.map(id=><button key={id} type="button" className="tree-filter queue-tree-filter" aria-pressed={filter===id} style={{borderLeftColor:workspace?.queueColors[id]??defaultColors[id]}} onClick={()=>setFilter(id)}>{queueNames[id]} <span>{cases.filter(c=>c.queueId===id).length}</span></button>)}
    <h3>{t.personal}</h3><button type="button" className="tree-filter" aria-pressed={filter==='bookmarks'} onClick={()=>setFilter('bookmarks')}>★ {t.bookmarks} <span>{cases.filter(c=>c.organization.bookmark).length}</span></button>
    <button type="button" className="tree-filter" aria-pressed={filter==='confirmed'} onClick={()=>setFilter('confirmed')}>{t.confirmed}</button><button type="button" className="tree-filter" aria-pressed={filter==='dispatched'} onClick={()=>setFilter('dispatched')}>{t.dispatched}</button>
    {orderedFolders.map(f=><div key={f.id} className="custom-folder" style={{paddingLeft:f.depth*15}}><button type="button" className="tree-filter" aria-pressed={filter===f.id} style={{borderLeftColor:f.color}} onClick={()=>setFilter(f.id)}>{f.label}<span>{cases.filter(c=>c.organization.folderId===f.id).length}</span></button><button type="button" className="text-button" title="Unterordner anlegen" aria-label={'Unterordner in '+f.label+' anlegen'} onClick={()=>{begin('folder',[]);setParentId(f.id);}}>＋</button><button type="button" className="text-button" aria-label={t.editFolder+': '+f.label} onClick={()=>{begin('rename',[]);setFolderId(f.id);setLabel(f.label);setColor(f.color);}}>⋯</button></div>)}
   </aside><div ref={explorerContentRef} className="explorer-content">
    <div className="explorer-toolbar"><strong>{visible.length} {t.cases} · {selection.length} {t.selected}</strong><button type="button" className="secondary" disabled={!visible.length||busy} onClick={()=>setSelection(visible.slice(0,100).map(c=>c.caseId))}>{t.selectVisible}</button><button type="button" className="secondary" disabled={!selection.length||busy} onClick={()=>setSelection([])}>{t.clearSelection}</button>
     <button type="button" className="secondary" disabled={!selection.length||busy} onClick={()=>begin('move')}>{t.moveSelection}</button><button type="button" className="secondary" disabled={!selection.length||busy} onClick={()=>begin('color')}>{t.markSelection}</button><button type="button" className="secondary" disabled={busy||!canConfirm(selected)} onClick={()=>begin('confirm')}>{t.confirmSelection}</button><button type="button" className="secondary" disabled={busy||!canDispatch(selected)} onClick={()=>begin('dispatch')}>{t.dispatchSelection}</button>
     {groups.includes(filter)&&<button type="button" className="secondary" onClick={()=>{begin('queueColor',[]);setFolderId(filter);setColor(workspace?.queueColors[filter]??defaultColors[filter]);}}>{t.queueColor}</button>}
    </div>
    <div className="explorer-table-scroll"><table className={"explorer-table"+(rowColors?" status-row-colors":"")}><thead><tr><th>{t.selection}</th><th>{t.caseTitle}</th><th>{t.caseId}</th><th>{t.status}</th><th>{t.folder}</th><th>{t.actions}</th></tr></thead><tbody>{visible.map(c=><tr key={c.caseId} tabIndex={0} aria-selected={selection.includes(c.caseId)} style={{'--case-color':caseColor(c)} as CSSProperties} data-status={c.organization.dispatched?'complete':c.organization.confirmed?'complete':c.queueId} onClick={e=>{setActiveCase(c.caseId);if((e.target as HTMLElement).closest("button,input"))return;setSelection(previous=>e.ctrlKey||e.metaKey?(previous.includes(c.caseId)?previous.filter(id=>id!==c.caseId):previous.length<100?[...previous,c.caseId]:previous):[c.caseId]);}} onDoubleClick={()=>open(c.caseId)} onContextMenu={e=>context(e,c.caseId)} onKeyDown={e=>{if(e.key==='ContextMenu'||e.shiftKey&&e.key==='F10')context(e,c.caseId);else if(e.key==='Enter'&&e.target===e.currentTarget)open(c.caseId);}}>
     <td><input type="checkbox" aria-label={t.selectCase+': '+c.caseId} checked={selection.includes(c.caseId)} disabled={busy} onChange={e=>setSelection(e.target.checked?selection.length<100?[...selection,c.caseId]:selection:selection.filter(id=>id!==c.caseId))}/></td>
     <td><button ref={node=>{openButtons.current[c.caseId]=node;}} type="button" className="case-title-button" aria-label={de.workQueues.select+': '+c.caseId} onClick={()=>open(c.caseId)}>{c.organization.bookmark&&<span aria-label={t.bookmarked}>★ </span>}{c.title}</button>{c.organization.note&&<small className="case-note">{c.organization.note}</small>}</td><td className="case-identifier">{c.caseId}</td><td><span className="queue-badge">{status(c)}</span></td><td>{c.organization.color&&<span className="personal-mark-chip" style={{backgroundColor:c.organization.color}} aria-label="Eigene Farbmarkierung"/>}{folders.find(f=>f.id===c.organization.folderId)?.label??'—'}</td><td><button type="button" className="secondary" aria-label={t.caseActions} onClick={e=>context(e,c.caseId)}>⋯</button></td>
    </tr>)}</tbody></table>{!workspace?<p role="status">{de.documents.loading}</p>:!visible.length&&<p>{t.noCases}</p>}</div>
    {detailOpen&&<>
     <div className="explorer-pane-splitter" role="separator" aria-label="Fallliste und Detailbereich vergrößern oder verkleinern" aria-orientation="horizontal" aria-valuemin={25} aria-valuemax={70} aria-valuenow={detailPercent} tabIndex={0}
      onPointerDown={event=>{resizing.current=true;event.currentTarget.setPointerCapture(event.pointerId);}}
      onPointerMove={resizeDetail}
      onPointerUp={event=>{resizing.current=false;if(event.currentTarget.hasPointerCapture(event.pointerId))event.currentTarget.releasePointerCapture(event.pointerId);}}
      onPointerCancel={()=>{resizing.current=false;}}
      onKeyDown={event=>{if(event.key==='ArrowUp'||event.key==='ArrowDown'){event.preventDefault();setDetailPercent(value=>Math.max(25,Math.min(70,value+(event.key==='ArrowUp'?5:-5))));}}}>
      <span aria-hidden="true">⋮⋮</span>
     </div>
     <section className="explorer-detail-pane" style={{flexBasis:detailPercent+'%'}} aria-label="Fallakte und Dokumente">
      {focused?<><header className="explorer-detail-heading"><strong title={focused.title}>{focused.title}</strong><span className="case-identifier">{focused.caseId}</span><span className="opened-case-status">{status(focused)}</span><div className="explorer-detail-actions">
       <button type="button" className="secondary" onClick={()=>open(focused.caseId,'documents')}>Vollansicht</button>
       <button type="button" className="secondary" onClick={()=>begin('move',[focused.caseId])}>Verschieben nach …</button>
       <button type="button" className="secondary" onClick={()=>begin('color',[focused.caseId])}>Markieren</button>
       <button type="button" className="secondary" disabled={busy} onClick={()=>command('BOOKMARK',[focused.caseId],{value:!focused.organization.bookmark})}>{focused.organization.bookmark?'Lesezeichen entfernen':'Lesezeichen setzen'}</button>
      </div></header>
      <DocumentCaseFile key={focused.caseId} caseId={focused.caseId} initialView="documents" showAssessment/>
      </>:<p className="explorer-detail-empty">Fall in der Tabelle auswählen, um Dokumente und Prüfergebnis hier anzuzeigen.</p>}
     </section>
    </>}
    <footer className="explorer-footer" role="status">Anzeige {visible.length} von {cases.length} · {selection.length} ausgewählt · {filter==='all'&&!query?'Kein Filter':'Filter aktiv'} · {t.localNotice}</footer>
   </div></div></>}
  {menu&&<div ref={menuRef} className={"case-context-menu"+(menu.x>window.innerWidth-530?" submenu-to-left":"")} role="menu" aria-label={t.caseActions} style={{left:menu.x,top:menu.y}} onKeyDown={e=>{if(e.key==='Escape'){setMenu(null);returnFocus.current?.focus();}if(e.key==='Tab')setMenu(null);if(['ArrowDown','ArrowUp','Home','End'].includes(e.key)){e.preventDefault();const buttons=Array.from(menuRef.current?.querySelectorAll<HTMLButtonElement>('button:not(:disabled)')??[]);const i=buttons.indexOf(document.activeElement as HTMLButtonElement);buttons[e.key==='Home'?0:e.key==='End'?buttons.length-1:(i+(e.key==='ArrowDown'?1:buttons.length-1))%buttons.length]?.focus();}}}>
    <button type="button" role="menuitem" onClick={()=>open(menu.ids[0])}>{de.workQueues.select}</button>
    <div className={"context-submenu"+(coarsePointer?" touch-submenu":"")}><button type="button" role="menuitem" aria-haspopup="true" aria-expanded={coarsePointer?subMenu==='color':undefined} onClick={()=>setSubMenu(prev=>prev==='color'?null:'color')}>Markieren <span aria-hidden="true">›</span></button><div className={"context-submenu-panel"+(coarsePointer&&subMenu==='color'?" is-open":"")} role="group" aria-label="Farbmarkierung">{Object.entries(colorPresets).map(([name,hex])=><button key={name} type="button" onClick={()=>{const ids=menu.ids;setMenu(null);command('COLOR',ids,{color:hex});}}><span className="color-swatch" style={{backgroundColor:hex}}/>{t.colors[name as keyof typeof t.colors]}</button>)}<label className="menu-custom-color">Eigene Farbe<input type="color" aria-label="Eigene Markierungsfarbe" defaultValue="#497ab7" onChange={e=>{const ids=menu.ids;setMenu(null);command('COLOR',ids,{color:e.target.value});}}/></label><button type="button" onClick={()=>{const ids=menu.ids;setMenu(null);command('COLOR',ids,{color:null});}}>Markierung entfernen</button></div></div>
    <div className={"context-submenu"+(coarsePointer?" touch-submenu":"")}><button type="button" role="menuitem" aria-haspopup="true" aria-expanded={coarsePointer?subMenu==='folder':undefined} onClick={()=>setSubMenu(prev=>prev==='folder'?null:'folder')}>Verschieben nach <span aria-hidden="true">›</span></button><div className={"context-submenu-panel"+(coarsePointer&&subMenu==='folder'?" is-open":"")} role="group" aria-label="Ordner wählen">{folders.map(f=><button type="button" key={f.id} onClick={()=>{const ids=menu.ids;setMenu(null);command('MOVE',ids,{folderId:f.id});}}><span className="color-swatch" style={{backgroundColor:f.color}}/>{f.label}</button>)}<button type="button" onClick={()=>begin('folder',[])}>{t.newFolder}</button></div></div>
    <button type="button" role="menuitem" disabled={busy} onClick={()=>{const ids=menu.ids;setMenu(null);command('BOOKMARK',ids,{value:!menuCases.every(c=>c.organization.bookmark)});}}>{menuCases.every(c=>c.organization.bookmark)?t.removeBookmark:t.addBookmark}</button>
    <button type="button" role="menuitem" onClick={()=>begin('move',menu.ids)}>{t.move}</button><button type="button" role="menuitem" disabled={busy||!menuCases.some(c=>c.organization.folderId)} onClick={()=>{const ids=menu.ids;setMenu(null);command('MOVE',ids,{folderId:null});}}>{t.removeFromFolder}</button><button type="button" role="menuitem" disabled={menu.ids.length!==1} onClick={()=>begin('note',menu.ids)}>{t.note}</button>
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
   {dialog==='confirm'&&<p>{t.confirmHelp} ({targets.length} {t.cases})</p>}{dialog==='dispatch'&&<p>{t.dispatchHelp} ({targets.length} {t.cases})</p>}{dialog==='delete'&&<p>{t.deleteHelp}</p>}
   <div className="actions"><button type="submit" className="primary" disabled={busy}>{dialog==='folder'||dialog==='rename'?t.saveFolder:dialog==='move'?t.move:dialog==='confirm'?t.confirm:dialog==='dispatch'?t.simulateDispatch:dialog==='delete'?t.delete:t.save}</button><button type="button" className="secondary" disabled={busy} onClick={closeDialog}>{t.cancel}</button>{dialog==='rename'&&<button type="button" className="text-button" onClick={()=>setDialog('delete')}>{t.deleteFolder}</button>}{dialog==='color'&&<button type="button" className="text-button" disabled={busy} onClick={()=>command('COLOR',targets,{color:null})}>{t.clearColor}</button>}</div>
  </form></dialog>}
 </section>;
}
