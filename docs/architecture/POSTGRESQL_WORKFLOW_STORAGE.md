# PostgreSQL workflow run storage

`IWorkflowRunStore` is the storage-neutral Application contract. Its optional
`PostgresWorkflowRunStore` adapter appends a complete `WorkflowRunRecord`, loads the
latest version, or loads an explicitly requested historical revision. It does not
expose HTTP routes or infer case records, actor identities or active Knowledge.

## Version rows and migrations

Migration 003 creates `normacase.workflow_run_versions` with `(run_id, revision)`
as primary key. Each row stores the exact strict run JSON as PostgreSQL `json`, a
SHA-256 fingerprint and queryable metadata: case, Knowledge Pack/Release, workflow
id/version, current state, platform version, recording time and exact UTC ticks.

The migration is embedded, transactional and checksum-verified by the existing
runner. Previous migrations are unchanged. UPDATE and DELETE triggers reject row
mutation with SQLSTATE 55000. Runtime privileges must not permit schema changes,
TRUNCATE, disabling triggers or bypassing the adapter.

There is deliberately no foreign key to a case table: this platform has an identity
contract but no persisted case aggregate yet. A typed CaseId is not proof that a
case exists or that an actor may access it.

## Transactional append

Each append:

1. serializes a replay-validated immutable run,
2. begins a PostgreSQL transaction,
3. acquires a transaction-scoped advisory lock derived from the exact run id,
4. reads and verifies the latest persisted version,
5. accepts creation only at revision zero or exactly one subsequent transition,
6. compares the incoming historical prefix to the exact stored JSON,
7. inserts one version and commits.

The lock also serializes competing creation before a row exists. A rare hash
collision delays unrelated writes but cannot mix run identities. Locks release on
commit, rollback or connection disposal; no permanent lock row is needed. The
primary key remains an additional duplicate-write constraint.

Case/platform substitution, a changed earlier actor/reason/time/source/graph,
skipped revisions and stale or repeated writes are conflicts. A write never replaces
the previous row. The complete history is repeated in each revision; storage cost
grows with history length, so this initial format is for bounded workflows rather
than high-volume event streaming.

These append guarantees apply to cooperating adapter writers. A privileged direct
INSERT can bypass application history checks; loads still verify each row's own
JSON/replay/fingerprint/metadata. This is not a database authorization mechanism.

## Read integrity and historical continuation

Before returning a version, the adapter checks its SHA-256, strict JSON format,
complete transition replay and all indexed metadata. PostgreSQL timestamps have
microsecond precision, so exact .NET ticks are retained separately and both values
are verified. An explicit historical revision stays independent from later appends
and current Knowledge Packs and can be continued through `WorkflowRunService`.

SHA-256 detects inconsistency and accidental corruption. It does not authenticate
the writer or stop a privileged actor who can replace JSON, metadata and checksum
together. Signed provenance, actor authentication, case authorization, retention,
backups and operational controls are still required before real sensitive data.

Connection failures, conflicts and integrity errors do not include connection
strings, stored JSON or free-text reasons. Database cancellation is propagated.

## Verification

The existing PostgreSQL CI service runs the new integration tests alongside the
assessment/audit tests. Coverage includes repeatable migration 003, exact roundtrip,
historical reads and continuation, competing creation and transition writes,
skipped/divergent histories, case/platform substitution, database mutation rejection,
fingerprint/JSON/metadata corruption and credential redaction. Fixtures are synthetic.

The unauthenticated loopback workbench remains disconnected from this store.
