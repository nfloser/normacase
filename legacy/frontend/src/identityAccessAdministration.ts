export type IdentityAccessState={actorId:string;revision:string;suspended:boolean};

export function identityAccessChangeRequest(
  state:IdentityAccessState,
  suspended:boolean,
  reason:string
):string{
  if(!/^(0|[1-9][0-9]*)$/.test(state.revision))throw new Error('invalid_revision');
  if(state.suspended===suspended)throw new Error('no_change');
  const trimmed=reason.trim();
  if(!trimmed||trimmed.length>1000||Array.from(trimmed).some(character=>/\p{Cc}/u.test(character)))
    throw new Error('reason_required');
  return JSON.stringify({expectedRevision:state.revision,suspended,reason:trimmed});
}
