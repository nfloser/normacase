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

Die Oberfläche zeigt Wissens-/Plattformstand und Quellenrevision. „Technische
Prüfspur“ zeigt das originale Server-JSON. Der JSON-Download exportiert genau diesen
Text. Dezimalwerte werden verlustfrei transportiert; JavaScript Number wird nicht
für Fallwerte verwendet. Die Eingabe erlaubt Komma oder Punkt, ohne
Tausendertrennzeichen. Änderungen an Eingaben brechen laufende Requests ab und
entfernen veraltete Ergebnisse.

Generische Texte liegen in frontend/src/de.json, fach-/beispielspezifische
Beschriftungen in knowledge/demo-*/presentation.de-DE.json. Die Präsentationsdaten
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

Die Datei wird lokal an den Loopback-Dienst übertragen und im Arbeitsspeicher
geprüft. Sie wird nicht serverseitig oder im Browser-Speicher persistiert.
Dateiauswahl oder Änderungen der Prüfangaben entfernen vorherige Ergebnisse und
brechen veraltete Anfragen ab. Ausschließlich synthetische Daten verwenden.
