# Knowledge Pack format

Status: evolving synthetic format. It is intentionally small and grows only as independent synthetic domains prove a need.

## Boundary

Knowledge Packs are declarative data consumed by the platform. They do not execute code.

Current repository examples:
- `knowledge/demo-a/pack.json`: truth values, nested AND/OR and UNKNOWN semantics.
- `knowledge/demo-b/pack.json`: numeric thresholds, inclusive ranges and temporal rule versions.
- `knowledge/demo-c/pack.json`: evidence-gated nested alternatives and explicit human review.
- `knowledge/demo-d/pack.json`: derived numeric expressions with range lookup, sum, max and UNKNOWN propagation.
- `knowledge/demo-e/pack.json`: independent, source-backed categorical domain outputs with per-output UNKNOWN.

All five packs are `SYNTHETIC`.

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

The platform's in-memory release catalog treats `packId` + `releaseId` as the exact
historical identity. Several releases of one pack may coexist; there is deliberately
no implicit latest/current selection. Registering different JSON under an existing
identity is rejected, while an exact-byte duplicate is idempotent.

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

Knowledge JSON requires numeric tokens for numeric properties, including format/rule
versions, thresholds, bounds and lookup values. Quoted numeric strings are rejected
rather than coerced. Source revision strings remain strings; this requirement applies
to properties declared as numeric by the format.

A missing required field is represented as unknown and forces the final platform outcome to `INCOMPLETE`.

Case values must match the field type declared by the pack. The evaluator rejects type mismatches rather than coercing values.

## Sources

Every rule references a source id. Synthetic fixtures use synthetic sources only.

Source id, authority, title, document type and status are required for every source. The model also carries publication date, source validity interval, version, source location, retrieval date and a content hash.

Content hashes use `sha256:<64 hexadecimal characters>`. Invalid hashes and source validity intervals are rejected.

Synthetic packs do not invent external provenance. Format v1 therefore keeps version and source location optional for `SYNTHETIC` packs. For `PUBLIC_REFERENCE`, `DOMAIN_REVIEWED` and `PRODUCTION_APPROVED`, every source must additionally provide a version, source location, retrieval date and content hash. This ensures a governed rule cannot load with only a human-readable title or anonymous URL-less citation.

A missing source reference fails pack validation and the pack is not partially loaded.

## Conditions

Current condition kinds are:
- `field_equals` for `truth` fields,
- `number_gte` for inclusive numeric thresholds,
- `number_in_range` for inclusive numeric ranges,
- `all`,
- `any`,
- `not`.

Groups may be nested.

Operators are validated against field types. A numeric operator cannot target a truth field, and vice versa.

### Derived numeric expressions

`number_gte` and `number_in_range` may consume either a raw numeric `field` or one `numericExpression`. Declaring both is invalid.

Numeric expressions are bounded declarative data. Supported kinds are:
- `field`: reads one declared numeric case field,
- `range_lookup`: maps a numeric input through ordered, non-overlapping inclusive bands,
- `sum`: adds one or more numeric operands,
- `max`: selects the greatest value from one or more numeric operands.

A range lookup band declares `minimum`, `maximum` and the numeric `value` returned for that interval. Missing bounds, reversed bounds, overlapping/out-of-order bands and empty band lists are rejected during pack validation.

Missing or UNKNOWN inputs propagate UNKNOWN through range lookup, sum and max. Sum never treats an unknown value as zero, and max never chooses a known operand while another operand is unknown. A value outside every lookup band also produces UNKNOWN so incomplete knowledge fails closed.

Decision Trace stores the recursive numeric-expression tree, every intermediate value and the selected range band for a successful lookup. The expression model uses decimal arithmetic and has no scripting, network or implicit-time behavior.

Demo D proves this capability with synthetic scoring data only; it does not encode medical thresholds or a domain-specific result model.

### Boolean UNKNOWN semantics

- `all`: any not-matched child -> not matched; otherwise any unknown -> unknown; otherwise matched.
- `any`: any matched child -> matched; otherwise any unknown -> unknown; otherwise not matched.
- unknown final condition -> `INCOMPLETE`.

### Negation

`not` contains exactly one condition in `conditions`. It can wrap a truth,
numeric, group, evidence or another negation condition. It cannot simultaneously
declare a field, expected value, threshold/bounds, numeric expression or evidence
reference. The child is validated recursively.

```json
{
  "kind": "not",
  "conditions": [
    { "kind": "field_equals", "field": "criterion_a", "expected": "YES" }
  ]
}
```

| Child condition | Negated condition |
| --- | --- |
| Matched | NotMatched |
| NotMatched | Matched |
| Unknown | Unknown |

The complete child trace stays available. Negation acts on the condition result;
it does not rewrite the underlying fact. For example, a NOT_APPLICABLE fact
remains NOT_APPLICABLE in the trace, and negating "equals YES" does not convert it
to NO. Missing inputs/evidence remain UNKNOWN, globally missing required fields
still force INCOMPLETE, and conflicting evidence still forces HUMAN_REVIEW even
when another group branch masks the unknown condition.

Both entry rules and independent output conditions may use `not`. Existing
Knowledge/Assessment/Snapshot envelopes remain at their current versions: the
recursive condition tree already carries operator ids. Older platform builds
reject the new operator; replay still requires the recorded platform identity.
Existing released synthetic fixtures are not rewritten by this extension.

## Independent domain outputs

A Knowledge Pack may declare top-level `outputs` in addition to its entry rule. These outputs do not replace or alter the platform-level `AssessmentOutcome`; they represent separate bounded domain results that are evaluated independently.

The first supported output type is `choice`. Each output version declares:
- a stable id and positive version,
- `validFrom` and optional `validUntil`,
- its own source reference and deterministic condition,
- a finite set of allowed `choices`,
- explicit `onMatch` and `onNoMatch` values.

`UNKNOWN` is reserved by the engine and cannot be declared as a choice. If an output condition is unknown, the output value remains UNKNOWN; knowledge cannot map that state to a positive or negative choice in this slice. Blank/duplicate choices, undeclared branch values, missing sources, malformed conditions and overlapping versions fail validation.

Active output versions are selected using the same explicit assessment date as the entry rule. Outputs are evaluated independently, so one unknown output does not erase known sibling outputs. There is deliberately no output precedence, override mechanism or output-to-output dependency.

Every `DomainOutputTrace` records output id/version, selected value, condition result/tree and a detached snapshot of the exact source revision. Demo E proves five independent synthetic outputs: bounded decision/selection states, sibling segment states where one can remain UNKNOWN, and an explicit `PENDING_EXTERNAL` state that remains distinct from UNKNOWN.


## Workflow definitions

A Knowledge Pack may optionally declare source-bound generic workflow definitions.
Workflow data configures the platform lifecycle mechanics; it does not add executable
code and it does not automatically react to assessment outcomes.

Example:

```json
{
  "workflows": [
    {
      "id": "synthetic.review-flow",
      "version": 1,
      "sourceId": "SYNTH-SOURCE-001",
      "initialStateId": "submitted",
      "states": [
        { "id": "submitted", "terminal": false },
        { "id": "review", "terminal": false },
        { "id": "complete", "terminal": true }
      ],
      "transitions": [
        {
          "id": "request_review",
          "fromStateId": "submitted",
          "toStateId": "review"
        },
        {
          "id": "complete_review",
          "fromStateId": "review",
          "toStateId": "complete"
        }
      ]
    }
  ]
}
```

Each workflow requires:
- a stable id unique within the Knowledge Release,
- a positive version,
- a `sourceId` that resolves to a declared pack source,
- one declared initial state,
- declared state ids with an explicit terminal flag,
- declared transition ids with explicit from/to states.

The Knowledge validator materializes each definition through the same
`NormaCase.Domain.Workflow.WorkflowDefinition` contract used at runtime. Invalid
graphs therefore fail pack loading rather than being partially accepted. This covers
duplicate state/transition ids, undeclared state references, missing initial states
and outgoing transitions from terminal states.

State and transition ids remain opaque technical identifiers. User-facing labels
belong in presentation metadata. A later slice may connect workflow definitions to
workflow instances, persistence and UI, but no transition is inferred or triggered
by an assessment outcome in the current format.

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
- recursive derived numeric-expression traces where used,
- final outcome,
- independently evaluated domain-output traces, including exact output/source revisions.

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

The synthetic fixtures currently identify revision 1 of their repository pack,
but format v1 does not retroactively require those optional fields from every
`SYNTHETIC` pack. A trace snapshots them when present and preserves `null`
when absent. Governed non-synthetic packs require a version and source location,
plus the retrieval metadata described above.

A `sha256:` hash identifies the exact original source document bytes supplied by
the knowledge author, not a normalized title, extracted text or a web URL. It is
not a signature or proof of authenticity. Validation checks syntax and required
metadata only; it does not fetch remote content or verify bytes against the hash.
The source validity interval is recorded metadata; this change does not add an
implicit rule/source temporal policy. Rule selection still uses the explicit date.
