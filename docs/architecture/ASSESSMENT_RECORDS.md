# Assessment execution records

NormaCase separates deterministic evaluation from the historical record that an
application may later persist.

`RuleEvaluator` remains the decision core. The application layer wraps one completed
evaluation in an `AssessmentRecord` containing:

- caller-supplied assessment and case identifiers,
- the Knowledge Pack identifier,
- the platform version,
- an explicit UTC recording timestamp,
- a canonical typed input snapshot,
- the exact `AssessmentResult` returned by the evaluator.

## Canonical input snapshot

Before the record is returned, mutable caller dictionaries are copied. The evaluator
result is also detached recursively: missing-field lists, domain-output lists and
nested condition/expression children are copied into read-only collections so an
engine-owned array cannot become mutable historical state. Every field
declared by the Knowledge Pack is represented in the snapshot. An omitted field is
stored as explicit `UNKNOWN`; an omitted evidence requirement is stored as
`MISSING`.

The evaluator still validates the originally supplied keys and types. The application
layer does not discard unknown fields or reinterpret rule behavior.

The returned evaluator result is also defensively snapshotted. Collection-valued trace
members are recursively copied into read-only collections so a caller cannot cast an
engine array and silently rewrite the historical record in memory.

This gives persistence a stable representation of the input state and result used for
evaluation without making absence look like a negative answer.

## Explicit execution metadata

Assessment ids, case ids, platform version and recording time are supplied by the
caller. The application layer does not call the system clock, generate random ids or
perform network access. Recording timestamps must be UTC.

These values are execution/audit metadata. They do not participate in deterministic
rule evaluation.

## Serialization

`AssessmentRecordJson` is a strict versioned interchange format for the complete
record. It uses the same lossless `CaseValue`, enum and duplicate-property handling
as the existing assessment serialization boundary.

The format is a persistence/import-export contract, not proof of authenticity,
authorization or approval.

## Persistence boundary

No database is introduced by this slice. A future PostgreSQL adapter should persist
the record as an immutable historical assessment and add transactional case/audit
behavior around it. Storage code must not reconstruct inputs from a Decision Trace or
resolve source metadata from a newer Knowledge Release.

Authentication, authorization, identity separation and retention/deletion policy are
separate reviewed slices before any real sensitive data may be used.
