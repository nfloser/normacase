# NormaCase Angular pilot

The Angular case workbench for synthetic cases on the local preview host, plus the authenticated review-queue path (`#review`). The existing React
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

## Case workbench (no login, no database)

The default view keeps the concept of the existing React workbench – a mail
client combined with a file explorer – on Angular, TypeScript and RxJS:

- **Explorer:** all cases, work queues with colours and counts, personal filing
  (bookmarks, confirmed, simulated dispatch, own folders with sub-folders,
  unfiled), optional platform cases.
- **Case list:** two-line rows (person · subject, status, request, note),
  sorting, search (Ctrl+K) and column filters, multi-select with click,
  Ctrl/Shift and checkboxes (max. 100), arrow keys, Enter, context menu
  (Shift+F10). Columns give way to the reading pane via container queries.
- **Reading pane** right, bottom or off (F4), resizable; full view via Enter or
  double click, Alt+← returns to the list.
- **Case file:** header with file number, status and result; next processing
  step with confirm/dispatch; tabs for overview (open points, context,
  reasoning tree with rule and source, provenance, evidence, documents),
  documents, provenance table, case data and organisation history with note.
- **Document reader:** file list, continuous pages, page field, page width /
  whole page / zoom steps, rotation, open in new tab, download; provenance
  links jump to and highlight the cited page.
- **Organisation** uses the unchanged `/api/demo-workspace` commands with the
  current revision and a fresh operation ID; dispatch downloads the local
  package. Nothing changes an assessment and nothing is transmitted.

Styling uses the vendored Astryx neutral theme tokens (`src/vendor/astryx`,
MIT) with a medical-blue accent, light/dark/system modes and no external fonts.
The assessment is only read; decimals keep their JSON source text. The
previous React workbench remains unchanged at `/` as a fallback.
