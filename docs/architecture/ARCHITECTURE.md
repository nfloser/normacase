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

The current bootstrap contains only the framework-independent domain contracts needed to establish semantics.

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

Deterministic derived values are also Knowledge-defined data, not application preprocessing. Numeric transformations are intentionally limited to validated operations such as range lookup, sum and max. Calculations execute in explicit dependency order, propagate UNKNOWN, and are recorded in the assessment trace before rule evaluation.

## Versioning

Evaluation receives the assessment date and Knowledge Release explicitly. Temporal rule selection is deterministic and testable.

Historical assessments retain the versions used when they were created.

## Persistence

PostgreSQL is the preferred persistence technology once storage is introduced because the platform requires transactions, constraints, referential integrity and migrations.

Additional datastores require a demonstrated use case.

## Offline/on-premises design

Runtime-critical assets and domain knowledge are local. External fonts, CDNs, analytics, telemetry or cloud AI services are not required for core operation.
