# Architecture

## Shape

NormaCase starts as a modular monolith. Deployment topology must not leak into the deterministic domain core.

Initial logical modules are expected to evolve around:
- cases,
- knowledge,
- rule evaluation,
- workflows,
- evidence,
- assessments,
- auditing,
- authorization,
- shared technical primitives.

The current foundation contains the framework-independent domain/rule/audit contracts plus a thin application layer for explicit assessment execution records. Persistence remains an adapter concern.

## Dependency direction

```text
Web UI
  -> Application API
    -> Application layer
      -> Domain / Rule core
        <- Infrastructure adapters
```

The domain and rule core must not reference ASP.NET Core, Entity Framework, PostgreSQL, React, network clients or current system time.

## Knowledge boundary

Knowledge Packs are data, not executable extensions. They may define schemas, declarative rules, workflows, sources, templates and synthetic cases, but they may not execute arbitrary code.

The platform interprets a bounded declarative rule model.

## Assessment result layers

`AssessmentOutcome` remains a small platform disposition for supported, unsupported, incomplete, human-review and not-applicable states. It is not extended with medical or benefit-specific result vocabulary.

Knowledge-defined domain outputs form a separate bounded layer. They are independently evaluated, versioned, source-traceable categorical values. Their ids and choices live in Knowledge Packs rather than platform enums. UNKNOWN is preserved per output.

This split allows structurally different domains to expose several results without turning the platform core into one domain's result model.

## Versioning

Evaluation receives the assessment date and Knowledge Release explicitly. Temporal rule selection is deterministic and testable.

Historical assessments retain the versions used when they were created.

## Audit and human review

System assessments and human review are separate immutable concepts. A review references an assessment; it never rewrites the rule-engine result or its trace. Audit ordering, ids and UTC timestamps are explicit inputs rather than hidden clock/generated state in the domain core.

The framework-independent contract is documented in [AUDIT.md](AUDIT.md). Persistence, actor authentication/authorization and tamper-evident storage remain adapter/application concerns.

## Assessment execution records

The application layer wraps one deterministic evaluation in a storage-neutral `AssessmentRecord`. It captures the Knowledge Pack id, explicit execution metadata, a canonical typed snapshot of all declared facts/evidence and a defensively detached copy of the exact evaluator result.

Omitted declared facts remain explicit UNKNOWN and omitted evidence requirements remain MISSING. The application layer does not infer values, generate ids or read the system clock.

See [ASSESSMENT_RECORDS.md](ASSESSMENT_RECORDS.md).

## Persistence

The `AssessmentRecord` is the historical assessment boundary a storage adapter persists. Storage must not reconstruct inputs from traces or resolve provenance against newer knowledge.

PostgreSQL is the preferred persistence technology once storage is introduced because the platform requires transactions, constraints, referential integrity and migrations. A future adapter must also enforce audit ordering/append-only constraints transactionally.

Additional datastores require a demonstrated use case.

## Offline/on-premises design

Runtime-critical assets and domain knowledge are local. External fonts, CDNs, analytics, telemetry or cloud AI services are not required for core operation.


## PostgreSQL persistence adapter

Historical assessment persistence is implemented as an optional infrastructure adapter:
`NormaCase.Persistence.PostgreSql` depends on the storage-neutral Application and
Serialization contracts. Domain, Knowledge and RuleEngine do not depend on Npgsql or
PostgreSQL.

Assessment rows are append-only at the database boundary and retain the complete strict
AssessmentRecord JSON. See [POSTGRESQL_ASSESSMENT_STORAGE.md](POSTGRESQL_ASSESSMENT_STORAGE.md).


## Generic workflow lifecycle

The framework-independent domain layer contains a small immutable workflow
lifecycle with opaque state/transition ids, explicit workflow version and explicit
instance revision. It supplies transition mechanics only; domain-specific workflow
definitions and labels remain external knowledge/presentation concerns.

See [WORKFLOWS.md](WORKFLOWS.md). Knowledge Pack integration, actor authorization,
persistence, timers and automatic assessment-driven transitions remain separate
reviewed slices.

## Shared workbench presentation

The browser workbench uses viewport-bound queue/document/analysis panels and explicit
workspace routes. This same React presentation layer can be hosted in a future native
window; it does not depend on a desktop-specific bridge. Navigation stores only fixed
view identifiers in URL fragments and no case data in browser storage. Native hosting
and institution-specific adapters remain separate deployment/integration decisions.
