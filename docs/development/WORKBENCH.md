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
Beschriftungen in knowledge/presentation.de-DE.json. Die Präsentationsdaten
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
