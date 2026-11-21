# Binding assessment routing to case processing

`AssessmentTriageService` and the case-processing lifecycle have deliberately
separate responsibilities.

The assessment-routing layer answers only:

```text
What class of human work is required by this recorded assessment?
```

It returns one immutable/versioned disposition:

- `ReadyForApproval`
- `Incomplete`
- `HumanReview`

The case-processing layer answers:

```text
Which explicit transition is valid for this case-process configuration now?
```

`CaseProcessingRoutingService` is the narrow Application boundary between them.

## Versioned mapping

`CaseProcessingRoutingPolicy` records:

- its own id/version,
- the exact assessment-routing policy id/version it accepts,
- the exact workflow id/version it targets,
- an explicit mapping from routing disposition to opaque transition id.

The mapping may be intentionally partial. There is no default or fallback transition.

For example, a synthetic process configuration may map:

```text
ReadyForApproval -> prepare-approval
Incomplete       -> request-information
HumanReview      -> request-review
```

Those transition ids are process configuration. They are not platform outcome enums and
do not imply a legal or organizational decision.

## Fail-closed application

Applying a routing decision requires explicit expected case-input and process revisions.
A stale revision throws before any route is considered.

The service then verifies:

1. routing and process refer to the same case;
2. the recorded assessment-routing policy id/version matches the process-routing policy;
3. policy, process and supplied workflow definition share the same workflow id/version;
4. the routing disposition has an explicit configured mapping;
5. the mapped transition exists and is available from the current process state.

Identity/configuration mismatches and missing/unavailable mappings return an explicit
`Unresolved` result. The original process instance remains unchanged.

A successful result returns the exact transition id plus a new immutable
`CaseProcessingInstance` at the next process revision.

## No second decision engine

This boundary must not inspect `AssessmentRecord`, Decision Trace, evidence, rule
outcomes or Knowledge again. That work already happened in deterministic assessment
and conservative assessment routing.

This prevents a second hidden decision implementation from developing in process code.

In particular:

- `ReadyForApproval` means only that the recorded assessment can be routed to an
  approval-oriented process state under explicit policy;
- it never means the case is approved;
- `UNKNOWN`, incomplete inputs and review conditions cannot enter an approval route
  through fallback behavior;
- no clock, generated id, network call, AI service or vendor contract participates.

Authentication/authorization, transactional persistence and human approval remain
separate later boundaries.
