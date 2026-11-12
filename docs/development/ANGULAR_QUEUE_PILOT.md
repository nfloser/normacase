# Angular pilot: authenticated work-queue boundary

The proposed first read-only Angular/RxJS workflow consumes the existing
`GET /api/review/work-queues` endpoint. It must not duplicate evaluation, write
cases, or replace the existing workbench. The endpoint belongs to the opt-in
synthetic review host, not a production service. Authentication, PostgreSQL,
loopback restrictions and startup prerequisites are described in
[Synthetic review host](../security/SYNTHETIC_REVIEW_HOST.md).

The response contains `configurationId` (string), `configurationVersion`
(number), `nextPageCursor` (string or null), and `queues` (array). Each queue
has `queueId` and `items`; an item has string-valued `caseId`, `caseRevision`,
`processRevision`, `stateId`, `assessmentId`, and `auditRevision`, plus the
boolean `batchAllowed`. Revision strings must not be parsed as JavaScript
numbers. Queue and state identifiers are technical values, not German labels or
assessment outcomes. Unknown identifiers must not be silently mapped to a
known meaning. A future client needs explicit de-DE presentation resources.

The server validates `pageSize` (1–100) and `afterCaseId`, authenticates the
request and filters by the actor's current read scope. A client must preserve
these boundaries, handle 401/403 and cancellation, and must not persist tokens
or case contents in browser storage. Use synthetic data only. The response
shape is characterized by the PostgreSQL-backed synthetic review integration
test; it is not a formal versioned public API or an institutional contract.

[Astryx](https://github.com/meta/astryx) is a React design system. Its patterns,
accessibility guidance and theme concepts can inform Angular design, but its
React components are not Angular dependencies. Review licenses before copying
any source or assets. Keep locally served assets and the existing German,
medical-blue workbench direction. Actual styling remains subject to review.

Before considering this pilot complete, implement and verify the Angular client,
shared synthetic fixtures, authenticated browser flow and negative cases; add
CI and a separately justified Spring Boot slice. Preserve the existing
cross-platform and offline paths. See [migration issue #325](https://github.com/nfloser/normacase/issues/325).
