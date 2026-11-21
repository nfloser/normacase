# ADR 0002: Separate retention planning from reviewed aggregate erasure

- Status: Accepted for the read-only synthetic planning boundary
- Date: 2026-10-11

## Context

Immutable assessment and audit storage protects historical integrity, but does not
implement legally appropriate retention, restriction or deletion. Blanket UPDATE/
DELETE permissions would destroy existing invariants. Concrete retention periods,
holds and legal duties depend on the operator and cannot be inferred by NormaCase.

## Decision

Implement a storage-neutral, bounded, deterministic **read-only** retention planner.
It receives exact policy identity/version, explicit UTC evaluation time and detached
artifact inventory metadata. No default retention period, system clock, database,
filesystem access or domain interpretation is used. Missing expiry, unknown hold or
incomplete inventory requires human review. Expired copies are only eligible for
review; no result authorizes erasure. Two distinct synthetic scenarios exercise a
retained backup/hold and an entirely expired inventory.

Correction, access restriction and physical aggregate erasure are separate commands.
A future erasure adapter must bind verified identity, exact case and inventory/
policy revisions, independent approval, fresh holds, idempotency, atomic DB erasure
and resumable file/outbound cleanup. Normal runtime roles retain append-only rights.
No permissive DELETE migration or production erasure endpoint is introduced here.

## Consequences

The planner can inform a future administrative preview or institution-controlled
housekeeping process without granting authority. Plan inputs are assertions from an
adapter, not authenticated evidence; the planner is not a complete inventory scanner
or security boundary. A plan cannot be reused as a deletion approval after restore,
policy/hold changes or inventory drift. Productive erasure stays blocked until the
operator policy and an independently reviewed adapter exist.

See [the retention contract](../security/RETENTION_AND_DELETION.de.md) for data stores,
authorization, audit, backup handling and the executable acceptance boundary.
