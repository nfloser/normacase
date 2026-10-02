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
  NormaCase.Domain/       framework-independent core contracts
  NormaCase.Knowledge/    Knowledge Pack model, loader and validation
  NormaCase.RuleEngine/   deterministic rule evaluation and Decision Trace

tests/
  NormaCase.Domain.Tests/
  NormaCase.RuleEngine.Tests/

knowledge/
  demo-a/                 synthetic external Knowledge Pack
docs/
  project/
  architecture/
  adr/
  security/
  knowledge/
```

The system will grow as a modular monolith. ASP.NET Core, React + TypeScript, PostgreSQL and Docker are the preferred product stack, while the domain/rule core remains independent from framework and storage details.

## Local verification

Requires the .NET 10 SDK.

```bash
dotnet test tests/NormaCase.Domain.Tests/NormaCase.Domain.Tests.csproj --configuration Release
dotnet test tests/NormaCase.RuleEngine.Tests/NormaCase.RuleEngine.Tests.csproj --configuration Release
```

No external runtime service is required for the foundation project.

## Development

Read [AGENTS.md](AGENTS.md), [CONTRIBUTING.md](CONTRIBUTING.md), the architecture docs and relevant ADRs before changing code.

Development follows issue -> branch -> tests -> implementation -> documentation -> pull request -> CI -> review -> merge.
