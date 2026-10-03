# Local synthetic HTTP adapter

Follow root AGENTS.md. No patient-data endpoints. Remain loopback-only until a
separate reviewed authorization/privacy deployment design replaces this demo host.
Use the existing RuleEngine and strict Serialization adapter; do not fork semantics.
German error strings belong in resources. Reject cross-origin/non-local requests.
Bound body size; never log input content or expose exception text. No runtime network
requests, CORS, forwarded headers or external cloud dependencies. The separately
reviewed opt-in synthetic review boundary is documented in
`docs/security/SYNTHETIC_REVIEW_HOST.md`. Actor identity must come only from its
verified ASP.NET Core principal. Keep the anonymous preview unchanged by default.
Persistent synthetic review endpoints must require explicit PostgreSQL configuration,
case-scoped authorization, expected revisions and append-only aggregate writes. They
must never become a productive identity or patient-data endpoint by implication.
