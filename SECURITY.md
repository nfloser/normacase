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
