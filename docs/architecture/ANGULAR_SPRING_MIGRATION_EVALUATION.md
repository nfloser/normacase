# Angular / Spring Boot migration evaluation

Status: **staged migration requested; implementation and parity gates remain open**
Tracking: [#325](https://github.com/nfloser/normacase/issues/325)

## Why consider this

The existing NormaCase product is an independently useful and tested, synthetic-only platform. Its current UI uses React + TypeScript; its API and deterministic core use ASP.NET Core and .NET. The proposed Angular, RxJS, Java and Spring Boot stack enables transferable application-development experience without itself justifying a wholesale rewrite.

The requested direction is Angular for the workbench and Java/Spring Boot for the API, introduced incrementally. The architecture remains platform-first; the repository's acceptance and security gates take precedence over training goals.

## Current versus target capabilities

| Capability | Existing baseline | Candidate addition |
| --- | --- | --- |
| Case workbench | React 19 / TypeScript / Vite, Playwright | Angular, TypeScript, RxJS, Angular unit/component tests and Playwright |
| Application/API | ASP.NET Core / .NET 10 | Java / Spring Boot with explicit OpenAPI/JSON boundary |
| Decision core | Deterministic C# Domain and RuleEngine | Retain unless cross-language deterministic parity is proven |
| Persistence | PostgreSQL | Reuse database contracts; never allow independently written incompatible records |
| Delivery | Docker, GitHub Actions, native preview | Java/Angular build/test, controlled container workflow and rollback |

## Options and recommendation

1. **Thin Angular and Spring Boot pilot (recommended):** Implement one isolated synthetic read-only, authenticated work-queue view in Angular. Spring Boot may expose only a bounded adapter to existing versioned application APIs. No rule evaluation, case mutation, approvals, or duplicated domain semantics in Java. This establishes RxJS, Java/Spring tests, serialization, authorization and CI experience with minimal risk.
2. **Staged presentation/API migration:** Only if the pilot demonstrates contract parity, operating simplicity and maintained Windows/Linux/macOS support, migrate workbench routes and application responsibilities as independently reviewed vertical slices. Do not run two authoritative write paths.
3. **Java rule-core rewrite:** Defer. Only consider after an explicit total-cost decision and cross-language golden-fixture/immutable-replay proof. Rule, assessment and Knowledge Release semantics must remain unchanged.

Option 1 is the first implementation step, not an accepted ADR or proof of migration readiness. Do not retire the supported .NET/React path before explicit evidence supports doing so.

## Candidate pilot boundary

```text
Angular work-queue (synthetic fixtures, de-DE presentation)
  -> Spring Boot REST adapter (authentication/authorization, validation)
    -> versioned existing application read contract
      -> authoritative NormaCase application and PostgreSQL store

Existing C# Domain/RuleEngine/Knowledge remains the sole evaluator.
```

A pilot must not fabricate a production MDconnect interface; all case content must be synthetic. First establish whether the existing API has a suitable readonly endpoint and whether an adapter is needed at all. Avoid redundant proxy services if a typed Angular client against the existing API proves the learning objective sufficiently.

## Acceptance gates for subsequent implementation issues

- Preserve privacy, authorization, role/actor scoping, explicit review and immutable audit; verify bypasses are rejected server-side, not solely in Angular guards.
- Keep `UNKNOWN`, `INCOMPLETE`, `HUMAN_REVIEW`, evidence provenance and source references untouched; snapshots/replay must be byte-semantically and version-semantically stable.
- First test the contract with shared synthetic fixtures, including invalid input, missing permissions, stale revisions, and offline operation. Add negative security tests and Playwright UI regressions.
- Verify Java tests (JUnit and Spring integration), Angular unit tests and TypeScript checks, Java/TypeScript static analysis, container scans, and existing .NET and cross-platform preview CI.
- Pin toolchains and dependencies, use reproducible offline builds after dependencies have been provisioned, and assess image footprint and platform startup.
- Keep the pilot's code, docs, tests, CI and security review within a narrowly scoped PR.
- Do not claim either clinical correctness or production patient-data clearance from green CI.

## Sequencing

1. Run the thin pilot under a separate issue and verify the existing read contract.
2. Inspect current API read-contract, frontend route, authorization and fixture tests. Identify the one route and exact serialized response.
3. Implement contract-first Angular client + one view, keeping the current workbench supported.
4. Introduce Spring Boot through a bounded, tested application/API capability with an explicit contract and security boundary; do not introduce a redundant production proxy merely to claim migration progress.
5. Evaluate against regression, security, offline and cross-platform criteria before any broader migration proposal.

## Architectural implications

The choice is intentionally reversible. A training-oriented second stack does not override platform-first modular-monolith principles, domain independence, knowledge governance, German product-language requirements, or the ban on real patient data in Git and CI. The pilot must not become an uncontrolled second production application.
