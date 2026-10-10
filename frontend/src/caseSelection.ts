/** null rejects an oversized range; hidden anchors never include hidden cases. */
export function selectCaseRange(visible:string[],anchor:string|null,target:string):string[]|null{
 const end=visible.indexOf(target);
 if(end<0)return [];
 const start=anchor===null?-1:visible.indexOf(anchor);
 if(start<0)return [target];
 if(Math.abs(end-start)+1>100)return null;
 return visible.slice(Math.min(start,end),Math.max(start,end)+1);
}
