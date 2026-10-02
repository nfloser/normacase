# Lokale HTTP-Schnittstelle

Die lokale HTTP-Schnittstelle ist ein **Entwicklungsadapter für synthetische Daten**. Sie ist kein produktiver Patientendaten-Endpunkt und besitzt bewusst noch keine Authentifizierung oder Persistenz.

## Start

Voraussetzung: .NET 10 SDK.

Vom Repository-Root:

```bash
dotnet run --project src/NormaCase.Api --   --knowledge-root knowledge   --platform-version development   --port 5099
```

Der Prozess bindet ausschließlich an:

```text
127.0.0.1:5099
```

Ein anderer Port kann mit `--port` gesetzt werden.

## Sicherheitsgrenzen

Der Entwicklungsdienst:

- bindet ausschließlich an IPv4-Loopback,
- akzeptiert nur lokale `Host`-Werte,
- akzeptiert einen `Origin` nur für `http`/`https` auf Loopback,
- aktiviert kein CORS und keine Forwarded Headers,
- führt keine Runtime-Netzwerkabfragen durch,
- persistiert keine Fälle,
- protokolliert keine Request-Bodies,
- begrenzt JSON-Requests auf 8 MiB,
- akzeptiert für Prüfungen nur `application/json` in UTF-8,
- liefert `Cache-Control: no-store` und `X-Content-Type-Options: nosniff`,
- gibt bei Eingabefehlern keine Fallwerte, Dateipfade oder Exceptiontexte zurück.

Nur Knowledge Packs mit `validationLevel = SYNTHETIC` werden in den lokalen Katalog aufgenommen.

## Knowledge-Pack-Katalog

```http
GET /api/v1/packs
```

Die Antwort enthält pro synthetischem Pack:

- technische Pack-ID,
- Knowledge Release,
- Lifecycle- und Validation-Level,
- deklarierte Felder mit Typ/Pflichtstatus,
- deklarierte Evidence-Requirements.

Beispiel-ID:

```text
synthetic.demo-a
```

## Prüfung ausführen

```http
POST /api/v1/packs/{packId}/assessments
Content-Type: application/json
```

Der Request verwendet exakt den versionierten `CaseInput`-Vertrag der Offline-CLI.

Beispiel:

```json
{
  "formatVersion": 1,
  "assessmentDate": "2026-10-02",
  "facts": {
    "criterion_a": { "kind": "TRUTH", "truth": "YES" },
    "criterion_b": { "kind": "TRUTH", "truth": "YES" },
    "criterion_c": { "kind": "TRUTH", "truth": "NO" }
  }
}
```

Beispielaufruf:

```bash
curl --fail-with-body   -H "Content-Type: application/json"   --data @case.json   http://127.0.0.1:5099/api/v1/packs/synthetic.demo-a/assessments
```

Die erfolgreiche Antwort ist exakt das versionierte, verlustfreie Assessment-Dokument aus `NormaCase.Serialization`. Die über `--platform-version` gesetzte Plattformidentität wird mit ausgegeben.

Das Prüfdatum muss im Request stehen. Der Dienst erfindet kein "heute".

## Statuscodes

- `200`: Katalog oder ausgeführte Prüfung
- `400`: ungültige strukturierte Eingabe
- `403`: nicht-lokaler Host/Origin
- `404`: synthetisches Pack nicht verfügbar
- `413`: Request zu groß
- `415`: nicht unterstützter Medientyp

Fehlermeldungen sind deutsch und absichtlich generisch.

## Tests

```bash
dotnet test tests/NormaCase.Api.Tests/NormaCase.Api.Tests.csproj --configuration Release
```

Die Integrationstests starten einen echten Kestrel-Listener auf einem dynamischen Loopback-Port und prüfen unter anderem API/Engine-Parität, Host-/Origin-Abweisung, Größenlimit und Fehlerdaten-Leaks.

## Grenzen

Dieser Adapter ist bewusst noch **nicht**:

- produktionsreif für echte Patientendaten,
- authentifiziert oder autorisiert,
- persistent,
- über das Netzwerk freigegeben,
- ein Ersatz für fachliche Freigabe oder menschliche Verantwortung.

Eine spätere UI soll dieselbe lokale Schnittstelle konsumieren, ohne Entscheidungslogik zu duplizieren.
