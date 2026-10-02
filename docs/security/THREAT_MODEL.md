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

Baseline: versioned Knowledge Releases and immutable historical assessments. Recorded assessments retain the exact versioned input/output artifacts and SHA-256 fingerprints; human review is a separate append-only provenance record. Fingerprints detect replacement/corruption but are not signatures, authorization controls or proof of actor identity.

### Malicious Knowledge Pack
A pack could attempt code execution or invalid/cyclic references.

Baseline: declarative schemas only; no scripting/eval; validate schema, references, dependencies, temporal overlaps and tests before activation.

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
