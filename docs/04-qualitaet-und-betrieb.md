# Qualität und Betrieb

Stand: 21.11.2026 · Ein Abschnitt je Querschnittsthema. Jeder Abschnitt nennt die Entscheidung, den Grund und den Nachweis.

## Auth und Berechtigungen

- **Anmeldung** über OpenID Connect. Lokal und im CI ein Keycloak mit Testrealm und synthetischen Benutzern, im Haus der vorhandene Identitätsanbieter. Die SPA nutzt Authorization Code mit PKCE; das Backend ist OAuth2 Resource Server und prüft JWTs lokal.
- **Rollen** kommen aus dem IdP (siehe System Design, Abschnitt 3). Korbzuordnungen und fallbezogene Rechte führt das Modul `identity`.
- **Fallbezogene Prüfung** in jedem lesenden und schreibenden Anwendungsfall, nicht nur am Controller. Fehlendes Recht und nicht vorhandener Fall liefern dieselbe 404.
- **Technische Konten** für Fachsysteme mit Client Credentials, eingeschränkt auf ihre Schnittstelle.
- Nachweis: Spring-Security-Tests je Endpunkt und Rolle, Negativtests für fremde Fälle.

## Sicherheit

- Sichere Voreinstellungen: alles gesperrt, was nicht ausdrücklich freigegeben ist.
- HTTP-Header: strikte CSP ohne externe Quellen, `frame-ancestors 'none'`, HSTS hinter TLS, keine Server-Banner.
- Eingaben gegen JSON-Schema bzw. Bean Validation; Größenlimits für Anfragen und Dokumente.
- Abhängigkeiten: Dependabot, OWASP Dependency-Check bzw. `npm audit`, CycloneDX-SBOM als CI-Artefakt, CodeQL.
- Geheimnisse nur über Umgebung oder Kubernetes Secrets, nie im Repository.
- Bedrohungsmodell aus `legacy/docs/security/THREAT_MODEL.md` wird für die neue Architektur fortgeschrieben.

## Rate Limiting

- Bucket4j im Backend für Einlieferung (je technischem Konto) und für teure Abfragen. Bei Überschreitung 429 mit `Retry-After` als Problem Details.
- Anmeldeversuche begrenzt der IdP.
- Im Kubernetes-Betrieb zusätzlich Begrenzung am Ingress; die Anwendung verlässt sich aber nicht darauf.

## Caching (kein CDN)

- Ein CDN entfällt bewusst: Betrieb im Haus, ohne Internet, und Gesundheitsdaten dürfen nicht in fremde Zwischenspeicher.
- Statische Angular-Dateien mit Hash im Namen werden lange gecacht, `index.html` gar nicht.
- Knowledge Releases sind unveränderlich und werden im Backend mit Caffeine gehalten; Schlüssel ist die Release-ID.
- API-Antworten mit Fallbezug: `Cache-Control: no-store`.

## Fehlerverfolgung und Logs

- Strukturierte JSON-Logs (Spring Boot Structured Logging) mit Korrelations-ID aus `traceparent` bzw. eigener Kennung.
- Positivliste statt Schwärzung: Geloggt werden technische IDs, Regel-IDs, Fehlercodes, Dauer. Fallinhalte nie.
- Fehler gehen als Problem Details an den Aufrufer und mit Stacktrace nur ins Log. Kein externer Fehlerdienst; ein im Haus betriebener (etwa GlitchTip) ist anschließbar.
- Nachweis: `LogRedactionTest` (siehe Datenschutzkonzept).

## Monitoring und Alarme

- Spring Boot Actuator: Liveness, Readiness (inkl. Datenbank und geladenes Release), Info mit Plattform- und Release-Version.
- Micrometer mit Prometheus-Endpunkt. Fachliche Kennzahlen: Bewertungen je Ergebnis, offene Vorgänge je Korb, Alter des ältesten Vorgangs, fehlgeschlagene Rückgaben.
- Beispiel-Dashboards (Grafana) und Alarmregeln im Repository: Readiness rot, Rückgaben scheitern wiederholt, Vorgänge überschreiten ihre Ablaufzeit, Fehlerquote steigt.
- Metriken tragen nie Fall- oder Personenbezug als Label.

## Tests

| Ebene | Werkzeug | Zweck |
|---|---|---|
| Regel-Engine | JUnit, eigenschaftsbasierte Tests (jqwik) | Determinismus, `UNKNOWN` bleibt `UNKNOWN` |
| Parität | JUnit gegen Golden Files aus `legacy/` | gleiche Ergebnisse wie der Referenzstand |
| Architektur | Spring Modulith, ArchUnit | Modulgrenzen, keine Framework-Abhängigkeit im Kern |
| Persistenz | Testcontainers PostgreSQL | echte Migrationen, Rechte je DB-Rolle |
| API | Spring MockMvc/RestClient, OpenAPI-Abgleich | Vertrag und Fehlerformat |
| Frontend | Vitest/Jasmine, Playwright, axe | Komponenten, Abläufe, Barrierefreiheit |
| Last | k6 | p95-Ziel, Verhalten unter Rate Limit |

Abdeckung mit JaCoCo; ein Mindestwert gilt für `assessment` und `knowledge`, nicht als Selbstzweck für alles.

## Skalierung

- Der Bewertungskern ist zustandslos und horizontal skalierbar; Zustand liegt in PostgreSQL.
- Befristete Vorgänge und Ablaufjobs laufen mit Datenbanksperren (ShedLock), damit mehrere Instanzen sicher nebeneinander laufen.
- Erst wenn Messungen es zeigen: Lesereplikat, getrennter Bewertungsdienst oder Nachrichtenkanal. Der modulare Monolith lässt diese Schnitte zu, ohne sie vorwegzunehmen.

## Hosting

- Ziel ist der Betrieb im Rechenzentrum des Hauses. Keine Abhängigkeit von einem Cloud-Anbieter.
- Lokal: `docker compose up` startet PostgreSQL, Keycloak, Backend und Frontend mit synthetischen Daten.
- Kubernetes: Manifeste (Kustomize) mit Probes, Ressourcenlimits, NetworkPolicies, nicht-root, schreibgeschütztes Dateisystem.
- Container-Images: schlanke Basis, reproduzierbar gebaut, mit SBOM.

## CI/CD und Versionsverwaltung

- Ablauf je Änderung: Issue → Branch → Pull Request → CI → Review → Squash-Merge nach `main`.
- CI (GitHub Actions): Build und Tests Backend und Frontend, Architekturtests, Testcontainers, Linting (Checkstyle bzw. ESLint), Sicherheitsprüfungen, Container-Build, Dokumentationsprüfung.
- Versionen nach SemVer; Plattformversion und Knowledge-Release-Version sind getrennt.
- Lieferung: Container-Images als Release-Artefakt; Infrastruktur als Code im Repository.
