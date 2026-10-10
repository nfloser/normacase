# Deutsche Prüfwerkstatt

Die React-/TypeScript-Oberfläche nutzt den lokalen ASP.NET-Core-Adapter und dieselbe
deterministische Engine wie CLI und API. Verwende ausschließlich synthetische Daten.

Build und Start aus dem Repository-Verzeichnis:

```bash
npm --prefix frontend ci
npm --prefix frontend run build
dotnet run --project src/NormaCase.Api
```

Öffne anschließend http://localhost:5080. Wähle einen Prüfbereich und ein Beispiel.
Über „Beispiel laden“ werden das ausdrückliche Prüfdatum und die synthetischen
Angaben vorbelegt. Danach kann der Fall verändert und mit „Jetzt prüfen“ ausgewertet
werden. Fehlendes bleibt unbekannt; fehlende Evidenz kann manuelle Prüfung auslösen.

Die schreibgeschützte Sektion „Fallwarteschlangen“ zeigt zusätzlich einen
deterministischen 100-Fall-Vorführbestand. 60 Fälle werden zur Freigabe vorbereitet,
20 zur Informationsklärung, 15 zur Gegenprüfung und 5 als technische Ausnahme
geroutet. Diese Verteilung entsteht serverseitig aus der bestehenden Bewertungs- und
Routing-Logik; die Oberfläche zeigt keine erfundenen UI-Zähler. Für eine kompakte
Vorführung werden pro Arbeitsliste nur fünf repräsentative Fälle als Drill-down
angeboten. Der Bestand wird bei jedem Start neu aufgebaut und nicht persistiert.

Die Oberfläche zeigt Wissens-/Plattformstand und Quellenrevision. „Technische
Prüfspur“ zeigt das originale Server-JSON. Der JSON-Download exportiert genau diesen
Text. Dezimalwerte werden verlustfrei transportiert; JavaScript Number wird nicht
für Fallwerte verwendet. Die Eingabe erlaubt Komma oder Punkt, ohne
Tausendertrennzeichen. Änderungen an Eingaben brechen laufende Requests ab und
entfernen veraltete Ergebnisse.

Generische Texte liegen in frontend/src/de.json, fach-/beispielspezifische
Beschriftungen in knowledge/demo-*/presentation.de-DE.json. Diese Metadaten werden
durch die generische Knowledge-Präsentationsschicht streng gegen Felder, Evidenz,
Ausgaben und Auswahlwerte des jeweiligen Packs validiert; der API-Adapter löst nur
noch die referenzierten synthetischen Beispieldateien auf. Die Präsentationsdaten
beeinflussen keine Regelentscheidung. Schrift und alle JavaScript-/CSS-Assets sind
lokal; es gibt keine CDN-/Cloud-/Analytics-Abhängigkeit zur Laufzeit. Falldaten
bleiben im Arbeitsspeicher, solange die Seite geöffnet ist. Keine Speicherung oder
Patientenverwaltung, keine produktive fachliche Freigabe oder Berechtigungsverwaltung.

Prüfungen:

```bash
npm --prefix frontend run typecheck
npm --prefix frontend test
npm --prefix frontend run build
dotnet build src/NormaCase.Api --configuration Release
cd frontend
npx playwright install chromium
npm run test:e2e
```

Die Browser-Tests starten den echten API-Prozess und prüfen erfüllte Voraussetzungen,
UNKNOWN/unvollständige Angaben, manuelle Prüfung, genaue Dezimalwerte,
Ergebnis-Reset und mobile Darstellung. npm Restore/Browserinstallation benötigen
beim Einrichten Netzwerkzugriff; die gebündelte Anwendung benötigt ihn nicht.

## Berechtigte Sammelfreigabe

Im opt-in PostgreSQL-Reviewmodus zeigt die Arbeitsliste eine Auswahl ausschließlich
für lesbare Fälle, die im Freigabestand stehen und serverseitig sowohl `ACCEPT` als
auch `BATCH` besitzen. Eine gemeinsame ausdrückliche Begründung gilt für alle
ausgewählten Systemergebnisse. Die Oberfläche überträgt höchstens 100 Fallbefehle mit
den angezeigten exakten Revisionen; jeder Fall bleibt eine eigene Transaktion.

Nach Abschluss erscheinen geordnete deutsche Einzelergebnisse für gespeichert,
abgelehnt, zwischenzeitlich geändert, policy-seitig ausgeschlossen oder nicht
bearbeitet. Die Oberfläche deutet UNKNOWN nicht um und führt keine automatische
Wiederholung gegen einen neueren Fallstand aus. Wird eine Übertragung abgebrochen oder
bleibt ihre Antwort wegen eines Netzfehlers unklar, bleibt exakt derselbe Request im
Arbeitsspeicher für eine bewusste idempotente Wiederholung gesperrt. Abmeldung oder
401 löschen Auswahl, Wiederholungsdaten und Ergebnis. Details stehen in
[Sammelfreigabe](../architecture/BATCH_REVIEW.md).

## Prüfsnapshots herunterladen und wiederholen

Nach „Jetzt prüfen“ steht zusätzlich „Prüfsnapshot herunterladen“ bereit. Die Datei
enthält das ursprüngliche Wissenspaket, sämtliche Eingaben, das Ergebnis und die
Prüfwerte. Ergebnis und Snapshot werden aus derselben Auswertung erzeugt. Die
JSON-Texte werden im Browser unverändert heruntergeladen; auch sehr genaue
Dezimalwerte werden nicht durch JavaScript Number gerundet.

Unter „Gespeicherte Prüfung offline überprüfen“ kann eine synthetische Snapshotdatei
bis 1 MiB ausgewählt werden. Der lokale Dienst validiert das eingebettete Wissen und
berechnet die ursprüngliche Prüfung erneut. Bei Erfolg erscheinen eine deutsche
Bestätigung, der ursprüngliche Prüfzeitpunkt, Wissens- und Plattformstand sowie ein
Download des bestätigten Ergebnisses. Die Anzeige ist schreibgeschützt; aktuelle
Fachlabels werden nicht als historische Präsentationsmetadaten verwendet. Die
vollständige bestätigte Prüfspur bleibt im JSON verfügbar.

Der Plattformstand muss zur tatsächlich gestarteten API-Version passen. Bei einer
älteren Datei ist der passende archivierte Programmstand erforderlich. Ein
abweichender Plattformstand oder ein inkonsistentes Ergebnis wird abgelehnt.
Eine bestandene Wiederholung bestätigt keine Herkunft, Berechtigung oder fachliche
Freigabe; siehe [Prüfsnapshots](ASSESSMENT_SNAPSHOTS.md).

Ungültiges UTF-8 wird bereits vor einer Übertragung abgelehnt. Gültige Dateien werden lokal an den Loopback-Dienst übertragen und im Arbeitsspeicher
geprüft. Sie wird nicht serverseitig oder im Browser-Speicher persistiert.
Dateiauswahl oder Änderungen der Prüfangaben entfernen vorherige Ergebnisse und
brechen veraltete Anfragen ab. Ausschließlich synthetische Daten verwenden.

## Synthetische Vorgänge bearbeiten

Wähle „Demo F – Vorgang und Prüfung“. Unter „Vorgang bearbeiten“ kannst du
synthetische Vorgangsangaben laden oder Fall-ID, Vorgangs-ID, Bearbeiter-ID,
UTC-Zeitpunkt und Begründung ausdrücklich eingeben. „Vorgang starten“ erzeugt
Revision 0 im Zustand „In Vorbereitung“.

„Zur Prüfung geben“, „Zur Vorbereitung zurückgeben“ und „Prüfung abschließen“
stammen aus dem Wissenspaket. Die Oberfläche zeigt nur Übergänge des aktuellen
Zustands. Jeder Schritt benötigt Bearbeiter, UTC-Zeitpunkt und Begründung; gleiche
Zeitpunkte sind erlaubt, rückwärts laufende Zeitpunkte werden abgelehnt. Der
Zeitpunkt wird nicht automatisch ergänzt. Nach Abschluss sind keine weiteren
Übergänge möglich. Eine Fallauswertung löst keinen Prozessübergang automatisch aus.

Die Historie enthält alle Revisionen mit deutschem Zustand/Übergang, Bearbeiter,
UTC-Zeitpunkt und Begründung. Der Download „Vorgang als JSON herunterladen“ speichert
den originalen JSON-Text des Servers mit exakten Zeitwerten und sämtlichen Graph- und
Quellenständen. Es wird nichts in localStorage oder einer Datenbank gespeichert.

„Vorgangsdatei laden und prüfen“ prüft eine lokale JSON-Datei und erlaubt danach die
Fortsetzung. Die Datei muss zum ausgewählten Prüfbereich und dem installierten
Wissens-/Plattformstand passen. Der Dienst prüft die komplette Übergangshistorie und
den exakten ursprünglichen Graph/Quellenstand gegen den synthetischen Katalog.
Ein später geändertes Wissen wird nicht still als historische Definition verwendet.
Zum Fortsetzen archivierter Vorgänge ist der passende archivierte Programm- und
Wissensstand erforderlich.

Dateien sind auf 1 MiB begrenzt; ungültiges UTF-8 wird vor Übertragung abgelehnt.
Der API-Adapter begrenzt exportierbare Vorgangs-JSON zusätzlich auf 256 KiB, damit die
spätere Übergangsanfrage mit eingebetteter Historie innerhalb der Request-Grenze
bleibt. Eine zu große Fortsetzung verändert den bisher geladenen Vorgang nicht.
Änderungen an künftigen Schrittdaten brechen laufende Anfragen ab; die bisher bestätigte
Historie bleibt erhalten. Ein Wechsel des Prüfbereichs verwirft den geladenen Vorgang.

Bearbeiter- und Fall-IDs sind hier synthetische Angaben, keine authentifizierten
Identitäten. Die Prüfung einer Datei bestätigt interne Konsistenz und passenden
Programm-/Wissensstand, keine Herkunft, Berechtigung oder fachliche Freigabe. Die
separat getestete PostgreSQL-Speicherung wird durch diese Demo-Oberfläche nicht geöffnet.

## Aufgezeichneten Prüfweg lesen

„Prüfweg nachvollziehen“ zeigt für die aktuelle Auswertung die vollständige
Bedingungsstruktur und jede unabhängige Fachausgabe mit ihrer ursprünglichen Quelle.
Bedingungsergebnisse bleiben vom abschließenden Prüfergebnis getrennt. Bei fehlenden
oder widersprüchlichen Nachweisen zeigt die Ansicht das aufgezeichnete unbekannte
Ergebnis, auch wenn die untergeordnete Bedingung erfüllt ist.

Feld-, Nachweis- und Ausgabebeschriftungen stammen aus den externen Metadaten des
aktiven Wissenspakets; Operatoren und Statusbeschriftungen aus de.json. Zahlenwerte,
Grenzen, Bereichszuordnungen und Berechnungsschritte werden aus dem ursprünglichen
Serverergebnis angezeigt. Dezimaltokens bleiben exakt und bekommen ausschließlich
ein deutsches Dezimalkomma. Die Oberfläche berechnet keine Bedingungen, Summen,
Maxima oder Entscheidungen neu. Unbekannt, Nein, nicht anwendbar und ein nicht
aufgezeichneter Wert bleiben unterschiedliche Zustände.

Die technische Prüfspur und beide Downloads bleiben unverändert. Importierte
historische Snapshots erhalten diese aktuelle Präsentation nicht: historische
Beschriftungen sind bislang nicht Teil des Snapshotvertrags. Ihre bestätigte
originale Prüfspur ist weiterhin als JSON verfügbar.

## Dokumentakten und lesbarer Einstieg

Die Arbeitslisten besitzen jetzt eigene PDF-/Scan-/Textakten mit Fundstellen,
Vorschau und Download. Zwölf zusätzliche Referenzakten zeigen öffentlich
recherchierte Inhaltsbereiche. [Dokumentakten](DOCUMENT_CASES.de.md) beschreibt
Bedienung, Testadapter und die ausdrücklichen fachlichen Grenzen.

## Gemeinsamer Arbeitsplatz für Browser und Desktop

Die Oberfläche verwendet einen kompakten Arbeitsplatz mit festen Arbeitsbereichen:
Arbeitslisten, Referenzfälle, Prüfwerkstatt, Review und Vorgänge. Auf Desktopgrößen
bleiben Navigation und Demo-Hinweis sichtbar. Die Fallübersicht wird beim Öffnen
eines Falls durch dessen Ergebnisansicht ersetzt. Ergebnisansicht und Dokumente
sind getrennte Fallansichten; lange Inhalte scrollen innerhalb des Arbeitsbereichs.
Auf schmalen Fenstern darf der Inhalt über die Seite scrollen. Größere Schriften und Browser-Zoom bleiben möglich.

Die Navigation verwendet ausschließlich feste URL-Fragmente (z. B. `#workbench`),
keine Falldaten. Eingaben bleiben während eines Bereichswechsels im Speicher; ein
Neuladen setzt sie zurück. Review-Anmeldung und Abmeldung behalten ihre bisherigen
Sicherheits- und Löschregeln. Snapshot-Werkzeuge liegen in der Prüfwerkstatt; ein
Prüfbereich für Vorgänge lässt sich direkt im Bereich „Vorgänge“ auswählen.

Dies ist die gemeinsame React-Präsentationsschicht für den Browser und einen späteren
Desktop-Host. Ein nativer Fenster-Host und institutionelle MD-Adapter sind damit noch
nicht implementiert. Die APIs und der deterministische Kern ändern sich nicht.

### Fallübersicht und Ergebnis zuerst

Der Start öffnet die fachlichen Referenzfälle. Ein Klick auf einen Fall ersetzt die
Übersicht durch die Fallansicht; „Zur Fallübersicht“ führt zurück und stellt den
Tastaturfokus auf den zuvor geöffneten Fall. Die Startansicht zeigt Ergebnis,
Klärungsbedarf, Prüfumfang und die dokumentierten Angaben mit Fundstellen.
„Dokumente“ öffnet den Beleg in einem eigenen breiten Bereich. Ein Klick auf eine
Fundstelle wechselt direkt zur Dokumentansicht und fordert die entsprechende Seite
an. Die PDF-Ansicht zeigt lokal gerenderte Seiten mit eigener Seitenwahl und Zoom.
Die Ganzseitenansicht bleibt im Fenster begrenzt; die Originaldatei ist separat verfügbar.
Die fiktiven Plattformtests bleiben getrennt unter „Arbeitslisten“ verfügbar.

Die Referenzübersicht zeigt konkrete fachliche Fragestellungen. Im geöffneten Fall
stehen Auftrag und Fragestellung unter dem Ergebnis; der erfundene Hintergrund ist
über „Fallhintergrund ansehen“ zugänglich. Alle Angaben bleiben als synthetisch
gekennzeichnet. Die Prüfung ist auf den jeweils angegebenen Umfang beschränkt.


## Persönliche Fallorganisation

Die Startansicht „Arbeitslisten“ ist ein medizinisch blauer Explorer mit Arbeitslistenbaum, Falltabelle und separater Fallansicht. Fälle lassen sich per Doppelklick, Eingabetaste oder Rechtsklick öffnen. Das Kontextmenü ist auch mit Umschalt+F10 erreichbar. Der Dokumenteintrag im Baum öffnet direkt den begrenzten Dokumentleser.

Eigene Ordner können angelegt, umbenannt, eingefärbt und gelöscht werden. Ausgewählte Fälle können in diese Ordner verschoben werden; dies verändert ausschließlich die Zuordnung, niemals Quelldateien oder Prüfergebnisse. Beim Löschen eines Ordners bleiben alle Fälle erhalten. Arbeitslisten und Fälle sind unabhängig einfärbbar. Fallfarben lassen sich zurücksetzen. Dunkle Schrift auf heller Oberfläche bleibt unabhängig von der gewählten Farbe lesbar; Farbe ist eine zusätzliche Randmarkierung. Lesezeichen und Fallnotizen erleichtern die persönliche Wiedervorlage.

Die Organisation liegt im Speicher des lokalen Demo-Servers: Browser-Neuladen erhält sie, ein Neustart setzt sie zurück. Maximal 50 Ordner, 100 Fälle je Aktion, 1000 Zeichen je Notiz und 500 Änderungen je Serverlauf. Revisionen verhindern versehentliches Überschreiben; wiederholte identische Operationskennungen liefern denselben Erfolg. Der Demo-Pfad ist bei aktiviertem persistentem Review-Host deaktiviert.

„Bestätigen (Demo)“ und „Abschicken (Demo)“ sind ausdrücklich lokale Probeläufe. Nur vollständige, dafür geeignete Fälle können gemeinsam bestätigt werden; eine ungeeignete Auswahl wird vollständig abgelehnt. Versand erzeugt ausschließlich eine herunterladbare JSON-Datei mit unveränderten Assessment-Zeichenfolgen und `SYNTHETIC_LOCAL_ONLY`. Es gibt weder eine MD-Übertragung noch eine rechtlich verbindliche Freigabe.

Persönliche Ordner besitzen einen aufklappbaren Fallbaum mit direkten Unteransichten für Ergebnis und Dokumente. Der eigene Filterknopf neben dem Baum behält die tabellarische Ordneransicht bei.
