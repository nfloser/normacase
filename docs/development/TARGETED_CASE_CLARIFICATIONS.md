# Targeted clarification and explicit resolution

Versioned application policies bind a workflow/version, allowed starting states and
whether targets must currently be missing. Missing-information requests and manual-
review questions are demonstrated separately with synthetic policies. Targets are
explicit retained-schema field/evidence IDs. The platform does not infer medical
questions or turn unknown values into known facts.

Requests record exact assessment/case/process/audit revisions, trusted actor/authority,
explicit UTC time, policy/version, bounded reason and detached targets. Migration 017
retains requests and resolutions append-only, with database mutation triggers and
foreign keys to the exact review snapshot and answering correction. Live READ/CLARIFY
case authority is required to create requests. Resolution requires live READ/CORRECT
case authority and the actual retained correction relation and input.

`POST /api/review/work-cases/{caseId}/clarifications` accepts the strict versioned
policy/revision context, clarification ID, reason and requested field/evidence arrays.
`GET .../clarifications` returns bounded verified pages. German workbench controls
use Knowledge presentation labels and show open, answered and superseded requests.

A correction may name one optional `clarificationId`. It can resolve that request only
when the predecessor assessment/revision matches, requested facts are explicit known
values and requested evidence is present. Correction plus resolution commits together.
If resolution fails, all correction appends roll back and the original request remains
open. A correction without a request never silently marks an older question answered.

Application, real PostgreSQL, API and Chromium tests cover separate policies, guessed
targets, stale revisions, UNKNOWN preservation, failed-resolution rollback, immutable
requests/resolutions, trusted metadata and the complete German request/correction/
reapproval/outbound workflow. These are synthetic process capabilities, not approved
institutional question sets or clinical rules.
