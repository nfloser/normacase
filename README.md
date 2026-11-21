# NormaCase

Deterministische, nachvollziehbare Entscheidungsunterstützung für regelbasierte sozialmedizinische Begutachtung.

NormaCase prüft strukturierte Falldaten gegen versionierte Regelwerke (Knowledge Packs) und liefert ein Ergebnis mit vollständigem Entscheidungspfad: Eingabe → Kriterium → Regel → Quelle → Ergebnis. Fehlt etwas, sagt das System das, statt zu raten. Gedacht ist es als Baustein neben einem führenden Fachsystem (etwa MDconnect oder MEDIKOS): Der Fall kommt herein, wird bewertet und geht mit Begründung zurück. Gesundheitsdaten bleiben im führenden System.

> Alle Daten in diesem Repository sind synthetisch. Es gibt keine echten Patienten- oder Versichertendaten.

## Stand

Das Projekt wird mit **Angular/TypeScript/RxJS**, **Java/Spring Boot** und **PostgreSQL** neu aufgebaut. Der bisherige .NET/React-Stand liegt unverändert unter [`legacy/`](legacy/README.md) und dient als Vergleich und als Prüforakel für die neue Regel-Engine.

| Phase | Inhalt | Status |
|---|---|---|
| 1 | System Design, Architektur, Datenschutzkonzept, ADRs | erledigt |
| 2 | Lauffähiges Grundgerüst: Spring Boot, PostgreSQL/Flyway, Angular-Shell, CI, Compose | erledigt |
| 3 | Knowledge Packs und Regel-Engine in Java, Parität zum Referenzstand (118 Fälle exakt gleich) | erledigt |
| 4 | Arbeitskörbe und Fallakte (Postfach × Explorer, Dokumentleser, Vier-Augen-Freigabe, Löschung nach Rückgabe) | erledigt |
| 5 | Rate Limiting, Metriken und Alarme, Kubernetes, Lasttest, TR-03161-Checkliste | erledigt |

Details: [Roadmap](docs/ROADMAP.md).

## Lokal starten

Voraussetzung ist Docker (unter Windows Docker Desktop).

```bash
docker compose up --build
```

`docker compose up --build` startet PostgreSQL, Keycloak, Backend, Oberfläche und ein synthetisches Fachsystem, das Beispielvorgänge einliefert. Danach ist die Prüfwerkstatt unter <http://localhost:4200> erreichbar. Anmelden etwa mit `gutachterin.a` / `demo`; alle synthetischen Konten stehen in [ops/keycloak](ops/keycloak/README.md). Entwicklung ohne Container: [backend/](backend/README.md) und [frontend/](frontend/README.md).

## So arbeitet NormaCase

1. Ein Fachsystem liefert einen Vorgang über `POST /api/v1/intake` ein (oder bewertet nur, ohne Vorgang, über `POST /api/v1/assessments`).
2. Die Regel-Engine bewertet deterministisch gegen das angegebene Knowledge Release und schreibt einen Nachweis ohne Fallinhalt.
3. Unvollständige oder unklare Fälle landen in „Rückfragen“, eindeutige in „Zur Prüfung“.
4. Eine Gutachterin bestätigt, eine zweite Person gibt frei.
5. Das Fachsystem holt das Ergebnis ab und quittiert; NormaCase löscht Fall, Identifikationsdaten und Dokumente sofort.

Vertrag: [`openapi.yaml`](backend/src/main/resources/static/api/v1/openapi.yaml) (zur Laufzeit unter `/api/v1/openapi.yaml`).

## Dokumentation

- [System Design](docs/01-system-design.md) – Zweck, Rollen, Anwendungsfälle, Domänenmodell, Qualitätsziele
- [Architektur](docs/02-architektur.md) – Module, Schnittstellen, Datenfluss, Technik
- [Datenschutz und Datenhaltung](docs/03-datenschutz-und-datenhaltung.md) – Verarbeitung ohne Vorhaltung, Zuordnung zu SDM, BSI TR-03161, SGB V
- [Qualität und Betrieb](docs/04-qualitaet-und-betrieb.md) – Auth, Sicherheit, Rate Limiting, Caching, Logs, Monitoring, Tests, Skalierung, Hosting, CI/CD
- [Architekturentscheidungen (ADRs)](docs/adr/)
- [TR-03161-Checkliste](docs/sicherheit/tr-03161-checkliste.md)
- [Betrieb: Kubernetes](ops/k8s/), [Monitoring](ops/monitoring/), [Lasttest](ops/load/)
- [Entwicklungsregeln](AGENTS.md)
