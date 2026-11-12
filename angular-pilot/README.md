# NormaCase Angular pilot

An isolated, read-only synthetic review queue experiment. The existing React
workbench, ASP.NET API and .NET decision engine remain authoritative. No real
patient data; do not expose the synthetic host on a network.

## Build and test

Requires Node 24 and npm. From this directory run `npm ci`, `npm test --
--watch=false` and `npm run build`. The build produces local static assets under
`dist/angular-pilot/browser`; no CDN, remote fonts or runtime dependency is used.
Dependencies must first be provisioned for offline builds.

## Host integration (not yet end-to-end verified)

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
check the PR CI result before relying on it. There is no one-command startup yet.

The form holds the synthetic credential in memory only. It requests same-origin
`/api/review/work-queues`, does not use cookies, and shows one page at a time.
No review, case mutation, or domain decision is implemented. Error handling,
keyboard use and visual layout require full browser and accessibility testing.
The styles follow the existing medical-blue direction; Meta Astryx is a React
reference, not an Angular dependency.
