# Vorführfälle

Die Demo liefert 132 fiktive Vorgänge ein (`scripts/demo/seed.py`): die zehn hier ausgearbeiteten Vorgänge und alle Fälle der bisherigen NormaCase-Demo aus `legacy/examples/document-cases`.

| Herkunft | Anzahl | Arbeitslisten |
|---|---|---|
| Ausgearbeitete Vorgänge (PG-…, KT-…) | 10 | Zur Freigabe vorbereitet, Informationen nachfordern, Gegenprüfung |
| Referenzfälle der bisherigen Demo (NC-2026-4101 bis 4122): Krankenfahrt und Pflege mit Regelwerk | 10 | alle bewertenden Listen |
| Referenzfälle ohne Regelwerk: Reha, Onkologie, Arbeitsunfall, Überleitung, Arzneimittel, Hilfsmittel | 12 | Unterlagensichtung |
| Technischer Eingang (NC-2026-4190, unbekannte Angabe, `Prefer: handling=lenient`) | 1 | Technische Klärung |
| Plattformfälle (Massenfälle PF-2026-…, nur mit „Plattformfälle einbeziehen“) | 99 | alle Listen, 4 davon technische Klärung |

Ausgearbeitet sind zehn Vorgänge für Vorführung, Screenshots und Case Study: fünf Pflegebegutachtungen und fünf Anträge auf Krankenbeförderung. Jede Fallakte enthält fünf bis neun Dokumente in den Formaten, die in der Begutachtung tatsächlich ankommen:

| Format | Beispiel in der Akte | Was es zeigt |
|---|---|---|
| PDF mit Textebene | Auftrag, Befundbericht (2 Seiten), Erhebungsbogen, Bescheid, Verordnung | Seitenvorschau aus dem Fachsystem |
| PDF nur aus Bildern | Arztbrief, vom Angehörigen kopiert und eingescannt | Scan ohne Text, leicht schief, Kopierschatten |
| JPEG | Antrag, mit dem Handy auf dem Tisch fotografiert | Perspektive, Licht, Untergrund |
| TIFF, 1 Bit, mehrseitig | Pflegetagebuch oder Arztbrief per Fax | Seiten erzeugt der Server selbst (Grenzen siehe docs/03) |
| PNG | Medikationsplan aus der Praxissoftware, Ausweis-Scan | Bilddokument ohne Seiten |
| Text | Stellungnahme des Pflegedienstes, Terminübersicht der Praxis | Export aus Pflege- oder Praxissoftware |

Jedes Dokument trägt in `case.json` seine Art (`ORDER`, `MEDICAL`, `CARE`, `PROOF`, `OTHER`), das Ausstellungsdatum und die Fundstellen der bewerteten Angaben (`references`: Feld und Seite). Die Prüfansicht verlinkt darüber jede Angabe auf die Seite, aus der sie stammt. Beim Vorgang KT-2026-208157 zeigen Verordnung und Ausweis für dasselbe Merkzeichen auf unterschiedliche Aussagen; das ist der Widerspruch, den die Prüfung meldet.

**Alle Personen, Einrichtungen, Befunde und Kennungen sind frei erfunden.** Echte Gesundheitsdaten gehören nicht in ein Repository. Die Auswertung nutzt nur die vorhandenen Knowledge Packs (öffentliche Referenzen, fachlich nicht freigegeben); es werden keine medizinischen Regeln erfunden.

| Vorgang | Anlass | Erwartetes Ergebnis | Zeigt |
|---|---|---|---|
| PG-2026-104233 | Pflegegrad, Erstantrag nach Hirninfarkt | erfüllt (Pflegegrad 3) | eindeutiger Fall, Bestätigung, Vier-Augen-Freigabe |
| PG-2026-104271 | Höherstufung bei Alzheimer-Demenz | erfüllt (Pflegegrad 4) | Maximum aus Modul 2 und 3 |
| PG-2026-104308 | Erstantrag bei COPD | nicht erfüllt | Schwelle 12,5 nicht erreicht |
| PG-2026-104315 | Erstantrag bei Parkinson, Modul 4 offen | unvollständig → nach Rückfrage erfüllt (Pflegegrad 2) | Rückfrage mit Nachtrag in den Unterlagen |
| PG-2026-104342 | Höherstufung nach Querschnittlähmung | erfüllt (Pflegegrad 4) | hohe Punktwerte, mehrere Module |
| KT-2026-208114 | Krankenfahrt, Pflegegrad 4 | erfüllt | Nachweis über Pflegegradbescheid |
| KT-2026-208157 | Krankenfahrt, Merkzeichen aG laut Verordnung | manuelle Prüfung | Ausweis zeigt nur G: widersprüchlicher Nachweis, nichts wird geraten |
| KT-2026-208190 | Krankenfahrt, Pflegegrad 2 | nicht erfüllt | alle Ausnahmewege geprüft |
| KT-2026-208203 | Krankenfahrt, Notwendigkeit nicht angekreuzt | unvollständig | Pflichtangabe fehlt |
| KT-2026-208226 | Krankenfahrt, Merkzeichen Bl | erfüllt | Nachweis über Schwerbehindertenausweis |

### Bezug zu den Begutachtungs-Richtlinien (öffentliche Referenz)

Zusammensetzung und Gliederung der Pflege-Akten orientieren sich an den öffentlich veröffentlichten Begutachtungs-Richtlinien Pflege (BRi, Fassung MD Bund vom Dezember 2023, [Download](https://medizinischerdienst.de/fileadmin/MD-zentraler-Ordner/Downloads/01_Pflegebegutachtung/231221_BRi_Pflege.pdf)). Das ist `PUBLIC_REFERENCE`, keine fachliche Freigabe; die seit 1. Oktober 2026 geltende Fassung ist für diese Zuordnung noch nicht abgeglichen.

| BRi | Was dort steht | In der Demo-Akte |
|---|---|---|
| 3.1 Nr. 1 | Antragsinformationen der Pflegekasse für die Beauftragung | Auftrag der Pflegekasse |
| 3.1 Nr. 2 | eingereichte Unterlagen wie Arztbriefe, Entlassungsberichte, Pflegedokumentation; Selbstauskünfte; Hinweise auf Schwerbehinderung | Antrag (Foto), Arztbrief/Entlassungsbericht (Scan), Befundbericht, Medikationsplan, Stellungnahme des Pflegedienstes, Pflegetagebuch (Fax) |
| F 1.1 | pflegerelevante Fremdbefunde | Dokumentart „Ärztliche Unterlagen“ |
| F 4 | Module des Begutachtungsinstruments | Erhebungsbogen Module 1–6 (Fundstelle der Modulsummen) |

Welche Nachweise ein Regelwerk verlangt, steht im Knowledge Pack (`evidenceRequirements`); fehlt einer, zeigt die Fallakte ihn unter „Fehlende Nachweise“.

`DemoCasesTest` prüft jedes Ergebnis in der CI. Neu erzeugen: `python demo/generate.py` (braucht reportlab, Pillow, pdftoppm). Die Ausgabe ist byte-genau reproduzierbar; Rauschen und Verzerrung der Scans sind fest geseedet.
