# Threat model

Status: initial baseline. Expand alongside concrete interfaces and persistence.

## Assets

- structured case and evidence data,
- identity data,
- assessment outcomes and traces,
- domain knowledge and Knowledge Releases,
- audit history,
- credentials, keys and configuration.

## Trust boundaries

Expected boundaries include browser/API, authentication provider/application, application/database, knowledge import/application, operator/admin interfaces and export/import channels.

## Important threats

### Unauthorized access
Broken access control may expose sensitive case or identity data.

Baseline: deny by default, least privilege, explicit authorization and later separation of assessor/reviewer/knowledge/audit roles.

### Knowledge tampering
A changed rule or source could alter assessments while appearing legitimate.

Baseline: immutable version identities, content hashing/signature support, controlled activation, source traceability and audit events.

### Historical rewriting
Editing an active rule in place could destroy reproducibility.

Baseline: versioned Knowledge Releases and immutable historical assessments.

### Malicious Knowledge Pack
A pack could attempt code execution or invalid/cyclic references.

Baseline: declarative schemas only; no scripting/eval; validate schema, references, dependencies, temporal overlaps and tests before activation.

### Snapshot substitution or false provenance
A portable assessment snapshot can be internally consistent while still having been
created or replaced by an unauthorized actor. Its SHA-256 fingerprints detect
accidental alteration; they do not authenticate the producer, executable, storage
location or domain approval.

Baseline: replay validates embedded knowledge, inputs and the complete deterministic
result without network lookup, but treats platform identity as caller-supplied.
Sensitive snapshots stay under operator control and are not logged. Future productive
storage/import must add authorization, trusted provenance and tamper-evident or
signed storage where required.

### Local browser snapshot import
The synthetic workbench sends a selected JSON file only to its same-origin loopback
API. Import may contain malformed UTF-8, excessive content, substituted knowledge or
an internally consistent but unauthenticated assessment.

Baseline: bound file size in the UI and received byte count in the server, use strict
snapshot/knowledge parsing and full deterministic replay, require synthetic embedded
knowledge and the actual API platform identity. Keep results read-only and separate
from active pack presentation metadata. Abort and clear stale requests/results when
inputs change. Do not persist files or log their contents. Existing loopback, Origin,
CSP and no-store restrictions also cover capture and replay. These safeguards do not
provide productive authorization or authentic provenance.

### Workflow history substitution

Workflow run histories contain caller-supplied actor ids and free-text reasons.
Successful history replay proves internal graph/revision consistency only: an
unauthorized producer can supply a different internally consistent history. Import
does not authenticate actors, authorize transitions or confer domain approval.
Run JSON remains under operator control, uses bounded strict parsing and must not
be logged. The optional PostgreSQL adapter serializes run appends transactionally,
checks the unchanged historical prefix and rejects UPDATE/DELETE at the row boundary.
Its checksums detect inconsistency but do not authenticate authors; privileged direct
INSERT/schema access can bypass application guarantees. Productive exposure needs
authentication, case-level authorization and suitable provenance/retention controls.

### Sensitive logging
Medical or identity data could leak through logs and CI.

Baseline: log technical identifiers and error codes rather than case contents; synthetic data only in engineering systems.

### Web attacks
Future web interfaces face XSS, CSRF, injection, SSRF, session and upload threats.

Baseline: framework protections, validation at boundaries, safe output handling, anti-CSRF where relevant and no unrestricted server-side fetching.

### Supply-chain compromise
Dependencies or CI actions could be compromised.

Baseline: dependency review/scanning, Dependabot, minimal dependencies and progressively pinned CI actions.

## Open work

Future slices must refine this model when authentication, persistence, document upload/import, exports and Knowledge Bundle signatures are introduced.
