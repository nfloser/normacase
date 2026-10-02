# Contributing to NormaCase

## Before changing code

1. Read `AGENTS.md` and any more specific `AGENTS.md` in the area you change.
2. Read relevant architecture, security and knowledge-governance documentation.
3. Check existing issues and pull requests.
4. Use synthetic data only.

## Workflow

Create or use an issue for a meaningful unit of work, then create a focused branch. Prefer tests before implementation for behavioral changes and bug fixes.

A pull request should explain:
- the problem and intended behavior,
- the implemented approach and relevant design choices,
- tests performed,
- security/privacy and data impact,
- risks or follow-up work.

Do not work directly on `main`.

## Quality bar

Run the checks that exist for the affected area. At minimum for the current foundation:

```bash
dotnet test tests/NormaCase.Domain.Tests/NormaCase.Domain.Tests.csproj --configuration Release
dotnet test tests/NormaCase.Application.Tests/NormaCase.Application.Tests.csproj --configuration Release
```

Do not remove tests or features merely to make a new change pass. Keep documentation synchronized with behavior.

## Domain knowledge changes

Do not invent domain rules.

A domain rule must be source-backed, versioned, test-covered and reviewed. Publicly researched material must be identified as `PUBLIC_REFERENCE` until a suitable domain review has occurred.

If a source is ambiguous or conflicting, record the uncertainty and require domain review rather than choosing a rule implicitly.

## Pull-request review

Before merge, review the change separately for correctness, architecture, maintainability, security, privacy, error handling, edge cases, tests, docs and traceability. Resolve material findings and rerun checks.
