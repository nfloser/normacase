# Normalized case intake

External adapters map upstream contracts into Application `NormalizedIntakeRequest`.
Vendor-specific XML/JSON, transports, field names and authentication stay outside
Domain and RuleEngine. This is a generic boundary, not an implementation or claim
of an MDconnect, MEDIKOS or SPV-MD interface.

The request carries explicit platform case/type identity, source system, upstream
case/message/revision identity, adapter id/version, UTC receipt time, assessment
date, typed facts, structured evidence availability and opaque document references.
No identity or time is generated. Document references are identifiers, never paths
or URLs; this boundary does not fetch, open or verify document content.

Identifiers are bounded to 128 ASCII letters/digits plus `.`, `_`, `@`, `-`, starting
with an alphanumeric character. Adapters must preserve external identifiers in their
own mapping when normalization is necessary. Facts, evidence and reference maps each
have at most 256 entries; each evidence requirement has at most 32 unique references.
References are sorted and copied; all collections are detached and read-only.
Transport adapters must separately bound raw bytes, encoding, parser depth and their
own mapping vocabulary before constructing this typed request.

Normalization checks the selected Knowledge schema and records its exact pack/release.
Undeclared fields/evidence, wrong field types, invalid statuses and inconsistent
availability/reference metadata fail before storage. Omitted facts become UNKNOWN;
omitted declared evidence becomes MISSING. Present evidence requires a reference;
missing evidence cannot simultaneously claim attached references. This preserves the
upstream assertion, not professional verification of the underlying document.

`INormalizedIntakeStore.AppendAsync` is an atomic adapter contract. It must enforce:

- unique `(source system, message id)`;
- unique `(source system, upstream case id, upstream revision)`;
- stable platform case/type mapping for each upstream case across revisions;
- increasing new upstream revisions; existing identical historical receipts may replay.

The same semantic normalized content returns the **original** immutable receipt.
A retransmission may have a new message id or local receipt time; those delivery
values do not change the case content. Original provenance is retained. Changes to
case/type identity, upstream case/revision, adapter version, selected Knowledge,
assessment date, facts, evidence or references under an existing key conflict.
A new revision creates a separate record and never replaces the original.

The Application service validates the returned receipt binding and propagates
conflicts; it does not retry with new ids or rewrite history. Tests exercise the
atomic contract through an in-memory test adapter. A durable PostgreSQL adapter,
transactional assessment/process creation and authenticated transport remain future
integration slices. This interface alone does not provide persistent idempotency.

Two intentionally different synthetic upstream fixtures demonstrate the boundary:
`intake-alpha.json` uses an answer object and document list; `intake-beta.xml` uses
answer/attachment elements. Their test-only adapters normalize identical exact facts
and evidence while retaining distinct source/adapter identities. Decimal values use
invariant decimal parsing, unknowns remain explicit, unmapped truth values fail, and
the XML fixture parser rejects DTD/entity resolution. These fixtures are not real MD
formats and must not be advertised as compatible with any external system.
