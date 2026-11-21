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
| 3 | Knowledge Packs und Regel-Engine in Java, Parität zum Referenzstand | als Nächstes |
| 4 | Arbeitskörbe und Fallakte (Postfach × Explorer, PDF-Leser, Freigabe) | geplant |
| 5 | Adapter, fallbezogene Rechte, Monitoring, Lasttests, Kubernetes | geplant |

Details: [Roadmap](docs/ROADMAP.md).

## Lokal starten

Voraussetzung ist Docker (unter Windows Docker Desktop).

```bash
docker compose up --build
```

Danach ist die Prüfwerkstatt unter <http://localhost:4200> erreichbar. Anmelden etwa mit `gutachterin.a` / `demo`; alle synthetischen Konten stehen in [ops/keycloak](ops/keycloak/README.md). Entwicklung ohne Container: [backend/](backend/README.md) und [frontend/](frontend/README.md).

## Dokumentation

- [System Design](docs/01-system-design.md) – Zweck, Rollen, Anwendungsfälle, Domänenmodell, Qualitätsziele
- [Architektur](docs/02-architektur.md) – Module, Schnittstellen, Datenfluss, Technik
- [Datenschutz und Datenhaltung](docs/03-datenschutz-und-datenhaltung.md) – Verarbeitung ohne Vorhaltung, Zuordnung zu SDM, BSI TR-03161, SGB V
- [Qualität und Betrieb](docs/04-qualitaet-und-betrieb.md) – Auth, Sicherheit, Rate Limiting, Caching, Logs, Monitoring, Tests, Skalierung, Hosting, CI/CD
- [Architekturentscheidungen (ADRs)](docs/adr/)
- [Entwicklungsregeln](AGENTS.md)
