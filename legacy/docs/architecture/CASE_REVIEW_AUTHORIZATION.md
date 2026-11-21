# Exact case-scoped review grants

`GrantedCaseReviewAuthorizer` implements the existing Application
`ICaseReviewAuthorizer` seam with immutable trusted entitlement snapshots.
Each `CaseReviewGrant` binds an authenticated actor id **and** authentication
authority, an exact case id, review policy id/version, workflow id/version and
an explicit set of permitted human-review dispositions. There are no wildcard
cases, implicit administrator privileges or grants derived from request fields.

An empty grant set denies all reviews. Matching one actor or case alone is
insufficient. Caller-owned grant/action collections are detached on construction.
The authorizer also checks the case against the recorded assessment and process,
and requires the requested action to exist in the current review policy.
The review service retains aggregate binding, exact revision, audit, cancellation
and workflow-transition checks under its store transaction. Authorization does
not override a failed transition or turn missing evidence into a decision.

The local synthetic host now delegates entitlement matching to this implementation.
Its adapter still creates an entitlement only for the fixed authenticated synthetic
actor and its existing permitted synthetic cases. This keeps the local preview
boundary unchanged and is not an organizational entitlement-management system.

An identity/access adapter must obtain grants from trusted server-side configuration
or an authoritative store. It must validate issuer/subject before constructing an
`AuthenticatedReviewActor`, refresh permissions for each action, and account for
revocation and concurrent permission changes. Constructing a grant for the requesting
actor merely because that actor is authenticated is unsafe. Snapshot objects cannot
revoke themselves; a long-lived authorizer must not be used as a current permissions
store. Productive role/tenant mapping, user lifecycle, separation of duties,
permission persistence and administrative audit are dependent platform slices.

Synthetic tests cover actor/authority isolation, accept-only entitlement versus
override, policy-version mismatch, wrong command/grant case, deny-by-default and
mutation of both caller-owned collections. A successful permitted review still
commits the original assessment with the append-only human audit.
