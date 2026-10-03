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

## Application execution binding

`NormaCase.Application.Workflows.WorkflowExecutionService` is the first orchestration
layer above Knowledge and Domain workflow contracts.

Starting an execution requires an explicit workflow id and version. The service:

- validates the supplied Knowledge Pack,
- selects exactly that workflow id/version,
- materializes the generic immutable Domain definition,
- starts the Domain instance at revision 0,
- snapshots the Knowledge Pack id and Knowledge Release,
- copies the exact source revision referenced by the workflow.

The resulting `WorkflowExecution` retains the immutable materialized
`WorkflowDefinition`. Advancing an execution therefore does not re-read the caller's
mutable `KnowledgePack`; mutations after start cannot silently change the graph or
source metadata of the in-flight execution.

`Apply` requires one explicit transition id and delegates transition semantics to the
Domain instance. It returns a new execution with the same Knowledge/source/definition
snapshot and the next immutable instance revision.

This is deliberately an **in-memory application contract**, not a persistence format.
A later reviewed slice may define storage/reload and append-only workflow transition
audit records. Such a format must preserve the exact Knowledge Release, source
revision, workflow definition identity and instance revision needed for historical
reconstruction.

No assessment outcome triggers a transition automatically.

## Storage-neutral execution snapshots

`WorkflowExecutionService.Capture` converts an in-memory execution into a detached
`WorkflowExecutionSnapshot`. The snapshot contains the Knowledge Pack and Release
identity, the complete source revision, the full materialized workflow graph and the
current state/revision.

State and transition collections are copied into read-only collections. Restoring an
execution rebuilds a new immutable Domain `WorkflowDefinition` and restores the
`WorkflowInstance` from the snapshot only; it does not reload or resolve a current
Knowledge Pack. Invalid graph references, undeclared current states and negative
revisions therefore fail closed through the same Domain invariants used at runtime.

This snapshot is a storage-neutral in-memory contract. Database persistence,
authenticated provenance and tamper-evident storage remain separate reviewed slices.

## Versioned JSON interchange

`NormaCase.Serialization.WorkflowExecutionSnapshotJson` provides format version 1
for portable workflow execution snapshots. Transport records are separate from the
Application model so JSON constructor/property details do not shape the workflow
contract.

The shared strict interchange boundary rejects duplicate and unknown properties,
missing constructor fields, unsupported versions, oversized documents and excessive
nesting. Export validates the snapshot through the normal restore invariants before
writing JSON. Import reconstructs the Application snapshot and invokes that same
restore path. Invalid source metadata, graph definitions, current states and revisions
therefore fail closed on both sides of the interchange boundary.

The JSON document contains the complete source revision and workflow graph required to
continue the execution without loading current Knowledge. It is still only data:
successful parsing does not authenticate who produced it, prove domain approval or
provide tamper evidence.

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
