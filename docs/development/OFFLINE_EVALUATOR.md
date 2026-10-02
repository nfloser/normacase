# Offline-Evaluator

Der Offline-Evaluator führt lokale synthetische Knowledge Packs gegen strukturierte
JSON-Fälle aus. Er benötigt nach dem Build keinen Netzwerkdienst.

Voraussetzung für die folgenden Entwicklungsaufrufe: .NET 10 SDK. Ein Restore
kann beim ersten Build Paketquellen benötigen; die Auswertung selbst ist offline.

Aus dem Repository-Verzeichnis:

```bash
dotnet run --project src/NormaCase.Cli -- --help
dotnet run --project src/NormaCase.Cli -- evaluate --pack knowledge/demo-a/pack.json --case examples/cases/demo-a-supported.json --platform-version development
dotnet run --project src/NormaCase.Cli -- evaluate --pack knowledge/demo-a/pack.json --case examples/cases/demo-a-incomplete.json --platform-version development
dotnet run --project src/NormaCase.Cli -- evaluate --pack knowledge/demo-b/pack.json --case examples/cases/demo-b-supported.json --platform-version development
dotnet run --project src/NormaCase.Cli -- evaluate --pack knowledge/demo-c/pack.json --case examples/cases/demo-c-supported.json --platform-version development
dotnet run --project src/NormaCase.Cli -- evaluate --pack knowledge/demo-c/pack.json --case examples/cases/demo-c-review.json --platform-version development
```

Die Beispiele zeigen erfüllte Voraussetzungen, unvollständige Angaben und
manuelle Prüfung bei fehlender Evidenz. Deutsche Texte liegen in RESX-Ressourcen.
Technische IDs und das JSON-Format bleiben sprachunabhängige Verträge.

Mit `--json` wird ein versioniertes Ergebnis einschließlich Plattformstand,
Wissensstand, Quellenrevision und Decision Trace ausgegeben. Der angegebene
Plattformstand ist eine explizite Betreiberangabe, keine kryptografische
Build-Verifikation. Verwende für nachvollziehbare Läufe einen tatsächlichen
Release- oder Commit-Bezeichner.

Das Fallformat enthält `formatVersion: 1`, ein explizites `assessmentDate`,
`facts` und optional `evidence`. Feldwerte verwenden das strenge Format aus
[ASSESSMENT_JSON.md](../architecture/ASSESSMENT_JSON.md). Evidenzstatus:
`PRESENT`, `MISSING`, `CONFLICTING`. Fehlt eine Evidenzreferenz, bleibt sie fehlend.
Datumswerte im technischen JSON stehen in ISO-Form.

Rückgabecodes:
- 0: Die Auswertung wurde ausgeführt; auch INCOMPLETE/HUMAN_REVIEW sind gültige Ergebnisse.
- 2: Aufruf, Fall oder Knowledge Pack ungültig.
- 3: Datei nicht lesbar.

Die Anwendung akzeptiert ausschließlich SYNTHETIC-Packs. Sie ist ein
Engineering-/Demonstrationswerkzeug; Weboberfläche, produktive Speicherung,
Authentifizierung und Freigabeprozesse stehen weiterhin aus. Verwende
ausschließlich synthetische Fälle. JSON-Ausgaben enthalten Eingabe-/Tracewerte;
behandle spätere produktive Exporte als sensible Daten und protokolliere sie nicht
unbeabsichtigt. Fehlermeldungen enthalten keine Dateipfade oder Fallinhalte.

Tests:
```bash
dotnet test tests/NormaCase.Cli.Tests --configuration Release
dotnet test tests/NormaCase.Serialization.Tests --configuration Release
```
