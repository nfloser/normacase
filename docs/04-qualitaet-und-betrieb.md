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
- Abhängigkeiten: Dependabot und CycloneDX-SBOM (Backend und Frontend) als CI-Artefakt. Eine Schwachstellenprüfung der SBOM im Haus (etwa mit Dependency-Track) ist der nächste Schritt.
- Geheimnisse nur über Umgebung oder Kubernetes Secrets, nie im Repository.
- Bedrohungsmodell aus `legacy/docs/security/THREAT_MODEL.md` wird für die neue Architektur fortgeschrieben.

## Rate Limiting

- Eigener Token-Bucket je Konto im Backend (`RateLimiter`, ohne Fremdbibliothek, exakt testbar) für Einlieferung, Bewertung und Nachvollziehen. Grenze über `NORMACASE_RATE_LIMIT_PER_MINUTE` (Standard 120). Bei Überschreitung 429 mit `Retry-After` als Problem Details (`OperationsIntegrationTest`).
- Anmeldeversuche begrenzt der IdP.
- Im Kubernetes-Betrieb zusätzlich Begrenzung am Ingress; die Anwendung verlässt sich aber nicht darauf.

## Caching (kein CDN)

- Ein CDN entfällt bewusst: Betrieb im Haus, ohne Internet, und Gesundheitsdaten dürfen nicht in fremde Zwischenspeicher.
- Statische Angular-Dateien mit Hash im Namen werden lange gecacht, `index.html` gar nicht.
- Knowledge Releases sind unveränderlich und werden beim Start einmal geladen und im Speicher gehalten; Schlüssel ist die Release-ID. Eine Invalidierung ist nicht nötig, weil sich ein Release nie ändert (das Release-Register verhindert es).
- API-Antworten mit Fallbezug: `Cache-Control: no-store`.

## Fehlerverfolgung und Logs

- Strukturierte JSON-Logs (ECS) mit Korrelations-ID: nginx setzt `X-Request-Id`, das Backend übernimmt sie oder erzeugt eine und schreibt sie in jeden Logeintrag und in die Antwort.
- Positivliste statt Schwärzung: Geloggt werden technische IDs, Regel-IDs, Fehlercodes, Dauer. Fallinhalte nie.
- Fehler gehen als Problem Details an den Aufrufer und mit Stacktrace nur ins Log. Kein externer Fehlerdienst; ein im Haus betriebener (etwa GlitchTip) ist anschließbar.
- Nachweis: `LogRedactionTest` (siehe Datenschutzkonzept).

## Monitoring und Alarme

- Spring Boot Actuator: Liveness, Readiness (inkl. Datenbank und geladenes Release), Info mit Plattform- und Release-Version.
- Micrometer mit Prometheus-Endpunkt. Fachliche Kennzahlen: Bewertungen je Ergebnis, offene Vorgänge je Korb, Alter des ältesten Vorgangs, fehlgeschlagene Rückgaben.
- Dashboard und Alarmregeln liegen unter `ops/monitoring` (`docker compose --profile monitoring up`): Backend nicht erreichbar, Fehlerquote, Bewertung p95 > 200 ms, überschrittene Löschfrist, nicht abgeholte Ergebnisse, alte offene Vorgänge.
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
| Last | k6 (`ops/load`) | p95 < 200 ms bei 40 Bewertungen/s im CI |
| Ende-zu-Ende | Playwright gegen den Compose-Stack | Anmeldung, Prüfung, Vier-Augen-Freigabe, Abholung, Löschung; axe |

Abdeckung mit JaCoCo; ein Mindestwert gilt für `assessment` und `knowledge`, nicht als Selbstzweck für alles.

## Skalierung

- Der Bewertungskern ist zustandslos und horizontal skalierbar; Zustand liegt in PostgreSQL.
- Der Löschlauf sperrt Zeilen mit `for update skip locked`; mehrere Instanzen können ihn gleichzeitig ausführen, ohne sich zu stören. Einlieferungen sind über `pg_advisory_xact_lock` und den Idempotency-Key gegen doppelte Verarbeitung geschützt.
- Erst wenn Messungen es zeigen: Lesereplikat, getrennter Bewertungsdienst oder Nachrichtenkanal. Der modulare Monolith lässt diese Schnitte zu, ohne sie vorwegzunehmen.

## Hosting

- Ziel ist der Betrieb im Rechenzentrum des Hauses. Keine Abhängigkeit von einem Cloud-Anbieter.
- Lokal: `docker compose up` startet PostgreSQL, Keycloak, Backend und Frontend mit synthetischen Daten.
- Kubernetes: `ops/k8s` (Kustomize) mit Probes, Ressourcenlimits, PodSecurity `restricted`, NetworkPolicies mit Standard-Verbot, HPA und PDB, nicht-root, schreibgeschütztes Dateisystem. Regelwerke kommen als eigenes Image (`knowledge/Dockerfile`) über einen Init-Container. Die CI rendert und validiert die Manifeste mit kubeconform.
- Container-Images: schlanke Basis, reproduzierbar gebaut, mit SBOM.

## CI/CD und Versionsverwaltung

- Ablauf je Änderung: Issue → Branch → Pull Request → CI → Review → Squash-Merge nach `main`.
- CI (GitHub Actions): Dokumentation, Paritäts-Orakel, Backend (Tests mit Testcontainers, Architekturtests, JaCoCo-Grenze, SBOM), Frontend (ESLint, Vitest, Build, SBOM, keine externen Quellen), Kubernetes-Validierung, Compose-Stack mit Smoke-Test, Playwright und k6. Dependabot hält Abhängigkeiten aktuell.
- Versionen nach SemVer; Plattformversion und Knowledge-Release-Version sind getrennt.
- Lieferung: Container-Images als Release-Artefakt; Infrastruktur als Code im Repository.
