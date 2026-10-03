# Local synthetic HTTP adapter

Follow root AGENTS.md. No patient-data endpoints. Remain loopback-only until a
separate reviewed authorization/privacy deployment design replaces this demo host.
Use the existing RuleEngine and strict Serialization adapter; do not fork semantics.
German error strings belong in resources. Reject cross-origin/non-local requests.
Bound body size; never log input content or expose exception text. No runtime network
requests, CORS, forwarded headers, persistence or external cloud dependencies.

The explicit local synthetic review mode follows docs/security/SYNTHETIC_REVIEW_HOST.md.
That separately reviewed mode may use PostgreSQL and authenticated bearer commands
for its fixed synthetic fixtures only; default demo operation remains stateless.
