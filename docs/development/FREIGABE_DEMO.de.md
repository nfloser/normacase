# Lokale Freigabe-Demo

Diese ausdrücklich aktivierte Demo verbindet einen authentifizierten synthetischen
Prüfer mit PostgreSQL-Fallstatus, unveränderlicher Bewertung und menschlicher Historie.
Der normale Vorschau-Start bleibt schreibgeschützt und benötigt keine Datenbank.

## Windows-Vorschau

Voraussetzung: Docker Desktop läuft. Archiv vollständig entpacken und im entpackten
Ordner ausführen:

```powershell
powershell -ExecutionPolicy Bypass -File .\Freigabe-Demo-starten.ps1
```

Das Skript erzeugt beim ersten Start zufällige lokale Zugangsdaten und hält sie in der
Windows-Benutzerumgebung außerhalb des Projekts. Die Zugangsdaten bleiben beim nächsten
Start gleich, damit das dauerhafte Datenbankvolume weiter geöffnet werden kann.
Der lokale Demo-Zugangsschlüssel wird im eigenen Terminal angezeigt. Öffne
`http://localhost:5080`, gehe zu **Fallwarteschlangen** und gib ihn im maskierten Feld
**Lokaler Demo-Zugangsschlüssel** ein. Die Oberfläche speichert ihn nur für die laufende
Sitzung im Arbeitsspeicher. Er erscheint nicht in URL, Cookies oder Browser-Speicher.

Im Repository zuerst Frontend installieren/bauen (`npm ci` und `npm run build` im
Ordner `frontend`), dann `scripts/start_review_demo.ps1` starten. Dafür ist .NET 10 nötig;
die native Vorschau enthält ihre .NET-Laufzeit bereits.

## Linux oder manuelle Konfiguration

Erzeuge zwei verschiedene 256-Bit-Hexwerte mit einem lokalen kryptografischen Generator.
Setze `NORMACASE_REVIEW_DB_PASSWORD` und `NORMACASE_REVIEW_DEMO_KEY` in deiner lokalen
Umgebung außerhalb von Git und behalte das Datenbankkennwort für Folgestarts bei.

```bash
docker compose -f compose.review-demo.yml up -d --wait
export NORMACASE_REVIEW_DEMO=1
export NORMACASE_REVIEW_DEMO_CONNECTION="Host=127.0.0.1;Port=54329;Database=normacase_review_demo;Username=normacase_demo;Password=$NORMACASE_REVIEW_DB_PASSWORD"
./api/NormaCase.Api
```

Im Repository lautet der letzte Befehl `dotnet run --project src/NormaCase.Api -c Release`.
Nur der explizite Freigabe-Modus verwendet PostgreSQL. Beide Dienste binden lokal.

## Vorführung

1. **demo-g-supported** öffnen: ursprüngliche positive Bewertung und Prüfweg ansehen,
   Begründung eingeben und **Systemergebnis annehmen**. Der Fall erscheint anschließend
   unter **Abgeschlossene Prüfungen** mit serverseitig aufgezeichneter Prüferidentität.
2. **demo-g-not-supported** zeigt, dass auch vollständige negative Ergebnisse einer
   menschlichen Prüfung bedürfen.
3. **demo-g-review** öffnen: eigene Begründung und menschlich festgelegtes Ergebnis
   auswählen, dann **Ergebnis übersteuern**. Originalbewertung und Entscheidungsweg
   bleiben erhalten; die Abweichung steht getrennt in der Historie.
4. **demo-g-incomplete** und **demo-technical** bieten keine Freigabeaktion. Ein
   technischer Fehler enthält weiterhin keine erfundene Bewertung.
5. Browser oder API neu starten und erneut anmelden: die abgeschlossenen Prüfungen
   und Historien bleiben bestehen. Jeder Versuch mit altem Fall-/Auditstand scheitert.

Die Demo enthält einen festen synthetischen Prüfer, keine institutionellen Benutzer
oder Rollen. Sie ersetzt keine fachliche Freigabe, Datenschutzprüfung oder produktive
Identitätsintegration. Korrektur von Eingangsdaten ist ein eigener späterer Fallstand,
keine Änderung der ursprünglichen Bewertung. Die technische Queue bleibt eine
prozessbezogene synthetische Ausnahme; sie wird nicht als Assessment gespeichert.

Die Datenbank bleibt nach `docker compose ... stop` erhalten. Ein Löschen des Volumes
entfernt ausdrücklich auch die aufgezeichneten Demo-Prüfungen; dafür gibt es keinen
stillen automatischen Reset. Zugangsschlüssel kann durch Austausch der lokalen
Benutzerumgebung und Neustart rotiert werden. Das Datenbankkennwort darf bei bestehendem
Volume nicht ohne eine ausdrückliche PostgreSQL-Kennwortänderung ausgetauscht werden.
