# Durable exact Knowledge Releases

`IKnowledgeReleaseStore` is an Application boundary for immutable release artifacts.
The PostgreSQL adapter stores exact original JSON as `json` (not `jsonb`), its
lowercase SHA-256 fingerprint and the manifest identity/lifecycle/validation metadata.
Artifact registration uses the existing strict loader and validator. It does not
approve, activate, sign or promote a Knowledge Release.

Migration 008 creates `knowledge_release_artifacts` keyed by exact pack/release id,
with bounded identities and at most 1 MiB UTF-8 JSON. Database triggers reject
UPDATE/DELETE. Concurrent equal registration is idempotent; changed bytes under
the same identity fail with `KnowledgeReleaseIdentityConflictException`, including
changes of whitespace. No implicit latest or active selection exists.

Load checks the fingerprint before parsing, validates the original JSON again and
compares every indexed manifest attribute with that content. Invalid metadata fails
closed, including a database assertion of a stronger validation class. Callers get
the exact original JSON and a fresh validated pack when calling `LoadPack()`.
Database exceptions remain bounded technical errors without connection or content
details. Cancellation rolls back registration.

The opt-in persistent synthetic host registers its bundled releases before case
initialization and then loads the exact artifacts from the database. Unchanged
bundles can restart idempotently. Reusing an identity for a changed installed pack
prevents startup rather than rewriting the catalog. The anonymous preview needs
no database and continues loading its local synthetic files.

Upgrade from a previous build establishes the installed release artifacts at first
startup; it cannot reconstruct unknown historical pack bytes from old assessment
traces. Existing assessments remain immutable. Retained older releases remain
available through exact store lookup even when a newer release is registered.
Receipt correction/reassessment and controlled release activation are separate
application operations; this adapter does not silently change their release binding.

PostgreSQL backup/restore includes the release table, trigger and original JSON.
Archive platform bundles, external configuration and approved source artifacts with
the operator's backup policy. Checksums and mutation triggers are integrity controls,
not signatures or protection against a privileged database/schema administrator.

Tests cover concurrent idempotency, whitespace conflicts, exact restarted reads,
two structurally different synthetic packs, historical selection, fresh pack loads,
mutation rejection and false validation-level metadata. Host/database/browser and
restore CI use the integrated catalog. Knowledge Change review/approval history,
separation of duties, activation UI and signing remain open in issue #184.
