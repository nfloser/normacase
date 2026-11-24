# Datenschutz und Datenhaltung

Stand: 21.11.2026 · Dieses Dokument ist ein Umsetzungskonzept, keine Zertifizierung und keine Rechtsberatung. Es zeigt, an welchen anerkannten Kriterien sich NormaCase ausrichtet und wie jedes Kriterium technisch belegt wird.

## 1. Grundsatz: verarbeiten, nicht vorhalten

NormaCase ist als Baustein für ein führendes Fachsystem gedacht. Das Fachsystem besitzt die Fallakte und trägt die Aufbewahrungspflichten. NormaCase hält Gesundheitsdaten nur so lange, wie es sie zur Bearbeitung braucht, und behält danach ausschließlich den Nachweis, wie eine Bewertung zustande kam.

| Betriebsart | Fallinhalt in NormaCase | Wann sinnvoll |
|---|---|---|
| **Durchreichen** (Standard für headless) | nur im Arbeitsspeicher während des Aufrufs | Fachsystem fragt, NormaCase antwortet sofort |
| **Befristet** (Standard mit Prüfwerkstatt) | im Schema `workflow`, mit Ablaufzeit, gelöscht nach Rückgabe-Quittung | Rückfragen, Vier-Augen-Freigabe |
| **Aufbewahrung** (nur auf ausdrückliche Konfiguration) | nach Hausregel | nur wenn das Haus NormaCase bewusst als Ablage nutzt; nicht Ziel des Projekts |

## 2. Was dauerhaft bleibt

| Feld | Zweck | Personenbezug | Löschregel |
|---|---|---|---|
| Vorgangs-ID des Fachsystems | Zuordnung des Nachweises zum Vorgang dort | pseudonym (Schlüssel liegt beim Fachsystem) | mit Löschung im Fachsystem, spätestens nach konfigurierter Frist |
| SHA-256 der kanonischen Eingabe | Beweis, welche Eingabe bewertet wurde, ohne sie zu speichern | keiner aus sich heraus | wie oben |
| Plattformversion, Release-ID, Stichtag | Reproduzierbarkeit | keiner | wie oben |
| Regel-IDs und Teilergebnisse je Kriterium | Entscheidungspfad ohne Werte | keiner | wie oben |
| Ergebnis | Nachweis | im Zusammenhang mit Vorgangs-ID | wie oben |
| Handelnde Person (technische ID), Rolle, Zeitpunkt | Vier-Augen-Nachweis | Beschäftigtendaten | nach Hausregel |

Befristet im Vorgang liegt bei der technischen Klärung zusätzlich ein Fehlercode (`UNKNOWN_RELEASE`, `INVALID_CASE_INPUT`), ein technischer Bezeichner ohne Fallinhalt; er wird mit dem Vorgang gelöscht. Vorgänge der Unterlagensichtung haben weder Falldaten noch Bewertung, nur Dokumente und den Anzeigenamen.

Zu jedem Dokument speichert NormaCase zusätzlich die Dokumentart (`category`: Auftrag/Antrag, ärztliche Unterlagen, Pflege, Bescheide und Nachweise, Sonstiges), das Ausstellungsdatum (`issued_on`) und in `documents.reference` die Herkunft einzelner Angaben als Feld-ID und Seitenzahl. Zweck: die Fallakte geordnet anzeigen und jede bewertete Angabe bis zur Seite im Beleg zurückverfolgen. Löschung: mit dem Dokument, also mit den Fallinhalten bei der Quittung. TIFF-Scans werden beim Eingang in PNG-Seitenvorschauen umgewandelt (höchstens 50 Seiten, 40 Megapixel je Seite); das Original bleibt unverändert und wird ebenso gelöscht.

„Meine Ablage“ (Schema `workflow`, Tabellen `personal_folder`, `personal_mark`, `personal_queue_color`) ordnet Vorgänge je Konto: eigene Ordner mit Name und Farbe, je Vorgang Ordner, Farbmarkierung, Lesezeichen und eine Notiz (höchstens 1000 Zeichen), dazu eigene Farben der Arbeitslisten. Zweck: persönliche Arbeitsorganisation; andere Konten sehen nichts davon. Die Notiz ist Freitext und kann Fallbezug haben; sie wird deshalb wie ein Fallinhalt behandelt und bei der Rückgabe-Quittung gelöscht. Markierungen hängen am Vorgang und werden mit ihm gelöscht (`ON DELETE CASCADE`), Ordner bleiben, bis die Person sie löscht. Ordnernamen sind für Arbeitsbegriffe gedacht („Rückfragen diese Woche“), nicht für Personennamen; die Oberfläche weist darauf hin.

Im Browser der Nutzerin oder des Nutzers (`localStorage`, je Konto) liegt nur die Ansicht der Prüfwerkstatt: gewählte Spalten und Reihenfolge, Zeilenfärbung an/aus, Sortierung und gespeicherte Abfragen mit Korb und Filtern. Filter auf die Spalte „Vorgang“ können Namen enthalten; sie gelten nur in der laufenden Sitzung und werden nicht gespeichert. Fallinhalte, Tokens und Dokumente landen nie im Browserspeicher. Löschregel: durch die Nutzerin oder den Nutzer (Abfrage löschen, Browserdaten löschen).

Nicht gespeichert werden: Namen, Geburtsdaten, Versichertennummern, Freitexte, Befunde, Dokumente, konkrete Kriterienwerte. Eine Revision kann das Ergebnis trotzdem nachprüfen, indem das Fachsystem die Originaleingabe erneut einreicht: gleicher Hash, gleiches Release, gleicher Stichtag ergeben das gleiche Ergebnis.

## 3. Zuordnung zu Kriterien

### 3.1 Standard-Datenschutzmodell (DSK, SDM)

| Gewährleistungsziel | Umsetzung in NormaCase | Beleg |
|---|---|---|
| Datenminimierung | Durchreichen als Standard; Nachweis ohne Inhalte | Test: nach Abschluss keine Fallinhalte in der DB |
| Verfügbarkeit | Zustandsloser Kern, Wiederanlauf ohne Datenverlust offener Vorgänge, Health-Checks | Readiness-Probe, Wiederanlauftest |
| Integrität | Hash der Eingabe, unveränderliche Releases, nur anfügendes Audit | DB-Rechte ohne UPDATE/DELETE auf Audit, Paritätstest |
| Vertraulichkeit | OIDC, Rollen, fallbezogene Rechte, TLS, keine Inhalte in Logs | Sicherheitstests, Log-Prüfung im CI |
| Nichtverkettung | Getrennte Schemas und DB-Rollen je Modul; Identifikationsdaten nie im Audit oder in Metriken | ArchUnit-/Schema-Test |
| Transparenz | Entscheidungspfad zu jeder Bewertung; dokumentierte Felder und Löschregeln (diese Tabelle) | API-Vertrag, Doku |
| Intervenierbarkeit | Menschliche Prüfung bei `INCOMPLETE`/`HUMAN_REVIEW`; Zurückweisen jederzeit; Rückzug und sofortige Löschung auf Anforderung des Fachsystems (`DELETE /api/v1/intake/{id}`) | `WorkflowIntegrationTest` |

### 3.2 DSGVO

- **Art. 25 (Datenschutz durch Technikgestaltung und Voreinstellungen):** Die datensparsamste Betriebsart ist die Voreinstellung; mehr Speicherung erfordert bewusste Konfiguration.
- **Art. 32 (Sicherheit der Verarbeitung):** Verschlüsselung im Transport, Zugriffskontrolle, Wiederherstellbarkeit, regelmäßige Prüfung durch automatisierte Tests im CI.
- **Art. 28 (Auftragsverarbeitung):** Im Zielbild verarbeitet NormaCase im Auftrag des Hauses auf dessen Infrastruktur; es gibt keine Übermittlung an Dritte.

### 3.3 § 276 SGB V (Medizinischer Dienst)

Die Vorschrift verlangt unter anderem, dass der Medizinische Dienst Identifikationsdaten getrennt von medizinischen Daten speichert und Sozialdaten nach fünf Jahren löscht. Beides liegt beim führenden Fachsystem. NormaCase unterstützt es, indem es

- Identifikationsdaten gar nicht dauerhaft hält,
- im befristeten Betrieb Identifikations- und Fachdaten in getrennten Tabellen führt,
- Löschfristen als Konfiguration mit Höchstwert umsetzt und die Löschung selbst protokolliert.

### 3.4 BSI TR-03161 (Teil 2 Webanwendungen, Teil 3 Hintergrundsysteme)

Die Richtlinie richtet sich an Anwendungen im Gesundheitswesen. NormaCase nutzt ihre Prüfaspekte als Checkliste. Die Zuordnung steht in [`docs/sicherheit/tr-03161-checkliste.md`](sicherheit/tr-03161-checkliste.md), jeweils mit Verweis auf Test oder Konfiguration. Schwerpunkte:

- Authentisierung und Sitzungsverwaltung über OIDC, kurze Token-Laufzeiten, Abmeldung serverseitig wirksam
- Autorisierung serverseitig, fallbezogen, nicht unterscheidbare Antwort bei fehlendem Recht
- Eingabeprüfung gegen Schema, Ausgabekodierung, Content Security Policy ohne externe Quellen
- Protokollierung ohne sensible Inhalte, Integrität der Protokolle
- Abhängigkeitsprüfung und SBOM im CI

## 4. Prüfbarkeit im Code

Jede Zusage oben ist durch einen automatischen Test belegt:

- `WorkflowIntegrationTest`: Nach der Quittung sind Fall, Identifikationsdaten, Dokumente und Rückgabeinhalt gelöscht; Fristablauf und Rückzug durch das Fachsystem löschen vollständig.
- `AssessmentApiIntegrationTest.auditTableHasNoColumnsForCaseContent`: Die Nachweistabelle hat nur die Spalten der Positivliste.
- `AssessmentApiIntegrationTest.caseValuesNeverReachTheLog`: Ein markierter Fallwert erscheint nach einem vollständigen Ablauf nicht im Log.
- `OperationsIntegrationTest`: Metriken enthalten keine Vorgangskennungen oder Felder.
- `ModularityTest`: Spring Modulith verhindert Zugriffe über Modulgrenzen.

## 5. Offene Punkte

- Abstimmung der Nachweisfelder mit Datenschutz und Revision eines echten Hauses.
- Rechtsgrundlage und Vertragsform hängen vom Einsatzort ab und sind nicht Teil dieses Repositorys.
