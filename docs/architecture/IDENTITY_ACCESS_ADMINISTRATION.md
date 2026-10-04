# Identity access administration

The persistent synthetic review host has a narrow, reusable current-access boundary.
Configured test users still obtain their identity, case assignments and actions from
trusted external configuration. A separately configured synthetic administrator can
suspend or reactivate one of those exact users without restarting the host.

Every change supplies the expected current revision and appends a PostgreSQL audit row
containing the target actor, resulting state, administrator actor, UTC time and reason.
Rows reject update/delete at the database boundary. Per-actor advisory transaction locks
serialize competing first and later changes. Revision mismatches and no-op changes fail
closed instead of silently producing misleading audit entries.

Authentication checks the current persisted state on every protected request after the
credential has been matched. A suspended credential is therefore rejected for session,
queue, detail, review, intake and outbound requests without a process restart. Existing
browser state is not an authorization grant; the next server request is authoritative.

The German workbench recognizes only the exact separately authenticated synthetic
administrator identity returned by the server session. It loads the configured user
list and committed detail/audit contracts from the protected administration API. A
change contains the target path, exact expected revision, intended state and explicit
reason; administrator identity and UTC time remain server-owned. A stale revision
reloads committed state without retrying the mutation. Logout, unmount and a 401 abort
requests and clear credential, selected identity, form, state and audit from the UI.
Ordinary synthetic users never receive the administration view and remain independently
denied by the API.

This slice deliberately does not store credentials, expose credential values, introduce
wildcard permissions or infer organizational roles. The administrator credential remains
external configuration and can administer only the explicitly configured synthetic users.
General action/case-grant changes and configurable administrative separation-of-duties
policy remain part of issue #183. A productive identity provider,
session invalidation model and institution-specific roles require separate approval.
