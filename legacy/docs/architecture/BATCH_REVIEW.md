# Bounded batch review orchestration

Batch review is an explicit Application capability, not a shortcut around single-case
review. `BatchReviewService` accepts a versioned `BatchReviewPolicy` and delegates
every attempted item to the existing `CaseReviewService`. Each case therefore keeps
its authoritative aggregate update, actor/case/action authorization, exact revision
checks, workflow transition and append-only audit entry.

No batch is enabled merely because individual review is enabled. The batch policy
must bind the exact single-case review-policy id and version, choose a maximum between
1 and the platform ceiling of 100, and select one failure mode:

- `Continue` attempts later commands after a known case-local denial, conflict or
  policy rejection.
- `Stop` records later commands as `NotAttempted` after the first known case-local
  failure.

The Application orchestrator does not prescribe a cross-case transaction. With
independently committing stores, an earlier item remains committed when a later case
fails. The persistent HTTP adapter adds the authorization transaction described below. The ordered result contains only the
caller-supplied case/review identities and a bounded status: committed, denied,
conflict, policy rejected or not attempted. It contains no exception text or case
content and never retries a stale command against newer state.

Before the first transaction, the service materializes and validates the complete
bounded request. Empty/oversized input, duplicate case ids, duplicate review ids,
invalid commands and mismatched policies fail before mutation. Cancellation and
unexpected storage, binding or integrity failures propagate and stop orchestration;
already committed earlier case transactions remain authoritative when using standalone
stores. The persistent HTTP adapter rolls back its complete operation on such failures.

The opt-in persistent synthetic host exposes this orchestration at
`POST /api/review/batch-reviews`. It requires the verified server-side identity, exact
batch-policy id/version and a separate configured `BATCH` action for every requested
case. The legacy single-credential compatibility mode does not confer that action.
Every item carries explicit case, assessment and review identities plus expected
case/process/audit revisions. Actor and recording time remain server-controlled.

The adapter validates the complete strict JSON envelope, bounds it to 100 items,
rejects duplicate identities and verifies that all requested cases are readable,
batch-enabled, present and bound to the supplied assessment before the first mutation.
Unreadable cases return the same not-found response as unknown cases. It then uses the
existing PostgreSQL aggregate store inside one protected authorization transaction.
The current entitlement revision and account suspension are checked under the actor
lock. All per-case writes and request/result rows share that backend and commit before
the response is returned. Known case-local failures still follow the selected policy;
unexpected errors, cancellation or backend loss roll back the whole HTTP operation.
This avoids stale authority after lease loss and needs only one pool connection. The response
contains only ordered case/review ids and the stable technical status codes
`COMMITTED`, `DENIED`, `CONFLICT`, `POLICY_REJECTED` or `NOT_ATTEMPTED`.

Each API request additionally carries a bounded stable `requestId`. PostgreSQL binds
that identity append-only to the verified actor, a canonical SHA-256 request
fingerprint and the first server-assigned UTC review time. Request metadata and the
terminal ordered result live in separate insert-only tables. A transaction advisory lock in the protected host (session lock for standalone
store callers) serializes concurrent uses of the same identity; equal completed retries return the
exact retained result, while changed content or actor reuse fails with 409.

Older or standalone operations can leave registered request metadata without a terminal
result. New protected HTTP operations commit registration, successful items and the
terminal result together, or retain none of those writes. A
retry reuses the retained server time and recognizes an earlier case commit only when
review id, assessment, actor, time, disposition, reason and override all match the
retained command exactly. Merely observing a newer case revision never counts as
success. Remaining commands still pass through the normal single-case transaction,
and the reconstructed terminal result is appended before it is returned.

The German workbench projects `batchAllowed` only for readable approval-queue cases
that currently have both `ACCEPT` and `BATCH` grants. It submits one shared explicit
reason and exact string revisions for at most 100 selected cases. Selection never
changes the server authorization boundary. Ordered technical statuses are rendered as
bounded German per-case results rather than exposed as untranslated UI text.

The browser keeps an in-flight request body only in component memory. If cancellation
or a network failure makes completion ambiguous, selection and identities are locked
and the operator can send the exact same request again. A new identity is never
generated implicitly for that retry. Terminal results clear the retained request;
logout, authentication loss and unmount remove request, selection and result state.

This changes no assessment, UNKNOWN, Knowledge or Domain semantics and does not confer
organizational permission for batch review. The generic technical capability in #186
is covered by Application, API, PostgreSQL and real browser tests; productive use still
requires an institution-approved batch policy and role mapping.
