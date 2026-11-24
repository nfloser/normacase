# Pflegebegutachtung nach den BRi 2026 – Einzelkriterien, Sonderfälle, Kinder

Status: `PUBLIC_REFERENCE`, Lifecycle `IN_REVIEW`. Keine fachliche Freigabe durch den Medizinischen Dienst.

## Quelle

*Richtlinien des Medizinischen Dienstes Bund zur Feststellung der Pflegebedürftigkeit nach dem XI. Buch des Sozialgesetzbuches*, erlassen 26.08.2026, genehmigt 28.09.2026, in Kraft seit 01.10.2026 (lösen die Fassung vom 21.08.2024 ab). Gelesen wurde die vom MD Bund veröffentlichte Datei `BRi_Pflege_07_10_2026.pdf` (Fassung 26.08.2026); im Pack gepinnt bleibt das bereits gehashte Artefakt `BRi_Pflege_26_08_2026.pdf`. § 15 Abs. 1–7 SGB XI ist zum 01.01.2026 inhaltlich unverändert geblieben (neu nur Abs. 8, Berichtspflicht).

## Drei Regelwerke

| Pack | Für wen | Abschnitt |
|---|---|---|
| `pflege-2026-erwachsene` | Erwachsene (ab 18 Jahren) | 5.9, 5.10.1 |
| `pflege-2026-kinder` | Kinder und Jugendliche ab dem 19. Lebensmonat | 6.5; Erwachsenen-Systematik, unter 11 Jahren Einzelpunkte aus dem Altersvergleich |
| `pflege-2026-saeuglinge` | Kinder bis zum vollendeten 18. Lebensmonat | 6.6 |

Welches Regelwerk gilt, entscheidet das Alter am Tag der Begutachtung; das Fachsystem wählt es beim Eingang. Das bisherige Pack `pflege-adult-score` (nur Modulsummen) bleibt unverändert, damit ältere Bewertungen nachrechenbar bleiben.

## Rechenweg (alles im Pack, nichts im Code)

1. **Kriterien** (F 4.1–F 4.6): Stufe 0–3 je Kriterium; Modul 3 direkt als Einzelpunkte 0/1/3/5. Andere Werte treffen kein Band und bleiben `UNKNOWN` → `HUMAN_REVIEW`.
2. **Modul 4** (5.10.1): Essen dreifach (0/3/6/9), Trinken und Toilette doppelt (0/2/4/6). 4.11/4.12 nur bei überwiegender oder kompletter Inkontinenz bzw. künstlicher Ableitung, sonst 0. 4.13: keine/nicht täglich/nicht auf Dauer/selbständig = 0, täglich zusätzlich zu oraler Nahrung = 6, ausschließlich oder nahezu ausschließlich = 3.
3. **Modul 5** (5.10.1): Häufigkeiten je Kriterium pro Tag, Woche, Monat; Woche ÷ 7, Monat ÷ 30, auf vier Nachkommastellen.
   - 5.1–5.7, Summe pro Tag: unter 1 → 0, 1 bis 3 → 1, über 3 bis 8 → 2, über 8 → 3.
   - 5.8–5.11, Summe pro Tag: seltener als einmal wöchentlich → 0, ab einmal wöchentlich → 1, 1 bis unter 3 täglich → 2, ab 3 täglich → 3.
   - 5.12–5.15 (+ 5.K bei Kindern): 5.12 täglich = 60, wöchentlich × 8,6, monatlich × 2; 5.13 und 5.14 wöchentlich × 4,3, monatlich × 1; 5.15 wöchentlich × 8,6, monatlich × 2; 5.K wöchentlich × 4,3, monatlich × 1. Summe: unter 4,3 → 0, bis unter 8,6 → 1, bis unter 12,9 → 2, bis unter 60 → 3, ab 60 → 6.
   - 5.16: 0–3. Modulsumme = drei Zwischenergebnisse + 5.16.
4. **Gewichtung** (5.10.1, § 15 Abs. 3 SGB XI): Modul 1 10 %, Modul 2/3 15 % (höherer Wert), Modul 4 40 %, Modul 5 20 %, Modul 6 15 %.
5. **Pflegegrad** nach Gesamtpunkten: 12,5 / 27 / 47,5 / 70 / 90. Bis 18 Monate eine Stufe höher: 12,5 → PG 2, 27 → PG 3, 47,5 → PG 4, 70 → PG 5 (§ 15 Abs. 7 SGB XI, 6.6.1).
6. **Besondere Bedarfskonstellation** (F 4.1.B, § 15 Abs. 4 SGB XI): Gebrauchsunfähigkeit beider Arme und beider Beine mit vollständigem Verlust der Greif-, Steh- und Gehfunktionen, nicht durch Hilfsmittel kompensierbar; altersunabhängig zu prüfen. Die Feststellung trifft der Gutachter (Angabe ja/nein); NormaCase leitet sie nicht aus Beispielen ab. Ergebnis: pflegebedürftig und eigene Teilbewertung „Pflegegrad 5 wegen besonderer Bedarfskonstellation“; der Pflegegrad nach Punkten bleibt daneben sichtbar.

## Kinder

- Ab 19 Monaten werden Module 1, 2, 4 und 6 im Vergleich mit altersentsprechend entwickelten Kindern bewertet (6.5, Tabellen je Kriterium). Das Regelwerk nimmt die Einzelpunkte nach diesem Vergleich entgegen; die Altersvergleichstabelle selbst rechnet es nicht. Module 3 und 5 sind altersunabhängig. Ab 11 Jahren gilt die Erwachsenen-Punktesystematik (Kinderformular).
- Bis 18 Monate: nur Modul 3, Modul 5, F 4.1.B und KF 4.4.0 (gravierende Probleme bei der Nahrungsaufnahme, 20 Einzelpunkte).

## Offen für die fachliche Prüfung (`needs-domain-review`)

- **Gewichtung bis 18 Monate:** Die Richtlinie nennt die bewerteten Module und die 20 Einzelpunkte für KF 4.4.0, im gelesenen Text aber keine eigene Umrechnung. Angewandt ist die allgemeine Gewichtung (20 Einzelpunkte → 30 gewichtete Punkte, Modul 2/3-Platz nur mit Modul 3).
- **Altersvergleich ab 19 Monaten:** Die Punkte-Matrix (Kind vs. altersentsprechend entwickeltes Kind) ist im Text an Beispielen erläutert; deshalb erfasst das Regelwerk die Einzelpunkte und rechnet den Vergleich nicht selbst.
- **Rundung Modul 5:** gerundet wird je Woche- und Monatswert auf vier Nachkommastellen, dann summiert. Bei ganzzahligen Häufigkeiten ändert das an keiner Schwelle das Ergebnis.
- **Prognose (mindestens sechs Monate)**, Empfehlungen (Reha, Hilfsmittel, Prävention) und Fristen sind nicht Teil der Berechnung.

## Erzeugung und Tests

Die Packs entstehen mit `python3 scripts/knowledge/pflege_2026.py` aus den Tabellen dieses Dokuments (byte-genau reproduzierbar). `PflegeRegelwerk2026Test` prüft jede Schwelle genau an der Grenze, die Gewichtungen, Modul 5 einschließlich Rundung, die besondere Bedarfskonstellation, unzulässige und fehlende Werte sowie Kinder und Säuglinge.
