# NormaCase: Arbeitsplatz kompakt – verbindliche UI-Zielvorgaben

Stand 10.10.2026. Abgeleitet aus der bereitgestellten Arbeitsanweisung `UI_ARBEITSANWEISUNG.md`. Diese UI-Anforderungen ergänzen die Master-Spezifikation und haben bei Fragen der Bildschirmgestaltung Vorrang. Die Entwicklung erfolgt in überprüfbaren PRs.

## Produktrolle und Sicherheitsgrenze

NormaCase bleibt eine eigenständige, optionale Zusatzanwendung zum Hauptsystem, keine Übernahme der produktiven MD-Verfahren. Anmeldung, Rollen, Vertretung, Mandantentrennung, Scan, Aufbewahrung und Löschung werden nicht als zusätzliche Komponenten in diesem *Arbeitsplatzbildschirm* dargestellt. Das bedeutet **nicht**, dass NormaCase APIs oder eigene Datenhaltung ohne Zugriffskontrollen betrieben werden dürfen. Die Security-, Governance-, Audit- und Datenschutzanforderungen aus den Projektregeln gelten unverändert. Keine Fallinhalte in URL, Logs, Browsertelemetrie oder unverschlüsselte Browser-Persistenz. In Demo und CI nur synthetische Daten.

## Layout und Arbeitsablauf

- Kopf: NormaCase und aktiver Fall mit Fallnummer, Aktenzeichen, fachlichem Kurztitel; bei fehlendem Fokus „Kein Fall ausgewählt“.
- Menüleiste: Datei, Bearbeiten, Ansicht, Fälle, Extras, Hilfe.
- Darunter kompakte Symbolleiste mit Icon und Text; nur sinnvolle verfügbare Aktionen, sonst deaktiviert mit Begründung.
- Links 20–25 % breite, verstellbare Navigation: Systemarbeitslisten und persönliche Ordner/Suchen; **keine Fallknoten im Baum**. In einem separaten Bereichswechsler unten: Fälle, Berichte/Export, Wissen & Regeln, Einstellungen.
- Hauptbereich oben: tabellarische Fallliste mit Spaltenkopf und Filterzeile. Standard mindestens 20 sichtbare Fälle bei 1920×1080. Sollzeilenhöhe 28 px, kompakt 24 px, großzügig 34 px. Keine Zeilenaktionsbuttons; Kontextmenü, Enter und Toolbar übernehmen Bedienung.
- Darunter verstellbarer, standardmäßig geöffneter Detailbereich mit Dokumentenansicht und Registern Dokumente, Prüfung, Falldaten, Nachweise, Regelgrundlagen, Verlauf, Notizen. Standard Listenteil 55 %, Detailbereich 45 %, mit F4 ein-/ausblendbar.
- Statusleiste: sichtbare/gesamte Fälle, Auswahl, Filterzustand, Arbeitsstand und einmaliger Hinweis auf synthetische Demo.
- Entwicklerwerkzeuge nicht in der Hauptnavigation: ausschließlich Extras → Entwicklung im Demo-Modus.
- Browser-Erlebnis als normale React-Präsentationsschicht; später Desktop-Hülle ohne Neubau des fachlichen Kerns.

## Tabelle

Standardspalten: Auswahl, Markierung, Fallnummer, Aktenzeichen, Sache/Thema, Fallart, Bearbeitungsstand, Prüfergebnis, Priorität, Zuständig, Eingang, Wiedervorlage, Dokumentanzahl. Eigener Ordner ist keine Standardspalte. Technische Schlüssel gehören in eine optionale Spalte oder einen Tooltip; die sichtbaren Falltitel sind fachlich lesbar.

Textfilter pro Spalte, Auswahlfilter für Status und Fallart, Datumsintervalle, visuelle Anzeige aktiver Filter, Zurücksetzen. Serverseitige stabile Ein- und Mehrfachsortierung, stabile Pagination und explizite Auswahlgrenzen (Seite vs. gefilterte Menge). Klick fokussiert, Doppelklick bzw. Enter öffnet, Strg/Shift wählt mehrere, Rechtsklick öffnet Kontextaktionen. Keine stillschweigende Aktion auf alle gefilterten Fälle.

Kontextmenü perspektivisch: Öffnen, neues Fenster, Bestätigen, Rückfrage, Zuständigkeit, Ordner, Farbe, Lesezeichen, Wiedervorlage, Verlauf, Kopie des Ergebnisses, Export, Archiv. Nicht verfügbare Aktionen mit Begründung deaktivieren.

## Status- und Organisationsfarben

Grundtokens: Primär #123C63, Aktion #256DA8, Hintergrund #F5F8FB, Fläche #FFFFFF. Bearbeitungsstand bestimmt *den Zeilenhintergrund*, unabhängig von persönlichen Farben:
- Neu #EAF3F9
- Angaben nachfordern #FFF6CC
- Gegenprüfung #FFE3C7
- technische Klärung / Konflikt #FBD9D9
- Unterlagensichtung #EADFF5
- Freigabebereit / abgeschlossen #DDF2DD
- Auswahl #256DA8, Text weiß

Es gilt die fachlich separat zu prüfende Statuspriorität: technische Klärung vor Konflikt vor Gegenprüfung vor Rückfrage vor Unterlagensichtung vor Freigabebereit vor Neu. Sekundärstatus bleibt als Text/Symbol sichtbar. Eigene Markierung/Farben nur als Punkt oder Chip, *nie* als fachlicher Status. Farben nicht alleiniger Bedeutungsträger (WCAG-Kontrastziel 4,5:1). Zeilenfärbung ein/aus schaltbar.

Persönliche Ablage: Farbordner und gegebenenfalls verschachtelte Ordner, Lesezeichen, gespeicherte Suchen; mehrere Ordnerzuordnungen pro Fall als anzustrebendes Datenmodell, ohne Fallduplikation oder Änderung von Ergebnis/Workflow. Die aktuelle Demo-API erlaubt noch nur eine Zuordnung. Vorgabe zur Umstellung als eigener separater Backendschritt verfolgen.

## Detail- und Dokumentansicht

Fall bleibt sichtbar: Liste oben, Dokumente unten. Dokumentteil mit Ordnerbaum, Dateiliste, Viewer, Dateimetadaten/Pfad. Viewer für PDF, Bilder und Text: Seitenwechsel, Zoom, Fit, Drehen, Suche, Thumbnails, Download und verknüpfter Quellensprung. Dokument und Kriterien sollen nebeneinander und mit verschiebbarer Trennung nutzbar sein; Viewer auf zweiten Monitor auslagerbar. Scans zeigen an, ob Textsuche möglich ist.

Prüfung zeigt nachvollziehbar Eingabe → Kriterium → Regel → Version/Quelle → Teilbewertung → Gesamtergebnis; zwischen NICHT ERFÜLLT, UNKNOWN und NICHT ANWENDBAR strikt unterscheiden. Verlauf fachverständlich, ohne rohe JSON-Blöcke.

## Export, persönliche Einstellungen, Sammelaktionen

Ergebnis, Aktenzeichen und Nachforderungstext kopieren. Quellen- und versionsgebundene Prüfberichte als PDF und Word; Hauptsystem-Verknüpfung nur nach abgestimmtem, konfiguriertem URL-Schema und Berechtigungsprüfung. Exporte enthalten Bewertungsdatum und Knowledge-Release.

Persistente persönliche Einstellungen: Dichte, Spalten und Breiten, Sortierung/Filter, Trenner, Farben, Ordner, Lesezeichen, gespeicherte Ansichten; „Ansicht speichern“, kontrollierter Import/Export persönlicher Einstellungen. Browser und später Desktop benötigen datenschutzkonforme, benutzerbezogene Persistenz. Demo-Speicher im Prozess ist dafür noch keine Lösung.

Sammelaktionen: explizite Auswahl → Vorschau mit pro Fall begründeter Zulässigkeit → Bestätigung → erneute serverseitige Berechtigungs- und Revisionsprüfung → pro Fall differenzierte Ergebnisse und ggf. Fortschritt. Technische Fehler dürfen nicht als fachliche Ablehnung erscheinen. Organisationstätigkeiten ändern keine fachlichen Resultate.

Tastatur: Pfeile, Enter, Esc, Entf mit Bestätigung ausschließlich für eigene Ordner, Strg+F, Strg+K, F4, F5, Alt+↑/↓, Shift+F10; robuste Fokusführung und deutliche Fokusindikatoren.

## Umsetzung und Abnahme

Implementieren in nachvollziehbaren Slices: (1) Shell/Statusfarben/Kompakttabelle; (2) serverseitige Filter/Sortierung/Spalten; (3) dauerhafter Split-Dokumentbereich; (4) Prüfungsregister; (5) persönliche Ablage und Einstellungen; (6) Export/Sicherung; (7) sichere Sammelaktionen; (8) Tastatur/Fehler/Glossar/Desktop-Bridge.

Eine Anforderung ist erst erfüllt, wenn die tatsächliche Funktion/Datenspeicherung und API-Grenze getestet ist. Tests müssen mindestens 20 Zeilen bei 1080p, Statusfarbe samt Text, kontextgebundene aktive Fallanzeige, fehlende unberechtigte Aktionen, Dokumentansicht ohne Seitenwechsel und reproduzierbare Revisionskonflikte prüfen. Ausstehende fachliche Zuordnungen (Statusliste, Pflichtspalten, Hauptsystem-URL, Dateiformate, Mehrfachordner, 50k-Falllast) als explizite Review-Punkte führen, nicht erraten.

## Umsetzung des ergänzenden Layoutauftrags (Issue #286)

Die neuere konkrete Layoutvorlage setzt das Chrome-Budget auf 32 px Menü + 36 px
Symbolleiste + 24 px Statusleiste. Der Standardteiler folgt ihr mit 45 % Liste und
55 % Detail. Für die Abnahme gelten damit mindestens zwölf sichtbare Zeilen bei
geöffnetem Detail und 24 mögliche Zeilenplätze bei geschlossenem Detail; die
frühere pauschale Vorgabe von 20 Zeilen bei geöffnetem Detail ist ersetzt.

Die normale Ansicht startet mit den 22 medizinischen Referenzakten. Die 100
historischen Plattformfixtures bleiben vollständig vorhanden, über Extras und
optional „Plattformfälle einbeziehen“. Fallnummer, Person und fachlicher Auftrag
werden aus vorhandenen Aktenbezeichnungen dargestellt; technische IDs bleiben
Such-/API-Schlüssel. Die fachlichen Pflege-Kurztitel werden in den externen Fallprofilen geführt.
Knowledge-Bezeichnungen und technische Schlüssel bleiben für Replay unverändert. Organisationsstatus und Prüfergebnis bleiben getrennt.

Die vorhandenen 90 PDFs sind die Ausgangsakte, keine neu erfundene Begutachtung.
31 strukturierte Beleganlagen erhalten eine lesbare Tabelle auf Seite 2; der
kontrollierte Übermittlungsvermerk, Inputs und Fundstellen bleiben unverändert.
Nachweise, offene Punkte und Prüfanlass stammen aus denselben Dokumentendaten;
Nachforderungsschreiben ersetzen weiterhin keine fehlenden Arztberichte. Die
öffentliche Referenzkennzeichnung und unveränderten Outcome-/Inputtests bleiben
maßgeblich. Zuständigkeit ist mangels erfasster Zuweisung ausdrücklich „Nicht
zugewiesen“; Eingang, Priorität und Wiedervorlage werden nicht erfunden.

Die Fallprüfung zeigt Ergebnis und Fundstellen nebeneinander. Scores erscheinen
erst im aufklappbaren Register „Prüfung und Regelgrundlagen“, mit Originalwerten,
Vergleich, Regelversion, Release und Quelle aus der verlustfrei gelesenen Prüfspur.
Die vollständige Prüfspur bleibt erreichbar. Negative Teilbewertungen werden
neutral dargestellt; UNKNOWN und nicht anwendbar behalten eigene Bezeichnungen.

Explorer-Einträge verwenden dieselbe NavTreeItem-Komponente. Farbänderung für
Arbeitslisten und Ordnerbearbeitung sind auch im Explorer-Kontextmenü erreichbar.
Die Tabelle hat lokale Filter und stabile Sortierung für den begrenzten Demo-
Bestand. Die Vollakte erhält Explorer, Menü-, Symbol- und Statusleiste. Dokumente
werden ausschließlich über die Dateiliste gewählt. Der gemeinsame Viewer nutzt
lokale PNG-Seiten des originalen PDFs, fortlaufende Seiten, ResizeObserver,
Breiten-/Ganzseiten-/Prozentzoom, Drehung, Seitenfeld, Quellensprung und Download.
Es wird kein neues PDF- oder OCR-Parsing behauptet. Die vorhandenen Vorschauen
haben 100 dpi; hochauflösendes PDF-Rendering und Textsuche bleiben separate Arbeit.

Ansichtsdichte, Trenner, Ordnerspalte, Sortierung und Zoom gelten ausdrücklich für
die Sitzung. Persistente benutzergebundene Ansichten (#281), mehrere Ordner,
Produktivpagination, Spaltenverschiebung/-breiten, Wiedervorlage und PDF-/Word-
Prüfberichte werden nicht durch Browser-Speicher oder scheinbar aktive Funktionen
ersetzt. Backend-/API-Verträge bleiben in diesem Präsentationsschritt unverändert.
Die bestehende serverseitige Ablagepersistenz im Demoprozess bleibt erhalten.

CI prüft reale API-Browserabläufe und erstellt Vorher-/Nachher-Bilder bei
1920×1080 und 1366×768. Die Vorherbilder stammen aus dem exakten PR-Basiscommit,
nicht aus nachgebautem HTML. Die Bilder liegen im CI-Artefakt
`workbench-browser-results`.
