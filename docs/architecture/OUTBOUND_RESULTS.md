# Reviewed outbound result contract

Issue #150 introduces the first bounded outbound slice of the vendor-neutral integration architecture. It defines what NormaCase may hand to a future transport adapter after a case has reached a reviewed terminal state. It does **not** deliver anything and contains no MDconnect, MEDIKOS, SAP or other vendor contract.

## Boundary

`ReviewedOutboundResultBuilder` accepts three authoritative application inputs:

1. the recorded `NormalizedIntakeRecord`,
2. the committed `CaseReviewState`,
3. the exact versioned `WorkflowDefinition`.

The caller also supplies `OutboundResultCommand` with a new outbound message id, correlation id and the exact case/process/audit revisions it observed.

The builder fails closed unless:

- intake, assessment and process refer to the same platform case;
- the assessment's recorded case revision equals the process case revision;
- knowledge-pack id and knowledge release match the normalized intake;
- the exact normalized assessment date, facts and evidence equal the immutable assessment input;
- the audit belongs to the assessment and starts at the assessment recording timestamp;
- expected case, process and audit revisions still match committed state;
- workflow id/version match and the current process state is terminal;
- the last audit event is an explicit human review for the same assessment.

This slice assumes the `NormalizedIntakeRecord` and `CaseReviewState` are loaded from their authoritative stores. Serialization or possession of a structurally valid object does not confer trust.

## Result semantics

The result preserves both machine and human semantics rather than rewriting history:

- `originalOutcome` is the immutable deterministic assessment outcome;
- `disposition` records whether the reviewer accepted or overrode it;
- `humanOutcome` equals the original outcome for an accept and is the review's explicitly recorded override outcome for an override;
- review id, actor, UTC timestamp, reason and optional review reference remain explicit;
- the original normalized facts, evidence and evidence references are retained;
- upstream source system, upstream case/message/revision, intake adapter identity/version and receipt timestamp are retained;
- platform, knowledge, workflow, case, process, audit and assessment identities/versions are retained separately.

An override is not assumed to be numerically or semantically “different” from the original generic outcome beyond the existing domain requirement that an override carries an explicit outcome. The contract does not invent medical or institutional semantics.

## JSON format

`ReviewedOutboundResultJson` defines format version `1` at the serialization boundary.

Parsing is strict:

- unknown properties are rejected;
- duplicate properties are rejected recursively;
- required constructor properties and nullable annotations are respected;
- enum values are symbolic and integer enum values are rejected;
- malformed values are rejected without coercion;
- collection order is canonicalized for reproducible serialization.

All potentially large revision counters are encoded as canonical base-10 **strings**:

- upstream revision,
- case revision,
- process revision,
- audit revision.

This prevents loss of 64-bit integer precision in JavaScript or other consumers whose native JSON number representation cannot exactly represent every .NET `long`. Leading zeros, exponents, signs and numeric JSON tokens are rejected for these fields. Roundtrip tests exercise values near `long.MaxValue`.

## Deliberate exclusions

This contract is transport-neutral. It does not yet define:

- HTTP endpoints,
- files or message-broker envelopes,
- delivery acknowledgement,
- retry classification,
- idempotent sinks,
- vendor field mappings,
- authentication to an upstream system,
- a full inbound-to-outbound runnable roundtrip.

Those remain in #120. Productive organizational identity/privacy review remains tracked separately in #119. Concrete adapters require authoritative interface specifications and approved access.

Synthetic data only.
