# Aufbewahrung, Einschränkung und Löschung

## Nachgewiesene Grenze

NormaCase besitzt einen deterministischen **Nur-Lese-Planer** für versionierte
Aufbewahrungsmetadaten. Er schreibt oder löscht keine Daten. Er ersetzt weder eine
juristische Prüfung noch eine vollständige Dateninventur oder institutionelle Freigabe.
Die synthetischen Szenarien enthalten keine gesetzlichen Fristen oder Patientendaten.

`RetentionPlanner.Plan` erhält Fall-ID, Policy-ID/-Version, expliziten UTC-Zeitpunkt,
Vollständigkeitsangabe und maximal 1024 Artefaktmetadaten. Jedes Artefakt hat technische
Identität, Speicherklasse, explizites Aufbewahrungsende oder unbekannt sowie Legal-Hold-
Zustand. Keine Patientenwerte, Inhaltsdateien oder medizinischen Regeln gelangen hinein.
Eingaben werden geprüft und in unveränderlicher, ordinal sortierter Form festgehalten.
Die Vollständigkeitsangabe ist eine Adapteraussage, kein Beweis der tatsächlichen Inventur.

| Zustand | Ergebnis |
| --- | --- |
| Aktiver Hold | Aufbewahren wegen Hold |
| Unvollständige Inventur | Manuelle Prüfung |
| Hold unbekannt oder Frist fehlt | Manuelle Prüfung |
| Frist noch nicht erreicht | Aufbewahren |
| Explizite Frist erreicht, Hold aufgehoben, Inventur vollständig | Für menschliche Prüfung geeignet |

`DeletionAuthorized` bleibt **immer false**, auch wenn alle Artefakte für Prüfung
geeignet sind. Der Plan ist eine Metadatenauswertung, kein Freigabetoken. Außerhalb
des Planers müssen Zugriffsbeschränkung, befugte Datenerhebung und zuverlässige
Frist-/Hold-Herkunft nachgewiesen werden. Der Planer verändert keine Fallrechte.

## Dateninventur und Zuständigkeit

| Bestand | Aktuelle Ablage | Zu prüfende Kopien / Abhängigkeiten |
| --- | --- | --- |
| Fall-/Eingangsdaten | `intake_streams`, `intake_records`, `intake_messages` | Quelle, Wiederholungsnachrichten, alle Revisionen |
| Assessment und Prozess | `assessment_records`, `workflow_run_versions`, `case_review_versions` | Historische Assessments, korrigierte/ersetzte Revisionen |
| Review, Korrektur, Rückfrage | `assessment_audit_trail_versions`, `case_correction_links`, `case_clarification_requests/resolutions` | Gründe können sensible Daten enthalten; Review ist keine Anonymisierung |
| Sammelaktionen | `batch_review_requests/results` | Eingaben, Fallbindungen, gespeicherte Resultate |
| Rückgabe | `authorized_outbound_requests`, `outbound_delivery_receipts`, synthetische Inbox/Datei | Resultatkopien, Exportdateien, Empfängeraufbewahrung |
| Dokumente/Snapshots | Lokale synthetische Dokumentakten, heruntergeladene Exporte | Originale, Vorschaubilder, extrahierter Text, portable Snapshots |
| Berechtigungs-/Knowledge-Audit | Identity-/Knowledge-Tabellen | Akteurbezug; separate Policy, keine pauschale Falllöschung |
| Sicherungen | DB-Dumps und Dateisicherungen der Wiederherstellungsprobe | Unveränderliche Backups, Ablauf, Restore-Nachbereinigung |
| Betriebsdiagnose | Betreiber-Sink / Proxy / OS / APM | Separates Zugriff-/Aufbewahrungskonzept |

Dies ist eine versionierte Architektur-Inventur, kein automatisch vollständiger
Scan. Der produktive Adapter muss Nichtvorhandensein, Revision, Eigentümer, Policy,
Hashes/Kopien und Empfänger für jede relevante Speichergrenze nachweisen. Ein unbekannter
Bestand darf nicht still als leer gelten. Rückgabeempfänger liegen außerhalb der
NormaCase-Datenbank und brauchen einen gesonderten, bestätigten Prozess.

## Drei unterschiedliche Vorgänge

1. **Berichtigung:** Neue Fallrevision und neues Assessment; Original bleibt nachvollziehbar.
2. **Einschränkung:** Aktuelle autoritative Zugriffspolitik verhindert unzulässige Nutzung
   einschließlich Warteschlangen, Dokumenten, Exporten und Hintergrundjobs. Ein UI-Ordner
   oder ausgeblendeter Fall ist keine rechtliche Einschränkung.
3. **Löschung:** Bewusst freigegebene Entfernung des gesamten definierten Bestands samt
   zugeordneten Kopien, abhängig von Pflichtaufbewahrung/Holds und Backupkonzept.

Gesetzliche Zulässigkeit, konkrete Fristen, Fristbeginn, Holds, Betroffenenrechte und
gegebenenfalls Ausnahmen sind durch den Betreiber zu prüfen. Referenzen: DSGVO
Art. 5/17/18/32 und [SGB X §84](https://www.gesetze-im-internet.de/sgb_10/__84.html).
NormaCase leitet daraus keine allgemeine Frist ab. [ADR 0002](../adr/0002-retention-and-reviewed-aggregate-erasure.md)
trennt diesen Prozess vom deterministischen medizinischen Kern.

## Vertrag für spätere Ausführung

Eine separate Löschkomponente darf erst nach Review dieses Vertrags aktiviert werden:

- Verifizierter Serverakteur mit eigener Fall-/Löschberechtigung; keine normalen
  Assessor-/Review-Rechte als implizite Löschberechtigung.
- Plan mit exakter Fall-, Inventur-, Policy- und Hold-Revision; bei Änderung neu planen.
- Unabhängige menschliche Freigabe, eindeutige Operation-ID, Grundcode und Policybezug;
  Freitext/PDF-Inhalte dürfen nicht in Löschlogs übernommen werden.
- Entzug und Hold unmittelbar vor Commit nochmals prüfen; normaler Runtime-Account
  erhält keine DDL-/DELETE-Rechte. Eine gesonderte minimal berechtigte Komponente und
  explizite überprüfte Migration wären erforderlich.
- Fallaggregat transaktional behandeln; keine einzeln löschbaren historischen
  Revisionen, die eine falsche Resthistorie erzeugen. Dateien/Exporte über resumierbares,
  idempotentes Journal; Teilerfolg bleibt sichtbar und ist keine abgeschlossene Löschung.
- Minimierter, separat geregelter Nachweis (Operation, Policyversion, Zeitpunkt,
  Ergebnis, Bestandklassen); auch dieser Nachweis kann personenbezogen sein und
  benötigt eigene Zugriff-/Aufbewahrungsregeln.
- Kein automatischer Reimport eines gelöschten/eingeschränkten Falls durch Replay,
  Wiederholungsnachricht oder Restore. Geschützte Suppression-/Restore-Daten dürfen
  keine kompletten gelöschten Inhalte als vermeintlichen Nachweis behalten.

Diese Ausführung ist **noch nicht implementiert**. Ein Nur-Lese-Plan darf nicht
als physischer Löschmechanismus vorgeführt werden. Ohne institutionelle Policy und
geprüfte Löschkomponente bleibt reale Datenverarbeitung gesperrt.

## Backups und Wiederherstellung

Backup-Fristen gelten separat. Ein Fall mit abgelaufenen Primärdaten kann noch
Aufbewahrung oder manuelle Prüfung benötigen, solange eine Sicherung nicht abgearbeitet
ist. Keine nicht belegte Behauptung sicherer Einzel-Löschung aus unveränderlichen Dumps.
Nach Restore muss der aktuelle externe Policy-/Hold-/Einschränkungsstand vor Freigabe
der Anwendung erneut angewendet werden; ein mitrestaurierter alter Plan reicht nicht.
Zugriffe bleiben bis zur aktuellen Inventur und Nachbereinigung eingeschränkt.
Der Betreiber bestätigt Backupablauf und nachgewiesene Wiederherstellung.

## Ausführbare synthetische Abnahme

```bash
dotnet test tests/NormaCase.Application.Tests --configuration Release --filter FullyQualifiedName~RetentionPlanningTests
```

Die Tests beweisen den Nur-Lese-Vertrag: abgelaufene Daten mit nicht abgelaufenem
Backup/aktivem Hold, vollständig abgelaufener Bestand ohne Löschfreigabe, unbekannte
Frist/Hold/Inventur, Restore mit geänderter Policy, unveränderliche Ergebnisse und
ungültige Metadaten. Bestehende PostgreSQL-Tests müssen weiterhin direkte UPDATE/
DELETE-Versuche ablehnen. Dies sind synthetische Abnahmeszenarien, keine produktive Löschprobe.
