# NormaCase engineering rules

NormaCase is a deterministic, auditable and modular decision-support platform for rule-based medical and social-medical assessment workflows.

## Non-negotiable architecture rules

- NormaCase is a platform first. The platform core must not encode individual medical services or case-specific rules.
- `PLATFORM != DOMAIN KNOWLEDGE`. Domain knowledge belongs in versioned Knowledge Packs.
- The decision core is deterministic. No LLM, generative model, probabilistic classifier or external AI API may determine domain outcomes.
- The same structured input, platform version, knowledge release and assessment date must produce the same result.
- Missing information is explicit. `UNKNOWN` must never be coerced to yes/no. Prefer `INCOMPLETE` or `HUMAN_REVIEW` over guessing.
- Rule evaluation must not depend on implicit system time, network access or arbitrary code execution.
- Every evaluation result must be traceable to structured inputs, rules and sources.
- Historical assessments are immutable. Platform and Knowledge versions are separate and must be recorded.
- Publicly researched domain knowledge is `PUBLIC_REFERENCE`, never implicitly domain-approved.
- Never invent medical or social-medical rules. If evidence is insufficient, mark it unresolved / `needs-domain-review`.

## Security and privacy

- Treat health and social data as highly sensitive.
- No real patient data in Git, issues, pull requests, tests, screenshots, CI logs, telemetry or external services.
- Prefer on-prem/offline-capable architecture. Core runtime functionality must not require external cloud, telemetry, fonts, CDNs or AI APIs.
- Avoid sensitive values in logs; prefer technical identifiers such as case, rule, error and correlation IDs.
- Apply least privilege, secure defaults, defense in depth and separation of duties where relevant.

## Architecture

- Start as a modular monolith.
- Keep domain/rule code independent from UI, database and framework details.
- Prefer clear module boundaries, dependency inversion, high cohesion and low coupling.
- New domains should normally be addable through Knowledge Packs without changing platform core code.
- Do not introduce microservices, secondary databases or other infrastructure without demonstrated need.
- Preferred baseline: .NET / ASP.NET Core, React + TypeScript, PostgreSQL and Docker.

## Product operating model

- Production operation is integration-first. New cases should normally arrive through explicit upstream adapters; manual file import is a fallback for synthetic demos, testing, administration and exceptional workflows, not the primary assessor workflow.
- Keep external systems behind adapter / anti-corruption boundaries. MDconnect, MEDIKOS, SPV-MD exchange formats or any future institution-specific system must not leak vendor-specific contracts into the generic Domain or RuleEngine.
- The target processing flow is: `intake -> normalization -> completeness/evidence checks -> deterministic assessment -> routing -> human approval/review -> outbound integration`.
- Keep assessment outcomes separate from case/workflow processing states. For example, `SUPPORTED` is an assessment result; a state such as `READY_FOR_APPROVAL` is workflow state.
- Clear and complete cases may be routed into a review/approval queue, but the platform must not assume that legal or organizational approval can be automated. Approval policy is explicit, configurable and auditable.
- `INCOMPLETE`, `UNKNOWN`, conflicting evidence and `HUMAN_REVIEW` must route to focused human work rather than be guessed or coerced.
- Product UI development should optimize for work queues, triage, batch review/approval where policy allows, and detailed case drill-down with append-only review history rather than requiring users to start every case manually.
- Future AI/document-extraction capabilities are optional upstream adapters. They may propose structured data or draft text, but they must not become the deterministic decision authority and must preserve source/provenance and review boundaries.

## Knowledge governance

Knowledge is versioned, source-bound, testable, reviewable and historically reproducible.

A productive rule must have:
- a stable rule/version identity,
- explicit validity interval,
- source reference,
- tests,
- review state.

Knowledge changes follow:
`Source -> Impact analysis -> Knowledge change -> Review -> Tests -> Approval -> Knowledge release -> Activation`.

Do not overwrite historical sources, rules or releases.

## Development workflow

Use:
`Issue -> Branch -> Tests/TDD -> Implementation -> Documentation -> Pull Request -> CI -> Review -> Fixes -> Merge`.

- Do not develop directly on `main`.
- Read this file plus relevant area-specific `AGENTS.md`, architecture docs, ADRs, security docs, issues, PRs and CI before changes.
- Keep changes small and code-complete.
- Write failing/reproducing tests first for bugs and behavior changes where practical.
- Run the available build, test, lint, type, knowledge-validation and security checks.
- Green tests are necessary but not proof of domain correctness; verify acceptance criteria and end-to-end behavior.
- Update docs with code.
- Perform an independent technical review before merge and fix findings in the same PR.
- Commit, issue and PR text should be concrete and natural, not repetitive templates.

## Product language

- German (de-DE) is the default for the complete user-facing product: navigation, forms, field labels, validation and error messages, workflow/status descriptions, human-readable Decision Traces, reports, exports and user help.
- Keep user-visible text in translation resources. Future locales must not require changes to deterministic rules.
- Present dates and numbers using de-DE conventions at the presentation boundary. Keep core values and persisted/API representations culture-invariant.
- Code symbols, stable rule/source/field IDs, API properties and enum values remain technical contracts; display their German labels separately.
- Domain-specific labels belong in Knowledge Pack presentation metadata, not hard-coded platform branches.
- Preserve original source titles and quoted content; do not translate source identity or silently alter meaning.
- German language does not limit the platform to one institution, domain or service.
- Verify German user-facing text and absence of untranslated technical status codes as part of UI/export acceptance checks.

## Definition of done

A change is complete only when its relevant implementation, tests, integration, documentation, security/data impact, CI and review are complete.

When something cannot be verified, say so explicitly. Never claim production or domain readiness without the corresponding validation.
