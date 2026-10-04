# Bounded batch review orchestration

Batch review is an explicit Application capability, not a shortcut around single-case
review. `BatchReviewService` accepts a versioned `BatchReviewPolicy` and delegates
every attempted item to the existing `CaseReviewService`. Each case therefore keeps
its own authoritative transaction, actor/case/action authorization, exact revision
checks, workflow transition and append-only audit entry.

No batch is enabled merely because individual review is enabled. The batch policy
must bind the exact single-case review-policy id and version, choose a maximum between
1 and the platform ceiling of 100, and select one failure mode:

- `Continue` attempts later commands after a known case-local denial, conflict or
  policy rejection.
- `Stop` records later commands as `NotAttempted` after the first known case-local
  failure.

The batch itself is deliberately not one cross-case transaction. A committed item is
never rolled back because a later case fails. The ordered result contains only the
caller-supplied case/review identities and a bounded status: committed, denied,
conflict, policy rejected or not attempted. It contains no exception text or case
content and never retries a stale command against newer state.

Before the first transaction, the service materializes and validates the complete
bounded request. Empty/oversized input, duplicate case ids, duplicate review ids,
invalid commands and mismatched policies fail before mutation. Cancellation and
unexpected storage, binding or integrity failures propagate and stop orchestration;
already committed earlier case transactions remain authoritative.

The opt-in persistent synthetic host exposes this orchestration at
`POST /api/review/batch-reviews`. It requires the verified server-side identity, exact
batch-policy id/version and a separate configured `BATCH` action for every requested
case. Every item carries explicit case, assessment and review identities plus expected
case/process/audit revisions. Actor and recording time remain server-controlled.

The adapter validates the complete strict JSON envelope, bounds it to 100 items,
rejects duplicate identities and verifies that all requested cases are readable,
batch-enabled, present and bound to the supplied assessment before the first mutation.
Unreadable cases return the same not-found response as unknown cases. It then uses the
existing PostgreSQL aggregate store and per-case review transaction. The response
contains only ordered case/review ids and the stable technical status codes
`COMMITTED`, `DENIED`, `CONFLICT`, `POLICY_REJECTED` or `NOT_ATTEMPTED`.

This changes no assessment, UNKNOWN, Knowledge or Domain semantics and does not confer
organizational permission for batch review. A repeated request is not yet a durable
idempotent batch operation: already committed items return revision conflicts. Issue
#186 therefore still requires durable whole-request identity/result retention and the
German selection/result workflow before the larger capability can be considered
complete.
