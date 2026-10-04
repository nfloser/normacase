# Threat model

Status: maintained synthetic integration baseline. Refine again for each concrete institutional interface, productive identity model and deployment boundary.

## Assets

- structured case and evidence data,
- identity data,
- assessment outcomes and traces,
- domain knowledge and Knowledge Releases,
- audit history,
- credentials, keys and configuration.

## Trust boundaries

Expected boundaries include browser/API, authentication provider/application, application/database, knowledge import/application, operator/admin interfaces and export/import channels.

## Important threats

### Unauthorized access
Broken access control may expose sensitive case or identity data.

Baseline: deny by default, least privilege, explicit authorization and later separation of assessor/reviewer/knowledge/audit roles.

### Knowledge tampering
A changed rule or source could alter assessments while appearing legitimate.

Baseline: immutable version identities, content hashing/signature support, controlled activation, source traceability and audit events.

### Historical rewriting
Editing an active rule in place could destroy reproducibility.

Baseline: versioned Knowledge Releases and immutable historical assessments.

### Malicious Knowledge Pack
A pack could attempt code execution or invalid/cyclic references.

Baseline: declarative schemas only; no scripting/eval; validate schema, references, dependencies, temporal overlaps and tests before activation.

### Snapshot substitution or false provenance
A portable assessment snapshot can be internally consistent while still having been
created or replaced by an unauthorized actor. Its SHA-256 fingerprints detect
accidental alteration; they do not authenticate the producer, executable, storage
location or domain approval.

Baseline: replay validates embedded knowledge, inputs and the complete deterministic
result without network lookup, but treats platform identity as caller-supplied.
Sensitive snapshots stay under operator control and are not logged. Future productive
storage/import must add authorization, trusted provenance and tamper-evident or
signed storage where required.

### Local browser snapshot import
The synthetic workbench sends a selected JSON file only to its same-origin loopback
API. Import may contain malformed UTF-8, excessive content, substituted knowledge or
an internally consistent but unauthenticated assessment.

Baseline: bound file size in the UI and received byte count in the server, use strict
snapshot/knowledge parsing and full deterministic replay, require synthetic embedded
knowledge and the actual API platform identity. Keep results read-only and separate
from active pack presentation metadata. Abort and clear stale requests/results when
inputs change. Do not persist files or log their contents. Existing loopback, Origin,
CSP and no-store restrictions also cover capture and replay. These safeguards do not
provide productive authorization or authentic provenance.

### Workflow history substitution

Workflow run histories contain caller-supplied actor ids and free-text reasons.
Successful history replay proves internal graph/revision consistency only: an
unauthorized producer can supply a different internally consistent history. Import
does not authenticate actors, authorize transitions or confer domain approval.
Run JSON remains under operator control, uses bounded strict parsing and must not
be logged. The optional PostgreSQL adapter serializes run appends transactionally,
checks the unchanged historical prefix and rejects UPDATE/DELETE at the row boundary.
Its checksums detect inconsistency but do not authenticate authors; privileged direct
INSERT/schema access can bypass application guarantees. Productive exposure needs
authentication, case-level authorization and suitable provenance/retention controls.

### Synthetic workflow file continuation

The loopback workflow UI transports complete run histories and free-text reasons.
All import/advance operations validate strict bounded JSON, replay the history and
compare the original graph/source/release against installed SYNTHETIC Knowledge and
the actual API platform identity. The UI exports the exact server JSON, rejects
oversized/invalid UTF-8 files before transfer and cancels stale requests on prospective
metadata or pack changes. Runs are not persisted by the API or browser.

This is a stateless demo boundary: two copies of the same run can be continued
independently, and actor ids remain unauthenticated. Internal replay and catalog
matching do not prove who created the file. Productive multi-user case/workflow
operation must bind authenticated actors and case authorization to a transactional
store and suitable audit/provenance controls.

### Sensitive logging
Medical or identity data could leak through logs and CI.

Baseline: log technical identifiers and error codes rather than case contents; synthetic data only in engineering systems.

### Operational probe disclosure and dependency masking
Health endpoints could disclose infrastructure details, claim readiness from a stale
startup result or accidentally turn a domain check into a production-validity claim.

Baseline: the loopback guard executes first; payloads contain only a fixed German
status and local scope, with no persistence mode, dependency type, identifiers,
versions, counts or exceptions. Liveness has no
dependencies. Persistent readiness opens a current bounded PostgreSQL connection and
compares the complete migration ledger, failing closed on timeout, connectivity or
schema mismatch. It does not evaluate knowledge, cases or approvals and is explicitly
not evidence of medical, privacy, backup or disaster-recovery readiness. See
[operational health probes](../architecture/OPERATIONAL_HEALTH.md).

### Database credential over-privilege

A long-lived host connection with schema-owner rights would let an application
compromise bypass append-only controls by altering or dropping database objects.

Baseline: persistent review can use a separate startup-only migration connection.
The provisioned runtime role has schema usage and table `SELECT`/`INSERT`, but no
DDL, `UPDATE`, `DELETE`, sequence or direct routine rights. Public database/schema
privileges are reduced, and the bootstrap owner is not an application credential.
Real PostgreSQL CI exercises the full host with separated roles and proves forbidden
DDL/deletion fail. Single-connection configuration remains only a compatibility mode,
not the least-privilege deployment profile. See
[PostgreSQL least-privilege host boundary](../architecture/POSTGRESQL_LEAST_PRIVILEGE.md).

### Web attacks
Future web interfaces face XSS, CSRF, injection, SSRF, session and upload threats.

Baseline: framework protections, validation at boundaries, safe output handling, anti-CSRF where relevant and no unrestricted server-side fetching.

### Supply-chain compromise
Dependencies or CI actions could be compromised.

Baseline: dependency review/scanning, minimal dependencies and immutable CI action
references. Repository workflows pin every external GitHub Action to an exact commit
SHA while retaining the reviewed major-version comment for readability. Dependabot
continues to check GitHub Actions weekly, so moving a pin remains an explicit reviewed
repository change rather than an implicit tag movement.

A commit pin prevents a mutable release tag from silently changing already reviewed
workflow code; it does not make the pinned action, GitHub-hosted runner, package
registry or upstream repository trustworthy by itself. Action-update pull requests
still require normal review and CI before merge.

## Current boundary and remaining work

The local synthetic product path now includes authenticated review, PostgreSQL
persistence, bounded JSON/XML intake, reviewed outbound delivery, restart replay and
backup/restore rehearsal. These controls are development acceptance evidence for the
generic architecture; they do not establish productive identity, privacy approval,
medical/domain approval or compatibility with any institution.

The threat model must be refined again when a concrete institutional identity provider,
role/tenant model, real document channel, vendor transport, retention policy,
Knowledge Bundle signing policy or target deployment environment is selected.

### Atomic human-review boundary

The Application case-review service requires a trusted authentication-adapter actor
and an explicit case-scoped authorizer. These types do not authenticate a caller by
themselves. The local synthetic HTTP host derives its actor only from the verified
ASP.NET Core principal and uses the PostgreSQL aggregate store so process/audit updates
commit together with exact case, assessment and revision binding. Request DTOs cannot
supply actor identity.

That proves the architecture against the fixed synthetic-local identity only.
A productive adapter still requires reviewed organizational authentication, case-level
authorization, role/tenant separation, session policy and privacy controls.
See [CASE_REVIEW.md](../architecture/CASE_REVIEW.md).

### Synthetic local bearer identity

An opt-in bootstrap credential authenticates one fixed synthetic actor through
ASP.NET Core. An attacker with the credential can impersonate that shared actor;
loopback does not protect against local compromise. Credentials are external,
canonical 256-bit Base64, checked in fixed time and accepted only from one Bearer
Authorization header. Query/body actor claims are ignored. Local/origin guards and
no-store headers remain mandatory. The default preview exposes no review session;
no mutation endpoint or permissive case authorizer is added. This is not productive
organizational identity. See [synthetic review host](SYNTHETIC_REVIEW_HOST.md).

In multi-user persistent mode, an independent externally configured synthetic
administrator may suspend or reactivate exact configured users. Current state and every
change are retained as revision-checked append-only PostgreSQL audit. Authentication
checks current state on every protected request, so restart is not needed for suspension.
This does not revoke a credential outside NormaCase, provide productive session/token
revocation or establish institutional administrator roles. Reviewed entitlement changes
use separately configured proposal and decision credentials; each endpoint rechecks the
verified server identity. Pending work is bounded, caller-supplied actor/time fields are
not accepted, and stale approvals fail under a per-target transaction lock. Approved
records are not yet a live authorization source: connecting them without transaction-safe
current-revision enforcement would create a time-of-check/time-of-use gap. See
[identity access administration](../architecture/IDENTITY_ACCESS_ADMINISTRATION.md).

The German administration view is not an authorization boundary. It appears only for
the exact verified synthetic administrator, while every list/detail/change endpoint
checks the same server identity independently. Commands contain no caller-selected
administrator or time. Credentials remain in React memory; logout, unmount and 401
abort active requests and clear displayed administrative state. A stale 409 reloads
committed state without automatic retry. The UI shows technical synthetic actor ids,
status, revisions and bounded audit, but never credential material or case content.

### Delayed authenticated browser responses

Review credentials exist only in React memory; no cookies or browser storage are
used. Logout and unmount abort outstanding requests. Every response is checked
against the active request controller after asynchronous work, so an old session
cannot restore case data or controls. A selected case clears previous detail; review
commands carry exact server revisions. A 409 invalidates the stale detail and reloads
committed queues/case without automatic retry. A 401 clears identity, credential,
forms and case data. German status messages are locally bounded, rather than echoing
arbitrary server text that could expose credentials. The browser controls are not
an authorization boundary: the server remains responsible for every case action.

### Batch review amplification and partial completion

A batch endpoint can amplify one authorization or stale-state mistake across many
cases, leak case existence through mixed results or encourage unsafe automatic retry
after partial completion. The synthetic host therefore requires an explicit `BATCH`
grant per readable case and an exact versioned policy, rejects more than 100 or
duplicate commands, verifies every case/assessment binding before the first mutation
and then reuses the single-case authorization/revision transaction for each item.
Unknown and unreadable cases share one not-found response; result entries contain only
case/review ids and bounded status codes. Committed earlier items remain committed on
later failure. PostgreSQL binds a stable request id append-only to actor, canonical
request hash and server time, serializes concurrent retries and retains the exact final
result separately. Recovery recognizes only a fully matching committed review; a
newer revision alone is never success. Changed actor/payload reuse fails with a bounded
409. This is replay safety for the synthetic host, not permission for unattended
organizational bulk approval. The workbench exposes selection only when the readable
queue projection reports current `ACCEPT` plus `BATCH` eligibility, but treats this as
presentation rather than an authorization decision. An ambiguous cancellation or
network failure locks the exact in-memory request for an intentional identical retry;
it never creates a fresh request identity automatically. Logout and 401 erase this
state, and bounded German labels prevent raw server errors or technical statuses from
becoming user-visible diagnostics.

### Reviewed outbound result substitution

A syntactically valid outbound message is not a signature or actor identity. The
factory binds authoritative intake values and knowledge to immutable assessment/review
state and requires exact revisions plus a reviewed terminal state. The authenticated
synthetic host loads persisted authoritative state before export and records durable
delivery receipts for its synthetic PostgreSQL inbox/file destinations; duplicate
delivery identity is replay-safe and conflicting reuse fails closed.

These guarantees stop at the synthetic adapter boundary. A productive exporter still
needs institution-approved case-scoped authorization, destination authentication,
transport guarantees, retention/logging policy and the authoritative vendor contract.
Imported result JSON on its own remains an unauthenticated assertion. See
[outbound results](../architecture/OUTBOUND_RESULTS.md).


### Synthetic Knowledge administration

Exact configured synthetic users receive independent PROPOSE/REVIEW/ACTIVATE
assignments. Unknown users and shared proposer/reviewer fail startup; every endpoint
checks the verified actor and the current authentication suspension gate. Server-owned
IDs/times/hash cannot be supplied through strict bounded command bodies. Only verified
SYNTHETIC artifacts can enter this host's mutation path. Technical approval never
promotes domain validation. Activation is optimistic and transaction-serialized; a
stale UI cannot replace history. This configuration is a trusted local adapter, not
productive role mapping or evidence authentication. References do not prove the
contents or quality of source/test documents. Direct privileged database inserts
remain outside the application trust boundary. The German UI discards delayed responses
on logout and stores no credentials; it is not an authorization boundary. The host
does not automatically consume recorded activations. Transaction-safe live permission
administration and institution-approved authority remain separate implementation and
external policy concerns.
