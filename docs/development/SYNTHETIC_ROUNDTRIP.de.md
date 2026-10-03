# Synthetischer Eingang bis zur Rückgabe

Nur lokale synthetische Daten verwenden. Es sind keine MD-Schnittstellen, keine
produktive Identität und keine fachlich genehmigten Regeln enthalten. Die bestehende
Anleitung `SYNTHETIC_REVIEW.de.md` beschreibt PostgreSQL und das externe Bearer-Geheimnis.

Der authentifizierte lokale Host bietet zusätzlich:

| Aufruf | Bedeutung |
|---|---|
| `POST /api/review/intake/json` | Synthetischer JSON-Eingang, `application/json` |
| `POST /api/review/intake/xml` | Synthetischer XML-Eingang, `application/xml` |
| `GET /api/review/work-queues` | Vorbereitete und neu angenommene Fälle aus gespeichertem Prozesszustand |
| `POST /api/review/work-cases/{caseId}/reviews` | Bestehende Freigabe mit Grund und exakten Revisionen |
| `POST /api/review/work-cases/{caseId}/outbound` | Geprüftes Ergebnis an `synthetic-inbox` oder `synthetic-file` |

JSON-Eingang (Schema 1) enthält `formatVersion`, `order`, `message`, die kanonische
Revision als Zeichenkette `revision` und `input` im bestehenden CaseInput-Format.
XML verwendet `<SyntheticCase formatVersion="1" order="..." message="..."
revision="1" date="2026-10-03">`, `<Fact id="..." kind="TRUTH" value="YES"/>`
und `<Evidence id="..." value="PRESENT"/>`. Wahrheit ist YES/NO/UNKNOWN, Zahlen
verwenden `kind="NUMBER"` und eine exakt darstellbare Dezimalzahl. DTDs und externe
Entitäten sind verboten. Beide Formate sind unabhängig erfundene synthetische
Testprotokolle. Sie wählen im Host ausdrücklich das synthetische demo-g-Paket.
Eine PRESENT-Evidenz bezeichnet einen synthetischen Testbeleg, kein echtes Dokument.

Der Host leitet stabile interne Fall-IDs aus Formatquelle und Auftrags-ID ab. Nur
Revision 1 wird angenommen; fachliche Korrektur und Nachforderung benötigen eine
explizite Prozessregel und werden nicht geraten. Fehlende Angaben bleiben UNKNOWN
und werden in die Klärung geleitet. Vorbereitete Demofälle besitzen keinen externen
Eingangsbeleg und können über diesen neuen Aufruf nicht exportiert werden.

Fallannahme speichert zuerst den unveränderlichen normalisierten Eingang. Bewertung
und initialer Prozesszustand werden anschließend gemeinsam in einer Transaktion
angelegt. Das ist bewusst keine einzelne Transaktion über beide Schritte: nach einem
Abbruch zwischen ihnen wiederholt der Sender dieselbe Nachricht. Der ursprüngliche
Eingang bleibt erhalten, und die Initialisierung wird fortgesetzt. Bereits angelegte
Bewertungen werden beim Wiederholen/Neustart nicht neu bewertet. Fehlt die exakt
gebundene Knowledge-Version bei noch ausstehender Initialisierung, schlägt der Host
sicher fehl. Parallele Initialisierungen erzeugen keine doppelten Bewertungen.

Rückgabe-JSON enthält `messageId`, `correlationId`, `destinationId`,
`expectedCaseRevision`, `expectedProcessRevision` und `expectedAuditRevision`.
Revisionen sind kanonische Zeichenketten. Nur ein abgeschlossener menschlicher
Review mit passenden Revisionen kann exportiert werden. Die Nachricht enthält
ursprüngliche externe Identität, Originalbewertung und menschliches Ergebnis.
Nachrichten-ID plus Ziel bestimmen den Zustellschlüssel. Identische Wiederholung
liefert den gespeicherten Beleg; geänderter Inhalt unter demselben Schlüssel ergibt
409. Transportfehler ändern keine medizinischen Ergebnisse oder Prozesszustände.

`synthetic-inbox` speichert Nachricht und Zustellbeleg atomar in PostgreSQL.
Dateiversand benötigt ein ausdrücklich gesetztes
`SyntheticReview__OutboundDirectory` mit geschütztem lokalem Verzeichnis.
`synthetic-file` schreibt eine temporäre Datei und veröffentlicht sie erst nach
vollständigem Flush durch Umbenennung ohne Überschreiben. Ein Absturz nach
Veröffentlichung, aber vor dem Datenbankbeleg ist wiederholbar: dieselbe Datei wird
verglichen und der Beleg nachgetragen. Betriebssystem-/Hardware-Garantien für
Stromausfall sowie Verzeichnisrechte müssen im Zielbetrieb geprüft werden.

Eingänge und Rückgabeanforderungen sind auf 64 KiB begrenzt. Die synthetische
Arbeitsliste begrenzt sich auf 500 gespeicherte Fälle und schlägt bei Überschreitung
fehl, statt Fälle still auszublenden. Produktive Pagination, Rollen-/Mandantengrenzen
und Korrekturpolitik sind eigene Anforderungen. Geheimnisse niemals in URLs,
Screenshots, Shell-Historie oder Repository ablegen.

## Wiederherstellungsprobe

`scripts/smoke_synthetic_roundtrip.py create <manifest.json>` startet den gebauten
lokalen Host gegen `NORMACASE_POSTGRES_TEST_CONNECTION` (ausschließlich eine eigene
synthetische Datenbank). Es erzeugt vier frische Fälle für Freigabe, Abweichung,
Unvollständigkeit und manuelle Prüfung. Zwei freigegebene Ergebnisse gehen an beide
Ziele. Das Manifest enthält nur synthetische Erwartungsdaten; Geheimnisse werden
nicht gespeichert. Die Prüfung beendet den Host wieder.

`verify <manifest.json>` startet ihn erneut und vergleicht dieselben Eingänge,
Bewertungen, Review-Historien und Zustellbelege exakt. CI führt anschließend
`pg_dump` mit dem PostgreSQL-18-Client im selben Container aus, stellt den Dump in
einer zweiten frisch angelegten Datenbank wieder her und führt `verify` dort erneut
aus. Das Dateiverzeichnis wird dabei ebenfalls geprüft. Migrationen sind beim
Neustart checksum-geprüft und werden nicht erneut angewendet.

Im Zielbetrieb gehören Datenbank, freigegebene Knowledge-Versionen, Plattform-Build,
Dateiausgang und externe Konfiguration zusammen zur Wiederherstellung. Geheimnisse
separat geschützt bereitstellen. Die CI-Probe ersetzt keine institutionell genehmigte
Backup-Verschlüsselung, Aufbewahrung, RPO/RTO-Messung oder Notfallübung. Änderungen
werden nur vorwärts migriert; ein Downgrade auf einen älteren Build wird nicht
behauptet. Vor Updates ein überprüftes Backup anlegen und Wiederherstellung mit dem
freigegebenen Build in einer separaten Umgebung proben.
