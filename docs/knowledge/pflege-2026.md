# Pflegebegutachtung nach den BRi 2026 – Einzelkriterien, Sonderfälle, Kinder

Status: `PUBLIC_REFERENCE`, Lifecycle `IN_REVIEW`. Keine fachliche Freigabe durch den Medizinischen Dienst.

## Quelle

*Richtlinien des Medizinischen Dienstes Bund zur Feststellung der Pflegebedürftigkeit nach dem XI. Buch des Sozialgesetzbuches*, erlassen 26.08.2026, genehmigt 28.09.2026, in Kraft seit 01.10.2026 (lösen die Fassung vom 21.08.2024 ab). Gelesen wurde die vom MD Bund veröffentlichte Datei `BRi_Pflege_07_10_2026.pdf` (Fassung 26.08.2026); im Pack gepinnt bleibt das bereits gehashte Artefakt `BRi_Pflege_26_08_2026.pdf`. Beim Abgleich nach der Übernahme lag die [korrigierte Veröffentlichung des MD Sachsen](https://www.md-sachsen.de/fileadmin/MD-zentraler-Ordner/Downloads/01_Pflegebegutachtung/261007_RICHTLINIE_PFLEGE_2026_15_korrigert.pdf) vor (Abruf 24.11.2026, SHA-256 `dfd7bdd8fda327d001ae665e438cf3458af2aae607676cef46fa765fc017e594`). Maßgeblich für die Punkte sind außerdem [§ 15 SGB XI](https://www.gesetze-im-internet.de/sgb_11/__15.html), [Anlage 1](https://www.gesetze-im-internet.de/sgb_11/anlage_1.html) (Kriterien und Einzelpunkte) und [Anlage 2](https://www.gesetze-im-internet.de/sgb_11/anlage_2.html) (Punktbereiche und Gewichte).

### Rechtsprechung und Eingabegrenzen

Der [Medizinische Dienst Bund](https://md-bund.de/aktuell/aktuelle-meldungen/neue-richtlinien-zur-begutachtung-von-pflegebeduerftigkeit.html) nennt die Urteile zur Begutachtung bei kindlichem Diabetes Typ 1 und Mukoviszidose als Anlass für die Konkretisierungen. Das BSG hat am 12.12.2024 entschieden, dass krankheitsbedingte Angst und Abwehr notwendiger Maßnahmen bei der Bewertung in Modul 3 zu berücksichtigen sein können (B 3 P 9/23 R, B 3 P 2/24 R). In Modul 4 können einzelfallabhängig eigenständige Unterstützungsbedarfe unmittelbar beim Essen und Trinken berücksichtigt werden. Ein eigenständiger Hilfebedarf beim Einhalten der Diät gehört in Modul 5 (bei Mukoviszidose insbesondere B 3 P 1/24 R und B 3 P 7/23 R). Diese Kriterien erhalten die bereits erhobenen Stufen beziehungsweise Häufigkeiten als Eingabe. Aus einer Diagnose allein leitet das Pack keine Stufe ab. [BSG-Urteil B 3 P 9/23 R](https://www.bsg.bund.de/SharedDocs/Downloads/DE/Entscheidungen/2024/2024_12_12_B_03_P_09_23_R.pdf) · [BSG-Terminbericht mit den weiteren Entscheidungen](https://www.bsg.bund.de/SharedDocs/Downloads/DE/Terminberichte/2024/2024_44_Terminbericht.pdf)

## Drei Regelwerke

| Pack | Für wen | Abschnitt |
|---|---|---|
| `pflege-2026-erwachsene` | Erwachsene (ab 18 Jahren) | 5.9, 5.10.1 |
| `pflege-2026-kinder` | Kinder und Jugendliche ab dem 19. Lebensmonat | 6.5; Erwachsenen-Systematik, unter 11 Jahren berechnet das Pack die Einzelpunkte aus dem Altersvergleich |
| `pflege-2026-saeuglinge` | Kinder bis zum vollendeten 18. Lebensmonat | 6.6 |

Welches Regelwerk gilt, entscheidet das Alter am Begutachtungstag. Das einliefernde Fachsystem wählt es beim Eingang. Die Säuglingsregel gilt einschließlich des Tages, an dem das Kind den 18. Lebensmonat vollendet (BRi Kapitel 6). Das Kinder-Pack erwartet danach vollendete Lebensmonate im Bereich 18 bis 215. Das bisherige Pack `pflege-adult-score` (nur Modulsummen) bleibt unverändert, damit ältere Bewertungen nachrechenbar bleiben.

## Rechenweg (alles im Pack, nichts im Code)

1. **Kriterien** (F 4.1–F 4.6): Stufe 0–3 je Kriterium; Modul 3 direkt als Einzelpunkte 0/1/3/5. Andere Werte treffen kein Band und bleiben `UNKNOWN` → `HUMAN_REVIEW`.
2. **Modul 4** (5.10.1): Essen dreifach (0/3/6/9), Trinken und Toilette doppelt (0/2/4/6). 4.11/4.12 nur bei überwiegender oder kompletter Inkontinenz bzw. künstlicher Ableitung, sonst 0. 4.13: keine/nicht täglich/nicht auf Dauer/selbständig = 0, täglich zusätzlich zu oraler Nahrung = 6, ausschließlich oder nahezu ausschließlich = 3.
3. **Modul 5** (5.10.1): Häufigkeiten je Kriterium pro Tag, Woche, Monat; Woche ÷ 7 und Monat ÷ 30. Die Auswertung für 5.1–5.11 verwendet den gemeinsamen Nenner 210 ohne Zwischenrundung.
   - 5.1–5.7, Summe pro Tag: unter 1 → 0, 1 bis 3 → 1, über 3 bis 8 → 2, über 8 → 3.
   - 5.8–5.11, Summe pro Tag: seltener als einmal wöchentlich → 0, ab einmal wöchentlich → 1, 1 bis unter 3 täglich → 2, ab 3 täglich → 3.
   - 5.12–5.15 (+ 5.K bei Kindern): 5.12 täglich = 60, wöchentlich × 8,6, monatlich × 2; 5.13 und 5.14 wöchentlich × 4,3, monatlich × 1; 5.15 wöchentlich × 8,6, monatlich × 2; 5.K wöchentlich × 4,3, monatlich × 1. Summe: unter 4,3 → 0, bis unter 8,6 → 1, bis unter 12,9 → 2, bis unter 60 → 3, ab 60 → 6.
   - 5.16: 0–3. Modulsumme = drei Zwischenergebnisse + 5.16.
4. **Gewichtung** (5.10.1, § 15 Abs. 3 SGB XI): Modul 1 10 %, Modul 2/3 15 % (höherer Wert), Modul 4 40 %, Modul 5 20 %, Modul 6 15 %.
5. **Pflegegrad** nach Gesamtpunkten: 12,5 / 27 / 47,5 / 70 / 90. Bis 18 Monate eine Stufe höher: 12,5 → PG 2, 27 → PG 3, 47,5 → PG 4, 70 → PG 5 (§ 15 Abs. 7 SGB XI, 6.6.1).
6. **Besondere Bedarfskonstellation** (F 4.1.B, § 15 Abs. 4 SGB XI): Gebrauchsunfähigkeit beider Arme und beider Beine mit vollständigem Verlust der Greif-, Steh- und Gehfunktionen, nicht durch Hilfsmittel kompensierbar; altersunabhängig zu prüfen. Die Feststellung trifft der Gutachter (Angabe ja/nein); NormaCase leitet sie nicht aus Beispielen ab. Ergebnis: pflegebedürftig und eigene Teilbewertung „Pflegegrad 5 wegen besonderer Bedarfskonstellation“; der Pflegegrad nach Punkten bleibt daneben sichtbar.

## Kinder

- Ab 19 Monaten werden Module 1, 2, 4 und 6 im Vergleich mit altersentsprechend entwickelten Kindern bewertet (6.5, Tabellen je Kriterium). Das Kinder-Pack vergleicht die beobachtete Stufe mit den Alterstabellen und wendet anschließend Einfach-, Zweifach- oder Dreifachwertung an. Altersbedingt nicht zu erhebende Stufen werden im Eingabevertrag explizit mit 0 befüllt. Fehlende Eingaben bleiben unvollständig. Module 3 und 5 sind altersunabhängig. Ab 11 Jahren gilt die Erwachsenen-Punktesystematik (Kinderformular).
- Bis 18 Monate: nur Modul 3, Modul 5, F 4.1.B und KF 4.4.0 (gravierende Probleme bei der Nahrungsaufnahme, 20 Einzelpunkte).

## Offen für die fachliche Prüfung (`needs-domain-review`)

- **Gewichtung bis 18 Monate:** KF 4.4.0 ergibt 20 Einzelpunkte (Kapitel 6, S. 148). Die analoge Gewichtung gemäß Kapitel 5.10.1 und Anlage 2 ergibt 30 gewichtete Punkte. Modul 3 ist bei Säuglingen der einzige bewertete Beitrag an der Stelle von Modul 2/3.
- **Prognose (mindestens sechs Monate)**, Empfehlungen (Reha, Hilfsmittel, Prävention) und Fristen sind nicht Teil der Berechnung.

## Erzeugung und Tests

Die Packs entstehen mit `python3 scripts/knowledge/pflege_2026.py` aus den Tabellen dieses Dokuments (byte-genau reproduzierbar). `PflegeRegelwerk2026Test` prüft jede Schwelle genau an der Grenze, die Gewichtungen, Modul 5 einschließlich exakter Grenzfälle, die besondere Bedarfskonstellation, unzulässige und fehlende Werte sowie Kinder und Säuglinge.
