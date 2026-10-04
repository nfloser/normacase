# Operational health probes

Status: implemented for the loopback synthetic host. This is deployment evidence,
not productive operational approval.

## Contract

The host exposes two unauthenticated, loopback-only `GET` endpoints:

- `/health/live` returns `200` when the ASP.NET Core process can serve requests.
  It does not access PostgreSQL or evaluate knowledge.
- `/health/ready` returns `200` when the configured operating mode is usable. In
  anonymous preview mode no persistence is required. In persistent review mode it
  opens a fresh PostgreSQL connection and verifies that every installed migration
  version has the repository checksum and that no unexpected migration exists.
  A connectivity, timeout, disposal or schema mismatch returns `503`.

Both responses contain only fixed German resource strings. They expose no connection
string, database/server name, exception, migration version, credential, case count or
knowledge identity. Existing host/origin restrictions and `no-store`, CSP, frame,
referrer and content-type security headers apply before the endpoint executes.

## Failure and timing semantics

The persistent readiness query has a two-second linked cancellation boundary. It is a
current request-time signal rather than a cached startup result, so a database lost
after successful startup makes the next completed readiness check fail closed. The
liveness endpoint remains successful in that situation, allowing an operator to
distinguish a running process from an unusable persistence dependency.

The host still performs fail-fast migrations before serving persistent review traffic.
Readiness does not run migrations, repair a database, select a Knowledge Release or
provide a deep domain test. Monitoring must not treat either endpoint as evidence of
medical validity, identity/privacy approval, backup success or disaster recovery.

## Deployment use

Probe only through the same loopback boundary as the application. A local supervisor
may use liveness for process restart decisions and readiness for traffic admission.
Do not publish these endpoints by weakening the local-only host guard. Concrete target
probe intervals, restart thresholds and orchestration remain deployment-specific.

Integration tests cover preview readiness, a migrated PostgreSQL store and a store
that becomes unavailable after startup. PostgreSQL adapter verification compares the
database migration ledger with the embedded ordered migration set.
