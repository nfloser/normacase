# Security

NormaCase is intended for workflows that may process highly sensitive health and social data. Security and privacy are architectural requirements, not later hardening work.

## Reporting a vulnerability

Do not open a public issue containing exploit details, credentials, personal data or patient information. Use GitHub's private security reporting mechanism for this repository when available, or contact the repository owner privately.

## Data handling during development

Real patient or claimant data is prohibited in:
- source control,
- issues and pull requests,
- tests and fixtures,
- screenshots,
- CI logs,
- telemetry,
- external SaaS tools.

Use synthetic data only.

## Baseline principles

- least privilege and explicit authorization,
- secure defaults and separation of duties,
- no secrets in source control,
- dependency and supply-chain review,
- input validation at trust boundaries,
- protections against broken access control, injection, XSS, CSRF and SSRF as applicable,
- encryption in transit and appropriate encryption at rest,
- minimal sensitive logging,
- backup/restore and secure update planning,
- no mandatory external cloud or AI runtime dependency.

See `docs/security/THREAT_MODEL.md` for the evolving threat model.

## Static source analysis

Pull requests and changes to `main` that touch source/tooling paths run CodeQL over
C#, JavaScript/TypeScript and Python. The workflow uses the supported no-build mode
and one analysis job so it complements, rather than duplicates, the normal build/test
matrix. Findings are source-level security signals only: a green scan does not approve
an operational deployment, privacy design, medical rule set, identity model or target
institution configuration.

CodeQL runs in GitHub Actions against the public repository during development. It is
not a NormaCase runtime dependency and no real patient or claimant data may be supplied
to the workflow.
