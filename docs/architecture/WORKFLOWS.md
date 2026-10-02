# Generic workflow lifecycle

NormaCase now has a small framework-independent workflow lifecycle in
`NormaCase.Domain.Workflow`.

This layer models **how** a declared workflow advances. It does not contain
medical, social-medical or benefit-specific process knowledge.

## Contracts

A `WorkflowDefinition` contains:

- an explicit technical workflow id,
- a positive version,
- an explicitly declared initial state,
- opaque state ids with an explicit terminal flag,
- opaque transition ids with declared from/to state ids.

A `WorkflowInstance` contains only:

- workflow id and version,
- current state id,
- explicit revision.

Starting a definition creates revision 0. Applying one transition that is
available from the current state returns a new instance at revision +1. The
previous instance is unchanged.

Restoring an instance requires an explicit declared state and revision. The
domain model does not infer either value.

## Fail-closed behavior

Definition construction rejects:

- missing or duplicate state ids,
- missing or duplicate transition ids,
- an undeclared initial state,
- transitions that reference undeclared states,
- outgoing transitions from terminal states.

Runtime transition attempts reject:

- unknown transition ids,
- transitions declared for a different current state,
- any transition from a terminal state,
- use of a workflow definition with a different id/version.

There is no default transition and no inferred fallback.

## Knowledge boundary

State and transition ids are technical identifiers, not platform enums.
The core deliberately contains no labels such as medical review stages,
institution names or benefit-specific process states.

Knowledge Packs may carry optional versioned, source-bound declarative workflow
definitions. `NormaCase.Knowledge.Workflow.KnowledgeWorkflowMaterializer` converts
a validated definition into this generic Domain contract. The Knowledge validator
requires the workflow source to exist and delegates graph invariants back to the
Domain workflow definition rather than maintaining separate transition semantics.

The next integration step may bind a selected Knowledge workflow definition to an
explicit workflow instance. It must not hard-code domain vocabulary into
`NormaCase.Domain` or infer transitions from assessment outcomes.

Presentation labels belong in external presentation metadata.

## Determinism and audit boundary

The lifecycle:

- does not read system time,
- does not create ids,
- does not authenticate actors,
- does not persist state,
- does not call networks,
- does not automatically react to assessment outcomes.

Those concerns require explicit application/infrastructure inputs. A future
workflow audit record may carry actor, timestamp and reason, but must not rewrite
historical workflow revisions.

Automatic rule/outcome-driven transitions are intentionally out of scope until
their knowledge/versioning and audit semantics are designed explicitly.
