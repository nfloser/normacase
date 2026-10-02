# NormaCase

NormaCase is a deterministic, auditable decision-support platform for rule-based medical and social-medical assessment workflows.

The platform separates executable application code from versioned domain knowledge. It is designed to make rule evaluation reproducible, uncertainty explicit, source traceability inspectable and human review first-class.

## Project status

NormaCase is in early foundation work. It is **not** a medical device, production assessment system or domain-approved knowledge base.

Only synthetic data belongs in the repository.

## Core principles

- Platform first; domain knowledge stays external and versioned.
- Deterministic evaluation with no AI decision core.
- `UNKNOWN` is a real state, never an implicit yes/no.
- Historical assessments keep their platform, knowledge and rule versions.
- Every productive domain rule must be traceable to a source.
- Public reference knowledge is not equivalent to expert/domain approval.
- On-premises and offline-capable operation is a design goal.
- Humans remain responsible for domain decisions.

## Repository direction

```text
src/
  NormaCase.Domain/       framework-independent case/decision contracts
  NormaCase.Knowledge/    Knowledge Pack model, loader and validation
  NormaCase.RuleEngine/   deterministic rule evaluation and Decision Trace

tests/
  NormaCase.Domain.Tests/
  NormaCase.RuleEngine.Tests/

knowledge/
  demo-a/                 truth-valued AND/OR/UNKNOWN synthetic pack
  demo-b/                 numeric/range/temporal-version synthetic pack
  demo-c/                 evidence dependency and review synthetic pack
  demo-d/                 numeric transformation/aggregation synthetic pack

docs/
  project/
  architecture/
  adr/
  security/
  knowledge/
```

The system grows as a modular monolith. ASP.NET Core, React + TypeScript, PostgreSQL and Docker remain the preferred product stack, while the domain/rule core stays independent from framework and storage details.

## Current deterministic slice

The current core can:
- load external JSON Knowledge Packs,
- validate manifests, fields, sources, rule references and validity intervals,
- evaluate typed truth and numeric case values,
- derive numeric values through validated range lookup, sum and max calculations,
- evaluate nested AND/OR, truth equality, inclusive numeric thresholds and ranges,
- preserve UNKNOWN instead of coercing it,
- gate criteria on structured evidence availability and escalate conflicts,
- configure safe unknown outcomes as INCOMPLETE or HUMAN_REVIEW,
- select a rule version from an explicit assessment date,
- return source-backed recursive Decision Trace data.

## Local verification

Requires the .NET 10 SDK.

```bash
dotnet test tests/NormaCase.Domain.Tests/NormaCase.Domain.Tests.csproj --configuration Release
dotnet test tests/NormaCase.RuleEngine.Tests/NormaCase.RuleEngine.Tests.csproj --configuration Release
```

No external runtime service is required for the current core.

## Development

Read [AGENTS.md](AGENTS.md), [CONTRIBUTING.md](CONTRIBUTING.md), the architecture docs and relevant ADRs before changing code.

Development follows issue -> branch -> tests -> implementation -> documentation -> pull request -> CI -> review -> merge.
