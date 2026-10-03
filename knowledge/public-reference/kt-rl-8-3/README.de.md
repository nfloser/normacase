# KT-RL § 8 Absatz 3 — PUBLIC_REFERENCE

Status: `PUBLIC_REFERENCE`, Lifecycle `IN_REVIEW`.

Dieses Knowledge Pack bildet ausschließlich einen eng abgegrenzten, öffentlich
dokumentierten Referenzpfad aus der Krankentransport-Richtlinie (KT-RL) des
Gemeinsamen Bundesausschusses ab. Es ist **keine** fachliche Freigabe durch den G-BA,
den Medizinischen Dienst, eine Krankenkasse oder eine andere Stelle und darf nicht als
produktive Entscheidungsvorgabe verstanden werden.

## Gepinnte Quelle

- Behörde: Gemeinsamer Bundesausschuss (G-BA)
- Dokument: Krankentransport-Richtlinie / KT-RL
- Änderung: 15.05.2025
- Veröffentlichung: BAnz AT 05.08.2025 B1
- in Kraft seit: 06.08.2025
- offizielle PDF:
  `https://www.g-ba.de/downloads/62-492-3867/KT-RL_2025-05-15_iK-2025-08-06.pdf`
- abgerufen: 02.10.2026
- SHA-256 des gepinnten PDF-Artefakts:
  `114a9ca2dd4ef1b49433898abfffb62570911a58e756c01f5e0f3329c9f93dd6`

Die ausführliche Quellenanalyse liegt unter
`docs/knowledge/research/KRANKENTRANSPORT_2025_SOURCE_ANALYSIS.md`.

## Abgebildeter Entscheidungsgegenstand

Das Pack beantwortet **nicht**, ob eine Krankenbeförderung insgesamt verordnet werden
darf. Es prüft nur, ob der ausdrücklich modellierte Ausnahmeweg für Krankenfahrten zur
ambulanten Behandlung nach § 8 Absatz 3 anhand bereits festgestellter strukturierter
Fakten und vorliegender Nachweise unterstützt wird.

Explizit modelliert sind:

- ambulante Behandlung als Prüfkontext;
- zwingende medizinische Notwendigkeit als vom Aufrufer bereits festgestellte
  Voraussetzung;
- Schwerbehindertenausweis mit Merkzeichen `aG`, `Bl` oder `H`;
- Pflegegrad 4 oder 5;
- Pflegegrad 3 zusammen mit ausdrücklich festgestelltem dauerhaftem
  mobilitätsbedingtem Beförderungsbedarf;
- die Übergangsregel für bis 31.12.2016 bestehende Pflegestufe 2 und seit 01.01.2017
  mindestens Pflegegrad 3.

Die Nachweise werden getrennt von den fachlichen Fakten als Evidence-Status geführt.
Ein fehlender oder widersprüchlicher Nachweis wird nicht in ein Nein umgedeutet.

## Quellenzuordnung

| Modellierter Teil | Quelle |
| --- | --- |
| zwingende medizinische Notwendigkeit | § 3 Abs. 1, § 8 Abs. 1 und Abs. 5 KT-RL |
| Merkzeichen aG/Bl/H | § 8 Abs. 3 Satz 1 KT-RL |
| Pflegegrad 4/5 | § 8 Abs. 3 Satz 1 KT-RL |
| Pflegegrad 3 + dauerhafte Mobilitätsbeeinträchtigung mit Beförderungsbedarf | § 8 Abs. 3 Satz 1 KT-RL |
| Übergangsregel frühere Pflegestufe 2 | § 8 Abs. 3 Satz 2 KT-RL |
| Genehmigung für bestätigten §-8-Abs.-3-Pfad gilt als erteilt | § 8 Abs. 6 Satz 2 KT-RL |

## Ergebnissemantik

Das Plattform-Ergebnis `SUPPORTED` bedeutet nur: **der hier modellierte
§-8-Abs.-3-Referenzpfad ist anhand der gelieferten Fakten und Nachweise erfüllt**.

`NOT_SUPPORTED` bedeutet entsprechend nur, dass dieser enge Pfad bei vollständigen
relevanten Angaben nicht erfüllt ist. Es ist **keine** Aussage, dass andere Wege der
KT-RL ausgeschlossen sind.

Fehlende oder widersprüchliche branchenspezifische Nachweise führen zu
`HUMAN_REVIEW`. Fehlende globale Pflichtangaben führen zu `INCOMPLETE`.

Der zusätzliche Output `approval_state` kann:

- `DEEMED_GRANTED` liefern, wenn der modellierte §-8-Abs.-3-Pfad bestätigt ist;
- `NOT_DETERMINED_BY_THIS_PACK` liefern, wenn dieser Pfad vollständig geprüft und
  nicht erfüllt ist;
- `UNKNOWN` bleiben, wenn die für den Pfad nötigen Angaben oder Nachweise nicht
  eindeutig vorliegen.

Das Pack erteilt selbst keine Genehmigung.

## Bewusst nicht modelliert

Insbesondere nicht enthalten sind:

- § 8 Absatz 2 und die nicht abschließende Liste aus Anlage 2;
- die Auslegung einer vergleichbaren Mobilitätsbeeinträchtigung nach § 8 Absatz 4;
- Diagnosen oder die Ableitung medizinischer Tatsachen aus Freitext;
- die Berechnung eines Pflegegrads;
- Auswahl zwischen Taxi/Mietwagen, privatem Pkw, öffentlichem Verkehr,
  Krankentransport oder Rettungsfahrt;
- Hin-/Rückweg-Bewertung;
- § 8a, § 8b und Entlassmanagement;
- lokale Krankenkassen- oder MD-Prozesse;
- echte Patientendaten.

Diese Grenzen sind fachlich beabsichtigt. Ungeklärte oder interpretationsbedürftige
Punkte dürfen nicht durch zusätzliche Annahmen in diesem Pack geschlossen werden.
