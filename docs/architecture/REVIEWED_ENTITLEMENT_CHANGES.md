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

Protected mutations acquire the same transaction-scoped per-identity advisory lock as
approval. The effective snapshot is loaded only after that lock is held and the lock is
retained until the protected operation has committed or failed. Concurrent revocation
therefore orders entirely before or after the operation; a stale pre-check cannot cross
the change boundary. Read-only projections remain bounded by the current exact case set.
The non-persistent legacy preview retains its historical local configuration behavior
and receives no implicit `BATCH` permission.

A productive identity provider and institution-specific role mapping remain external
prerequisites; they do not alter this generic version/locking contract.
