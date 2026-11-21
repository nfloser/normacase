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

## Planning and sequencing

Use the shortest code-complete path through the product workflow.

- Prefer one small vertical slice that advances an inbound case toward a usable work-queue/review flow over several disconnected abstractions.
- Check whether the required capability already exists before creating a new module or contract.
- Mark issue dependencies explicitly and finish blockers before dependent UI/integration work.
- Parallel work is appropriate only when branches are independent and cannot create competing contracts.
- Keep concrete third-party adapters out of implementation until an authoritative interface specification is available. Develop their generic seam with synthetic fixtures and contract tests first.
- Design application capabilities so they can be consumed by the first-party German UI or headlessly by an external system.
- Avoid speculative generalization. Require at least two materially different examples before promoting an integration-specific shape into a generic platform abstraction.
- Do not postpone security, privacy, provenance, idempotency or historical reproducibility to a later cleanup phase.

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
