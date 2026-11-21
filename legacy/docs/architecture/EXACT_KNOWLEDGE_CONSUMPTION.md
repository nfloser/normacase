# Exact reviewed Knowledge consumption

`KnowledgeActivationSelection` binds pack id, activation revision, release id and
SHA-256 explicitly. `KnowledgeActivationSelectionService` loads only that historical
activation, then verifies its approved change, distinct reviewer, chronology and
retained typed source/impact/test evidence. The release artifact must match all exact
identities and hash. Missing or mismatched records fail closed. No current/latest
activation lookup, implicit release selection or validation promotion occurs.

The generic selection contract is proven with structurally different synthetic packs.
A real domain approval remains a separate external requirement; a reviewed synthetic
release remains `SYNTHETIC`.

The persistent synthetic host accepts these operator configuration keys:

| Key | Value |
|---|---|
| `SyntheticReview:IntakeKnowledgeActivation:PackId` | `synthetic.demo-g` |
| `SyntheticReview:IntakeKnowledgeActivation:Revision` | Canonical positive activation revision, such as `1` |
| `SyntheticReview:IntakeKnowledgeActivation:ReleaseId` | Exact retained reviewed release id |
| `SyntheticReview:IntakeKnowledgeActivation:Sha256` | Exact 64-character lowercase release hash |

All four values are required together. Unknown keys, an absent activation, wrong hash,
missing retained evidence, incompatible schema, non-active lifecycle or non-synthetic validation fail startup.
The selected release must retain the installed adapter's field ids/types and evidence
ids and must not add domain outputs/workflows. A structurally different pack needs its
own explicit intake mapping and German presentation; the generic selection service
itself has no demo-g restriction. Without selection configuration, the existing
explicit installed synthetic release remains the default.

Selection is loaded at startup and applies to **new intake only**. A later activation
never hot-switches the running host. Operators explicitly change the four values and
restart to adopt another reviewed release. Anonymous preview, installed demonstration
seeds, their catalog/presentation and historical assessments retain their installed or
recorded release. Historical/duplicate intake resolves its original exact retained
release before normalization, including recovery of any missing initial assessment.
Intake receipt, assessment and workflow initialization commit in one authorized
transaction. Original review and outbound history is preserved.

Integration coverage proves that a new synthetic rule version changes the new intake
result while the old input, assessment JSON, human review and outbound JSON survive
selection/restart unchanged. Exact service tests also prove that later activations do
not replace a selected historical revision. This is the explicit host-consumption
boundary, not a Knowledge editor or institution-specific release approval.
