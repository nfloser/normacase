# Krankentransport-Richtlinie 2025 — source analysis

Status: **PUBLIC_REFERENCE research only**  
Research date: 2026-10-02  
Issue: #32  
Scope: current G-BA Krankentransport-Richtlinie (KT-RL) as a second real reference domain. This document remains a source analysis; a later narrow PUBLIC_REFERENCE implementation is maintained separately under `knowledge/public-reference/kt-rl-8-3/`.

This analysis records semantics supported by the current official source and uses them to challenge NormaCase's generic platform abstractions. It is not a G-BA, Medizinischer Dienst or other domain approval of NormaCase. It must not be promoted to `DOMAIN_REVIEWED` or `PRODUCTION_APPROVED` without the corresponding governance process.

## Official source

Authority: Gemeinsamer Bundesausschuss (G-BA)

Title: *Richtlinie über die Verordnung von Krankenfahrten, Krankentransportleistungen und Rettungsfahrten nach § 92 Absatz 1 Satz 2 Nummer 12 SGB V (Krankentransport-Richtlinie/KT-RL)*

Official landing page:

`https://www.g-ba.de/richtlinien/25/`

Canonical artifact:

`https://www.g-ba.de/downloads/62-492-3867/KT-RL_2025-05-15_iK-2025-08-06.pdf`

Source metadata stated by the G-BA:
- original version: **2004-01-22**
- latest change: **2025-05-15**
- publication of the latest change: **BAnz AT 05.08.2025 B1**
- effective from: **2025-08-06**

Source locations: G-BA landing page and PDF front matter, displayed page 1 / PDF index 0.

### Captured artifact provenance

The exact official PDF artifact was retrieved from the canonical G-BA URL by a temporary GitHub Actions runner on **2026-10-02** solely to capture deterministic provenance metadata.

- retrieved at: **2026-10-02**
- size: **163,489 bytes**
- SHA-256: **`114a9ca2dd4ef1b49433898abfffb62570911a58e756c01f5e0f3329c9f93dd6`**
- Knowledge source hash representation: **`sha256:114a9ca2dd4ef1b49433898abfffb62570911a58e756c01f5e0f3329c9f93dd6`**

The temporary workflow used to calculate the hash was removed before merge. Runtime evaluation does not fetch this source.

## Research boundary

This slice analyzes the decision shape of the current KT-RL. It deliberately does **not**:
- create a productive Krankentransport Knowledge Pack,
- infer medical facts from diagnoses or free text,
- encode local Krankenkassen or MD operational practice not stated in the source,
- calculate a Pflegegrad,
- equate examples or non-exhaustive lists with exhaustive machine rules,
- turn terms requiring professional judgment into guessed boolean criteria,
- add a new platform result model before comparing this domain with the already researched Pflege scoring domain.

References to Pflegegrad in § 8 are inputs to the KT-RL decision context. They do not authorize duplicating or reimplementing Pflegebegutachtung rules inside a transport Knowledge Pack.

## Source-backed decision structure

### 1. Prescription assessment and timing

Section § 2 requires the prescribing person to check the directive's prerequisites and select the required transport means. The prescription is normally issued before transport. Retrospective prescription is allowed only in exceptional cases, especially emergencies.

Private-car and public-transport journeys do not require a prescription. Journeys to ambulatory or inpatient rehabilitation measures are likewise not prescribed under this directive; the patient is referred to the insurer to clarify travel.

Source: § 2(1)–(4), displayed page 3 / PDF index 2.

The source also permits assessment through video consultation under stated prerequisites and, exceptionally, after prior telephone contact when the relevant current condition and mobility information has already been established and no further prescription-relevant information is required. If a sufficiently reliable assessment cannot be made by video, the source directs the prescriber to a direct personal examination instead.

Source: § 2(5), displayed pages 3–4 / PDF indices 2–3.

**Platform implication:** the rule system must be able to end in an explicit review/collection step when a source requires professional assessment and the available evidence is not sufficient. It must not infer that the prerequisite is satisfied.

### 2. Global medical-necessity prerequisite

A transport prescription requires the journey to be strictly medically necessary in connection with an insurer benefit. The medical reason is recorded on the prescription. The source gives examples of journeys without such necessity, such as appointment coordination or collecting prescriptions, for which a transport prescription is not permitted.

Source: § 3(1), displayed page 4 / PDF index 3.

The source says journeys are generally necessary only on the direct route between the patient's location and the nearest suitable treatment option. Necessity must be assessed separately for the outbound and return journey.

Source: § 3(2), displayed page 4 / PDF index 3.

**Platform implication:** outbound and return are not safely modeled as one implicit boolean. They may be separate assessment subjects or explicit dimensions in one assessment.

### 3. Selection of transport means

The transport means under §§ 5–7 is selected solely according to strict medical necessity in the individual case, while observing the economic-efficiency requirement. The current health state and ability to walk are specifically identified as relevant to the selection.

Source: § 4, displayed page 4 / PDF index 3.

This is not a numeric optimization rule in the source. NormaCase must not invent a scoring formula for transport-mode selection.

### 4. Rettungsfahrten

A Rettungsfahrt applies where the patient's condition requires transport by a qualified rescue means or such a condition is expected to arise during transport.

The source then distinguishes:
- RTW for emergency patients requiring additional measures to maintain or restore vital functions,
- NAW/NEF where life-saving immediate measures requiring emergency-physician care must be performed or are expected,
- RTH where ground transport is not sufficiently fast, or rapid delivery of an emergency physician is necessary.

The named rescue means are requested through the locally responsible rescue control center.

Source: § 5(1)–(5), displayed pages 4–5 / PDF indices 3–4.

**Implementation status:** the structural distinctions are source-backed, but any future machine criterion still requires precise domain modeling of the clinical facts supplied to the platform. NormaCase must not diagnose an emergency condition.

### 5. Krankentransport

A Krankentransport may be prescribed when professional support during the journey or special KTW equipment is required, or when such a requirement is expected because of the patient's condition.

Source: § 6(1), displayed page 5 / PDF index 4.

The source states that Krankentransport should also be prescribed where this can avoid transmission of severe infectious diseases. The normative wording is distinct from an unconditional requirement and must be preserved during rule authoring.

Source: § 6(2), displayed page 5 / PDF index 4.

Approval state depends on the context:
- ambulatory-treatment Krankentransport generally requires prior insurer approval,
- journeys to pre-/post-inpatient treatment under § 115a SGB V and ambulatory operations under § 115b SGB V are explicit exceptions,
- Krankentransport to inpatient services does not require insurer approval.

Source: § 6(3), displayed page 5 / PDF index 4.

### 6. Krankenfahrten

Krankenfahrten include journeys using public transport, private vehicles, rental cars and taxis. The source explicitly distinguishes them from transports requiring professional medical support during the journey.

Source: § 7(1), displayed page 5 / PDF index 4.

The source specifies contexts in which taxi or rental-car Krankenfahrten may be prescribed, including inpatient services and defined pre-/post-inpatient or ambulatory-surgery scenarios.

Source: § 7(2), displayed pages 5–6 / PDF indices 4–5.

Taxi or rental-car Krankenfahrt may only be prescribed where public transport or a private vehicle cannot be used for strict medical reasons.

Source: § 7(3), displayed page 6 / PDF index 5.

Where private/public transport can be used in the specified ambulatory-operation and § 8 contexts, the source provides for no transport prescription; an attendance certificate may instead be issued on request for submission to the insurer.

Source: § 7(4), displayed page 6 / PDF index 5.

Where several patients must travel to the same destination, a shared journey is prescribed if no medical reason opposes it.

Source: § 7(5), displayed page 6 / PDF index 5.

Krankenfahrten under § 7 do not require insurer approval.

Source: § 7(6), displayed page 6 / PDF index 5.

### 7. Exceptional ambulatory-treatment paths under § 8

Section § 8 allows Krankenfahrten to ambulatory treatment in special exceptional cases when strict medical necessity exists. The source also includes specified preventive examinations and treats care including diagnostics in a Geriatrische Institutsambulanz as ambulatory treatment for this purpose.

Source: § 8(1), displayed page 6 / PDF index 5.

#### § 8(2): high-frequency / long-duration treatment path

Both source conditions are required:
1. the underlying disease prescribes a therapy schedule with high treatment frequency over a longer period, and
2. the treatment or disease course impairs the patient such that transport is indispensable to avoid harm to life or bodily integrity.

Source: § 8(2) sentence 1, displayed page 6 / PDF index 5.

Annex 2 identifies cases in which these conditions are **generally** fulfilled:
- dialysis treatment,
- oncological radiotherapy,
- parenteral antineoplastic drug therapy / parenteral oncological chemotherapy.

The directive explicitly states that this list is not exhaustive.

Source: § 8(2) sentences 2–3 and Annex 2, displayed pages 6 and 9 / PDF indices 5 and 8.

**Rule-authoring constraint:** Annex 2 must not be converted into an exhaustive allowlist, and "in der Regel" must not be silently strengthened to "always".

#### § 8(3): specified disability / Pflegegrad evidence path

The source permits ambulatory-treatment journeys for insured persons presenting:
- a severe-disability card with marker `aG`, `Bl` or `H`, or
- an SGB XI classification notice for Pflegegrad 3, 4 or 5.

For Pflegegrad 3, the source additionally requires a transport need caused by permanent mobility impairment. A transitional rule for people formerly classified in Pflegestufe 2 is also stated.

Source: § 8(3), displayed page 6 / PDF index 5.

**Knowledge boundary:** a future KT-RL pack may consume an externally established Pflegegrad/evidence fact. It must not reproduce the Pflege assessment algorithm as transport-domain logic.

#### § 8(4): comparable mobility impairment

The source also allows prescription when no proof under § 8(3) sentence 1 exists, if the person has a mobility impairment comparable to the criteria of § 8(3) sentence 1 and needs ambulatory treatment over a longer period.

Source: § 8(4), displayed page 6 / PDF index 5.

**Implementation status: `needs-domain-review`.** The source does not provide a complete bounded machine definition of "comparable" in this paragraph. NormaCase must not invent one.

#### § 8(5)–(6): justification and approval

The strict medical necessity of both the journey and transport means must be justified.

Krankenfahrten under § 8 generally require prior insurer approval. For ambulatory-treatment Krankenfahrten under § 8(3), however, the source states that approval is deemed granted under § 60(1) sentence 5 SGB V.

Source: § 8(5)–(6), displayed pages 6–7 / PDF indices 5–6.

**Platform implication:** approval cannot be represented accurately as a single yes/no derived from prescription eligibility. At minimum the source distinguishes `required`, `not required` and `deemed granted` contexts.

### 8. Tagesstationäre Behandlung and Entlassmanagement

Under § 8a, hospitals may prescribe Krankenfahrten during day-inpatient treatment for insured persons meeting § 8(3). Other § 8 provisions do not apply to that path, and the journeys do not require insurer approval.

Source: § 8a, displayed page 7 / PDF index 6.

Under § 8b, hospitals may prescribe transport in discharge management when necessary for care immediately after discharge, subject to the stated scope and exceptions.

Source: § 8b, displayed page 7 / PDF index 6.

These are context-sensitive workflow paths, not merely transport-mode predicates.

### 9. Approval procedure

Approval-required prescriptions must be submitted to the insurer early. The insurer determines the duration and scope of approval, with examples including transport means and outbound/return journey.

Source: § 9, displayed page 7 / PDF index 6.

**Platform implication:** a deterministic support result may need to express that the next responsible actor is the insurer and that some scope remains externally determined. The engine must not fabricate the insurer's approval.

### 10. Prescription content

Annex 1 requires the prescription to record, among other information:
- outbound journey, return journey or both,
- the reason for transport / underlying insurer service,
- treatment day or treatment frequency and the nearest suitable treatment location,
- the medically necessary transport means.

Source: Annex 1, displayed page 8 / PDF index 7.

This supports modeling direction, purpose and transport mode as explicit structured data rather than free-text side effects.

## Normative-strength and uncertainty notes

A future Knowledge Pack must preserve distinctions in the source instead of flattening them:

| Source wording / shape | Modeling consequence |
| --- | --- |
| strict medical necessity / explicit prerequisite | may become a deterministic condition only when the required fact itself is available and governed |
| "kann" | do not convert into an unconditional mandatory conclusion |
| "soll" | preserve the normative distinction; do not silently rewrite as "must" |
| "in der Regel" | represent as a default/source-supported path, not an absolute universal truth |
| examples ("zum Beispiel", "insbesondere") | do not treat as exhaustive unless the source says so |
| explicitly non-exhaustive list | a closed enum/allowlist would be incorrect |
| professional assessment such as "hinreichend sichere Beurteilung" or "vergleichbare Beeinträchtigung" | `HUMAN_REVIEW` / `needs-domain-review` until a reviewed bounded criterion exists |
| insurer approval | an external workflow/state, not something the rule engine may pretend to grant |

## Cross-domain architecture findings

The Pflege scoring source and KT-RL have materially different shapes.

### Pflege scoring shape

The researched adult Pflege path is dominated by:
- discrete numeric transformations,
- sum/max aggregation,
- score boundaries,
- a source-defined exceptional override.

The numeric-expression slice implemented after that research addresses the generic numeric part without embedding Pflege semantics.

### KT-RL shape

KT-RL is dominated by:
- prerequisite gates,
- medical-selection branches,
- evidence/status inputs,
- context-specific exceptions,
- approval workflow,
- multiple coexisting outputs,
- separate outbound/return assessment,
- explicit human/professional judgment boundaries.

The numeric-expression capability is therefore not the central abstraction for this domain. This is useful evidence that NormaCase must not evolve into a scoring engine with a medical UI attached.

## Result-model implication

A single scalar score or a single domain result code is insufficient to represent the KT-RL source faithfully.

A future generic assessment result may need multiple independently traceable, UNKNOWN-capable output dimensions such as:

- **decision / prescription state**  
  e.g. permitted, not permitted, prescription not required, human review required;

- **transport mode**  
  e.g. Rettungsfahrt variant, Krankentransport, taxi/rental-car Krankenfahrt, private/public transport path;

- **approval state**  
  e.g. prior approval required, not required, deemed granted, externally pending;

- **direction scope**  
  outbound, return, or both, without assuming both have the same necessity result;

- **next action / workflow responsibility**  
  e.g. issue prescription, provide attendance certificate, submit to insurer, refer to insurer, obtain direct personal examination;

- **justification/evidence obligations**  
  source-backed facts that must be documented or reviewed.

This is an architecture finding, **not yet a platform contract**.

The existing `AssessmentOutcome` should continue to describe the platform-level assessment disposition (`SUPPORTED`, `NOT_SUPPORTED`, `INCOMPLETE`, `HUMAN_REVIEW`) until a separate generic domain-output design is proven. It should not be overloaded with KT-RL-specific transport modes or approval states.

## Requirements for a future generic output model

Before implementation, a synthetic slice should prove that a generic output mechanism can:

1. emit multiple named outputs from one deterministic evaluation;
2. use bounded typed values rather than arbitrary executable expressions;
3. carry `UNKNOWN` per output rather than coercing missing information;
4. retain the exact rule and source revision that produced each output;
5. distinguish a decision result from a workflow action or external pending state;
6. represent outputs whose values are selected independently for outbound and return journeys;
7. prevent one output from silently overriding another without explicit precedence semantics;
8. remain domain-neutral — no transport, Pflege or benefit-specific names in the core.

No implementation is justified by this research alone. A synthetic design/test slice should precede any core change.

## Domain-review backlog exposed by this source

The following points must not be encoded from this source analysis alone:

- a machine definition of "vergleichbare Beeinträchtigung der Mobilität" under § 8(4);
- diagnosis-to-emergency classification for Rettungsfahrt/RTW/NAW/RTH;
- inference that an infectious disease satisfies the § 6(2) transport criterion;
- interpretation of "hohe Behandlungsfrequenz" or "über einen längeren Zeitraum" beyond source-backed facts supplied to the pack;
- interpretation of individual mobility/health facts that require medical judgment;
- local insurer workflows, forms, service-level expectations or practices not contained in the pinned source;
- any automatic Pflegegrad derivation.

These remain `needs-domain-review` or external-input boundaries.

## Proposed next engineering split

1. Create a **synthetic** structured-output design covering several independent outputs and UNKNOWN behavior.
2. Keep `AssessmentOutcome` as the platform disposition while testing a separate domain-output representation.
3. Include an external/pending workflow state in the synthetic domain so "approval required" cannot be mistaken for approval granted.
4. Test separate direction-scoped output values without transport-specific core names.
5. Make every produced output traceable to rule/source revision.
6. The synthetic model is now stable enough for the narrow § 8(3) PUBLIC_REFERENCE implementation under `knowledge/public-reference/kt-rl-8-3/`; keep its scope limited to explicit source-backed facts.
7. Keep § 8(4) and other professional-judgment criteria out of machine rules until dedicated domain review exists.

If the generic output model needs names such as `transportMode`, `pflegegrad` or `krankenkasseApproval` in platform code, the abstraction is wrong.
