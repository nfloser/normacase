/** Presentation of existing server eligibility; never derives medical approval. */
export function processingStep(item:{canConfirm:boolean;organization:{confirmed:boolean;dispatched:boolean}}):'confirm'|'blocked'|'dispatch'|'complete'{
 if(item.organization.dispatched)return 'complete';
 if(item.organization.confirmed)return 'dispatch';
 return item.canConfirm?'confirm':'blocked';
}
