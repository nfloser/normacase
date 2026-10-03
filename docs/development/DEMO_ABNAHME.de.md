# NormaCase ausprobieren und synthetisch abnehmen

Der synthetische Gesamtablauf ist implementiert und durch automatisierte Prüfungen
abgedeckt. Diese Anleitung führt zum vorführbaren Stand und beschreibt die manuelle
Abnahme auf deinem Rechner. Eine bestandene Demo ist keine produktive MD-Freigabe.

## 1. Anwendung ohne Entwicklungsumgebung starten

Öffne [Synthetic preview bundles](https://github.com/nfloser/normacase/actions/workflows/preview.yml).
Wähle einen erfolgreichen Lauf für den gewünschten Commit auf `main` und lade
`synthetic-preview-win-x64` oder `synthetic-preview-linux-x64` herunter. Prüfe auch
die zugehörige CI; ein erfolgreicher Paketbau allein ersetzt diese nicht.
Artefakte stehen 14 Tage bereit.

Entpacke den Download und anschließend das enthaltene Anwendungs-ZIP vollständig.
Prüfe die Archiv-Prüfsumme gemäß [Startanleitung](PREVIEW_START.de.md).
Unter Windows starte `Pruefwerkstatt-starten.cmd`, unter Linux folge der
Startanleitung. Öffne <http://localhost:5080> und lasse den Dienst geöffnet.
Notiere `sourceCommit` und `platformVersion` aus `preview.json` für deine Abnahme.

## 2. Pitch und deterministische Prüfung abnehmen

Folge [Pitch-Demo](PITCH_DEMO.de.md) im Prüfbereich
„Pitch-Demo – Synthetische Fallprüfung“.

| Test | Erwartung |
|---|---|
| Pflichtangabe fehlt | „Angaben unvollständig“; fehlende Pflichtangabe wird benannt |
| Nachweis fehlt | „Manuelle Prüfung erforderlich“; kein geratenes Ergebnis |
| Vollständiger Fall | „Voraussetzungen erfüllt“; Regel und fiktive Quelle sichtbar |
| Explizites negatives Kriterium | „Voraussetzungen nicht erfüllt“ |
| Snapshot herunterladen und wieder einlesen | Identisches Ergebnis mit ursprünglichem Wissens- und Plattformstand |

Prüfe deutsche Beschriftungen, Prüfdatum und Quellenanzeige. Für Screenshots und
Videos ausschließlich mitgelieferte synthetische Beispiele verwenden.
Die Vorschau benötigt zur Laufzeit keine Cloud oder externen KI-Dienste.

## 3. Gespeicherte Fallbearbeitung abnehmen

Richte den optionalen lokalen Modus nach
[Persistente synthetische Fallprüfung](SYNTHETIC_REVIEW.de.md) ein. Dafür werden
eine separate PostgreSQL-Instanz und ein lokal erzeugter Review-Schlüssel benötigt.
Der normale Vorschau-Start aktiviert diesen Modus nicht.

| Test | Erwartung |
|---|---|
| Anmeldung und Arbeitsliste | Vier vorbereitete synthetische Fälle mit passender Zuordnung |
| Vollständigen Fall mit Begründung freigeben | Fall abgeschlossen; Originalbewertung bleibt erhalten |
| Negatives Ergebnis ausdrücklich übersteuern | Menschliche Abweichung mit Begründung zusätzlich zur Originalbewertung |
| Unvollständigen Fall öffnen | Keine Freigabe durch Umdeutung unbekannter Angaben |
| Zwei Browser bearbeiten denselben Stand | Zweiter veralteter Versuch wird abgelehnt und lädt den gespeicherten Stand neu |
| Abmelden | Schlüssel, Fallansicht und Eingaben aus aktiver Sitzung entfernt |
| Dienst neu starten | Gespeicherte Fälle und Review-Historie unverändert verfügbar |

Für frische JSON-/XML-Eingänge und zwei synthetische Rückgabeziele verwende
[Synthetischer Gesamtablauf](SYNTHETIC_ROUNDTRIP.de.md). Diese Schritte erfolgen
über die dokumentierten API-Verträge und das ausführbare Smoke-Skript; sie sind
keine zusätzlichen Import-/Versand-Schaltflächen der Browseroberfläche.
Die Wiederherstellungsprobe vergleicht Originaleingänge, Bewertungen, Historien
und Zustellbelege nach Neustart und nach Wiederherstellung in einer frischen Datenbank.

## Abschluss und verbleibende Voraussetzungen

Dokumentiere zu deinem Test den Commit, das Betriebssystem, bestandene Schritte
und reproduzierbare Abweichungen. Automatisierte Browser-, Datenbank-, Replay-,
Paket- und Security-Prüfungen sind in GitHub Actions nachvollziehbar; die manuelle
Vorführung auf deinem Rechner wird dadurch nicht vorweggenommen.

Der [Readiness-Stand](../project/EXTERNAL_DATA_FREE_READINESS.md) grenzt die
implementierte synthetische Plattform von noch benötigten institutionellen
Spezifikationen, Rollen, Korrektur-/Nachforderungspolitik, fachlich freigegebenen
Knowledge Packs sowie Datenschutz- und Betriebsfreigaben ab. Diese Anforderungen
werden vor einem produktiven Pilot verbindlich geklärt. Batch-Freigabe und echte
MD-Adapter werden nicht als Bestandteil dieses Demo-Abschlusses behauptet.
