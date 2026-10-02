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
