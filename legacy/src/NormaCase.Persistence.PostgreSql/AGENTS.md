# PostgreSQL persistence adapter

Follow the root AGENTS.md.

- This project is infrastructure only. Do not add rule, domain, UI or HTTP behavior.
- Depend on the storage-neutral Application contract and strict Serialization contract.
- Historical assessment rows are append-only. UPDATE and DELETE must be rejected by PostgreSQL itself.
- Persist the exact AssessmentRecord JSON as PostgreSQL `json`, not `jsonb`, so whitespace/property order are not normalized by storage.
- Verify SHA-256 before deserializing a loaded record.
- Never log or include connection strings, stored JSON, case values or database exception detail in public exception messages.
- Migrations are explicit, versioned, transactional and checksum-verified.
- Do not invent ids, timestamps, active knowledge or platform versions.
- Integration tests use synthetic fixtures and a real PostgreSQL instance.
