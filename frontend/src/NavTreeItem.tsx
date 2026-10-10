import {WorkspaceIcon} from './WorkspaceIcon';
import type {CSSProperties,MouseEvent,KeyboardEvent} from 'react';
export function NavTreeItem({label,count,selected,color='#7a93ab',kind='list',onSelect,onContext}:{label:string;count:number;selected:boolean;color?:string;kind?:'list'|'folder'|'bookmark'|'confirmed'|'dispatch';onSelect:()=>void;onContext?:(event:MouseEvent<HTMLButtonElement>|KeyboardEvent<HTMLButtonElement>)=>void}){
 return <button type="button" className="tree-filter" title={label} aria-pressed={selected} style={{borderLeftColor:color} as CSSProperties} onClick={onSelect} onContextMenu={onContext} onKeyDown={e=>{if(e.key==='ContextMenu'||e.shiftKey&&e.key==='F10'){onContext?.(e);}}}><WorkspaceIcon name={kind}/>{label}<span>{count}</span></button>;
}
