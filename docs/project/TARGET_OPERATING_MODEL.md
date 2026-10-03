# Target operating model

This document records the product-flow direction for NormaCase. It is intentionally independent from one institution, one medical domain and one external software vendor.

## Product objective

NormaCase should reduce routine manual case handling by preparing the complete inbound workload for human decision responsibility.

The primary operating model is not:

```text
assessor receives case
-> assessor imports file manually
-> assessor starts evaluation
-> assessor reads every rule result
```

The target is:

```text
upstream case source
-> integration adapter
-> normalized case and evidence
-> deterministic pre-assessment
-> process routing
   -> ready for approval
   -> incomplete / waiting for information
   -> human review
   -> technical/integration exception
-> human approval/review/correction
-> outbound integration
```

The exact names and transitions of productive states remain versioned process configuration. The diagram describes responsibilities, not a hard-coded domain enum.

## Integration first

Production cases should normally enter NormaCase continuously through an explicit integration boundary.

Potential upstream or downstream systems may include institution-specific applications and standardized exchange formats. MDconnect, MEDIKOS and SPV-MD are relevant future adapter targets only where authoritative specifications, access and organizational approval are available.

NormaCase must not guess undocumented APIs or encode those contracts in the Domain or RuleEngine.

Use an anti-corruption boundary:

```text
External contract
-> adapter
-> validated import contract
-> normalized NormaCase case/evidence
```

The normalized platform model stays stable when an upstream vendor, file format or transport changes.

Manual import remains supported as a synthetic preview, test, administrative and exceptional fallback mechanism. It is not the desired routine assessor workflow.

## Deterministic triage

Every routable result must come from explicit structured state.

A routine case can only be prepared for approval when all required conditions for that configured process are satisfied. Missing or unresolved information remains explicit.

Examples:

- `UNKNOWN` remains unknown.
- `INCOMPLETE` routes to missing-information handling.
- conflicting evidence can route to human review.
- a deterministic supported/not-supported result can be prepared for approval when the configured workflow allows it.
- no assessment outcome silently creates legal or organizational approval.

Routing must be reproducible from recorded case/evidence, Knowledge Release, platform version and explicit workflow configuration.

## Assessment result != process state

Keep domain evaluation separate from case processing.

For example:

```text
Assessment outcome: SUPPORTED
Workflow state:      READY_FOR_APPROVAL
```

or:

```text
Assessment outcome: INCOMPLETE
Workflow state:      WAITING_FOR_INFORMATION
```

This separation allows different organizations or case types to use different approval processes without changing deterministic rule semantics.

## Human responsibility

NormaCase prepares decisions; it does not assume away required human responsibility.

The product should support:

- an approval-oriented queue for clear cases,
- focused queues for incomplete/conflicting/review cases,
- detailed per-case drill-down,
- complete Decision Trace and source/evidence inspection,
- append-only acceptance, correction and override history,
- individual approval,
- batch approval only where the configured organizational policy explicitly permits it.

The original deterministic assessment is never rewritten by a human correction or override.

## User-interface direction

The main productive UI should become a workload/work-queue interface rather than a collection of manual import tools.

A useful dashboard can expose counts such as:

```text
Ready for approval
Needs information
Human review
Technical exception
Completed
```

Selecting a queue opens cases with the information needed for that task. A user can drill into the complete case, evidence and Decision Trace before taking an audited action.

The current synthetic workbench remains valuable as an engineering/reference surface, but it is not the final information architecture for productive case work.

## Outbound flow

NormaCase should be capable of returning approved/reviewed state through an outbound adapter rather than require users to copy results manually.

The outbound contract must preserve relevant case identity, assessment identity, Knowledge/platform versions, workflow revision and audit references. The exact external message format is adapter-specific.

## Future AI boundary

AI is not required for this operating model.

A later AI component may assist with transcription, document extraction, classification of document sections, structured-data proposals, summaries or draft narrative text.

Such a component belongs before or around deterministic assessment:

```text
document/audio
-> optional AI extraction/drafting adapter
-> proposed structured values + provenance
-> validation / human confirmation as required
-> deterministic NormaCase assessment
```

AI output must not silently become authoritative domain truth. Model/provider/version, source material and review state need explicit provenance where AI-derived data is used.

The deterministic decision core remains free from LLM or probabilistic decision authority.

## Process-discovery rule

Public information can guide architecture and synthetic adapters, but undocumented internal MD processes, interfaces or approval requirements must not be invented.

Before a productive integration is implemented, obtain and review the authoritative interface specification, security/authentication model, transport, error semantics, ownership of identifiers, retention requirements and organizational approval process.

Unknown integration details remain explicit project questions rather than assumptions.

## Near-term engineering implications

Future implementation work should prioritize generic capabilities in this order:

1. explicit case lifecycle and processing-state contract separated from assessment outcomes;
2. normalized intake/application boundary with synthetic adapter fixtures;
3. work-queue queries and approval/review orchestration;
4. authenticated actor and authorization boundary;
5. transactional persistent case/workflow operation;
6. outbound adapter contract;
7. concrete external adapters when authoritative contracts are available.

This preserves the platform-first rule: a new institution or inbound system should normally require an adapter/configuration change, not a rewrite of the rule engine.
