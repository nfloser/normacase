# Durable correction transaction and history

The PostgreSQL case-review adapter implements `ICaseCorrectionTransactionStore`.
A correction requires the current live authorization scope, exact READ and CORRECT
actions and the exact case grant. It borrows that backend and transaction; it cannot
reconnect or commit independently. Input, assessment, review snapshot and correction
relation append together. The outer authorization scope commits once.

The intake stream lock precedes the existing case-review lock, matching intake lock
order. Human review and correction share the case lock. The application validates
expected case/process/audit revisions against the state actually loaded under lock.
A stale attempt fails explicitly; there is no implicit retry or overwrite.

Migration 016 adds immutable correction relations bound to their exact review snapshot
and both retained assessments. Reload verifies hashes, relation metadata, previous and
new revisions, original/new intake identities and inputs, chronology, exact Knowledge
release and fresh workflow routing. Ordinary human-review transitions retain their
existing append checks. Input verification uses the same connection, including when
a pool has only one connection.

`LoadHistoryPageAsync` returns at most the requested page plus one continuation row
ordered by the explicit storage version. `LoadRevisionAsync` returns one exact stored
snapshot. Both verify the complete retained chain under the case lock. The storage
version is distinct from case/process/audit revisions. These adapter methods confer
no caller access; the authenticated API must wrap them in its live READ authorization.

Tests use real PostgreSQL to cover concurrent corrections, exact original receipt and
assessment preservation, new human approval after correction, stale approval denial,
one-connection rollback of all staged appends, exact bounded historical reads and
database rejection of relation mutation. #185 remains open for clarification records,
German workbench/API behavior and the completed correction-to-outbound product journey.
