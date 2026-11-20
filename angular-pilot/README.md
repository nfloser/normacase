# NormaCase Angular pilot

A read-only Angular workplace for synthetic cases: an anonymous demo on the local preview host and an authenticated review-queue path. The existing React
workbench, ASP.NET API and .NET decision engine remain authoritative. No real
patient data; do not expose the synthetic host on a network.

## Build and test

Requires Node 24 and npm. From this directory run `npm ci`, `npm test --
--watch=false` and `npm run build`. The build produces local static assets under
`dist/angular-pilot/browser`; no CDN, remote fonts or runtime dependency is used.
Dependencies must first be provisioned for offline builds.

## Local host integration

The protected API does not allow cross-origin requests. Build the API in Release
mode (`dotnet build src/NormaCase.Api --configuration Release` from the repository
root), then run `npm run stage:local --prefix angular-pilot`. The staging command
copies the built pilot only into the disposable API build output, beside the
existing workbench. Start the local API and open
`http://localhost:5080/angular-pilot/index.html`. Set up its separate
opt-in synthetic review and PostgreSQL mode according to
[the synthetic review host guide](../docs/security/SYNTHETIC_REVIEW_HOST.md).
The server requires its own setup and access control; the client does not grant
permissions. The repository CI exercises this staging path and browser flow;
check the CI result for the exact revision before relying on it. There is no one-command startup yet.

The form holds the synthetic credential in memory only. It requests same-origin
`/api/review/work-queues`, does not use cookies, and shows one page at a time.
Selecting a case opens a read-only detail view from
`/api/review/work-cases/{caseId}`: system outcome, workflow state, evidence
status, missing required fields, version identifiers and the immutable audit
history. Revisions are shown as the exact strings from the host and the
assessment is never re-evaluated in the browser. Back, Escape, logout and
leaving the page cancel a pending request and return focus to the opened case.
No review, case mutation, or domain decision is implemented. Error handling,
keyboard use and visual layout still require broader accessibility testing. The light/dark
appearance switch is held in memory for the current page and does not store preferences.
The counts describe only the currently displayed page, not total queue sizes.
The styles follow the existing medical-blue direction; Meta Astryx is a React
reference, not an Angular dependency.

## Synthetic demo (no login, no database)

„Demo öffnen“ uses the read-only endpoints of the anonymous local preview host:
`/api/work-queues`, `/api/document-cases` and `/api/document-cases/{caseId}`
with its page previews. It shows the four work queues with totals and search,
the 22 reference files, and a case file with document tabs, page navigation,
zoom, provenance (field → document/page), recorded evidence, findings, context,
source and the recorded decision trace (condition → rule → source → outcome).
Numbers in the trace are shown from their exact JSON source text. Nothing is
evaluated, written or stored in the browser; folder, confirmation and dispatch
simulation remain in the existing workbench at `/`.
