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

## Read-only synthetic workbench

The loopback API exposes `GET /api/work-queues` and `GET /api/work-cases/{caseId}`.
The fixed demo-g examples run through AssessmentRecorder, AssessmentTriageService,
CaseProcessingRoutingService and CaseWorkQueueProjectionService at host startup.
Both complete positive and negative results await human approval. Missing input and
human-review results enter separate queues. A fifth technical case uses process-only
membership and has null assessment identity/JSON and no evidence.

Case and process revisions are invariant decimal strings at the HTTP boundary.
The detail contains the original serialized immutable assessment and recorded evidence
statuses; the frontend parses assessment decimals losslessly and reuses DecisionTrace.
Queue/state labels are German translation resources; React never chooses membership.
Unknown case ids return a bounded generic 404. Existing origin, loopback and no-store
protections cover both endpoints.

This is deterministic in-memory synthetic demonstration data, recreated on restart.
It has no authenticated productive case management, persistence, human approval,
overrides or batch actions. Issue #116 remains open for those subsequent slices.

Authorized review transactions are specified in [CASE_REVIEW.md](CASE_REVIEW.md).
The current HTTP demo remains read-only; future queue refreshes must use the
successfully committed process returned by that boundary.
