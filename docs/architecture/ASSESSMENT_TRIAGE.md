# Assessment routing into human work queues

The Application `AssessmentTriageService` routes an already recorded deterministic
assessment. It does not change the original input, result, trace, release or date,
re-evaluate rules, approve a case, authenticate a reviewer or infer medical facts.
Upstream normalization and assessment capture precede this boundary.

| Routing disposition | Meaning |
| --- | --- |
| `ReadyForApproval` | Complete, known recorded result allowed by the explicit routing policy; still requires human approval. |
| `Incomplete` | Recorded assessment is incomplete or required fields are missing; request clarification. |
| `HumanReview` | Remaining uncertainty or policy restriction requires focused human review. |

`ApprovalRoutingPolicy` has an explicit id/version and a detached set of allowed
known outcomes. Both supported and unsupported results can await approval; neither
is approval itself. Incomplete/human-review outcomes cannot be permitted by policy.
An empty policy routes every otherwise complete assessment to human review.

Routing checks the complete recorded condition tree, referenced evidence dependencies,
derived numeric-expression children and independent domain outputs. Missing rule
trace, unknown conditions/calculated values/output values, unresolved referenced
evidence and any conflicting supplied evidence block readiness. This is deliberately
conservative: an unknown optional branch blocks readiness even when another branch
already determines the platform outcome. Unused optional facts and missing evidence
that no recorded dependency references do not independently block routing.

Required-field incompleteness takes precedence over human-review queue placement;
all other reasons remain attached so conflicts cannot disappear from the record.
Reasons contain technical codes and field/output/path references, not patient values.
Human interfaces translate these codes separately from Knowledge-owned field labels.

These three dispositions describe routing policy output, not the case lifecycle.
They do not define workflow states. `CaseProcessingRoutingService` may consume this
recorded routing decision and map it through a separately versioned process-routing
policy to one explicit opaque transition. It does not re-inspect assessment facts or
invent a fallback transition; missing/unavailable mappings fail closed. Configured
lifecycle states and revisioned transitions remain owned by the workflow/case layer.

The detached routing result retains case id, assessment id and policy identity. It is
a projection of a trusted recorded result, not an authenticity check or a substitute
for strict record deserialization/replay, authenticated intake or persistence integrity.
Input/output uncertainty is never converted to yes/no. No implicit ids, time, network,
AI, vendor contract or current Knowledge lookup enters routing.

An inbound adapter must later bind upstream order/revision identity to an immutable
case revision and assessment. Queue reads and human approvals must refer to that exact
revision; new input invalidates readiness for the previous revision. Automatic upstream
polling/webhooks, idempotency, durable queue persistence, authorizations and outbound
acknowledgements remain separate integration slices. The current demo remains synthetic.
