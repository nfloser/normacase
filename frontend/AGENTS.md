# Workbench frontend

Follow root AGENTS.md. German generic text belongs in src/de.json and domain
presentation belongs in external knowledge metadata. Never use Number/parseFloat
for case decimal values or reconstruct audit JSON through ordinary JSON.stringify.
Use lossless transport and export the original server response. Do not persist case
data in localStorage or send it to analytics. No CDN/fonts/cloud runtime dependency.
Clear and abort stale evaluations when inputs change. Keep UI rules out of the engine.
Verify type/build, decimal model tests, actual API browser flows and responsive layout.
