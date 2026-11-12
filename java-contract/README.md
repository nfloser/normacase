# Java review-queue contract experiment

This module contains a local synthetic Spring Boot queue adapter and a Java
contract reader. It never evaluates cases or derives clinical conclusions. The
.NET host remains responsible for authentication, case-scoped authorization and
the authoritative assessment. The reader shares the example in
`../examples/contracts/` with the .NET integration test.

With JDK 21 and Maven 3.6.3 or newer, run `mvn --batch-mode verify` from this
directory. Dependencies must first be provisioned; after that Maven can be run
with `--offline`. CI runs the tests on a clean runner. No external service,
patient data or database is required by the Java tests.

The reader checks JSON wire types, preserves revisions as strings, retains
unknown identifiers verbatim, permits new extra fields, and rejects malformed,
duplicate or oversized input without echoing it. This example characterizes
one synthetic response shape; it is not a versioned public contract or proof of
security, behavioral parity or production readiness.

`LoopbackReviewQueueClient` reads the existing synthetic .NET review endpoint
through IPv4 loopback only. It forwards a caller-provided synthetic credential
without retaining it, and the .NET host remains responsible for identity and
case-scoped authorization. The client disables proxies and redirects, limits
response size and request duration, validates UTF-8 and parses the shared wire
contract. Its error categories are technical and contain no upstream body or
credential. The Spring adapter uses that client on every request and is not a
productive identity system. No patient data or externally accessible binding.

## Local synthetic HTTP adapter

First start the separate .NET synthetic review host with PostgreSQL as described
in [the review host guide](../docs/security/SYNTHETIC_REVIEW_HOST.md). Run
`mvn --batch-mode package` from this directory, then start
`java -jar target/review-queue-contract-0.1.0-SNAPSHOT.jar`. The adapter binds
only `127.0.0.1:5081`; it exposes `GET /api/review/work-queues` and forwards a
caller-supplied bearer credential and optional `afterCaseId` to the .NET host on
`127.0.0.1:5080`. Authentication and live read-scope checks remain there for
every request. Credentials are not saved or deliberately logged. Do not put
credentials in URLs, command-line arguments or committed configuration.

The adapter denies external hosts, foreign browser origins, forwarded headers,
unsupported routes/methods, duplicate credentials and malformed queries. It
preserves validated upstream JSON, including unknown future properties. Errors
are generic German messages and responses are not cacheable. It does not serve
the Angular pilot yet. Use only synthetic data: local processes remain in the
trust boundary, there is no productive identity, rate limiting, TLS termination,
or institutional authorization. Do not expose it to a network or treat it as
approved for health data. CI exercises both servers with synthetic PostgreSQL.
