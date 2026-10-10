import {useEffect,useState} from 'react';
import de from './de.json';
type Mode='system'|'light'|'dark';
/** Session-only display preference: no case data or browser persistence. */
export function ThemeControl(){
 const [mode,setMode]=useState<Mode>('system');
 useEffect(()=>{
  const media=window.matchMedia('(prefers-color-scheme: dark)');
  const apply=()=>{document.documentElement.dataset.theme=mode==='system'?(media.matches?'dark':'light'):mode;};
  apply();media.addEventListener('change',apply);
  return()=>media.removeEventListener('change',apply);
 },[mode]);
 return <label className="theme-control"><span className="visually-hidden">{de.theme.label}</span><select aria-label={de.theme.label} title={de.theme.help} value={mode} onChange={event=>setMode(event.target.value as Mode)}><option value="system">{de.theme.system}</option><option value="light">{de.theme.light}</option><option value="dark">{de.theme.dark}</option></select></label>;
}
