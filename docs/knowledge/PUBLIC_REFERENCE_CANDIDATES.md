# Initial public-reference candidates

Research snapshot: 2026-10-02.

This document selects the first real-domain candidates for research. It does **not** define medical rules and does not imply approval by the Medizinischer Dienst Bund, the G-BA, a health insurer or any other domain authority.

Any Knowledge Pack derived from these sources starts as `PUBLIC_REFERENCE`.

## Selection principles

The first pair should:
- use authoritative public primary sources,
- exercise different rule structures,
- test the platform abstraction rather than a single service,
- include at least one domain directly connected to Medizinischer Dienst assessment work,
- be implementable without inventing missing domain logic.

## Candidate A: assessment of long-term care dependency

### Primary source

Authority: Medizinischer Dienst Bund.

Current official landing page:
https://md-bund.de/richtlinien-publikationen/pflegeversicherung/pflegebegutachtung-rechtliche-grundlagen.html

Current guideline linked there:
"Richtlinien des Medizinischen Dienstes Bund zur Feststellung der Pflegebedürftigkeit nach dem SGB XI".

The Medizinischer Dienst Bund states that this version entered into force on **2026-10-01** and replaced the guidelines dated **2024-08-21**.

Release announcement:
https://md-bund.de/aktuell/aktuelle-meldungen/neue-richtlinien-zur-begutachtung-von-pflegebeduerftigkeit.html

The announcement is dated 2026-09-30 and states that the Federal Ministry of Health approved the guidelines on 2026-09-28.

### Why this is a useful platform test

This domain is directly related to Medizinischer Dienst assessment work and uses a structured assessment instrument spanning multiple areas of daily life.

Before implementation, the current guideline PDF must be reviewed section-by-section to establish the exact:
- criteria and permitted values,
- applicability rules,
- scoring/aggregation rules,
- age-dependent rules for children and adolescents,
- special cases and escalation paths,
- source sections supporting every derived rule.

Likely platform gaps such as weighted aggregation or age-dependent applicability are **research hypotheses**, not requirements to implement until confirmed from the source.

### Status

Selected for detailed source analysis.

No production rule has been derived yet.

## Candidate B: prescribed patient transport

### Primary source

Authority: Gemeinsamer Bundesausschuss (G-BA).

Official directive page:
https://www.g-ba.de/richtlinien/25/

Current PDF:
https://www.g-ba.de/downloads/62-492-3867/KT-RL_2025-05-15_iK-2025-08-06.pdf

The G-BA page states that the current version entered into force on **2025-08-06** and was last changed on **2025-05-15**.

The G-BA describes the directive as regulating, in particular:
- prerequisites for prescribed patient transport,
- exceptions from health-insurer approval requirements,
- selection of the required means of transport.

### Why this is a useful platform test

This domain is structurally different from long-term-care assessment. It is expected to exercise categorical prerequisites, exceptions, branching and multiple possible transport/workflow outcomes rather than a weighted assessment instrument.

Before implementation, the current PDF and its annexes must be reviewed to identify:
- explicit rule conditions and exceptions,
- which statements are prescriptive versus explanatory,
- approval/workflow states that belong in domain knowledge,
- transport categories/outcomes,
- effective dates and transitional rules,
- exact source sections for every rule.

Potential needs such as set-membership operators or richer domain outcomes must be justified by the directive before engine changes are made.

### Status

Selected for detailed source analysis.

No production rule has been derived yet.

## Deferred candidate: hearing aids

The G-BA Hilfsmittel-Richtlinie remains a useful later candidate.

Current guideline PDF:
https://www.g-ba.de/downloads/62-492-3815/HilfsM-RL_2025-02-20_iK-2025-05-16.pdf

The current consolidated Hilfsmittel-Richtlinie was last changed on 2025-02-20 and entered into force on 2025-05-16.

Hearing-aid provisions are sufficiently structured to be useful later, but they are intentionally not the first real pack. NormaCase originated partly from discussion of cochlear implantation; choosing different first domains reduces the risk of accidentally shaping the platform around hearing-specific workflows.

## Required research workflow before rule encoding

For each selected candidate:

1. Capture the exact official source artifact and canonical source page.
2. Record publication/effective/retrieval metadata.
3. Compute and store the SHA-256 of the captured source artifact.
4. Review the source itself, not an unofficial summary.
5. Build a source-section -> candidate-rule matrix.
6. Mark ambiguity, conflicts or missing interpretation as `needs-domain-review`.
7. Separate procedural/workflow requirements from clinical/assessment criteria.
8. Create synthetic reference cases for every proposed branch and boundary.
9. Only then add a `PUBLIC_REFERENCE` Knowledge Pack.
10. Never upgrade the validation level without an actual domain review process.

## Non-goals

This research does not:
- claim that public text captures every real operational MD workflow,
- infer undocumented local practice,
- treat an active public source as domain approval of NormaCase,
- make an automated medical or legal decision,
- authorize use with real patient data.
