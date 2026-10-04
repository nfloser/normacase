# Lokaler synthetischer HTTP-Adapter

Start aus dem Repository-Verzeichnis mit .NET 10:

```bash
dotnet run --project src/NormaCase.Api
```

Der Dienst lauscht ausschließlich auf Loopback-Port 5080. Aufrufparameter oder
ASPNETCORE_URLS ändern diese Bindung nicht. Es gibt keine Runtime-Netzwerkabfragen.
Alle sieben mitgelieferten synthetischen Packs werden lokal geladen und validiert.

- `GET http://localhost:5080/api/packs`: Pack-/Release-IDs, Felder, Evidenzreferenzen sowie externe deutsche Präsentationsmetadaten und Beispiel-IDs.
- `GET http://localhost:5080/api/packs/{packId}/examples/{exampleId}`: synthetische UI-Vorlage mit explizitem Prüfdatum; numerische Werte werden als verlustfreie Strings ausgeliefert.
- `POST http://localhost:5080/api/assessments/synthetic.demo-c`: versionierter Fall als JSON.
- Antwort: verlustfreies Assessment-JSON mit Build-/Wissensstand, Decision Trace und gegebenenfalls unabhängigen Fachausgaben.

Beispiel (curl ist unter Windows als curl.exe verfügbar):

```bash
curl http://localhost:5080/api/packs
curl -H "Content-Type: application/json" --data-binary @examples/cases/demo-c-supported.json http://localhost:5080/api/assessments/synthetic.demo-c
```

Der Fall enthält das Prüfdatum explizit. Die Plattformversion stammt aus der
Assembly-InformationalVersion des Builds; es wird kein aktueller Zeitpunkt
hinzugefügt. Die Engine ist dieselbe wie im CLI-Adapter.

Fehlerstatus: 400 ungültige Eingabe, 403 nichtlokaler Host/fremder Origin,
404 unbekanntes Pack, 413 Eingabe zu groß, 415 falscher Inhaltstyp. Deutsche
Meldungen liegen in RESX-Ressourcen. Antworten sind nicht cachebar; Eingabeinhalte,
Dateipfade und Exception-Texte werden nicht ausgegeben oder protokolliert.

Requests sind auf 1 MiB begrenzt. Keine CORS-Freigabe, Forwarded-Header-Auswertung
oder externe Bindung. Browserantworten setzen restriktive CSP-, Frame-, Referrer-
und MIME-Sicherheitsheader. Nach einem Frontend-Build wird die lokale Workbench
unter `http://localhost:5080/` aus demselben Prozess ausgeliefert. Standardmäßig
bleibt die Vorschau anonym und ohne persistente Review-Mutationen. Eine optionale
synthetische Authentifizierungs- und PostgreSQL-Review-API ist ausschließlich für
lokale Entwicklung vorgesehen; produktive Identität, Patientendatenverwaltung und
fachliche Freigabe sind damit ausdrücklich nicht umgesetzt.

```bash
dotnet test tests/NormaCase.Api.Tests --configuration Release
```

CI prüft zusätzlich zum HTTP-Testhost auch das tatsächlich gestartete Kestrel-
Programm: Pack-Katalog, Demo-E-Assessment-JSON v2 einschließlich UNKNOWN-Fachausgabe,
Sicherheitsheader und Origin-Ablehnung. Der separate Workbench-Job baut die lokal
gebündelten React-/TypeScript-Assets und prüft reale Chromium-Szenarien gegen den
gleichen .NET-Prozess. Das Smoke-Skript gibt keine Fallinhalte aus.

## Snapshot-Adapter

- `POST /api/snapshots/{packId}` empfängt denselben strukturierten Fall wie der
  bestehende Assessment-Endpunkt. Die Antwort enthält `snapshotJson` und
  `assessmentJson` als JSON-Zeichenfolgen aus einer Auswertung.
- `POST /api/snapshots/replay` empfängt den vollständigen Snapshot direkt als
  JSON-Body. Die Antwort enthält das bestätigte `assessmentJson` als Zeichenfolge.

Die Zeichenfolgen erhalten exakte Dezimalwerte auch in JavaScript-Clients. Der
Replay-Adapter verwendet ausschließlich das eingebettete Wissenspaket und die
tatsächliche Assembly-InformationalVersion der API. Der Aufrufer kann den
Plattformstand nicht per Request überschreiben. Das eingebettete Pack muss
SYNTHETIC sein; ein aktueller Katalogeintrag wird für historische Wiederholung
nicht vorausgesetzt.

Zusätzliche Fehler: 403 nichtsynthetischer Snapshot; 409 Plattformstand,
Wissensrelease oder neu berechnetes Ergebnis stimmt nicht überein.
Prüfsummen-/Strukturfehler bleiben 400. Der gemeinsame JSON-Adapter zählt maximal
1 MiB tatsächlich empfangene Bytes, auch bei unbekannter Content-Length und
mehrbyteigem UTF-8. UTF-8 mit optionaler UTF-8-BOM wird unterstützt.
Die übrigen Host-, Origin-, Inhalts-, Datenschutz- und Sicherheitsgrenzen gelten
unverändert auch für beide Snapshot-Endpunkte.

## Synthetische Workflow-Vorgänge

- `POST /api/workflows/{packId}/start`: JSON mit `workflowId`, `workflowVersion`,
  `runId`, `caseId`, `actorId`, `recordedAtUtc` und `reason`.
- `POST /api/workflows/advance`: JSON mit `runJson` (vollständige Vorgangsdatei als
  Zeichenfolge), `expectedRevision`, `transitionId`, `actorId`, `recordedAtUtc`, `reason`.
- `POST /api/workflows/verify`: vollständige Vorgangsdatei direkt als JSON-Body.

Antworten enthalten `runJson` als originalen versionierten JSON-Text und `view` mit
externen deutschen Labels, aktuellem Stand, verfügbaren Übergängen und Historie.
Revisionen im View sind Strings; der erwartete Revisionswert im Übergangskommando
ist ein ganzzahliges JSON-Token. Der Plattformstand stammt ausschließlich aus der
gestarteten Assembly. Alle IDs, Bearbeiter, UTC-Zeiten und Gründe sind explizit.
Der Server generiert keine IDs, liest keine Uhrzeit und speichert keine Vorgänge.

Import und Übergang prüfen zusätzlich zum strengen JSON-/Historienvertrag die
exakte erste Graph-/Quellensnapshot gegen das installierte SYNTHETIC-Wissen.
Vorgänge mit abweichendem Wissens-/Plattformstand oder substituiertem Graph werden
mit 409 abgelehnt. Veraltete Revisionen oder nicht verfügbare Übergänge sind ebenfalls
409; rückwärts laufende oder nicht explizite UTC-Zeitpunkte sind 400. Die APIs
übernehmen dieselben Loopback-/Origin-/UTF-8-/Body-/no-store-Grenzen wie Assessments.
Exportierbares Vorgangs-JSON ist auf 256 KiB begrenzt; Überschreitungen sind 413.

Es handelt sich um einen stateless synthetischen Dateiaustausch. Die erwartete
Revision bezieht sich auf die übergebene Datei, nicht auf einen zentralen gespeicherten
Stand. Mehrere Kopien einer Datei können daher unabhängig fortgesetzt werden.
Authentifizierung, produktive Case-Verwaltung und PostgreSQL-Anbindung bleiben
separate Voraussetzungen; Actor-IDs und Gründe sind unauthentifizierte Angaben.


## Persistente synthetische Review-API

Die Authentifizierungsgrenze aus `docs/security/SYNTHETIC_REVIEW_HOST.md` kann ohne
Datenbank separat getestet werden. Persistente Fallprüfung wird erst mit allen
folgenden Einstellungen aktiviert:

```text
SyntheticReview__Enabled=true
SyntheticReview__PersistenceEnabled=true
SyntheticReview__Credential=<kanonische Base64-Kodierung von 32 Zufallsbytes>
ConnectionStrings__SyntheticReview=<lokale PostgreSQL-Verbindung>
ConnectionStrings__SyntheticReviewMigrations=<optionale getrennte Migrationsverbindung>
```

Ohne `PersistenceEnabled=true` werden keine persistenten Review-Endpunkte registriert
und PostgreSQL wird nicht benötigt. Bei aktivierter Persistenz migriert der Host das
vorhandene NormaCase-Schema und initialisiert nur synthetische `demo-g`-Fixtures,
sofern sie noch nicht vorhanden sind. Für den Betrieb mit minimalen Datenbankrechten
sind beide Verbindungen erforderlich; die Laufzeitverbindung erhält kein DDL-,
UPDATE- oder DELETE-Recht. Siehe
[PostgreSQL least-privilege host boundary](../architecture/POSTGRESQL_LEAST_PRIVILEGE.md).

Geschützte Endpunkte:

- `GET /api/review/work-queues`
- `GET /api/review/work-cases/{caseId}`
- `POST /api/review/work-cases/{caseId}/reviews`
- `POST /api/review/batch-reviews`

Review-Kommandos enthalten erwartete Case-, Prozess- und Audit-Revisionen, Disposition
und Begründung. Actor, Review-ID und Aufzeichnungszeit stammen vom Server. Der
synthetische Authorizer erlaubt Accept/Override nur aus `awaiting-approval`; stale
Revisionen liefern 409, nicht erlaubte Zustände 403. Die ursprüngliche deterministische
Bewertung bleibt unverändert und der Review wird append-only auditiert.

Die synthetische Sammelprüfung benötigt zusätzlich die konfigurierte Aktion `BATCH`
für jeden ausgewählten Fall und die exakte Policy
`synthetic-reviewed-batch-policy`, Version `1`. Sie akzeptiert höchstens 100
eindeutige Fall-/Review-Kommandos, prüft alle lesbaren Fall-/Assessment-Bindungen vor
der ersten Änderung und führt anschließend pro Fall dieselbe Autorisierung,
Revisionsprüfung und PostgreSQL-Transaktion wie die Einzelprüfung aus. Die technischen
Ergebnisstatus sind stabil, enthalten aber keine Falldaten oder Fehlerdetails. Eine
wiederholte Gesamtanfrage ist derzeit nicht idempotent; bereits übernommene Elemente
liefern einen Revisionskonflikt.
