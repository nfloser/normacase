# Knowledge Pack format

Status: evolving synthetic format. It is intentionally small and grows only as independent synthetic domains prove a need.

## Boundary

Knowledge Packs are declarative data consumed by the platform. They do not execute code.

Current repository examples:
- `knowledge/demo-a/pack.json`: truth values, nested AND/OR and UNKNOWN semantics.
- `knowledge/demo-b/pack.json`: numeric thresholds, inclusive ranges and temporal rule versions.
- `knowledge/demo-c/pack.json`: generic evidence requirements and explicit missing-evidence escalation.

All three packs are `SYNTHETIC`.

## Manifest

```json
{
  "packId": "synthetic.demo-a",
  "releaseId": "demo-a-2026.1",
  "validationLevel": "SYNTHETIC",
  "entryRuleId": "DEMO-A-ELIGIBILITY"
}
```

`releaseId` is carried into every assessment result. `entryRuleId` identifies the logical rule resolved for the explicit assessment date.

Validation levels currently recognized by the loader are:
- `SYNTHETIC`
- `PUBLIC_REFERENCE`
- `DOMAIN_REVIEWED`
- `PRODUCTION_APPROVED`

Public reference material must never be promoted to a stronger level implicitly.

## Fields and case values

Current field types are:
- `truth`
- `number`

Truth runtime values are `YES`, `NO`, `UNKNOWN` and `NOT_APPLICABLE`.

Numeric runtime values use exact decimal values in the deterministic core.

A missing required field is represented as unknown and forces the final platform outcome to `INCOMPLETE`.

Case values must match the field type declared by the pack. The evaluator rejects type mismatches rather than coercing values.

## Evidence requirements

A pack may declare generic evidence requirements independently from case fields:

```json
{
  "id": "evidence_primary",
  "description": "Synthetic primary supporting evidence",
  "missingOutcome": "HUMAN_REVIEW"
}
```

A rule can reference them with `evidence_present`. Missing evidence is represented as an unknown condition, never as false.

For safety, `missingOutcome` is limited to `INCOMPLETE` or `HUMAN_REVIEW`. A pack cannot configure missing evidence to become `SUPPORTED` or another positive/negative substantive conclusion.

Evidence supplied at runtime must be declared by the active Knowledge Pack.

## Sources

Every rule references a source id. Synthetic fixtures use synthetic sources only.

Source id, authority, title and document type are required. A missing source reference fails pack validation and the pack is not partially loaded.

## Conditions

Current condition kinds are:
- `field_equals` for `truth` fields,
- `number_gte` for inclusive numeric thresholds,
- `number_in_range` for inclusive numeric ranges,
- `evidence_present` for declared evidence requirements,
- `all`,
- `any`.

Groups may be nested.

Operators are validated against field types. A numeric operator cannot target a truth field, and vice versa.

### Boolean UNKNOWN semantics

- `all`: any not-matched child -> not matched; otherwise any unknown -> unknown; otherwise matched.
- `any`: any matched child -> matched; otherwise any unknown -> unknown; otherwise not matched.
- unknown final condition -> `INCOMPLETE`.

## Temporal rule versions

Rules carry `validFrom` and optional `validUntil`.

Evaluation receives `assessmentDate` explicitly and never reads system time. Versions for the same logical rule may not overlap.

Demo B deliberately switches from rule version 1 to version 2 on 2026-07-01 to regression-test historical resolution.

## Decision trace

An evaluation result records:
- Knowledge Release,
- assessment date,
- missing required fields,
- selected rule id/version,
- source id,
- recursive condition trace,
- typed actual/expected values or numeric range bounds,
- missing evidence ids and their configured safe escalation outcome,
- final outcome.

This is the current base for Source -> Rule -> Test traceability.
