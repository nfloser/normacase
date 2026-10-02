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
