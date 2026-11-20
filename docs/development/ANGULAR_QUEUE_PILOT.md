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

## Read-only case detail

From a queue, the pilot can open one case through
`GET /api/review/work-cases/{caseId}` (on the Spring pilot or the .NET host).
The response is requested as text and checked against the same contract as
`ReviewCaseDetailParser` in Java: matching `caseId`, string revisions, string
evidence statuses, string actions and well-formed audit entries. A mismatch is
shown as an invalid response instead of partial data. `assessmentJson` is read
only for its display fields (`outcome`, `knowledgeRelease`,
`missingRequiredFields`); an empty value means no assessment and an unreadable
value is labelled as such rather than guessed.

Unknown and inaccessible cases share one German message. A 401 ends the
session; a 403 or network failure keeps the session and shows an error in the
detail view. Technical field IDs are shown as code until Knowledge Pack
presentation metadata is available to this client. The view offers no
approval, override or correction controls (issue
[#344](https://github.com/nfloser/normacase/issues/344)).

## Anonymous synthetic demo

Without a review session the pilot can open the read-only synthetic workspace of
the local preview host (issue [#352](https://github.com/nfloser/normacase/issues/352)).
Responses are validated as a whole; a case file for a different ID or with
malformed documents, observations or labels is not shown. Page images and
documents are loaded from the same origin only, external source links are
offered only for `https` URLs. The decision trace is read from `assessmentJson`
with exact decimal text (`JSON.parse` source access); a browser without that
capability shows the assessment as unreadable instead of rounding. Field and
evidence names come from the case file's presentation labels, never from
platform code.

[Astryx](https://github.com/meta/astryx) is a React design system. Its patterns,
accessibility guidance and theme concepts can inform Angular design, but its
React components are not Angular dependencies. Review licenses before copying
any source or assets. Keep locally served assets and the existing German,
medical-blue workbench direction. Actual styling remains subject to review.

Before considering this pilot complete, implement and verify the Angular client,
shared synthetic fixtures, authenticated browser flow and negative cases; add
CI and a separately justified Spring Boot slice. Preserve the existing
cross-platform and offline paths. See [migration issue #325](https://github.com/nfloser/normacase/issues/325).
