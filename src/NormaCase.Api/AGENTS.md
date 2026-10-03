# Local synthetic HTTP adapter

Follow root AGENTS.md. No patient-data endpoints. Remain loopback-only until a
separate reviewed authorization/privacy deployment design replaces this demo host.
Use the existing RuleEngine and strict Serialization adapter; do not fork semantics.
German error strings belong in resources. Reject cross-origin/non-local requests.
Bound body size; never log input content or expose exception text. No runtime network
requests, CORS, forwarded headers or external cloud dependencies.

The separately reviewed opt-in synthetic review mode follows
`docs/security/SYNTHETIC_REVIEW_HOST.md`. It may use PostgreSQL and authenticated
review commands for fixed synthetic fixtures only. Actor identity must come only
from its verified ASP.NET Core principal. Preserve case-scoped authorization and
atomic revision/audit checks; keep the anonymous preview stateless by default.
