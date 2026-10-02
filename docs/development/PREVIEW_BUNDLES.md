# Self-contained synthetische Preview-Bundles

Die Preview-Bundles sind dafür gedacht, den aktuellen synthetischen NormaCase-Stand
auf Windows x64 oder Linux x64 ohne installiertes Node.js und ohne installierte
.NET-Runtime auszuprobieren.

Sie sind **keine produktive Distribution** und dürfen ausschließlich mit
synthetischen Daten verwendet werden.

## Inhalt

Jedes getestete Archiv enthält:

- den self-contained ASP.NET-Core-Dienst unter `api/`,
- die lokal gebündelte deutsche React-Arbeitsoberfläche,
- das self-contained Offline-CLI unter `cli/`,
- alle fünf synthetischen Knowledge Packs,
- die synthetischen Beispielfälle,
- einen deutschen Starter,
- `build-info.json` mit Quellcommit, Plattformstand und Runtime-Identifier,
- `SHA256SUMS.txt` für alle Dateien im Bundle.

Zusätzlich liegt neben dem Archiv eine SHA-256-Datei für das vollständige Archiv.

## Getestete Zielsysteme

CI baut nativ:

- `linux-x64` auf einem Ubuntu-Runner,
- `win-x64` auf einem Windows-Runner.

Der Plattformstand wird beim Publish explizit auf

`preview-<vollständiger Git-Commit-SHA>`

gesetzt. Die API übernimmt exakt diesen Wert als Assembly-InformationalVersion.
Snapshots aus der Preview können daher nur mit demselben Plattformstand erfolgreich
wiederholt werden.

## Start

Windows:

`start-normacase.cmd`

Linux:

`./start-normacase.sh`

Der Dienst bindet weiterhin ausschließlich an Loopback-Port 5080. Danach ist die
Arbeitsoberfläche unter `http://localhost:5080/` erreichbar.

Beenden mit `Strg+C`.

Eine PostgreSQL-Instanz ist für diese Preview nicht erforderlich. Die vorhandenen
PostgreSQL-Adapter werden nicht automatisch aktiviert oder über die lokale
Preview-API exponiert.

## CI-Verifikation

Das erzeugte Archiv wird vor dem Upload erneut in ein frisches temporäres Verzeichnis
entpackt. Erst dieser entpackte Stand wird getestet.

Der Smoke-Test:

1. verifiziert die SHA-256-Prüfsumme des Archivs,
2. verifiziert `SHA256SUMS.txt` für sämtliche Bundle-Dateien,
3. prüft fünf synthetische Knowledge Packs und die Beispielfälle,
4. startet den veröffentlichten API-Apphost direkt,
5. lädt die gebündelte deutsche Arbeitsoberfläche,
6. führt eine reale Demo-B-Auswertung mit hochpräzisem Dezimalwert aus,
7. erstellt und wiederholt einen Snapshot über die reale HTTP-API,
8. erstellt und wiederholt denselben Präzisionsfall über das veröffentlichte CLI.

Der Test setzt `DOTNET_ROOT` absichtlich auf einen nicht vorhandenen Pfad und ruft
die veröffentlichten nativen Apphosts direkt auf. Dadurch hängt der getestete Start
nicht vom installierten .NET-SDK des CI-Runners ab.

Build/Restore und die Playwright-/npm-Installation dürfen Netzwerk benötigen. Das
fertige Bundle selbst benötigt für die Kernfunktion keine Runtime-Internetverbindung.

## Artefakte

Erfolgreiche Pull-Request-/main-CI-Läufe laden zwei kurzzeitig aufbewahrte Artefakte
hoch:

- `normacase-preview-linux-x64`
- `normacase-preview-win-x64`

Sie enthalten jeweils das getestete Archiv und dessen `.sha256`-Sidecar.

Eine Veröffentlichung als GitHub Release ist **nicht** Teil dieses Foundation-Slices
und benötigt eine bewusste Release-/Versionierungsentscheidung.

## Integritätsgrenze

SHA-256 erkennt Übertragungs- oder Dateiveränderungen, ist aber keine digitale
Signatur. Das Preview-Bundle besitzt dadurch weder einen Herkunftsnachweis noch eine
fachliche oder regulatorische Freigabe.
