# Immutable case correction boundary

Part of #185. `CaseCorrectionService` provides the headless boundary for a new input
revision. The persistence adapter must serialize correction with ordinary review and
append the intake, assessment, correction relation and review snapshot in one live
authorization transaction. The API and workbench integration are tracked by #185.

A versioned `CaseCorrectionPolicy` binds one workflow/version and its explicitly
allowed starting states. It grants no actor access. The independent authorizer checks
the trusted authentication adapter output and exact policy against the current case.
Information completion and manual-review correction are separate synthetic policies.

The command names the correction and new assessment identities, expected case,
process and audit revisions, explicit UTC recording time and executing platform version, bounded reason and full
structured corrected input. The retained original receipt must match the current
assessment facts/evidence/date, exact Knowledge release, case and upstream stream.
Only the consecutive upstream/case revision is accepted. Corrections use that same
retained Knowledge release; adopting another release requires a separate migration
policy. The caller supplies the executing platform version; it is never inherited from a historical assessment.

Preparation reuses normalization, deterministic assessment, triage and workflow
routing. Missing data remains UNKNOWN/INCOMPLETE. The new process starts afresh;
previous human approval never carries over. Its separate assessment audit begins at
one. The append-only relation retains the preceding assessment, message, case/process/
audit revisions, configured policy/version, trusted actor/authority, reason and time.
Previous assessment and input objects remain unchanged.

Tests demonstrate both policies, stale revisions, denial, wrong stream/input binding,
nonconsecutive revisions, UNKNOWN routing, bounded detached policy configuration,
cancellation and backward time. Transactional competition, durable history and German
API/UI behavior remain integration acceptance criteria under #185. No institutional
medical, clarification or approval policy is inferred.
