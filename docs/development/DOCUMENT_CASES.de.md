# Synthetische Dokumentakten

Die lokale Demo bietet pro Arbeitslistenfall eine Dokumentakte sowie zwölf separat
anwählbare Referenzakten. Die Dateien sind tatsächlich lokal vorhanden: PDFs,
PNG-Scanansichten und Textübermittlungen. Vorschau, Fundstellen mit Seitenwahl,
Vergrößerung einer Scanansicht, neuer Tab und Download funktionieren ohne externe
Dienste. Öffentliche Quellenlinks sind optional und werden niemals zur Laufzeit
für eine Prüfung abgerufen.

## Bedienung

1. In „Arbeitslisten“ einen Fall öffnen oder „Referenzfälle“ wählen.
2. Unter „Dokumentakte“ eine Unterlage auswählen.
3. „Angaben und Fundstellen“ öffnen und eine Fundstelle auswählen. Das PDF öffnet
   sich auf der angegebenen Seite; die tatsächliche Seitennavigation hängt vom
   PDF-Viewer des Browsers ab. Alternativ „In neuem Tab öffnen“ oder herunterladen.
4. Hinweise auf fehlende oder widersprüchliche Angaben lesen und das Ergebnis bzw.
   den aufgezeichneten Prüfweg vergleichen.

Alle 100 bisherigen Arbeitslistenfälle behalten ihre ursprünglichen Ergebnisse und
Prüfdaten. Die Dokumentakten werden beim Start gegen diese Ergebnisse geprüft.
Alle Fälle einer Arbeitsliste können über „Alle Fälle dieser Arbeitsliste anzeigen“
erreicht werden, ohne die anfängliche kompakte Vorführung zu verlieren.

## Referenzakten und fachliche Grenze

| Bereich | Fälle | Tatsächlich geprüfter Umfang |
|---|---:|---|
| Krankenfahrt mit Taxi/Mietwagen zur ambulanten Behandlung | 4 | Bestehendes PUBLIC_REFERENCE-Pack KT-RL § 8 Abs. 3; vollständig, fehlend, widersprüchlich und negativer Referenzpfad |
| Pflegebegutachtung Erwachsene | 4 | Bestehendes PUBLIC_REFERENCE-Pack: Gewichtung bereits festgestellter Modulsummen; vollständig, fehlend, widersprüchlich und außerhalb des Tabellenbereichs |
| Rehabilitation | 2 | Sichtung synthetischer Auftragsangaben und Befundbericht; keine fachliche Rehabilitationsprüfung |
| Onkologie | 2 | Sichtung einer synthetischen Aktenübersicht und Board-Anlage; keine CAR-T-Therapieentscheidung |

Öffentlich dokumentiertes Wissen bleibt PUBLIC_REFERENCE/IN_REVIEW. Es erfolgt
keine fachliche Freigabe und keine endgültige Leistungs- oder Pflegegradentscheidung.
Der anonyme Hauptkatalog bleibt unverändert auf SYNTHETIC-Wissen beschränkt. Die
Referenzfälle sind ein eigener, schreibgeschützter Pfad und werden nicht in den
persistenten Review-/Freigabeprozess eingeschleust.

## Dokumentverarbeitung und Provenienz

`scripts/generate_document_cases.py` erstellt eigene, ausdrücklich synthetisch
markierte Dokumente. Keine Originalformulare, realen Patientenberichte, Logos oder
Unterschriften werden kopiert oder als echte Akte ausgegeben.

Die PDFs enthalten einen eigenen kontrollierten NCF1-Textabschnitt. Der Generator
liest diesen mit pypdf tatsächlich aus den erzeugten PDF-Seiten zurück, validiert
Feldnamen und Werte und zeichnet Dokument-ID, Seite, Feld und Wert auf. Aus den
Beobachtungen entsteht die normalisierte Eingabe. Fehlendes bleibt UNKNOWN;
unterschiedliche Werte desselben Feldes werden UNKNOWN. Keine Quelle wird bei
Widerspruch automatisch bevorzugt. Der unveränderte Regelkern verarbeitet diese
Eingabe. Ob ein Nachweis vorhanden bzw. widersprüchlich ist, wird im synthetischen
Fixture ausdrücklich festgelegt; Dateivorhandensein ist keine fachliche Bestätigung.

NCF1 ist unser eigenes Testformat und **kein MD-, KBV-, KIS- oder eVV-Standard**.
Diese Funktion liest keine beliebigen freien Arztbriefe, Handschriften oder Scans.
PNG-Anlagen erhalten keine automatischen Extraktionsbeobachtungen. Die Textdatei
ist eine kontrollierte Ableitung derselben PDF-Seiten. Die Akten bilden einzelne
öffentlich dokumentierte Inhaltsbereiche ab, keine vollständige reale MD-Akte.

Der API-Start prüft die SHA-256-Werte der Dateien und normalisierten Eingaben,
Dokumentzuordnung, Seitenbereiche, zulässige Formate, Feldprovenienz, normalisierte
Werte und erwartete Engine-Ergebnisse. SHA-256 beweist interne Konsistenz, keine
Authentizität oder fachliche Freigabe. Die generierten Dateien gehören zur
versionierten Repository-Testbasis; die Engine benötigt keinen PDF-Parser.

## Sicherheit

`/api/document-cases` ist ein schreibgeschützter synthetischer Loopback-Demopfad.
Keine Uploadfunktion, freien Dateipfade, serverseitigen Downloads oder produktiven
Patientendaten. Dokumente liegen außerhalb des statischen Webroots und werden nur
über Fall-/Dokument-IDs aus einer geprüften Allowlist abgerufen. PDF/PNG/Text sind
auf 2 MiB pro Datei begrenzt und bleiben im Arbeitsspeicher. no-store, nosniff und
die bestehenden Host-/Origin-Prüfungen gelten auch für Dateien. Nur Dokumentantworten
erlauben same-origin Framing; die Anwendung bleibt gegen Framing geschützt. Eine
spätere produktive Dokumentablage braucht eigene fallbezogene Berechtigung,
Aufbewahrung, Malware-Prüfung und Betreiberfreigabe.

## Lesbarkeit und Verifikation

Normaler Bedien-/Lesetext ist überwiegend 16 px, Nebeninformationen mindestens
14 px. Hilfstexte, Aktionsfarben, Fokus und Eingaberahmen haben geprüfte AA-Kontraste.
Status bleibt als Text lesbar; sichtbarer Tastaturfokus, Sprunglink und eine
Einstiegsnavigation sind vorhanden. Dies ist keine vollständige WCAG-Zertifizierung
und ersetzt keinen Verständlichkeitstest mit neuen Nutzern.

Prüfungen:

```bash
python scripts/test_document_cases.py
npm --prefix frontend test
npm --prefix frontend run build
dotnet test tests/NormaCase.Api.Tests --configuration Release
npm --prefix frontend run test:e2e
```

Zur bewussten Neugenerierung: `python scripts/generate_document_cases.py`.
Dafür werden reportlab, pypdf, Pillow und DejaVu Sans benötigt; die Laufzeit benötigt
sie nicht. Prüfe anschließend PDF-Rendering, Hash-/Provenienztests, Engine-Ergebnisse
und den tatsächlichen Browserfluss. Die Corpusprüfung liest sämtliche PDFs zurück.
Die Repository-Attribute fixieren LF für gehashte Text-/JSON-Dateien auch unter Windows.

## Konkrete fachliche Beispielaufträge

Die zwölf Referenzakten enthalten einen erfundenen Fallhintergrund, einen Auftrag
und eine konkrete Fragestellung. Krankenfahrt und Pflege-Score verwenden die
bereits vorhandenen eng begrenzten öffentlichen Referenzregeln; Reha und Onkologie
bleiben Unterlagensichtungen. Die Alters-, Diagnose- und Alltagsangaben sind
synthetische Erzählangaben. Sie werden nicht als neue Entscheidungskriterien
interpretiert und nicht zur Ermittlung von Pflege-Modulsummen verwendet.

Die Texte liegen in `scripts/document-case-context.de.json`; der Generator bindet
den Hintergrund in die erste PDF und den Katalog ein. PDF-Text, extrahierte Werte,
Prüfsummen und normierte Eingaben werden gemeinsam geprüft. Fehlende Nachweise für
alternative, im konkreten Ergebnis nicht benötigte Prüfpfade werden nicht pauschal
als zwingender Klärungsbedarf ausgegeben. Die eigentlichen Eingaben, unbekannten
Werte und Regelergebnisse bleiben unverändert nachvollziehbar.
