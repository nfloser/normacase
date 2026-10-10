/** Only presentation preferences. Never store case identifiers, medical evidence or personal notes. */
export type WorkspaceLayoutPreferences={
 detailOpen:boolean;
 detailPercent:number;
 rowColors:boolean;
};
const key='normacase.ui.layout.v1';
const defaults:WorkspaceLayoutPreferences={detailOpen:true,detailPercent:45,rowColors:true};
export function readWorkspaceLayout():WorkspaceLayoutPreferences{
 try{
  if(typeof localStorage==='undefined')return {...defaults};
  const raw=localStorage.getItem(key);
  if(!raw||raw.length>256)return {...defaults};
  const value:unknown=JSON.parse(raw);
  if(!value||typeof value!=='object'||Array.isArray(value))return {...defaults};
  const p=value as Record<string,unknown>;
  if(typeof p.detailOpen!=='boolean'||typeof p.rowColors!=='boolean'
     ||typeof p.detailPercent!=='number'||!Number.isInteger(p.detailPercent)
     ||p.detailPercent<25||p.detailPercent>70)return {...defaults};
  return {detailOpen:p.detailOpen,detailPercent:p.detailPercent,rowColors:p.rowColors};
 }catch{return {...defaults};}
}
export function saveWorkspaceLayout(layout:WorkspaceLayoutPreferences):void{
 try{
  if(typeof localStorage==='undefined')return;
  localStorage.setItem(key,JSON.stringify({
   detailOpen:layout.detailOpen,
   detailPercent:Math.max(25,Math.min(70,Math.round(layout.detailPercent))),
   rowColors:layout.rowColors
  }));
 }catch{ /* Browser storage can be disabled; the workbench remains usable. */ }
}
