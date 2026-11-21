# Knowledge Pack rules

This file supplements the repository root `AGENTS.md` for everything under `knowledge/`.

- Do not invent domain rules.
- Repository Knowledge Packs use synthetic data unless a change explicitly introduces a researched public-reference pack.
- Every non-synthetic productive rule requires a traceable source.
- Knowledge is versioned. Do not overwrite historical rule/source meaning in place.
- Public-reference knowledge is not domain-approved knowledge.
- Every rule change requires tests that exercise its meaningful branches, including unknown/missing-data paths.
- Knowledge Packs are declarative data only. No scripts, eval, arbitrary code, network calls or executable extensions.
- Keep manifest, field, rule and source references internally consistent.
- If source material is ambiguous or conflicting, record the uncertainty and require domain review rather than choosing a rule silently.
