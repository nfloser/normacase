# Retained Knowledge evidence

`KnowledgeEvidenceArtifact` is a storage-neutral immutable record for SOURCE,
IMPACT or TESTS evidence. It retains exact text, a bounded title, SHA-256 of the
strict UTF-8 content, explicitly supplied actor and UTC microsecond timestamp.
Content is nonempty and at most 64 KiB UTF-8. Only tab/CR/LF control characters are
accepted; invalid surrogate sequences fail. Content is never normalized, executed,
parsed as HTML or fetched from a supplied URL. This boundary handles text evidence,
not PDF/binary extraction or domain interpretation.

Migration 013 retains these artifacts in PostgreSQL with append-only mutation
triggers. Exact duplicate registration is idempotent, including kind, title, actor
and time. Changed identity reuse conflicts. Loading recomputes the content hash and
validates metadata before returning the record. The hash detects content inconsistency;
it is not a producer signature. Privileged direct database/schema access remains
outside the application trust boundary. Existing migration-role default grants provide
only SELECT/INSERT to the runtime role.

## Governance binding

`PostgresReviewedKnowledgeActivationStore(requireRetainedEvidence: true)` verifies
all three referenced artifacts inside each proposal, decision and activation
transaction. Their kinds must match the reference slots, hashes must verify and
recording cannot postdate the proposal. Immutable identities bind the proposal to
retained exact evidence without rewriting release manifests or historical assessments.
A failed verification rolls back the command. Evidence cannot be overwritten to
repair an existing proposal silently.

The default storage contract remains reference-compatible for previously retained
history and trusted integrations using their own external evidence repository. The
synthetic HTTP host explicitly opts into retained evidence on every mutation. Old
reference-only proposals/decisions remain readable; they cannot be newly reviewed or
activated in this host without valid original evidence. Create a new proposal when
original evidence is missing. This is an explicit synthetic technical policy, not a
medical evidence standard or domain approval.

## Synthetic HTTP/workbench boundary

Only the configured PROPOSE identity can POST `/api/review/knowledge/evidence`.
The strict JSON body contains only kind/title/content; ID, actor and time are owned
by the server. A separate bounded request limit of 397312 bytes permits JSON escaping
of a 64 KiB text while the decoded UTF-8 limit remains independently enforced.
Other governance command bodies remain limited to 4 KiB. Wrong keys, duplicate keys,
invalid UTF-8, excessive content and unknown kinds fail. No file path, URL fetch or
executable document operation exists.

The German proposal form records the three synthetic texts, then submits the exact
returned evidence identities. A cancelled or failed sequence may retain unused
artifacts; it does not partially create a proposal or auto-retry. Reconcile the change
list before intentionally retrying an uncertain proposal. Retention of unused artifacts
is deliberate in this append-only synthetic boundary; productive retention policy is
an external operator requirement.

Authorized change detail includes verified evidence, original text/hash/provenance and
`retainedEvidenceComplete`. The browser shows text through React text nodes in a
wrapping, bounded-height block, including literal HTML/script text. It neither executes
nor interprets it. Missing/mistyped/late historical evidence disables new review and
activation controls; server verification remains authoritative. Existing logout,
unmount, 401 and stale-response guards apply throughout the evidence/proposal sequence.

Unit, real PostgreSQL, API and browser tests cover exact Unicode/whitespace, limits,
concurrent duplicate registration, restart, mutation triggers, false hashes, missing and
mistyped references, late evidence, authorization and script-text rendering. The generic
controlled consumption of activated releases remains open in #184; synthetic stored
texts do not supply institution-specific or medically authoritative approval.
