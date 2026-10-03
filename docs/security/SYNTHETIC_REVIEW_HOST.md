# Local synthetic review host design

The default API remains read-only and stateless. `NORMACASE_REVIEW_DEMO=1` explicitly
selects a separate synthetic review mode. It requires PostgreSQL and an externally
configured 256-bit hex bearer credential. Startup fails closed if configuration is
missing. There is no shipped credential, password database or token-issuance endpoint.

ASP.NET Core authentication validates the opaque credential with a constant-time
SHA-256 comparison and issues a server-owned synthetic principal. Actor id and grants
never come from payload, query or unsigned identity headers. A valid credential grants
access only to the fixed synthetic workload. Process state and disposition determine
which review actions the server permits; missing-information and technical cases have
no review action. Authorization is checked again within the aggregate transaction.

The mode retains loopback binding, host/origin guards, bounded strict JSON, no-store,
CSP, disabled request logging and no runtime cloud calls. Bearer authorization headers
avoid ambient cookie authentication/CSRF. The frontend holds the credential only in
memory, masks and clears its entry, and sends it only to same-origin review endpoints.
No localStorage, query token, analytics or cookies are used. Endpoint errors are bounded
German resources; exceptions and PostgreSQL details never appear in responses.

A server recording clock supplies explicit UTC time; the client supplies unique review
identity, target assessment, expected input/process/audit revisions, disposition and
reason. The service preserves the original result and appends the human decision.
Queue refresh follows the committed aggregate. Stale commands receive 409 without retry.

Database initialization is limited to four fixed synthetic assessment fixtures. Existing
records retain their original platform/release/assessment rather than being overwritten
on restart. Technical cases remain explicit process-only synthetic fixtures. This is
not a patient-data deployment, organizational identity provider or institution-approved
approval policy. Productive access requires a reviewed identity adapter, TLS deployment,
case ownership/role/separation policy, privacy review and operational validation.

Technical acceptance review covers missing/invalid credentials, forged actor fields,
forbidden/stale actions, strict origins, restart persistence, audit binding and browser
secret handling. This design permits PostgreSQL only in the explicit local synthetic
review mode; the default host's stateless boundary is retained.
