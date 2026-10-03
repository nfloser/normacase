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

`CaseWorkQueueProjectionService` requires one immutable assessment record, its
recorded `AssessmentRouting`, the current `CaseProcessingInstance` and a queue
configuration. It verifies case and assessment identity before projecting.

Queue membership depends only on:

```text
workflow id
+ workflow version
+ current process state id
```

Assessment outcome and triage disposition are copied into the projection for
drill-down/display, but they do not select or recalculate the queue. The transition
from assessment routing into process state already belongs to
`CaseProcessingRoutingService`.

This separation prevents UI/query code from silently becoming another decision layer.

A projected work item retains:

- case id and immutable case-input revision,
- assessment id and recorded assessment outcome,
- recorded triage disposition and policy identity,
- workflow id/version,
- current process state and process revision,
- work-queue configuration id/version and optional queue id.

Changing process state and revision changes queue membership on the next projection.
The projection does not transition, approve, authenticate, authorize or persist
anything.

Later adapters may persist/index these projections for efficient queue queries and
join them to immutable assessment, Decision Trace, evidence and audit history for
case drill-down. Productive authorization and human approval remain separate slices.

All current examples are synthetic and make no claim about MD-specific queue names,
roles or operational process states.
