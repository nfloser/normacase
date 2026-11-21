# Pflegebegutachtung Erwachsene – gewichteter Score — PUBLIC_REFERENCE

Status: `PUBLIC_REFERENCE`, Lifecycle `IN_REVIEW`.

Dieses Knowledge Pack bildet ausschließlich die veröffentlichten
Berechnungsregeln aus Abschnitt 5.10.1 [F 5.1] der Begutachtungs-Richtlinien des
Medizinischen Dienstes Bund ab. Es ist **keine** fachliche Freigabe durch den
Medizinischen Dienst Bund, eine Pflegekasse oder eine andere Stelle und darf nicht
als produktive Pflegegradentscheidung verwendet werden.

## Gepinnte Quelle

- Behörde: Medizinischer Dienst Bund (KöR)
- Dokument: *Richtlinien des Medizinischen Dienstes Bund zur Feststellung der
  Pflegebedürftigkeit nach dem XI. Buch des Sozialgesetzbuches*
- erlassen: 26.08.2026
- vom Bundesministerium für Gesundheit genehmigt: 28.09.2026
- in Kraft seit: 01.10.2026
- offizielle PDF:
  `https://md-bund.de/fileadmin/dokumente/Publikationen/SPV/Begutachtungsgrundlagen/BRi_Pflege_26_08_2026.pdf`
- abgerufen: 02.10.2026
- SHA-256 des gepinnten PDF-Artefakts:
  `f39b25b55a30cd2dcf9b5ac1547f73be3f9d077b12c87f69ded38e6fac104e4b`
- offizieller Richtlinienstand am 03.10.2026 erneut geprüft: die Fassung vom
  26.08.2026 ist seit 01.10.2026 in Kraft.

Die ausführliche Quellenanalyse liegt unter
`docs/knowledge/research/PFLEGE_2026_SOURCE_ANALYSIS.md`.

## Abgebildeter Gegenstand

Das Pack erhält ausschließlich sechs **bereits festgestellte** numerische
Modulsummen:

- Modul 1: Mobilität
- Modul 2: Kognitive und kommunikative Fähigkeiten
- Modul 3: Verhaltensweisen und psychische Problemlagen
- Modul 4: Selbstversorgung
- Modul 5: Umgang mit krankheits- oder therapiebedingten Anforderungen
- Modul 6: Gestaltung des Alltagslebens und sozialer Kontakte

Es bewertet keine einzelnen Kriterien und interpretiert keine medizinischen,
pflegerischen, kognitiven oder psychosozialen Sachverhalte.

Aus den Modulsummen werden entsprechend der veröffentlichten Tabelle die
gewichteten Punkte ermittelt. Für Modul 2 und Modul 3 geht ausschließlich der höhere
gewichtete Wert ein. Danach werden die fünf Beiträge zu einem gewichteten
Gesamtpunktwert summiert.

## Veröffentlichte Transformationen

| Modul | Modulsumme → gewichtete Punkte |
| --- | --- |
| 1 | 0–1 → 0; 2–3 → 2,5; 4–5 → 5; 6–9 → 7,5; 10–15 → 10 |
| 2 | 0–1 → 0; 2–5 → 3,75; 6–10 → 7,5; 11–16 → 11,25; 17–33 → 15 |
| 3 | 0 → 0; 1–2 → 3,75; 3–4 → 7,5; 5–6 → 11,25; 7–65 → 15 |
| 4 | 0–2 → 0; 3–7 → 10; 8–18 → 20; 19–36 → 30; 37–54 → 40 |
| 5 | 0 → 0; 1 → 5; 2–3 → 10; 4–5 → 15; 6–15 → 20 |
| 6 | 0 → 0; 1–3 → 3,75; 4–6 → 7,5; 7–11 → 11,25; 12–18 → 15 |

Die Summe besteht damit aus:

`Modul 1 + max(Modul 2, Modul 3) + Modul 4 + Modul 5 + Modul 6`

## Ergebnissemantik

Das Plattform-Ergebnis dieses Packs bezieht sich **nur auf die gewöhnliche
Score-Schwelle von 12,5 Punkten**:

- `SUPPORTED`: der berechnete gewichtete Score erreicht mindestens 12,5;
- `NOT_SUPPORTED`: der berechnete gewichtete Score liegt unter 12,5;
- `INCOMPLETE`: mindestens eine erforderliche Modulsumme fehlt;
- `HUMAN_REVIEW`: eine gelieferte Modulsumme liegt außerhalb der veröffentlichten
  Tabellenbereiche und kann deshalb nicht sicher transformiert werden.

`SUPPORTED` bedeutet ausdrücklich **nicht**, dass NormaCase einen Pflegegrad
festgestellt hat oder Pflegebedürftigkeit abschließend begutachtet hat.

Zusätzlich werden rein mathematische, source-backed Schwellen-Outputs erzeugt:

- `score_threshold_12_5`
- `score_threshold_27`
- `score_threshold_47_5`
- `score_threshold_70`
- `score_threshold_90`

Jeder Output ist `REACHED`, `NOT_REACHED` oder bei nicht berechenbarem Score
`UNKNOWN`. Die Schwellen entsprechen den in Abschnitt 5.10.1 veröffentlichten
Scoregrenzen; die Outputs selbst weisen **keinen Pflegegrad** zu.

## Bewusst nicht modelliert

Nicht Bestandteil dieses Packs sind:

- die Bewertung der einzelnen Kriterien innerhalb der Module 1–6;
- die fachliche Ermittlung der sechs Modulsummen;
- die besondere Bedarfskonstellation F 4.1.B und deren mögliche Zuordnung zu
  Pflegegrad 5 trotz eines Scores unter 90;
- die Prüfung bzw. Prognose eines Unterstützungsbedarfs über mindestens sechs Monate;
- die Feststellung eines endgültigen Pflegegrades;
- Kinder und Jugendliche;
- Beginn oder Befristung einer Pflegebedürftigkeit;
- Präventions-, Rehabilitations-, Hilfsmittel- oder sonstige Empfehlungen;
- leistungsrechtliche Entscheidungen einer Pflegekasse;
- echte Patientendaten.

Diese Grenzen sind absichtlich enger als die Richtlinie. Fehlende fachliche
Feststellungen werden nicht aus Diagnosen, Freitext oder anderen Daten geraten.
