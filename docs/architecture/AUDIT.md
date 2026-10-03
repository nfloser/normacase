# Assessment audit and human review contracts

NormaCase keeps deterministic system evaluation and human review as separate records.

## Invariant

A human review never mutates the original system assessment.

```text
Assessment result
  -> immutable assessment identity
  -> append-only audit event
  -> optional human review record
  -> append-only audit event
```

The system result remains available exactly as produced. A later human decision is additional information, not a rewrite of the rule-engine result.

## Domain contracts

The framework-independent audit contracts live in `NormaCase.Domain.Audit`.

- `AssessmentId` and `ReviewId` are explicit stable identities supplied by the caller.
- `HumanReviewRecord` contains assessment identity, actor id, UTC timestamp, disposition, reason, optional generic override outcome and optional structured reference.
- `HumanReviewDisposition.AcceptSystemResult` records an explicit acceptance and carries no override outcome.
- `HumanReviewDisposition.Override` requires one of the generic platform `AssessmentOutcome` values.
- `AssessmentAuditEvent` distinguishes assessment creation from a recorded human review.
- `AssessmentAuditTrail` starts at sequence 1 and returns a new trail when an event is appended. Existing trail instances are not rewritten.

The domain model does not create ids, read the system clock, authenticate actors, allocate sequence numbers or persist records. Those values are explicit inputs so behavior remains deterministic and testable.

## Time and ordering

Audit timestamps are required to be UTC `DateTimeOffset` values. This avoids ambiguous local timestamps in persisted history.

Sequence numbers are explicit and contiguous within one assessment trail. The trail rejects:

- cross-assessment events,
- duplicate or skipped sequence positions,
- a second assessment-created event,
- timestamps that move backwards.

A database adapter must enforce equivalent invariants transactionally when persistence is introduced.

## Human override

An override is a platform-level review disposition, not new domain knowledge. It may point to an existing generic `AssessmentOutcome`, but it does not modify rules, traces or Knowledge Releases.

The review reason is mandatory. An optional `ReviewReference` carries a typed external/internal reference without teaching the core about ticket systems, institutions or medical domains.

Authorization remains outside the domain contract. An application layer must verify that the authenticated actor is allowed to review or override an assessment before constructing and storing a review record.

## Application orchestration

`NormaCase.Application.Audit.AssessmentReviewService` binds the existing immutable
assessment-record and append-only audit-store contracts without changing either
domain model.

Initialization requires an already persisted `AssessmentRecord`. The service first
verifies that the store actually returned the requested assessment identity, then
creates sequence 1 from that record's exact assessment id and `RecordedAtUtc`;
callers only supply the explicit actor id. It refuses duplicate initialization and
does not invent a timestamp, id or fallback history.

Recording a human review first constructs the existing `HumanReviewRecord`, so
invalid actor, timestamp, disposition, reason or override combinations fail before
storage is touched. It then requires both the immutable assessment and its initialized
audit history, verifies that the history's creation event is bound to the persisted
assessment id and recording timestamp, appends exactly one event and asks the audit
store to persist that new history.

The service never re-evaluates the assessment, resolves current Knowledge or changes
the persisted input/result. Store conflicts and integrity failures are propagated; it
does not silently retry with a different sequence or payload.

Authentication and authorization remain outside this service. An actor id supplied to
the application contract is still only a claimed identity until a later authenticated
application boundary verifies permission before invoking the review service.


## Atomic case-review commit boundary

`CaseReviewCommitService` adds the storage-neutral boundary between an authenticated
human review and committed case-processing/audit state.

The request model deliberately contains no actor id. A trusted authentication adapter
supplies an `AuthenticatedReviewActor` separately. Inside the store transaction
callback, the service binds the command to the exact case, assessment, input revision,
workflow id/version, process revision and audit sequence, then asks an
`ICaseReviewAuthorizer` for a case-scoped authorization decision. Authorization is
therefore checked against the current locked snapshot before any append is prepared.

Accepting the recorded system result and overriding it are mapped through
`CaseReviewTransitionPolicy` to explicit opaque workflow transitions. Review
dispositions are not process-state names and no transition is inferred from an outcome.

`ICaseReviewTransactionStore` exposes one callback contract over:

```text
immutable AssessmentRecord
+ immutable input revision
+ current CaseProcessingInstance
+ current AssessmentAuditTrail
```

If the callback succeeds, the store must atomically commit the returned new
`CaseProcessingInstance` and `AssessmentAuditTrail`. If authorization, binding,
revision, duplicate-id, transition or storage checks fail, neither state may change.
The assessment itself is never part of the mutation and remains immutable.

The command carries expected input, process and audit revisions. Competing commands
against the same snapshot therefore produce one winner; a later contender observes
the committed revisions and fails stale instead of being silently replayed. Review ids
are checked against the committed audit history so a caller cannot append the same
review twice under fresh revisions.

This slice intentionally defines no HTTP mutation endpoint, identity provider or
durable combined PostgreSQL store. Authentication remains a trusted adapter
responsibility, while productive persistence must implement the transaction contract
on-premises and prove equivalent rollback/concurrency behavior. Repository tests use
a synthetic reference store to exercise the contract, including rollback and queue
projection after the committed process transition.

## Persistence boundary

This slice is deliberately not an audit database.

A future PostgreSQL adapter should provide:

- immutable assessment storage,
- append-only audit rows with uniqueness/ordering constraints,
- transactional sequence allocation,
- authenticated actor linkage,
- concurrency handling,
- retention/export controls,
- tamper-evidence or signature support where required.

Persist the exact assessment document/source snapshots needed for historical reproduction. Do not resolve old audit entries against current Knowledge Packs.

## Security and privacy

Review reasons and references may become sensitive in real operation. Do not log them wholesale. Repository tests use synthetic identifiers and text only.

No contract in this slice claims production authorization, non-repudiation, qualified electronic signatures or domain approval.

## Versioned JSON interchange

`NormaCase.Serialization.AssessmentAuditJson` exports and imports an entire
`AssessmentAuditTrail` using format version 1. The document preserves event
sequence, assessment and review identities, explicit UTC timestamps, actors,
dispositions, reasons, optional override outcomes and structured references.
Every field is present; optional values are explicit JSON nulls.

Import rebuilds events through the domain factories and the immutable trail
Start/Append operations. It rejects empty histories, sequence gaps, backwards
timestamps, cross-assessment events, incompatible review payloads and mismatches
between an event and its nested review. The shared strict JSON boundary also
rejects duplicate/unknown properties, missing required fields, numeric enum
values, unsupported versions and oversized/deep input.

This format carries the separate human history; it does not rewrite or embed
the original assessment result. There is no API or persistence integration yet.
An imported actor identity is a claim, not authenticated identity. The format
provides neither signatures, tamper evidence nor authorization. Reasons and
references must receive the same privacy controls as assessment content.


## PostgreSQL persistence boundary

The optional PostgreSQL adapter persists validated audit trails as immutable
version snapshots rather than mutable review rows. The complete strict JSON history
is retained for each accepted sequence; the adapter rejects skipped or divergent
prefixes and the database rejects UPDATE/DELETE.

See [POSTGRESQL_ASSESSMENT_STORAGE.md](POSTGRESQL_ASSESSMENT_STORAGE.md).
This persistence does not authenticate actors or authorize review actions.
