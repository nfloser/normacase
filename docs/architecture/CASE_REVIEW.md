# Authorized case-review transactions

`CaseReviewService` connects existing immutable assessment, human-review audit and
case-processing contracts. This is the Application boundary for a trusted headless
host or a future authenticated workbench adapter. It adds no medical rules and does
not infer a process transition from an assessment outcome.

## Trust boundary

`AuthenticatedReviewActor` is output from an authentication adapter. Constructing the
object is not authentication. A web adapter must authenticate the principal itself,
then supply a stable, globally unique, authority-qualified actor id. Neither actor nor
authority may come from a request body, user-entered field, unsigned header or imported
workflow. The authority remains available to the case authorizer; the qualified actor
id is recorded in the existing audit contract.

`ICaseReviewAuthorizer` checks current permission for the specific actor, authoritative
case/assessment, disposition and selected policy. It runs inside the transaction before
any append/transition. Denial or an exception fails closed. The implementation must be
bounded, must not change assessment data, and must not perform external network work
while holding a transaction. No permissive default authorizer exists.

The host supplies the active reviewed workflow/policy and explicit UTC recording time.
Authentication and permission sources remain adapter concerns; the core requires no
cloud identity service. This slice provides no real identity provider or HTTP mutation
endpoint. The existing synthetic demo remains read-only.

## One atomic aggregate

`ICaseReviewTransactionStore.ExecuteAsync` must lock the authoritative case aggregate,
verify stored integrity, then load its current immutable assessment, assessment's recorded case-input revision,
process and audit trail, invoke the update once and atomically commit process plus audit.
The assessment-to-case revision association is persisted metadata, never reconstructed
from the incoming command or current process revision. Missing aggregates fail rather
than silently initialize. Preserve the assessment and audit prefix. Roll back all
writes on validation, authorization, concurrency, persistence or cancellation failure.
Never silently retry a stale review against a newer aggregate. PostgreSQL implementations
must provide a real transaction/locking strategy; sequential calls to the existing
separate audit and process stores would not satisfy this interface.

Inside that boundary the service checks:

- exact case, assessment and audit identity and original assessment timestamp;
- assessment-input revision matches the process's bound case revision;
- explicit case-scoped authorization;
- expected case-input, process and audit revisions;
- review id has not previously been appended;
- exact workflow identity/version, policy mapping and transition availability.

It then appends `HumanReviewRecord` with the trusted actor, explicit UTC time, reason,
optional reference and explicit override outcome. Domain validates accept/override and
monotonic audit time. The original assessment and Decision Trace never change. Applying
the configured opaque process transition produces the next immutable process revision.
Queue queries must project committed process state after the transaction succeeds.

Accepting the system result is a review disposition, not a universal institutional
approval rule. The host's explicit process policy and authorizer determine where it is
allowed. Overrides preserve both the deterministic outcome and the separate human
outcome; they do not rewrite historical facts. Actual corrected input requires a new
case-input revision/assessment rather than editing the old assessment.

## Verification and remaining integration

Synthetic tests cover accepted and overridden reviews, permission denial, stale
revisions, one winner for competing commands, identity/revision binding, missing
transition, duplicate review id, invalid override, backward time, cancellation,
transaction/authorization failure, cancellation during authorization and queue movement from the committed state. The lock-based store
is a test contract fixture only, not runtime persistence or a database substitute.
Existing assessment replay remains independent from appended human decisions.

Issue #134 covers this boundary. Issues #119 and #116 remain open for the durable
PostgreSQL aggregate adapter, reviewed authentication/authorization adapter, German
HTTP/UI error mapping and productive multi-user integration. No real patient data or
claim of productive approval readiness is introduced.

## PostgreSQL aggregate adapter

`PostgresCaseReviewStore` stores each committed process/audit aggregate in the
append-only `case_review_versions` table (migration 004). The referenced immutable
assessment must already exist in `assessment_records`; initialization compares its
exact JSON and checksum before establishing the explicit assessment/input-revision
association. Initialization is trusted intake/application work, never a public DTO.

A transaction-scoped per-case advisory lock serializes initialization and review.
Each review creates one new aggregate row containing process state and audit together.
A rejected insert therefore cannot leave one half committed. UPDATE/DELETE are rejected
by PostgreSQL triggers. Loading verifies contiguous aggregate versions, SHA-256 before
strict deserialization, unchanged original assessment and input/workflow binding,
contiguous process revisions, configured state edges and exact retained audit prefixes.
The host resolves immutable workflow definitions by exact id/version.

This aggregate owns the authoritative audit for its case-review path. The earlier
independent assessment audit store remains a legacy/separate path; callers must not
write the same case's review history through both stores. Existing history is not
silently adopted. No migration of old independently recorded reviews is implied.
Full historical verification currently reads the case's retained versions; indexing
and snapshot optimization require measured need and must preserve these invariants.
This adapter supplies persistence, not authentication or a public mutation API.
