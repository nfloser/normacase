# Roadmap

Jede Phase endet mit lauffähigem, getestetem Stand und aktualisierter Dokumentation. Reihenfolge folgt dem kritischen Pfad: Eingang → Bewertung → Arbeitskorb → Freigabe → Rückgabe.

## Phase 1 – Konzept (erledigt)
System Design, Architektur, Datenschutz und Datenhaltung, Qualität und Betrieb, ADRs 0001–0008. Altstand nach `legacy/`.

## Phase 2 – Grundgerüst (erledigt: #361, #363, #365)
- `backend/`: Spring Boot, Module als Pakete mit Spring-Modulith-Test, Flyway-Schemas je Modul, Testcontainers, Actuator, strukturierte Logs, Problem Details, OpenAPI-Vertrag
- `frontend/`: Angular-Shell mit Astryx-Theme lokal, de-DE-Ressourcen, Anmeldung per OIDC
- `compose.yaml`: PostgreSQL, Keycloak mit Testrealm, Backend, Frontend
- CI: Build, Tests, Linting, Container-Build, SBOM
- Abnahme: `docker compose up`, anmelden, Health grün, leerer Arbeitskorb sichtbar

## Phase 3 – Regelwerk und Bewertung (erledigt: #367, #369)
- Knowledge-Pack-Format übernehmen, Releases laden und prüfen
- Regel-Engine in Java als reine Bibliothek
- Paritätstests gegen Golden Files aus `legacy/`
- `POST /api/v1/assessments` (Durchreichen), Entscheidungsnachweis

## Phase 4 – Arbeitskorb und Fallakte (erledigt: #371, #373)
- Befristete Vorgänge, Körbe, Bestätigen, Vier-Augen-Freigabe
- Prüfwerkstatt: Postfach × Explorer, Fallakte, PDF-Leser, Tastaturbedienung
- Rückgabe mit Quittung und Löschung; `RetentionTest`

## Phase 5 – Härtung und Betrieb (erledigt: #373)
- Generischer Eingangsvertrag (`/api/v1/intake`) mit synthetischem Fachsystem; konkrete Adapter erst mit verbindlicher Spezifikation
- Fallbezogene Rechte vollständig, TR-03161-Checkliste
- Rate Limiting, Metriken, Dashboards, Alarme
- Kubernetes-Manifeste, k6-Lasttest

## Danach

- Verbindliche Schnittstellenbeschreibung eines Fachsystems einholen und den ersten echten Adapter hinter dem Eingangsvertrag bauen
- Regelwerk-Redaktion in der Oberfläche (Entwurf, Review, Aktivierung mit Vier-Augen-Prinzip)
- Asynchroner Eingang (Nachrichten) und Rückgabe per Push, wenn ein Fachsystem es verlangt
- Externe Sicherheitsprüfung
