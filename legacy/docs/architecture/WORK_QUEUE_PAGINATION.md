# Authorized persistent queue pages

The persistent local host accepts `GET /api/review/work-queues?pageSize=100`
and an optional `afterCaseId` continuation cursor. Page size is 1–100; duplicate,
invalid and unsupported cursor parameters fail with 400. The default is 100.
The existing queue envelope gains `nextPageCursor`, null at the end. Items on one
case-id page are grouped into the configured queues; this is not 100 per queue.

`PostgresCaseReviewStore.ListCasePageAsync` selects distinct immutable case identities
using PostgreSQL C collation for both comparison and ordering. It reads one extra
identity to detect continuation. The trusted exact READ scope is applied in SQL
before the limit. An empty scope returns no cases; null is unrestricted at the
storage seam and must only be supplied by an authorized adapter. The local host
uses null solely for its existing shared synthetic demo mode. Grant scope is not
bound from query parameters. Each loaded case retains full integrity verification.
The previous bounded `ListCaseIdsAsync` remains available for existing callers;
the host no longer uses that 500-case all-or-fail operation.

The German browser shows a page, a next-page button and an explicit refresh button.
It replaces rather than concatenates pages. Selecting a page clears the detail and
review form; logout aborts requests and clears the cursor. Every response remains
guarded by the active request controller so a delayed old session cannot restore it.
Review/conflict refresh starts at the first page and fetches the selected detail
independently; it never repeats a review against newer revisions automatically.

This is live keyset browsing, not a consistent snapshot across requests. Human
reviews can move a case between queues but cannot change its ordering identity.
A newly inserted identity before the cursor appears after an explicit refresh;
the UI states this limitation. A stable full export requires a separately defined
snapshot contract. No total count of unauthorized cases is returned.

PostgreSQL tests cover ordered continuation, exact scopes, empty scopes and limits.
Real host tests traverse all seeded cases and verify scoped continuation cannot
reveal other users' cases. A browser test forces one-case pages through the real
API and verifies next/refresh/logout behavior. Operational installation and
deployment work remains tracked separately in issue #187.
