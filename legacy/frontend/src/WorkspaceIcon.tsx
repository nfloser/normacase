export type WorkspaceIconName='list'|'folder'|'bookmark'|'confirmed'|'dispatch'|'open'|'mark'|'refresh'|'document'|'copy';
/** Small, locally authored line icons; no external fonts or runtime assets. */
export function WorkspaceIcon({name}:{name:WorkspaceIconName}){
 const paths:Record<WorkspaceIconName,string>={list:'M4 5h16M4 12h16M4 19h16',folder:'M2 5h6l2 2h12v13H2z',bookmark:'m12 2 3 6 7 1-5 5 1 7-6-3-6 3 1-7-5-5 7-1z',confirmed:'m4 12 5 5L20 6',dispatch:'M3 5h18v14H3z M3 5l9 8 9-8',open:'M3 5h7l2 2h9v4M3 5v15h16l3-9H7L3 20',mark:'m5 15 9-10 5 5-9 10H5z M14 5l2-2 5 5-2 2',refresh:'M20 10a8 8 0 1 0-1 7 M20 3v7h-7',document:'M5 2h9l5 5v15H5z M14 2v6h5 M8 12h8M8 16h8',copy:'M9 7h12v15H9z M3 17V2h12v3'};
 return <svg className="workspace-icon" width="16" height="16" viewBox="0 0 24 24" aria-hidden="true" fill="none" stroke="currentColor" strokeWidth="1.5" strokeLinejoin="round" strokeLinecap="round"><path d={paths[name]}/></svg>;
}
