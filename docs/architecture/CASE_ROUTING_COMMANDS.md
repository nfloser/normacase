# Explicit case routing commands

`CaseRoutingCommandService` connects a normalized intake record, its exact captured
assessment and an immutable case-processing instance. It proposes a transition; it
never applies it, saves it, approves a case or changes the assessment.

Before routing, the service verifies case identity and upstream/case-input revision,
workflow definition id/version, exact selected Knowledge pack/release, assessment
date and all normalized facts/evidence. Assessment recording cannot predate receipt.
Substituted input or identity fails rather than routing an unrelated assessment.

`CaseRoutingPolicy` has explicit identity/version, the existing conservative outcome
policy, an immutable Workflow definition and bounded detached transition mappings.
Mappings are keyed by current opaque state and routing disposition. A separate explicit
mapping handles technical failure after normalized intake; malformed pre-intake messages
belong to the intake exception boundary and are not fabricated as assessed cases.

Every target transition must exist from the configured source state. Duplicate mappings
are rejected. Uncertain and technical routes cannot share the approval-preparation
route's target state, including through different transition aliases. Missing mappings
return an unresolved plan with no transition: there is no implicit/default approval.

A plan retains case/input revision, optional assessment identity, workflow identity,
expected process revision and routing policy id/version. Assessment plans also retain
the original outcome-routing policy id/version and structured reasons. Technical plans
have no fabricated assessment id. Plans contain no raw documents or new inferred values.
Policies and graph versions must be immutable and archived by the consuming application;
this in-memory contract does not claim durable policy activation or replay storage.

The caller executes a planned transition explicitly through the process contract only
when authorization, current case input and expected revision are transactionally verified.
A second command against an already advanced process fails rather than reusing stale
readiness. The Domain API still accepts no assessment result and performs no automatic
assessment-driven action. Legal/professional approval remains a separate human command.

Integration tests traverse two materially different opaque process configurations and
the real synthetic path: normalized schema input -> AssessmentRecorder/RuleEvaluator ->
conservative triage -> explicit transition plan -> immutable process Apply. They cover
known positive/negative cases, missing facts, missing/conflicting evidence, technical
failure, absent/ambiguous mappings, identity/input substitution and stale command rejection.
The first graph's terminal preparation state is a fixture endpoint, not an approved case.

Input revision binding relies on the intake store's unique upstream-owner constraint
for CaseId. The process case revision equals that authoritative stream's recorded
upstream revision. Independent sources cannot reuse CaseId/revision pairs; an explicit
aggregation adapter is required for future multi-source enrichment.
