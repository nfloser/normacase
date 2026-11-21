# Prüfsnapshots offline wiederholen

Ein Prüfsnapshot enthält das vollständige ursprüngliche Knowledge Pack als JSON-Zeichenfolge, strukturierte Eingaben einschließlich Prüfdatum und Nachweiszuständen, den expliziten Plattformstand und das ursprüngliche Ergebnis mit Prüfspur und Quellenrevisionen. Er benötigt für die Wiederholung keine separat aufbewahrte Falldatei und keinen aktiven Wissenskatalog.

## Erfassen und wiederholen

Mit dem .NET-10-SDK im Repository ausführen:

```bash
dotnet run --project src/NormaCase.Cli -- snapshot --pack knowledge/demo-e/pack.json --case examples/cases/demo-e-partial.json --platform-version development > snapshot.json
dotnet run --project src/NormaCase.Cli -- replay --snapshot snapshot.json --platform-version development
dotnet run --project src/NormaCase.Cli -- replay --snapshot snapshot.json --platform-version development --json
```

Für unvermischt maschinenlesbare Ausgabe das Projekt zuerst bauen und direkt starten:

```bash
dotnet build src/NormaCase.Cli --configuration Release
dotnet src/NormaCase.Cli/bin/Release/net10.0/NormaCase.Cli.dll snapshot --pack knowledge/demo-e/pack.json --case examples/cases/demo-e-partial.json --platform-version development > snapshot.json
dotnet src/NormaCase.Cli/bin/Release/net10.0/NormaCase.Cli.dll replay --snapshot snapshot.json --platform-version development --json
```

Der CLI-Adapter akzeptiert ausschließlich synthetische Knowledge Packs. `snapshot` schreibt immer JSON auf die Standardausgabe; Dateiumleitung und Zugriffsschutz verantwortet der Betreiber. `replay` zeigt eine deutsche Bestätigung mit Ergebnis oder mit `--json` ausschließlich das bestätigte Ergebnisdokument. `evaluate` bleibt verfügbar.

## Was die Wiederholung prüft

Das Snapshotformat hat Version 1 und enthält ein Ergebnisdokument in Version 2. `knowledgePackSha256` wird direkt aus den exakten UTF-8-Bytes der eingebetteten ursprünglichen Pack-Zeichenfolge berechnet. `contentSha256` umfasst zusätzlich die gesamte kanonisch serialisierte Snapshot-Nutzlast mit Formatversion, Pack-Fingerprint, allen Eingaben einschließlich unbenutzter Fakten und dem Ergebnis. Äußere JSON-Einrückung darf sich ändern; die eingebettete ursprüngliche Pack-Zeichenfolge bleibt unverändert.

Nach strikter Struktur- und Prüfsummenprüfung muss der explizit angegebene Plattformstand exakt übereinstimmen. Die Anwendung validiert das eingebettete Pack, prüft dessen Release gegen das gespeicherte Ergebnis und berechnet die ursprünglichen Eingaben mit dem ursprünglichen Prüfdatum erneut. Der vollständige Ergebnisvergleich umfasst auch fehlende Angaben, Nachweise, Regeln, Quellen und unabhängige fachliche Ausgaben. Es gibt keinen Netzabruf und keine implizite Systemzeit.

Der Plattformstand ist eine vom vertrauenswürdigen Aufrufer bereitgestellte Identität. Ein übereinstimmendes Etikett beweist nicht, dass tatsächlich die archivierte Binärdatei gestartet wurde. Für langfristige Reproduzierbarkeit müssen Betreiber zusätzlich passende Programmstände und deren vertrauenswürdige Herkunft archivieren.

| Rückgabecode | Bedeutung |
| --- | --- |
| 0 | Erfassung oder bestätigte Wiederholung erfolgreich |
| 2 | Ungültiger Aufruf, ungültige Struktur, Prüfsumme oder synthetisches Wissen |
| 3 | Datei nicht lesbar |
| 4 | Plattformstand, Wissensrelease oder neu berechnetes Ergebnis stimmt nicht überein |

Unvollständige fachliche Ergebnisse sind erfolgreiche technische Prüfungen und bleiben unverändert unvollständig. Fehlermeldungen geben keine Falldaten, Dateipfade oder Exceptiondetails aus. Eingaben und Snapshots sind auf 8 Mi Zeichen begrenzt, verschachteltes JSON auf 64 Ebenen. Das vollständig eingebettete Pack kann deshalb die maximal erfassbare Fallgröße reduzieren.

## Vertrauens- und Speichergrenze

Ein Angreifer kann nach einer Änderung einen neuen Prüfwert berechnen. Die erneute Berechnung erkennt ein zum Pack und Fall inkonsistentes Ergebnis; sie beweist weder Herkunft noch fachliche Freigabe. Auch ein vollständig neu erzeugter, konsistenter Snapshot bleibt technisch zulässig. Eine Prüfsumme ist keine Signatur, keine Benutzerberechtigung und kein manipulationssicherer Audit-Speicher.

Der aktuelle Dienst stellt weder Datenbank noch Zugriffsschutz, Aufbewahrung, Akteursidentität oder atomare Speicherung bereit. Das JSON ist ein transportierbarer Snapshot; seine Unveränderlichkeit muss später die Speichergrenze gewährleisten. Bei produktiven Daten enthält es sensible Eingaben und Prüfspuren. Nicht protokollieren oder in Git aufnehmen. Tests und Beispiele verwenden ausschließlich synthetische Daten.
