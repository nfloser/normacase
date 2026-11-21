# Roadmap

Jede Phase endet mit lauffähigem, getestetem Stand und aktualisierter Dokumentation. Reihenfolge folgt dem kritischen Pfad: Eingang → Bewertung → Arbeitskorb → Freigabe → Rückgabe.

## Phase 1 – Konzept (erledigt)
System Design, Architektur, Datenschutz und Datenhaltung, Qualität und Betrieb, ADRs 0001–0008. Altstand nach `legacy/`.

## Phase 2 – Grundgerüst
- `backend/`: Spring Boot, Module als Pakete mit Spring-Modulith-Test, Flyway-Schemas je Modul, Testcontainers, Actuator, strukturierte Logs, Problem Details, OpenAPI-Vertrag
- `frontend/`: Angular-Shell mit Astryx-Theme lokal, de-DE-Ressourcen, Anmeldung per OIDC
- `compose.yaml`: PostgreSQL, Keycloak mit Testrealm, Backend, Frontend
- CI: Build, Tests, Linting, Container-Build, SBOM
- Abnahme: `docker compose up`, anmelden, Health grün, leerer Arbeitskorb sichtbar

## Phase 3 – Regelwerk und Bewertung
- Knowledge-Pack-Format übernehmen, Releases laden und prüfen
- Regel-Engine in Java als reine Bibliothek
- Paritätstests gegen Golden Files aus `legacy/`
- `POST /api/v1/assessments` (Durchreichen), Entscheidungsnachweis

## Phase 4 – Arbeitskorb und Fallakte
- Befristete Vorgänge, Körbe, Bestätigen, Vier-Augen-Freigabe
- Prüfwerkstatt: Postfach × Explorer, Fallakte, PDF-Leser, Tastaturbedienung
- Rückgabe mit Quittung und Löschung; `RetentionTest`

## Phase 5 – Härtung und Betrieb
- Generischer Adapter-Vertrag mit zwei synthetischen Adaptern
- Fallbezogene Rechte vollständig, TR-03161-Checkliste
- Rate Limiting, Metriken, Dashboards, Alarme
- Kubernetes-Manifeste, k6-Lasttest
