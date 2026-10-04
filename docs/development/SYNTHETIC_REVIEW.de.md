# Persistente synthetische Fallprüfung vorführen

Diese lokale Demonstration benötigt keine MD-Daten oder interne Schnittstelle.
Sechs synthetische Fälle werden beim ersten Start bewertet, geroutet und mit ihrer
Originalprüfung in PostgreSQL gespeichert. Vier vollständige Fälle warten auf
menschliche Freigabe, darunter zwei getrennte Browser-Testfälle für die
Sammelfreigabe; ein Fall benötigt Angaben und einer eine Gegenprüfung.
Nur die vom Server erlaubten Aktionen erscheinen in der Oberfläche.

## Voraussetzungen und Start

Für den Quellcode: .NET 10, Node.js 24 und eine lokale PostgreSQL-Instanz.
Docker Compose kann ausschließlich die Demo-Datenbank bereitstellen:

1. Setze `NORMACASE_DEMO_DB_PASSWORD`,
   `NORMACASE_DEMO_DB_MIGRATION_PASSWORD` und
   `NORMACASE_DEMO_DB_RUNTIME_PASSWORD` auf drei unterschiedliche, außerhalb von
   Git erzeugte Passwörter.
2. Starte `docker compose -f compose.synthetic-review.yml up -d --wait`.
3. Setze die folgenden Umgebungsvariablen nur im Terminal des lokalen API-Prozesses:
   - `SyntheticReview__Enabled=true`
   - `SyntheticReview__PersistenceEnabled=true`
   - `SyntheticReview__Credential`: Base64-Kodierung von 32 kryptografisch zufälligen
     Bytes, ohne Leerzeichen; kein Passwort aus Beispielen verwenden.
   - `ConnectionStrings__SyntheticReview`:
     `Host=127.0.0.1;Port=54329;Database=normacase_synthetic;Username=normacase_runtime;Password=<externes Laufzeitpasswort>`
   - `ConnectionStrings__SyntheticReviewMigrations`:
     `Host=127.0.0.1;Port=54329;Database=normacase_synthetic;Username=normacase_migrator;Password=<externes Migrationspasswort>`
4. Baue die Oberfläche mit `npm --prefix frontend ci` und
   `npm --prefix frontend run build`.
5. Starte `dotnet run --project src/NormaCase.Api --configuration Release`.
6. Öffne <http://localhost:5080>. Gib unter „Persistente synthetische Fallprüfung“
   den lokalen Review-Schlüssel im maskierten Feld ein und melde dich an.

Erzeuge den Review-Schlüssel lokal mit einem Secret-Werkzeug oder beispielsweise
32 zufälligen Bytes über die Kryptografie-API des Betriebssystems. Bewahre ihn nur
für die lokale Demonstration auf. Nicht in Git, URLs, Screenshots oder Logs kopieren.
Die Datenbank ist ausschließlich an Loopback gebunden. Veröffentliche den API-Host
nicht über Reverse Proxy oder Portfreigabe.

Compose richtet die getrennten Rollen nur beim Initialisieren eines neuen Volumes
ein. Bei einem bestehenden Volume muss ein berechtigter Betreiber nach Sicherung und
Wiederherstellungsprobe `ops/postgresql/provision-least-privilege.sql` mit den beiden
Passwörtern in der Prozessumgebung ausführen. Das Skript ist wiederholbar und
überträgt bestehende Objekte im Schema `normacase` an die Migrationsrolle. Der
Bootstrap-Benutzer `normacase_synthetic` dient nur Einrichtung, Sicherung und
Wiederherstellung; seine Verbindung darf nicht an den API-Prozess gehen. Details:
[PostgreSQL least-privilege host boundary](../architecture/POSTGRESQL_LEAST_PRIVILEGE.md).

Auch selbstständige Vorschaupakete können mit diesen Umgebungsvariablen im Terminal
und einer separat bereitgestellten PostgreSQL-Instanz gestartet werden. Der normale
Doppelklick-/Vorschau-Start bleibt ohne Datenbank und ohne Review-Modus verwendbar.

## Vorführablauf

- Öffne `demo-g-supported`. Sieh Originalergebnis, Nachweise, Prüfweg und Historie an.
- Gib eine ausdrückliche synthetische Begründung ein und übernimm das Systemergebnis.
  Der Server schreibt die menschliche Entscheidung und den Prozessstand gemeinsam;
  der Fall erscheint anschließend unter „Abgeschlossen“.
- Öffne `demo-g-not-supported`. Gib eine Begründung und ein ausdrücklich gewähltes
  abweichendes Ergebnis ein. „Ergebnis übersteuern“ ergänzt die Historie; das
  ursprüngliche deterministische Prüfergebnis bleibt sichtbar und unverändert.
- Öffne die beiden übrigen Fälle. Fehlende Angaben bzw. nötige Gegenprüfung erzeugen
  keine Freigabe-Schaltflächen. UNKNOWN wird nicht umgedeutet.
- Im Mehrbenutzermodus mit ausdrücklich konfigurierter `BATCH`-Aktion wähle die beiden
  `demo-g-batch-*`-Fälle, gib eine gemeinsame Begründung ein und übernimm die
  Systemergebnisse. Die deutsche Ergebnisliste zeigt jeden Fall getrennt. Ein bewusst
  abgebrochener Request bleibt mit derselben ID für eine sichere Wiederholung erhalten.
- Melde dich ab. Schlüssel, Fallansicht und Eingaben werden aus der aktiven Sitzung
  entfernt. Der Browser speichert weder Schlüssel noch Sitzung dauerhaft.
- Starte den Host erneut: bereits gespeicherte Prüfungen und Reviews bleiben erhalten.

Im Mehrbenutzermodus meldest du dich mit dem getrennt konfigurierten
Verwaltungsschlüssel am selben Formular an. Statt der Fallarbeitsliste erscheint
„Identitäten verwalten“. Öffne eine konfigurierte Testidentität, gib eine Begründung
ein und sperre oder reaktiviere sie. Der angezeigte Zustand und die Historie stammen
aus PostgreSQL; Schlüssel werden nicht angezeigt. Die nächste geschützte Anfrage der
gesperrten Identität erhält 401. Dieser Ablauf ändert keine Fallzuweisung oder Aktion.

Zwei Browser können denselben noch offenen Fall laden. Nach einer Entscheidung im
ersten Browser wird der alte Versuch im zweiten mit 409 abgewiesen. Die Oberfläche
lädt den gespeicherten Stand neu; sie wiederholt keine Entscheidung automatisch.
401 beendet die lokale Sitzung, 403 zeigt einen deutschen Hinweis ohne den Fall
optimistisch zu ändern. Abmeldung bricht laufende Anfragen ab.

## Browserprüfung

`npm --prefix frontend run test:e2e:review` startet den realen lokalen API-Prozess.
Setze davor `NORMACASE_REVIEW_E2E_CONNECTION` auf eine **eigene leere synthetische
Testdatenbank**. Die Tests erzeugen getrennte zufällige Benutzer-, zweite Benutzer-
und Verwaltungsschlüssel im Arbeitsspeicher. Optional können
`NORMACASE_REVIEW_E2E_CREDENTIAL`, `NORMACASE_REVIEW_E2E_OTHER_CREDENTIAL` und
`NORMACASE_REVIEW_E2E_ADMINISTRATOR_CREDENTIAL` extern gesetzt werden.
Die Tests verändern ihre Fälle absichtlich; wiederholte Läufe brauchen eine frische
Testdatenbank. Eine produktive oder persönliche Datenbank ist hierfür unzulässig.

CI stellt eine eigene PostgreSQL-Datenbank bereit und prüft Browser → API → Datenbank,
Freigabe/Override, echte 401/403/409-Antworten, parallele Browser, unveränderte
Originalprüfungen, Live-Sperrung/Reaktivierung mit Verwaltungshistorie, Auswahl und
persistierte Sammelfreigabe, exakte Wiederholung nach Abbruch, Abmeldung während einer
Anfrage und leeren Browserspeicher.

## Grenzen

Für getrennte lokale Testidentitäten und genaue Fall-/Aktionsrechte siehe
[Getrennte lokale Testidentitäten](../security/LOCAL_SYNTHETIC_IDENTITIES.de.md).
Die folgende Grenze beschreibt den bisherigen gemeinsamen Demo-Schlüssel;
auch der Mehrbenutzermodus ist keine produktive Personenverwaltung.

Das ist ein synthetischer lokaler Akteur, keine produktive Personenverwaltung oder
fachliche MD-Freigabe. Die Sammelfreigabe erscheint nur im Mehrbenutzermodus mit der
separaten Aktion `BATCH`; der gemeinsame Kompatibilitätsschlüssel erhält sie nicht.
Institutionelle Rollen, organisatorische Sammelfreigabepolicy,
Datenschutz-/Betriebsfreigabe und verbindliche Schnittstellen bleiben gesonderte
externe Vorgaben. Der implementierte generische Ein-/Ausgang und die Wiederherstellungsprobe
stehen in [Synthetischer Gesamtablauf](SYNTHETIC_ROUNDTRIP.de.md).

`docker compose -f compose.synthetic-review.yml down` beendet die Demo-Datenbank
und erhält die synthetische Historie im Volume. Ein vollständiger Reset würde dieses
Volume löschen; sichere benötigte Demo-Historie vor einem bewusst gewählten Reset.
