# Reviewed outbound result contract

`ReviewedCaseResultFactory` creates a transport-neutral result from authoritative
normalized intake and committed case-review state. It adds no medical rules, transport,
clock reads, ids or implicit approval. The caller supplies message/correlation identities,
exact expected case/process/audit revisions and the immutable workflow definition.

The factory checks case/assessment/audit binding, original assessment creation time,
case-input revision association, exact knowledge identity, assessment date and the
complete normalized fact/evidence values. Revisions must match. The configured
workflow state must be terminal and the final audit event must be a human review
of the same original assessment. Nonterminal/unreviewed/substituted states fail.
Terminal state alone is not approval. Caller-supplied objects are not authentication:
the host must authorize export and load authoritative immutable records itself.

The result carries explicit outbound message/correlation identities, original source
system/upstream case/message/revision, NormaCase case/assessment identities,
platform/knowledge/workflow versions, exact revisions, terminal process state,
review id/time/disposition, original deterministic outcome and separate human outcome.
An accepted result retains the original outcome; an override retains both the original
and the explicit human result. UNKNOWN is never replaced or guessed. No patient
identifiers, facts, evidence content, actor credential or free-text reason are added.
Consumers resolve the audit reference through an authorized path when needed.

Upstream revision and internal case revision are separate streams. The factory does
not assume that their numbers are equal. The host must retain their authoritative
association; matching exact normalized values does not itself authenticate provenance.
Evidence references belong to the retained normalized intake; no document is fetched
or exported. The result references the complete immutable assessment; it does not
replace detailed source/rule traces or claim source authenticity/domain approval.

## JSON format 1

`ReviewedCaseResultJson` writes `{formatVersion: 1, result: ...}` with strict camel-case
properties and stable uppercase enum contracts. Every `long` revision is a canonical
invariant decimal **string**, including upstream revision. This preserves values beyond
JavaScript's safe integer range. Format/workflow versions remain bounded integers.

Deserialization rejects unsupported versions, unknown/missing/duplicate properties,
numeric/coerced/noncanonical/overflowed revision values and unknown enum values.
Application validation additionally checks identifiers, UTC time, bounded metadata,
positive revisions and accepted-outcome consistency. Serialization/replay validates
internal format only; it does not authenticate the producer or re-load current state.
Historical messages must not be recreated using current knowledge under the same id.

## Delivery boundary

`ReviewedCaseDeliveryService` accepts only a validated `ReviewedCaseResult`, an
explicit delivery id and an explicit destination id. The pair forms the stable delivery
key. A committed terminal receipt is returned unchanged for an identical repeat.
Reusing that key for different result content fails closed.

The application service calls a sink at most once per explicit invocation. It contains
no timer, automatic retry loop, system clock, random id or network dependency.
`RetryableFailure` is intentionally not committed, so a host may make a later explicit
retry under its own bounded policy. `Delivered` and permanent `Rejected` results are
committed. Delivery status is transport state only and never changes case, assessment,
workflow or human-review semantics.

Receipt persistence is represented by `IOutboundDeliveryReceiptStore`; Application
does not choose a database or claim exactly-once delivery. Downstream adapters receive
the same stable delivery identity and must also suppress duplicate side effects for
concurrent or replayed calls.

The PostgreSQL adapter now implements durable terminal receipts with
`PostgresOutboundDeliveryReceiptStore`. It stores the strict reviewed-result JSON as
PostgreSQL `json`, binds a SHA-256 integrity digest to the exact stored text, serializes
concurrent commits for the same delivery key and returns the original committed receipt
after restart. The database rejects UPDATE and DELETE on receipt rows. Retryable
failures are never written because they are explicitly non-terminal.

This persistence closes the restart gap for the receipt ledger, but it is not a claim of
generic exactly-once transport. A crash after an external side effect but before receipt
commit still requires destination-side idempotency under the stable delivery identity,
or a future transport-specific transactional/outbox mechanism where an authoritative
interface supports it.

## Synthetic adapters

`NormaCase.SyntheticIntegration` proves the boundary with two materially different
offline adapters:

- `InMemoryReviewedCaseResultSink` models an asynchronous/message-style destination.
  It records one side effect per delivery identity and can expose explicit delivered,
  retryable-failure and rejected transport outcomes in tests.
- `BoundedFileReviewedCaseResultSink` writes the strict versioned
  `ReviewedCaseResultJson` payload to a deterministic SHA-256-derived filename. It
  never derives a filename from upstream identifiers, caps payloads at 64 KiB and
  treats an existing different payload under the same delivery identity as a conflict.

These are synthetic adapters, not productive transports. A synchronous HTTP adapter,
message broker adapter or governed file exchange can implement the same sink contract
later without entering Domain or RuleEngine. Authentication, authorization, bounded
retry/outbox policy and vendor-specific protocol details remain host/adapter concerns.

## Executable synthetic roundtrip

The authenticated local synthetic host connects JSON/XML intake, immutable normalized
receipts, atomic assessment/process initialization, persisted queues and human review
to this delivery boundary. It uses the durable PostgreSQL receipt store for both a
synthetic inbox (one atomic committed row) and bounded atomic file delivery. Message
id plus destination is the host delivery key. Stale or unreviewed exports fail before
delivery; repeated identical exports preserve correlation and original receipt.

Real PostgreSQL host tests cover restart and both destinations. CI also runs the real
executable on a clean database, compares exact historical state on restart, restores
a pg_dump into a second clean database and compares it again. See
`docs/development/SYNTHETIC_ROUNDTRIP.de.md`. Productive permissions, domain/process
approval and actual vendor adapters require authoritative specifications and approved
access. No MDconnect, MEDIKOS, SAP or other vendor API is invented.
