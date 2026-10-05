# Reviewed entitlement changes

The first durable grant-administration slice stores complete case/action entitlement
snapshots rather than mutating individual permission rows. A proposal binds an explicit
change id, target actor, expected effective revision, bounded action and case-id sets,
proposer, UTC time and reason. All identities and times are supplied by a future trusted
authenticated adapter; the storage contract never invents them.

`EntitlementAdministrationPolicy.RequireDistinctDecisionActor` makes separation of
duties explicit. When enabled, the proposal actor cannot approve or reject the same
change. PostgreSQL repeats that check inside the decision transaction so bypassing the
Application service cannot remove the boundary. A decision is append-only and exactly
one decision can win for a proposal. A decision timestamp before its proposal is rejected
at both boundaries instead of producing contradictory audit chronology.

Approval obtains a transaction-scoped advisory lock for the target identity and compares
the proposal's expected revision with the latest effective revision. Only an exact match
appends the next complete entitlement version. Competing or stale approvals fail without
recording an effective version. Rejection records the independent decision but changes no
entitlements. Proposal, decision and effective-version tables reject `UPDATE` and
`DELETE` through the existing database audit trigger.

The contract is domain-neutral: action ids are bounded technical identifiers and case ids
are opaque. It does not infer roles, medical authority, wildcard scope or permission from
the requested actor. Empty snapshots are valid and represent removal of every managed
grant once an approved change becomes effective.

The persistent synthetic host reconciles each configured review identity into an
immutable revision-zero baseline. A restart accepts the equivalent sorted action/case
snapshot; an unexplained configuration mismatch fails startup instead of silently
changing authority. Approved versions supersede that baseline and are the live source
for queue scoping, case detail, review, batch, intake and outbound authorization.

Protected reads and mutations acquire the same transaction-scoped per-identity advisory
lock as approval. The effective snapshot is loaded only after that lock is held and the
lock is retained until the protected read or mutation has committed. Review, intake,
assessment initialization, batch request/results and inbox receipts borrow the same
connection and transaction. Nested stores cannot commit or dispose that transaction.
A connection loss therefore rolls back all protected writes instead of allowing a
stale callback to reconnect and commit separately. A protected operation uses one
pooled connection, including bounded batches. Concurrent revocation
therefore orders entirely before or after the operation; a stale pre-check cannot cross
the change boundary. Read-only projections remain bounded by the current exact case set.
File delivery is deliberately outside the database transaction: an exact immutable
outbound command (payload hash, authenticated actor, entitlement revision and UTC time)
is committed under the authorization lock before any file side effect. If that commit
fails, no file is dispatched. Revocation prevents new commands; it does not recall an
already committed command. Retry uses the same message/destination identity, bounded
idempotent file sink and durable terminal receipt. This is synthetic dispatch authority,
not institution approval or a general background delivery worker.

Persistent legacy wildcard credentials fail startup before connecting to PostgreSQL.
The non-persistent legacy preview retains its historical local configuration behavior
and receives no implicit `BATCH` permission.

A productive identity provider and institution-specific role mapping remain external
prerequisites; they do not alter this generic version/locking contract.
