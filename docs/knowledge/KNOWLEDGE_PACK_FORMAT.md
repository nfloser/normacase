# Knowledge Pack format

Status: evolving synthetic format. It is intentionally small and grows only as independent synthetic domains prove a need.

## Boundary

Knowledge Packs are declarative data consumed by the platform. They do not execute code.

Current repository examples:
- `knowledge/demo-a/pack.json`: truth values, nested AND/OR and UNKNOWN semantics.
- `knowledge/demo-b/pack.json`: numeric thresholds, inclusive ranges and temporal rule versions.
- `knowledge/demo-c/pack.json`: evidence-gated nested alternatives and explicit human review.

All three packs are `SYNTHETIC`.

## Manifest

```json
{
  "formatVersion": 1,
  "packId": "synthetic.demo-a",
  "releaseId": "demo-a-2026.1",
  "lifecycleStatus": "ACTIVE",
  "validationLevel": "SYNTHETIC",
  "entryRuleId": "DEMO-A-ELIGIBILITY"
}
```

`formatVersion` makes format changes explicit. Version 1 is currently supported. `releaseId` is carried into every assessment result. `entryRuleId` identifies the logical rule resolved for the explicit assessment date.

Release lifecycle and validation level are independent. Lifecycle states are `DRAFT`, `IN_REVIEW`, `APPROVED`, `ACTIVE`, `DEPRECATED` and `RETIRED`.

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

## Sources

Every rule references a source id. Synthetic fixtures use synthetic sources only.

Source id, authority, title, document type, status, version and source location are required for every source. The model also carries publication date, source validity interval, version, source location, retrieval date and a content hash.

Content hashes use `sha256:<64 hexadecimal characters>`. Invalid hashes and source validity intervals are rejected.

Synthetic packs do not invent external provenance. For `PUBLIC_REFERENCE`, `DOMAIN_REVIEWED` and `PRODUCTION_APPROVED`, every source must additionally provide a source location, retrieval date and content hash. This ensures a public-reference rule cannot load with only a human-readable title or anonymous URL-less citation.

A missing source reference fails pack validation and the pack is not partially loaded.

## Conditions

Current condition kinds are:
- `field_equals` for `truth` fields,
- `number_gte` for inclusive numeric thresholds,
- `number_in_range` for inclusive numeric ranges,
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
- source id and an immutable snapshot of its version, location, authority, title, type, status, publication/validity dates, retrieval date and content hash,
- recursive condition trace,
- typed actual/expected values or numeric range bounds,
- final outcome.

This is the current base for Source -> Rule -> Test traceability.

## Evidence dependencies

Declare stable requirement IDs in the pack's `evidenceRequirements` list.
A `requires_evidence` condition references one `evidenceRequirementId` and contains
exactly one child condition in `conditions`. Gates can nest, but cannot refer to
other rules or execute code. Undeclared references and duplicate requirement IDs
are rejected.

The caller passes structured availability separately from case facts:
`Missing` (also the default for omitted requirements), `Present`, or `Conflicting`.
These statuses do not prove authenticity or interpret document contents; upstream
evidence review remains responsible for that assertion. No documents are stored
by this slice.

A present gate returns its child's result. A missing or conflicting gate returns
UNKNOWN, retaining the child and the evidence ID/status in the trace. All children
are evaluated so the trace is complete. Standard AND/OR semantics still apply:
an alternative supported by independent evidence can match without an unavailable
alternative; a definite false AND child can determine no-match despite another
unknown child. Missing evidence itself is never converted to a false condition.

Rules may set `onUnknown` only to `INCOMPLETE` or `HUMAN_REVIEW`. Omission
defaults to `INCOMPLETE`. Missing globally required case fields always forces
`INCOMPLETE`. Otherwise any conflicting evidence in the evaluated trace forces
`HUMAN_REVIEW`, even when AND/OR would mask the unknown result. The trace condition
result and final outcome are recorded separately.

Demo C requires a confirmed synthetic request and verification evidence for either
a numeric threshold or an alternative confirmation. Missing verification escalates
to human review. Demo A/B need no evidence and retain their existing behavior.

## Source revision snapshots

Rule Trace contains a detached immutable `SourceTrace` record. Changing a source
collection or loading a later release cannot rewrite metadata already returned in
a historical result. Consumers should persist the result with this snapshot; a
source ID alone is insufficient to distinguish revisions. This is a metadata
snapshot, not document archival or a complete immutable assessment store.

Every source declares a non-empty `version` and `sourceLocation`. The synthetic
fixtures identify revision 1 of their repository pack; they leave retrieval dates
and hashes absent because there was no external document retrieval.

A `sha256:` hash identifies the exact original source document bytes supplied by
the knowledge author, not a normalized title, extracted text or a web URL. It is
not a signature or proof of authenticity. Validation checks syntax and required
metadata only; it does not fetch remote content or verify bytes against the hash.
The source validity interval is recorded metadata; this change does not add an
implicit rule/source temporal policy. Rule selection still uses the explicit date.
