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
unter `http://localhost:5080/` aus demselben Prozess ausgeliefert. Der Standardmodus
enthält keine Authentifizierung, Review-Mutation oder produktive Case-Verwaltung und
bleibt die read-only synthetische Vorschau. Eine produktive API benötigt weiterhin
eine gesonderte Berechtigungs-/Datenschutzarchitektur und geprüfte Betriebsfreigabe.

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


## Opt-in-Prüfmodus: Authentifizierungsgrenze

Der lokale synthetische Prüfmodus ist standardmäßig deaktiviert. Für die Aktivierung
müssen alle folgenden Werte extern konfiguriert sein:

- `SyntheticReview__Enabled=true`
- `SyntheticReview__BearerToken=<256-Bit-Wert als 43 Zeichen Base64url ohne Padding>`
- `SyntheticReview__ActorId=<serverseitige synthetische Actor-ID>`
- `ConnectionStrings__SyntheticReview=<PostgreSQL-Verbindungszeichenfolge>`

Bei fehlender oder schwacher Konfiguration startet der Prüfmodus nicht. Es gibt kein
eingebautes Kennwort und keinen Fallback-Actor. Das Bearer-Token darf nur über den
`Authorization: Bearer ...`-Header übertragen werden; Query-Parameter, Cookies,
localStorage und Request-Felder sind keine Identitätsquelle.

Solange dieser erste Slice von #138 noch keine Review-Mutation freigibt, dient
`GET /api/review-session` als absichtlich kleine Integrationsprobe. Der Endpunkt
existiert nur im aktivierten Prüfmodus, verlangt ASP.NET-Authentifizierung und gibt
ausschließlich die serverseitig gebundene synthetische Actor-ID sowie technische
Modusangaben zurück. Ungültige oder fehlende Credentials liefern eine deutsche 401-
Antwort. Die bestehenden Loopback-, Origin-, CSP- und `no-store`-Grenzen gelten
unverändert.

Dieser lokale Credential-Mechanismus ist keine produktive Identitätslösung. Er bietet
insbesondere keine Organisationskonten, MFA, Rollenverwaltung oder fachliche
Freigabe. Die nächsten #138-Slices binden die authentifizierte Principal-Identität
an explizite fallbezogene Autorisierung, den PostgreSQL-Review-Aggregatstore und
begrenzte Accept/Override-Kommandos.
