# NormaCase lokal ausprobieren

Dieses Testpaket enthält die deutsche Prüfwerkstatt, den Offline-Evaluator und
sieben synthetische Wissenspakete. Es benötigt weder Node.js, Python, Docker noch
eine installierte .NET-Laufzeit. Es verbindet sich nicht mit PostgreSQL.
Die aktuelle Funktionalität ist eine synthetische Demonstration ohne fachliche
Freigabe, Patientenverwaltung oder produktive Berechtigungsverwaltung.
Verwende ausschließlich synthetische Daten.

## Windows x64

Entpacke das gesamte ZIP in einen eigenen Ordner. Starte
`Pruefwerkstatt-starten.cmd` per Doppelklick. Lasse das Konsolenfenster geöffnet
und öffne anschließend <http://localhost:5080> im Browser.
Mit Strg+C beendest du den lokalen Dienst.

## Linux x64

Das Linux-Paket benötigt ein unterstütztes glibc-System mit den üblichen
nativen .NET-Systembibliotheken, etwa Ubuntu 24.04. Alpine/musl und ARM sind
keine unterstützten Zielplattformen dieser Pakete.
Die nativen Abhängigkeiten sind in der
[offiziellen .NET-Anleitung für Ubuntu](https://learn.microsoft.com/en-us/dotnet/core/install/linux-ubuntu-install)
aufgeführt; die mitgelieferte .NET-Laufzeit ersetzt diese Systembibliotheken nicht.

Entpacke das gesamte ZIP und starte aus dem Paketordner:

```sh
chmod +x pruefwerkstatt-starten.sh api/NormaCase.Api cli/NormaCase.Cli
./pruefwerkstatt-starten.sh
```

Öffne anschließend <http://localhost:5080>. Beenden: Strg+C.

## Erste Prüfung

Für eine reproduzierbare Präsentation liegt im Paket zusätzlich
`PITCH-DEMO.de.md`. Sie beschreibt die feste Reihenfolge der Pitch-Beispiele
und die erwarteten Ergebnisse.

Direkt unter dem Einstieg zeigt die Produktvorführung „Fallwarteschlangen“. Dort werden bei
jedem Start exakt 100 synthetische Fälle automatisch bewertet beziehungsweise als
technische Ausnahme eingeordnet und auf vier Arbeitslisten verteilt. Die Oberfläche
zeigt die vollständigen Zähler und je Liste nur fünf repräsentative Fälle.

Wähle danach einen Prüfbereich und ein Beispiel. Klicke auf „Beispiel laden“ und
danach auf „Jetzt prüfen“. Ändere anschließend Angaben oder Evidenzzustände, um
unvollständige Fälle und manuelle Prüfung auszuprobieren. Fehlendes bleibt
unbekannt. Die vollständige technische Prüfspur und ein selbstständiger
Prüfsnapshot können heruntergeladen werden. Über „Gespeicherte Prüfung offline
überprüfen“ lässt sich derselbe Snapshot wiederholen.

Der Dienst ist ausschließlich über Loopback auf Port 5080 erreichbar. Ist der
Port bereits belegt, beende zuerst die andere Instanz. Stelle keine Verbindung
über einen Reverse Proxy, eine Portfreigabe oder eine öffentliche Adresse her.
Bei Windows-Schutzmeldungen prüfe zuerst Herkunft und Prüfsumme des Pakets;
deaktiviere keine Schutzfunktionen pauschal. Die Programme sind nicht signiert.

## Kommandozeile

Der Offline-Evaluator liegt unter `cli/NormaCase.Cli.exe` (Windows) bzw.
`cli/NormaCase.Cli` (Linux). `--help` zeigt die deutschen Aufrufhinweise.
Beispiel aus dem Paketordner in PowerShell:

```powershell
$stand = (Get-Content -Raw preview.json | ConvertFrom-Json).platformVersion
.\cli\NormaCase.Cli.exe evaluate --pack knowledge/demo-c/pack.json --case examples/cases/demo-c-supported.json --platform-version $stand
```

Linux-Beispiel; setze den exakten `platformVersion`-Wert aus `preview.json` ein:

```sh
./cli/NormaCase.Cli evaluate --pack knowledge/demo-c/pack.json --case examples/cases/demo-c-supported.json --platform-version 'PLATTFORMSTAND-AUS-preview.json'
```

Archiviere für historische Wiederholungen das komplette ursprüngliche Testpaket
zusammen mit dem Snapshot. Ein späterer Programmstand kann alte Snapshots wegen
eines abweichenden Plattformstands ablehnen.

## Herkunft und Grenzen

`preview.json` nennt den vollständigen Git-Commit, den Plattformstand, die
Zielplattform und SHA-256-Prüfwerte sämtlicher Paketdateien. Die zusätzliche
`.zip.sha256`-Datei enthält den Prüfwert des Archivs. Prüfe beispielsweise in
PowerShell mit `Get-FileHash DATEI.zip -Algorithm SHA256` oder unter Linux mit
`sha256sum -c DATEI.zip.sha256`.

Prüfwerte erkennen versehentliche Veränderungen. Sie belegen weder die Identität
des Herausgebers noch fachliche Freigabe oder Manipulationssicherheit. Beziehe
Pakete nur aus dem vertrauenswürdigen GitHub-Projekt und prüfe den zugehörigen
CI-Lauf. Vorschauen sind keine freigegebenen Produkt-Releases.

Die Anwendung lädt keine CDN-Dateien, externen Schriften oder Cloud-Dienste zur
Laufzeit. Bei der Einrichtung des Pakets in CI werden Abhängigkeiten heruntergeladen.
Falldaten bleiben im Arbeitsspeicher, bis du sie ausdrücklich als Datei exportierst.
Exportierte Dateien liegen außerhalb dieser Speichergrenze und müssen entsprechend
geschützt werden. Keine realen Patientendaten verwenden.
