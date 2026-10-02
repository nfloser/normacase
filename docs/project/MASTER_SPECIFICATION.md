# NormaCase master specification

This document captures the durable product intent that bootstraps the repository. Detailed operational rules live in the root `AGENTS.md`; architecture decisions are recorded as ADRs.

## Vision

NormaCase is a deterministic, auditable and modular decision-support platform for rule-based medical and social-medical assessment workflows.

The platform supports structured cases, completeness checks, criteria and rule evaluation, evidence/source traceability, workflows, outcomes, human review and immutable audit history.

It must not be architecturally tied to one medical service, one institution or one reference case.

## System boundary

```text
PLATFORM != DOMAIN KNOWLEDGE
```

Platform concepts are generic: Case, CaseType, Field, Value, Criterion, Requirement, Evidence, Rule, RuleSet, DecisionTree, Workflow, Outcome, Source, Assessment, Review, KnowledgePack and KnowledgeRelease.

Domain-specific rules are external Knowledge Packs. A new domain should normally not require a platform-core change.

## Determinism

The decision core is not AI-based.

```text
same input
+ same platform version
+ same knowledge version
+ same evaluation date
= same result
```

No hidden system-time dependency, network lookup, dynamic scripting or arbitrary code execution may affect rule evaluation.

Missing data is explicit. At minimum the domain model distinguishes `YES`, `NO`, `UNKNOWN` and `NOT_APPLICABLE`. Safe generic outcomes include `SUPPORTED`, `NOT_SUPPORTED`, `INCOMPLETE`, `HUMAN_REVIEW` and `NOT_APPLICABLE`.

## Knowledge governance

Knowledge is a first-class, versioned subsystem. Sources, rules, schemas, workflows, templates, synthetic reference cases and tests belong to Knowledge Releases.

Knowledge changes follow controlled review and activation. Sources and historical rules are never silently overwritten.

Publicly researched reference packs are marked `PUBLIC_REFERENCE`; they are not represented as approved by an external medical organization without actual review.

When reliable sources do not support a clear rule, the rule remains unresolved and requires domain review.

## Time and historical reproducibility

Platform releases and Knowledge Releases are versioned separately. Assessments record the platform version, Knowledge Release, relevant rule/workflow versions and assessment date.

New rules must not silently change historical assessments. Re-evaluation against a later Knowledge Release is a separate operation.

## Traceability

Every meaningful evaluation must be explainable as:

```text
Input -> Criterion -> Rule -> Source -> Outcome
```

The Decision Trace is machine-readable, human-readable and reproducible.

Human override is allowed, but the original system result, override, reason, actor and timestamp remain available.

## Security and privacy

NormaCase is designed for on-premises operation and should remain usable without runtime internet access.

No real patient data is permitted in the development repository or normal engineering workflow. Runtime components should minimize exposure of identity data to assessment components and avoid logging medical content unnecessarily.

## Architecture direction

Start with a modular monolith.

Preferred baseline:
- .NET / ASP.NET Core backend,
- React + TypeScript frontend,
- PostgreSQL,
- Docker.

The domain and rule core remain independent from UI, persistence and web-framework details.

## Testing

Use unit, domain, rule, knowledge-validation, integration, regression, contract, migration, security and end-to-end tests as each layer appears.

Rule tests must exercise unknown paths and boundaries, not just code coverage.

## Delivery sequence

1. Repository, architecture, CI and security foundation.
2. Knowledge source/schema/release foundation.
3. Deterministic rule engine with explicit unknown semantics and Decision Trace.
4. Generic case/evidence/workflow/audit platform.
5. At least three materially different synthetic Knowledge Packs.
6. Public-reference packs based on official sources.
7. Domain-expert validation.
8. Controlled pilot only after security, privacy and domain review.

The first functional slice is synthetic: Knowledge Pack -> schema -> structured case -> rule evaluation -> outcome -> Decision Trace -> source reference.

## Long-term invariant

NormaCase is a platform first. Knowledge is external, versioned and governed. Decisions are deterministic and traceable. Uncertainty is explicit. Sensitive data stays under operator control. Humans remain responsible for domain decisions.
