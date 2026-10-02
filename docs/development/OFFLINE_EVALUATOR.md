# Offline-Evaluator

Der Offline-Evaluator führt lokale synthetische Knowledge Packs gegen strukturierte
JSON-Fälle aus. Er benötigt nach dem Build keinen Netzwerkdienst.

Voraussetzung für die folgenden Entwicklungsaufrufe: .NET 10 SDK. Ein Restore
kann beim ersten Build Paketquellen benötigen; die Auswertung selbst ist offline.

Aus dem Repository-Verzeichnis:

Ein Knowledge Pack kann unabhängig von einem Fall strukturell geprüft werden:

```bash
dotnet run --project src/NormaCase.Cli -- validate --pack knowledge/demo-a/pack.json
```

`validate` benötigt weder Fall, Prüfdatum noch Plattformbezeichner. Es verwendet
denselben strikten, größenbegrenzten Knowledge-Import wie die Auswertung und prüft
unter anderem Format, Referenzen, Quellenmetadaten, Regeln, Outputs und deklarative
Workflows. Die Prüfung darf auch Packs mit `PUBLIC_REFERENCE`,
`DOMAIN_REVIEWED` oder `PRODUCTION_APPROVED` strukturell einlesen; daraus folgt
**keine** fachliche Freigabe oder Bestätigung der Quellen. Diese Packs dürfen über
`evaluate`, `snapshot` und `replay` im aktuellen Demonstrations-CLI weiterhin
nicht ausgeführt werden.

Bei semantischen Fehlern gibt `validate` stabile technische Fehlercodes aus,
nicht die importierten Validator-Meldungen, Dateipfade oder Knowledge-Inhalte.

Für synthetische Auswertungen:

```bash
dotnet run --project src/NormaCase.Cli -- --help
dotnet run --project src/NormaCase.Cli -- evaluate --pack knowledge/demo-a/pack.json --case examples/cases/demo-a-supported.json --platform-version development
dotnet run --project src/NormaCase.Cli -- evaluate --pack knowledge/demo-a/pack.json --case examples/cases/demo-a-incomplete.json --platform-version development
dotnet run --project src/NormaCase.Cli -- evaluate --pack knowledge/demo-b/pack.json --case examples/cases/demo-b-supported.json --platform-version development
dotnet run --project src/NormaCase.Cli -- evaluate --pack knowledge/demo-c/pack.json --case examples/cases/demo-c-supported.json --platform-version development
dotnet run --project src/NormaCase.Cli -- evaluate --pack knowledge/demo-c/pack.json --case examples/cases/demo-c-review.json --platform-version development
dotnet run --project src/NormaCase.Cli -- evaluate --pack knowledge/demo-e/pack.json --case examples/cases/demo-e-mixed.json --platform-version development
```

Die Beispiele zeigen erfüllte Voraussetzungen, unvollständige Angaben, manuelle Prüfung bei fehlender Evidenz und mehrere unabhängige fachliche Ausgaben. Demo E enthält absichtlich einen bekannten und einen UNKNOWN-Ausgabepfad; die deutsche Zusammenfassung zeigt UNKNOWN als „Unbekannt“. Deutsche Texte liegen in RESX-Ressourcen.
Technische IDs und das JSON-Format bleiben sprachunabhängige Verträge.

Mit `--json` wird ein versioniertes Ergebnis einschließlich Plattformstand,
Wissensstand, Quellenrevision, Decision Trace und vorhandenen Domain-Output-Traces ausgegeben. Assessment JSON wird aktuell als Format v2 geschrieben; legacy v1 ohne Domain Outputs bleibt lesbar. Der angegebene
Plattformstand ist eine explizite Betreiberangabe, keine kryptografische
Build-Verifikation. Verwende für nachvollziehbare Läufe einen tatsächlichen
Release- oder Commit-Bezeichner.

Das Fallformat enthält `formatVersion: 1`, ein explizites `assessmentDate`,
`facts` und optional `evidence`. Feldwerte verwenden das strenge Format aus
[ASSESSMENT_JSON.md](../architecture/ASSESSMENT_JSON.md). Evidenzstatus:
`PRESENT`, `MISSING`, `CONFLICTING`. Fehlt eine Evidenzreferenz, bleibt sie fehlend.
Datumswerte im technischen JSON stehen in ISO-Form.

Rückgabecodes:
- 0: Die Strukturprüfung bzw. Auswertung wurde ausgeführt; auch INCOMPLETE/HUMAN_REVIEW sind gültige Ergebnisse.
- 2: Aufruf, Fall oder Knowledge Pack ungültig.
- 3: Datei nicht lesbar.

`evaluate`, `snapshot` und `replay` akzeptieren ausschließlich SYNTHETIC-Packs.
`validate` darf dagegen deklarierte Validation Levels strukturell prüfen, ohne
dadurch eine Ausführung oder fachliche Freigabe zu erlauben. Das CLI ist ein
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

Der Knowledge-Pack-Import weist unbekannte oder doppelte JSON-Eigenschaften,
numerische Enum-Werte sowie Null-Einträge in Arrays und erforderlichen Objekten
ab. Doppelte Namen sind auch bei abweichender Groß-/Kleinschreibung ungültig.
Optionale skalare Quellenmetadaten dürfen weiterhin null sein. JSON-Importfehler
werden als Eingabefehler gemeldet, ohne Parser-/Validator-Inhalte zu protokollieren.
