# German synthetic workbench

Follow the root AGENTS.md.

- This UI is a synthetic engineering workbench, not a production patient-data interface.
- All ordinary user-visible UI text is German (de-DE) and belongs in the UI resource module.
- Knowledge-specific pack, field, evidence, output and choice labels come from external presentation metadata served by the local API.
- Never duplicate or reinterpret decision logic in React. Submit structured inputs to the local API and display the returned assessment.
- Preserve decimal tokens as strings until they reach the .NET API. Never round domain input or trace values through JavaScript Number.
- UNKNOWN is explicit. Do not turn blank/unknown values into YES, NO or zero.
- Input changes abort pending evaluations and clear stale results.
- No CDN, external font, analytics, telemetry, cloud API or runtime internet dependency.
- Keep forms accessible: explicit labels, keyboard focus, semantic controls and readable responsive layouts.
- The exact assessment JSON returned by the API is the export/audit artifact; do not regenerate it from parsed JavaScript objects.
