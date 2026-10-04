# PostgreSQL least-privilege host boundary

Persistent review supports two independent PostgreSQL connections:

- `ConnectionStrings:SyntheticReviewMigrations` is used only during fail-fast
  startup migration and is disposed before the host starts serving requests.
- `ConnectionStrings:SyntheticReview` is the long-lived runtime connection used by
  stores and readiness checks.

If the migration connection is absent, the host deliberately falls back to the
runtime connection for backward-compatible developer setups. A least-privilege
deployment must set both values. Migration completes before Knowledge Release
registration, synthetic fixture seeding or endpoint mapping. Runtime code does not
invoke the migration runner.

## Provisioned roles

`ops/postgresql/provision-least-privilege.sql` reproducibly maintains two fixed
roles without embedding secrets:

| Role | Database/schema capability | Object capability |
|---|---|---|
| `normacase_migrator` | connect; owns `normacase` | create/alter migration-owned objects |
| `normacase_runtime` | connect; schema usage only | `SELECT` and `INSERT` on current and future migration-owned tables |

Both roles are non-superusers without database or role creation. Public database
connect/temporary access, public schema creation and public access to NormaCase
objects are revoked. Runtime receives no schema creation, `UPDATE`, `DELETE`,
`TRUNCATE`, sequence or direct routine rights. This complements the database
append-only triggers; it does not replace them. The bootstrap database owner remains
an operator-only provisioning, backup and recovery identity and must never be passed
to the application host.

The script is rerunnable by a sufficiently privileged database operator. It rotates
the two external passwords, transfers pre-existing objects inside the `normacase`
schema to the migrator, reapplies default privileges and resets runtime grants. It
does not delete data or databases. Operators must still back up and test recovery
before changing an existing installation.

## Verification and limits

Real PostgreSQL CI starts the host with a privileged migration connection and a
restricted runtime connection, executes the full synthetic roundtrip, proves that
runtime DDL and deletion fail, reruns provisioning, then verifies exact persisted
state. API integration additionally proves missing schema, `UPDATE` and `DELETE`
rights through PostgreSQL privilege checks and rejected commands.

This is a generic platform control using synthetic data. It is not target deployment
approval, a credential vault, a high-availability design or an institutional backup,
update and rollback policy. Target operators remain responsible for secret delivery,
TLS/network boundaries, monitoring, backups and controlled release orchestration.
