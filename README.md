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
  NormaCase.Domain/       framework-independent case/decision/workflow/audit contracts
  NormaCase.Knowledge/    Knowledge Pack model, loader and validation
  NormaCase.RuleEngine/   deterministic rule evaluation and Decision Trace
  NormaCase.Application/  storage-neutral assessment execution records
  NormaCase.Replay/       portable snapshot capture and verified offline replay
  NormaCase.Persistence.PostgreSql/
                         append-only PostgreSQL assessment record adapter

tests/
  NormaCase.Domain.Tests/
  NormaCase.RuleEngine.Tests/
  NormaCase.Application.Tests/
  NormaCase.Replay.Tests/

knowledge/
  demo-a/                 truth-valued AND/OR/UNKNOWN synthetic pack
  demo-b/                 numeric/range/temporal-version synthetic pack
  demo-c/                 evidence dependency and review synthetic pack
  demo-d/                 derived numeric expression synthetic pack
  demo-e/                 independent domain-output synthetic pack

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
- evaluate nested AND/OR, truth equality, inclusive numeric thresholds and ranges,
- derive numeric values through declarative range lookup, sum and max expressions,
- preserve UNKNOWN instead of coercing it,
- gate criteria on structured evidence availability and escalate conflicts,
- configure safe unknown outcomes as INCOMPLETE or HUMAN_REVIEW,
- select a rule version from an explicit assessment date,
- return source-backed recursive Decision Trace data,
- emit multiple independently evaluated, source-backed categorical outputs while preserving UNKNOWN per output,
- advance generic immutable workflow instances through explicitly declared deterministic transitions,
- model immutable human review and append-only assessment audit semantics without coupling the domain core to storage or system time,
- wrap one evaluation in a storage-neutral assessment record with explicit ids/time, a canonical typed input snapshot and a defensively detached evaluator result,
- persist complete assessment records append-only in PostgreSQL while preserving exact strict JSON, queryable version/date metadata and an integrity fingerprint.

## Local verification

Requires the .NET 10 SDK.

```bash
dotnet test tests/NormaCase.Domain.Tests/NormaCase.Domain.Tests.csproj --configuration Release
dotnet test tests/NormaCase.RuleEngine.Tests/NormaCase.RuleEngine.Tests.csproj --configuration Release
dotnet test tests/NormaCase.Application.Tests/NormaCase.Application.Tests.csproj --configuration Release
```

The deterministic core itself needs no external runtime service. The assessment-record contract is documented in [docs/architecture/ASSESSMENT_RECORDS.md](docs/architecture/ASSESSMENT_RECORDS.md), while human review/audit semantics are documented in [docs/architecture/AUDIT.md](docs/architecture/AUDIT.md).

An optional PostgreSQL adapter persists immutable assessment records without coupling the core to storage. Its schema, migration and integrity guarantees are documented in [docs/architecture/POSTGRESQL_ASSESSMENT_STORAGE.md](docs/architecture/POSTGRESQL_ASSESSMENT_STORAGE.md). It is not connected to the user-facing API yet.

## Development

Read [AGENTS.md](AGENTS.md), [CONTRIBUTING.md](CONTRIBUTING.md), the architecture docs and relevant ADRs before changing code.

Development follows issue -> branch -> tests -> implementation -> documentation -> pull request -> CI -> review -> merge.

## Offline synthetic evaluator

A runnable German command-line adapter is available for synthetic case files:

```bash
dotnet run --project src/NormaCase.Cli -- evaluate --pack knowledge/demo-c/pack.json --case examples/cases/demo-c-supported.json --platform-version development
```

Use `--json` for the versioned lossless assessment document. See
[the German offline guide](docs/development/OFFLINE_EVALUATOR.md) for supported,
incomplete and human-review examples, file formats, exit codes and limitations.
The adapter needs no runtime network service. It accepts SYNTHETIC packs only;
production authorization and persistence are not implemented yet.

## Local synthetic HTTP adapter

```bash
dotnet run --project src/NormaCase.Api
```

The ASP.NET Core development host listens on loopback port 5080 and provides
`GET /api/packs` and `POST /api/assessments/{packId}`. It uses the same engine and
lossless assessment contract as the CLI. See [the German API guide](docs/development/LOCAL_API.md)
for curl examples, limits and security boundaries. Synthetic knowledge only;
production authentication and persistence remain open work.

## German synthetic workbench

The browser UI uses the same local engine and assessment contract. Build its bundled
assets and start the API with Node.js 24 and the .NET 10 SDK:

```bash
npm --prefix frontend ci
npm --prefix frontend run build
dotnet run --project src/NormaCase.Api
```

Open http://localhost:5080. The workbench provides externally labelled German
synthetic examples, explicit dates, evidence states, source revisions, independent
domain outputs and exact JSON export. See [the German workbench guide](docs/development/WORKBENCH.md).
`frontend/` is the canonical browser project; its production build writes the API's
bundled web assets. No runtime CDN, external font or analytics service is used.

## Self-contained offline snapshots

The synthetic CLI can capture the original case, complete Knowledge Pack and
assessment in a portable snapshot, then verify replay against that embedded
knowledge and an explicit platform identity. See
[the German snapshot guide](docs/development/ASSESSMENT_SNAPSHOTS.md) for commands,
checksum limitations and historical replay requirements.

The German workbench also downloads complete snapshots after evaluation and verifies
uploaded synthetic snapshot files locally. Imported results remain read-only; see
[the workbench guide](docs/development/WORKBENCH.md).

## Downloadable synthetic preview

Native Windows x64 and Linux x64 preview packages are built and smoke-tested by
[the preview workflow](.github/workflows/preview.yml). Successful runs publish
`synthetic-preview-win-x64` and `synthetic-preview-linux-x64` artifacts for 14 days.
Download the artifact for your operating system from the GitHub Actions run,
extract the contained application ZIP completely, then follow `START.de.md`.
Node and an installed .NET runtime are not needed; Linux still needs the native
system libraries described in [the German start guide](docs/development/PREVIEW_START.de.md).

The preview includes the German workbench, offline CLI, all synthetic packs and
examples, its exact source/platform identity and corruption-detection checksums.
It keeps the existing loopback-only host and does not expose persistence or
production authorization. A CI preview is not an approved product release.
