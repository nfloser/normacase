# Durable normalized intake

`PostgresNormalizedIntakeStore` implements the application intake contract. Run the
checksum-verified migration runner before use. Migration 005 adds append-only stream,
record and message-alias tables. No patient fixtures or vendor contracts are included.

The store owns `(source system, upstream case)` to platform-case/type mapping.
Platform case ids cannot be shared across streams. Each revision stores the original
normalized input, knowledge identity, evidence references and original provenance as
exact format-1 JSON plus SHA-256. Loading verifies the digest and relational identity
before structural deserialization; it never loads current knowledge or re-normalizes
historical facts. Structural restore does not authenticate upstream provenance.

Appending takes a transaction-scoped global advisory lock. This deliberately simple
synthetic-host implementation serializes intake writers, including cross-stream
ownership checks. It is not a claim of high-throughput ingestion. Original records
and every message alias are committed together. Same-content retransmission returns
the original record, including its original message and receipt timestamp. Every
new alias is reserved, so reusing it for another revision fails. Content conflicts,
case/type remapping and new stale revisions fail; replaying an already recorded older
revision remains a valid duplicate. Transport receipt time and message id alone do
not change semantic content.

Database triggers reject UPDATE and DELETE on all three tables. Application failures
roll back the transaction, cancellation propagates, and database/integrity exceptions
have bounded messages without connection strings or stored payloads. Database owner
permissions can bypass triggers; protect them operationally. A checksum is corruption
detection, not a signature against a privileged administrator.

Real PostgreSQL integration tests cover original receipts, aliases, cross-source
ownership, stale/content conflicts, concurrent duplicates and database mutation
rejection. This is the durable boundary; connecting new accepted records to assessment,
routing and reviewed outbound delivery is a separate host integration step.
