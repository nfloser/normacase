# Work queue projections

NormaCase work queues are read models over recorded case-processing state. They are
not a second routing engine.

`CaseWorkQueueConfiguration` is explicit and versioned. Each queue has an opaque
technical queue id and assigns one or more opaque process state ids for one exact
workflow id/version. Labels such as German user-facing queue names remain presentation
metadata outside Application.

A state can belong to at most one queue for the same workflow version. Ambiguous
configuration is rejected. A valid process state with no configured queue produces an
explicit `Unassigned` projection; there is no fallback queue.

## Membership first

`CaseWorkQueueProjectionService.Project(process, configuration)` creates a
`CaseWorkQueueMembership` using only:

```text
workflow id
+ workflow version
+ current process state id
```

The membership retains case id/input revision and process revision, but has no
dependency on assessment, Decision Trace or triage.

This is required because a technical/integration exception may need human attention
before a deterministic assessment exists. Such a case can enter a configured technical
queue without fabricating an assessment.

## Optional assessment enrichment

When an assessment exists, the overloaded projection operation accepts the immutable
`AssessmentRecord` and its recorded `AssessmentRouting`. It first creates the same
process-only membership and then validates that case and assessment identities agree.

The resulting `CaseWorkItemProjection` adds:

- assessment id and recorded assessment outcome,
- recorded triage disposition and policy identity.

Those values are drill-down/display metadata. They do not select or recalculate queue
membership. The transition from assessment routing into process state already belongs
to `CaseProcessingRoutingService`.

This separation prevents UI/query code from silently becoming another decision layer.

Changing process state and revision changes queue membership on the next projection.
Neither membership nor enrichment transitions, approves, authenticates, authorizes or
persists anything.

Later adapters may persist/index memberships for efficient queue queries and join them
to immutable assessment, Decision Trace, evidence and audit history for case drill-down.
Productive authorization and human approval remain separate slices.

All current examples are synthetic and make no claim about MD-specific queue names,
roles or operational process states.

## Current synthetic workbench adapter

The loopback demo host materializes one deterministic in-memory workload for the
German workbench. Assessed items are created through the real application path:

```text
AssessmentRecorder
-> AssessmentTriageService
-> CaseProcessingRoutingService
-> CaseWorkQueueProjectionService
```

The workload covers approval-ready, incomplete/missing-information and human-review
routing. A separate integration-error item is projected with the process-only overload
and therefore has no fabricated assessment.

The HTTP adapter exposes technical queue/state ids and revisions. German labels live
in the frontend presentation resources. The browser consumes the recorded projection
and assessment trace; it does not infer routing or queue membership.

This adapter is intentionally read-only and in-memory. It does not authenticate,
approve, reject, override, batch-process or persist cases. Productive queue indexing,
authorization and audited human approval remain separate critical-path work.
