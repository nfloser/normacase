# Java review-queue contract experiment

This module is a small, offline-runtime Java contract reader using dependencies
managed by Spring Boot. It is not a web service, an authorization boundary or
a replacement for the existing .NET API. It never evaluates cases or derives
clinical conclusions. It reads the synthetic response example shared with the
.NET integration test in `../examples/contracts/`.

With JDK 21 and Maven 3.6.3 or newer, run `mvn --batch-mode verify` from this
directory. Dependencies must first be provisioned; after that Maven can be run
with `--offline`. CI runs the tests on a clean runner. No external service,
patient data or database is required by the Java tests.

The reader checks JSON wire types, preserves revisions as strings, retains
unknown identifiers verbatim, permits new extra fields, and rejects malformed,
duplicate or oversized input without echoing it. This example characterizes
one synthetic response shape; it is not a versioned public contract or proof of
security, behavioral parity, production readiness, or a Spring Boot HTTP API.
The next API slice requires a reviewed authentication and authorization boundary.

`LoopbackReviewQueueClient` reads the existing synthetic .NET review endpoint
through IPv4 loopback only. It forwards a caller-provided synthetic credential
without retaining it, and the .NET host remains responsible for identity and
case-scoped authorization. The client disables proxies and redirects, limits
response size and request duration, validates UTF-8 and parses the shared wire
contract. Its error categories are technical and contain no upstream body or
credential. It is a library for the future adapter, not an HTTP service or a
productive identity system. No patient data or externally accessible binding.
