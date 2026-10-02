# Pflegebegutachtung 2026 — source analysis

Status: **PUBLIC_REFERENCE research only**  
Research date: 2026-10-02  
Scope: adult scoring path only; no productive Knowledge Pack is defined by this document.

This analysis records statements that are directly supported by the current official Medizinischer Dienst Bund guideline. It is not an MD approval of NormaCase and must not be treated as domain-reviewed knowledge.

## Official source

Authority: Medizinischer Dienst Bund (KöR)

Title: *Richtlinien des Medizinischen Dienstes Bund zur Feststellung der Pflegebedürftigkeit nach dem XI. Buch des Sozialgesetzbuches*

Canonical artifact:

`https://md-bund.de/fileadmin/dokumente/Publikationen/SPV/Begutachtungsgrundlagen/BRi_Pflege_26_08_2026.pdf`

Official landing page:

`https://md-bund.de/richtlinien-publikationen/pflegeversicherung/pflegebegutachtung-rechtliche-grundlagen.html`

Source metadata stated by the guideline:
- issued by Medizinischer Dienst Bund: **2026-08-26**
- approved by Bundesministerium für Gesundheit: **2026-09-28**
- effective from: **2026-10-01**
- publication imprint: September 2026

Source locations in the PDF: front matter, page 2 of the PDF presentation (PDF text page index 1).

### Captured artifact provenance

The exact official PDF artifact was retrieved from the canonical URL by a GitHub Actions runner on **2026-10-02** solely to capture deterministic provenance metadata.

- retrieved at: **2026-10-02**
- size: **1,749,350 bytes**
- SHA-256: **`f39b25b55a30cd2dcf9b5ac1547f73be3f9d077b12c87f69ded38e6fac104e4b`**
- Knowledge source hash representation: **`sha256:f39b25b55a30cd2dcf9b5ac1547f73be3f9d077b12c87f69ded38e6fac104e4b`**

The temporary CI workflow used to calculate this value is removed before merge. Runtime evaluation remains offline and does not fetch this source.

## Scope boundary

The guideline separates adult assessment from assessment of children/adolescents. This slice covers the adult result calculation in section **5.10.1 [F 5.1] Pflegegrad**.

Child/adolescent scoring is intentionally out of scope. The 2026 revision contains child-specific changes, and the source explicitly documents a separate child/adolescent assessment section. Adult rules must not be silently reused for children where the guideline specifies different behavior.

## Source-backed adult scoring facts

### 1. Module weights

Section 5.10.1 states that module sums are transformed into weighted points and lists these weights:

| Module | Domain | Weight |
| --- | --- | ---: |
| 1 | Mobilität | 10% |
| 2 / 3 | Kognitive und kommunikative Fähigkeiten / Verhaltensweisen und psychische Problemlagen | shared 15% |
| 4 | Selbstversorgung | 40% |
| 5 | Bewältigung von und selbständiger Umgang mit krankheits- oder therapiebedingten Anforderungen und Belastungen | 20% |
| 6 | Gestaltung des Alltagslebens und sozialer Kontakte | 15% |

Source: section 5.10.1, displayed pages 96–97; PDF text lines around 2425–2517.

For Modules 2 and 3, the guideline explicitly states that only the **higher weighted point value** enters the total; they do not both contribute.

### 2. Module sum → weighted points

The guideline provides the following discrete mapping.

#### Module 1

| Raw module sum | Weighted points |
| --- | ---: |
| 0–1 | 0 |
| 2–3 | 2.5 |
| 4–5 | 5 |
| 6–9 | 7.5 |
| 10–15 | 10 |

#### Module 2

| Raw module sum | Weighted points |
| --- | ---: |
| 0–1 | 0 |
| 2–5 | 3.75 |
| 6–10 | 7.5 |
| 11–16 | 11.25 |
| 17–33 | 15 |

#### Module 3

| Raw module sum | Weighted points |
| --- | ---: |
| 0 | 0 |
| 1–2 | 3.75 |
| 3–4 | 7.5 |
| 5–6 | 11.25 |
| 7–65 | 15 |

The shared contribution of Module 2 / 3 is the greater of their weighted values.

#### Module 4

| Raw module sum | Weighted points |
| --- | ---: |
| 0–2 | 0 |
| 3–7 | 10 |
| 8–18 | 20 |
| 19–36 | 30 |
| 37–54 | 40 |

#### Module 5

| Raw module sum | Weighted points |
| --- | ---: |
| 0 | 0 |
| 1 | 5 |
| 2–3 | 10 |
| 4–5 | 15 |
| 6–15 | 20 |

#### Module 6

| Raw module sum | Weighted points |
| --- | ---: |
| 0 | 0 |
| 1–3 | 3.75 |
| 4–6 | 7.5 |
| 7–11 | 11.25 |
| 12–18 | 15 |

Source: section 5.10.1 table on displayed page 97; PDF text lines around 2442–2512.

### 3. Total score

The guideline states that the weighted module values are combined into a total score. The special handling of Modules 2 and 3 above applies before this total is formed.

This analysis does not infer any rounding rule beyond the exact discrete weighted values printed in the source.

### 4. Adult total score → Pflegegrad

The guideline states that Pflegebedürftigkeit exists from a total score of at least 12.5 points and maps adult totals as follows:

| Total score | Result stated in source |
| --- | --- |
| below 12.5 | no Pflegegrad in the displayed result table |
| 12.5 to <27 | Pflegegrad 1 |
| 27 to <47.5 | Pflegegrad 2 |
| 47.5 to <70 | Pflegegrad 3 |
| 70 to <90 | Pflegegrad 4 |
| 90 to 100 | Pflegegrad 5 |

Source: section 5.10.1, displayed page 98; PDF text lines around 2518–2532. The adult Formulargutachten repeats the same boundaries.

The phrase “no Pflegegrad” above reflects the adult result table representation. This research slice does not infer downstream benefit/legal consequences from that state.

### 5. Special constellation → Pflegegrad 5

Section **5.9.1 [F 4.1.B] Besondere Bedarfskonstellation** states that a special needs constellation can assign Pflegegrad 5 even when total points are below 90.

For adults, the guideline identifies the special constellation as:

**Gebrauchsunfähigkeit beider Arme und beider Beine** with complete loss of grasping, standing and walking functions that cannot be compensated by assistive devices.

The source contains further explanatory examples and edge descriptions in section F 4.1.B. These descriptions must be analyzed separately before a structured criterion is encoded; examples are not automatically equivalent to a complete machine rule.

Source: displayed pages 58–59 / section F 4.1.B; PDF text lines around 1401–1419. The resulting PG5 override is reiterated in section 5.10.1 and in the adult result form.

**Implementation status:** `needs-domain-review` before encoding the detailed constellation criterion.

## Generic platform capabilities implied by the source

These are architecture findings from the source shape, not Pflege-specific implementation instructions.

### A. Discrete range lookup

The existing `number_in_range` operator can test a range, but Pflege scoring requires a reusable transformation:

`numeric input -> value selected from non-overlapping ordered bands`.

A generic declarative lookup/table construct is preferable to hard-coding Pflege ranges into application logic.

### B. Aggregation by sum

Weighted module values must be combined into a deterministic total.

A generic numeric aggregation expression is needed if the Knowledge Pack is to calculate rather than receive the total as precomputed input.

### C. Aggregation by maximum

Modules 2 and 3 contribute the higher weighted value only.

The platform therefore needs a generic deterministic `max` aggregation if the complete calculation is to live in Knowledge rather than preprocessing code.

### D. Result band mapping

The final total maps into one of several domain outcomes by ordered score ranges.

This should be modeled generically as a banded result mapping, not a Pflege-specific switch in the engine.

### E. Explicit override / precedence

The special constellation can produce Pflegegrad 5 despite a total below 90.

A generic, traceable precedence/override mechanism may be needed. It must preserve:
- which ordinary score result would have applied,
- which explicit condition caused the override,
- the source section supporting the override.

Do not implement a generic override mechanism until its semantics have been designed so it cannot bypass UNKNOWN/fail-closed behavior.

## What is deliberately not derived yet

The following remain outside this source slice:
- the full item-level calculation inside Modules 1–6,
- detailed machine interpretation of the special constellation,
- children/adolescents and age-dependent rules,
- duration/prognosis/workflow recommendations,
- rehabilitation recommendations,
- real operational differences between published guidance and local MD practice,
- any legal or benefit entitlement conclusion beyond the documented Pflegegrad result calculation.

## Proposed next engineering split

1. Design a **generic** declarative numeric transformation/aggregation model:
   - range lookup,
   - sum,
   - max,
   - banded result mapping.
2. Protect UNKNOWN and type semantics with synthetic tests first.
3. Add a separate source-backed test matrix for the adult scoring boundaries.
4. Only then create an initial `PUBLIC_REFERENCE` Pflege scoring pack using the pinned source provenance above.
5. Model F 4.1.B only after a dedicated source/domain review of the detailed criterion.

If implementing these capabilities requires naming Pflege modules in platform code, the abstraction is wrong.
