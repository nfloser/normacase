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

## Remaining delivery work (#120)

This slice is not delivery. It adds no permissive exporter, public endpoint, durable
outbox or claim of exactly-once semantics. The next slice must connect the result to
at least two materially different synthetic sinks and a runnable inbound-to-outbound
roundtrip, preserving external correlation and explicit replay identity.

A sink must distinguish accepted delivery, duplicate identical payload, conflicting
reuse of identity and retryable transport failure. Transport failures must never alter
the original assessment or imply a different medical/process outcome. A durable
transactional outbox/delivery receipt boundary is needed before productive retry claims.
Synchronous HTTP, asynchronous messages and bounded file interchange remain replaceable
adapter choices. No MDconnect, MEDIKOS, SAP or other vendor API is invented.
