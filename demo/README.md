# Vorführfälle

Die Demo liefert 132 fiktive Vorgänge ein (`scripts/demo/seed.py`): die zehn hier ausgearbeiteten Vorgänge und alle Fälle der bisherigen NormaCase-Demo aus `legacy/examples/document-cases`.

| Herkunft | Anzahl | Arbeitslisten |
|---|---|---|
| Ausgearbeitete Vorgänge (PG-…, KT-…) | 10 | Zur Freigabe vorbereitet, Informationen nachfordern, Gegenprüfung |
| Referenzfälle der bisherigen Demo (NC-2026-4101 bis 4122): Krankenfahrt und Pflege mit Regelwerk | 10 | alle bewertenden Listen |
| Referenzfälle ohne Regelwerk: Reha, Onkologie, Arbeitsunfall, Überleitung, Arzneimittel, Hilfsmittel | 12 | Unterlagensichtung |
| Technischer Eingang (NC-2026-4190, unbekannte Angabe, `Prefer: handling=lenient`) | 1 | Technische Klärung |
| Plattformfälle (Massenfälle PF-2026-…, nur mit „Plattformfälle einbeziehen“) | 99 | alle Listen, 4 davon technische Klärung |

Ausgearbeitet sind zehn Vorgänge für Vorführung, Screenshots und Case Study: fünf Pflegebegutachtungen und fünf Anträge auf Krankenbeförderung, mit Auftrag, Befundbericht, Pflegetagebuch, Erhebungsbogen, Verordnung, Bescheid oder Ausweis-Scan.

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

`DemoCasesTest` prüft jedes Ergebnis in der CI. Neu erzeugen: `python demo/generate.py` (braucht reportlab, Pillow, pdftoppm).
