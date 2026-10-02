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

## Language and localization

- All end-user-facing product surfaces default to German (`de-DE`). This includes navigation, buttons, forms, status and error messages, help text, review guidance, outcome presentation, human-readable audit views, reports and exports.
- Use correct German MD/domain terminology. Do not introduce literal translations when an established German technical term exists; uncertain domain terminology requires review rather than invention.
- Keep user-visible text localizable. Do not scatter hard-coded display strings through UI components or bind platform semantics to German labels.
- Code, technical identifiers, APIs, core domain models and internal architecture names remain English.
- Knowledge Packs use the language appropriate to their domain. MD-related packs are authored in German; source titles and citations remain in their authoritative/original language where appropriate.
- Localization must not change deterministic rule semantics. Persist stable language-neutral identifiers and localize only their human-readable presentation.

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

## Definition of done

A change is complete only when its relevant implementation, tests, integration, documentation, security/data impact, CI and review are complete.

When something cannot be verified, say so explicitly. Never claim production or domain readiness without the corresponding validation.
