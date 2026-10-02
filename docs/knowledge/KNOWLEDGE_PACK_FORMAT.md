# Knowledge Pack format

Status: initial synthetic slice. The format will evolve through versioned changes as more rule operators and governance features are added.

## Boundary

Knowledge Packs are declarative data consumed by the platform. They do not execute code.

The first slice uses JSON and contains:
- manifest metadata,
- field definitions,
- sources,
- rule versions,
- nested conditions.

The repository fixture at `knowledge/demo-a/pack.json` is intentionally synthetic.

## Manifest

```json
{
  "packId": "synthetic.demo-a",
  "releaseId": "demo-a-2026.1",
  "validationLevel": "SYNTHETIC",
  "entryRuleId": "DEMO-A-ELIGIBILITY"
}
```

`releaseId` is carried into every assessment result. `entryRuleId` identifies the logical rule to resolve for the explicit assessment date.

## Fields

The first slice supports truth-valued fields:

```json
{
  "id": "criterion_a",
  "type": "truth",
  "required": true
}
```

Runtime values are `YES`, `NO`, `UNKNOWN` or `NOT_APPLICABLE`.

A missing required field is treated as unknown and forces the final platform outcome to `INCOMPLETE`.

## Sources

Every rule references a source id. The synthetic fixture uses a synthetic source only.

A rule referencing a missing source fails pack validation and the pack is not partially loaded.

## Rules

The first rule model supports:
- `field_equals`
- `all`
- `any`

Groups may be nested.

Rule validity uses explicit `validFrom` / `validUntil` dates. Evaluation receives the assessment date as input; it does not read system time.

Unknown condition semantics are fail-closed:
- `all`: any false -> not matched; otherwise any unknown -> unknown; otherwise matched.
- `any`: any true -> matched; otherwise any unknown -> unknown; otherwise not matched.
- unknown final condition -> `INCOMPLETE`.

## Decision trace

An evaluation result records:
- Knowledge Release,
- assessment date,
- missing required fields,
- rule id/version,
- source id,
- recursive condition trace,
- final outcome.

This is the first step toward full Source -> Rule -> Test traceability.
