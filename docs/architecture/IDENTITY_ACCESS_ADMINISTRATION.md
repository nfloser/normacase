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

This slice deliberately does not store credentials, expose credential values, introduce
wildcard permissions or infer organizational roles. The administrator credential remains
external configuration and can administer only the explicitly configured synthetic users.
General action/case-grant changes, administrative separation-of-duties policy and the
German administration UI remain part of issue #183. A productive identity provider,
session invalidation model and institution-specific roles require separate approval.
