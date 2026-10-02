# German synthetic workbench

Follow root AGENTS.md.

- React/TypeScript is a presentation adapter only. Never duplicate or infer rule logic in the browser.
- All user-visible platform text is German (de-DE) and lives in the frontend resource module. Pack-, field-, evidence- and output-specific labels come from external presentation metadata exposed by the local API.
- Preserve numeric input as strings until the strict case JSON body is constructed. Never round domain values through JavaScript Number.
- UNKNOWN must be explicit in the form and result rendering.
- Abort and discard stale assessment requests when input changes.
- No CDN, remote font, analytics, telemetry or runtime network dependency. Same-origin loopback API only.
- Use accessible native controls, visible focus states and responsive layouts.
- Tests and examples are synthetic only.
