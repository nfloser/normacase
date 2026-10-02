# PostgreSQL assessment storage

This adapter is the first production-shaped persistence boundary for immutable
`AssessmentRecord` documents. It does not change deterministic evaluation and is
not exposed through the user-facing HTTP API.

## Ownership and dependencies

`NormaCase.Application` owns the storage-neutral `IAssessmentRecordStore`
contract. `NormaCase.Persistence.PostgreSql` implements that contract with Npgsql.

Domain, Knowledge and RuleEngine do not depend on PostgreSQL.

The adapter uses the strict versioned `AssessmentRecordJson` serialization contract.
It never reconstructs a historical record from relational columns.

## Stored representation

Each row contains queryable technical metadata:

- assessment id,
- case id,
- Knowledge Pack id,
- Knowledge Release,
- platform version,
- assessment date,
- queryable UTC recording time,
- exact .NET UTC ticks for lossless timestamp verification,
- AssessmentRecord format version.

The complete record is stored separately in a PostgreSQL `json` column. `json`
is intentional: unlike `jsonb`, PostgreSQL preserves the original JSON text instead
of normalizing it. The adapter computes SHA-256 over the UTF-8 bytes of that exact
JSON text.

On load, the adapter:

1. reads the exact JSON text and stored SHA-256,
2. verifies the fingerprint in constant time,
3. deserializes through `AssessmentRecordJson`,
4. verifies that the queryable metadata matches the deserialized historical record.

A mismatch fails closed with `AssessmentRecordIntegrityException`.

## Append-only database boundary

`normacase.assessment_records` uses the assessment id as its primary key.
A duplicate id fails and cannot replace the original row.

PostgreSQL triggers reject both `UPDATE` and `DELETE` against assessment rows.
This protects the normal application/database boundary from accidental mutation.

This is not a substitute for operator controls. A database administrator with enough
privilege can alter schemas, triggers or raw storage.

## Integrity limitation

SHA-256 detects accidental corruption and inconsistent storage. It is **not**
authentication, a digital signature or tamper-proof evidence. An administrator able
to rewrite both JSON and fingerprint can create a matching pair.

Cryptographic signing/key management would require a separate threat model and
reviewed design.

## Migrations

SQL migrations are embedded in the PostgreSQL adapter and applied by
`PostgresMigrationRunner`.

The runner:

- creates the migration ledger explicitly,
- applies pending migrations in one database transaction,
- stores a SHA-256 checksum for each migration,
- accepts repeated runs,
- fails if an already applied migration's embedded SQL changes.

Migration execution is explicit. Constructing a store does not modify a database.

## Sensitive-data boundary

Tests and repository fixtures are synthetic only.

The current local HTTP workbench does not use this store. Production exposure,
authentication, authorization, retention/deletion policy, backup controls and
operator access are separate prerequisites before real sensitive records may be used.

## Integration test

The CI job starts a real PostgreSQL 18 service and sets:

```text
NORMACASE_POSTGRES_TEST_CONNECTION
```

Tests cover:

- repeatable migrations,
- lossless record roundtrip including UNKNOWN/evidence/source trace/high-precision decimal,
- concurrent duplicate assessment ids,
- database-enforced UPDATE/DELETE rejection,
- fingerprint failure before deserialization,
- redacted connection failures.


## Human review audit trail versions

Human-review history uses the same infrastructure boundary but remains a separate
immutable concept from the system assessment.

`PostgresAssessmentAuditTrailStore` stores an accepted `AssessmentAuditTrail` as
an immutable version row identified by assessment id and the trail's last sequence.

The first stored version must contain only sequence 1
(`ASSESSMENT_CREATED`). Every later write must contain the complete existing trail
plus exactly one new event. Before inserting, the adapter locks and verifies the
latest version, reconstructs it through `AssessmentAuditJson`, and compares the
incoming prefix with the exact stored JSON. A divergent or skipped history is rejected.

Competing writes for the same next sequence cannot create two histories: the
database primary key on `(assessment_id, last_sequence)` allows at most one
version. UPDATE and DELETE are rejected by database triggers.

Each version stores:

- assessment id and last sequence,
- queryable last-event UTC timestamp,
- exact .NET UTC ticks for lossless timestamp verification,
- audit JSON format version,
- the exact strict `AssessmentAuditJson` document in PostgreSQL `json`,
- SHA-256 of those UTF-8 JSON bytes.

The assessment id is a foreign key to the immutable assessment record. Audit
history therefore cannot be persisted for a non-existent assessment.

SHA-256 still provides corruption/inconsistency detection only. It does not prove
who created or reviewed a record. Actor ids imported from audit JSON remain
unauthenticated claims until authentication/authorization and operator identity
controls are implemented.
