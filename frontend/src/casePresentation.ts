/** Presentation only: never use these labels as clinical inputs or rule conditions. */
export function caseIdentity(id:string,title:string){
 const parts=title.split(' · ');
 return parts.length>=3&&/^NC-\d{4}-\d+$/.test(parts[0])
  ? {number:parts[0],person:parts[1],subject:parts.slice(2).join(' · ').replace('Pflege-Score:','Pflege:')}
  : {number:id,person:'',subject:title==='Synthetischer Arbeitslistenfall'?'Plattformprüfung':title};
}
