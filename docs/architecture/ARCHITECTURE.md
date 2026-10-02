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

## Assessment result layers

`AssessmentOutcome` remains a small platform disposition for supported, unsupported, incomplete, human-review and not-applicable states. It is not extended with medical or benefit-specific result vocabulary.

Knowledge-defined domain outputs form a separate bounded layer. They are independently evaluated, versioned, source-traceable categorical values. Their ids and choices live in Knowledge Packs rather than platform enums. UNKNOWN is preserved per output.

This split allows structurally different domains to expose several results without turning the platform core into one domain's result model.

## Versioning

Evaluation receives the assessment date and Knowledge Release explicitly. Temporal rule selection is deterministic and testable.

Historical assessments retain the versions used when they were created.

## Historical assessment records and review provenance

The application layer preserves a completed assessment as an immutable record containing the exact versioned CaseInput JSON, the exact Assessment JSON, explicit technical assessment/case ids, an explicit UTC recording instant, and SHA-256 fingerprints of both serialized artifacts.

Assessment date, platform version, Knowledge Release and the original system outcome are derived from the validated documents instead of being accepted as duplicate caller claims. Input/output assessment dates must agree.

A human review is append-only provenance that references the immutable assessment record. Confirmation, return-for-completion and system-outcome override are separate review actions. An override records both the original system disposition and the reviewer-selected platform disposition, plus actor, reason and explicit UTC review time; it never rewrites the system result.

These contracts intentionally contain no persistence technology or implicit clock. A later PostgreSQL adapter owns transactions, uniqueness, concurrency and durable append-only storage.

## Persistence

PostgreSQL is the preferred persistence technology once storage is introduced because the platform requires transactions, constraints, referential integrity and migrations.

Additional datastores require a demonstrated use case.

## Offline/on-premises design

Runtime-critical assets and domain knowledge are local. External fonts, CDNs, analytics, telemetry or cloud AI services are not required for core operation.
