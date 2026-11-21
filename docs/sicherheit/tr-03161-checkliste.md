# Checkliste nach BSI TR-03161 (Teil 2 und 3)

Stand: 21.11.2026 · Arbeitsliste, keine Prüfbescheinigung. Die Zuordnung folgt den Prüfbereichen der Richtlinie; die einzelnen Prüfaspekte sind vor einer echten Prüfung gegen die jeweils gültige Fassung abzugleichen. Spalte „Nachweis“ nennt Test, Konfiguration oder Dokument im Repository.

| Prüfbereich | Umsetzung | Nachweis | Stand |
|---|---|---|---|
| Anwendungszweck | Zweck und Systemgrenze beschrieben; Verarbeitung nur für die Bewertung, keine Nebenzwecke, keine Telemetrie | `docs/01-system-design.md`, `docs/03-datenschutz-und-datenhaltung.md` | erfüllt |
| Architektur | Modularer Monolith mit erzwungenen Modulgrenzen; Fallinhalte nur im Modul `workflow`, befristet | `ModularityTest`, `docs/02-architektur.md` | erfüllt |
| Architektur – Datenminimierung | Durchreichen als Standard, Nachweis ohne Inhaltsspalten, Löschung nach Quittung und Frist | `AssessmentApiIntegrationTest.auditTableHasNoColumnsForCaseContent`, `WorkflowIntegrationTest` | erfüllt |
| Quellcode | Strikte Eingabeprüfung (unbekannte/doppelte Eigenschaften, Tiefe, Größe), parametrisierte SQL-Abfragen, keine dynamische Codeausführung in Regeln | `StrictJson`, `KnowledgePackValidatorTest`, `RuleEngineTest` | erfüllt |
| Quellcode – Fehler | Problem Details ohne Stacktrace und ohne Fallinhalte | `SystemIntegrationTest`, `server.error.*` in `application.yaml` | erfüllt |
| Drittanbieter-Software | Abhängigkeiten über Dependabot aktuell halten, SBOM (CycloneDX) je Build | `.github/dependabot.yml`, CI-Artefakte `sbom-*` | erfüllt |
| Kryptographische Umsetzung | TLS am Ingress; Integrität über SHA-256 (Eingabe, Regelwerk, Dokumente); Signaturprüfung der Tokens im Backend | `ops/k8s/base/ingress.yaml`, `AssessmentService.inputSha256`, Resource-Server-Konfiguration | erfüllt (TLS-Konfiguration je Haus) |
| Zufallszahlen | Kennungen über `UUID.randomUUID()` (kryptographisch sicherer Zufall), PKCE-Werte über `crypto.getRandomValues` | `pkce.ts`, `auth.service.spec.ts` | erfüllt |
| Authentisierung | OIDC, Authorization Code + PKCE, Prüfung von Aussteller, Audience, Signatur; kurze Token-Laufzeit, Refresh, serverseitige Abmeldung beim IdP | `AuthenticationIntegrationTest`, `ops/keycloak`, `e2e/login.spec.ts` | erfüllt |
| Autorisierung | Rollen je Aktion, Vier-Augen-Prinzip technisch erzwungen, Fallakte ohne Rolle nicht unterscheidbar (404) | `WorkflowIntegrationTest.processesAreInvisibleWithoutAWorkbenchRole`, `…FourEyes…` | erfüllt |
| Passwörter | Keine eigene Passwortverwaltung; Richtlinien und Sperren beim IdP (Brute-Force-Schutz im Testrealm aktiv) | `ops/keycloak/normacase-realm.json` | beim IdP des Hauses |
| Datenspeicherung und Datenschutz | Identifikationsdaten getrennt, Dokumente mit Typprüfung, Nachweis nur anfügend (Trigger), keine Fallwerte in Logs und Metriken | `V2`/`V3`-Migrationen, `AssessmentApiIntegrationTest.caseValuesNeverReachTheLog`, `OperationsIntegrationTest` | erfüllt |
| Datenspeicherung – Verschlüsselung ruhender Daten | Verschlüsselung von Datenbank und Backups auf Ebene der Infrastruktur | Betriebshandbuch des Hauses | offen (Infrastruktur) |
| Kostenpflichtige Ressourcen | Keine | – | nicht anwendbar |
| Netzwerkkommunikation | Nur eigene Herkunft plus IdP in der CSP, keine CDNs; NetworkPolicies mit Standard-Verbot; Actuator nicht über Ingress | `nginx.conf.template`, `ops/k8s/base/network-policies.yaml`, `ingress.yaml` | erfüllt |
| Plattformspezifische Interaktionen | Sicherheitsheader (CSP, `nosniff`, `frame-ancestors 'none'`, Referrer-Policy), Dokumente mit `sandbox`-CSP, Tokens nicht im Browser-Speicher | `nginx.conf.template`, `WorkbenchController`, `auth.service.ts` | erfüllt |
| Resilienz | Rate Limiting je Konto (429 + Retry-After), Health-/Readiness-Probes, mehrere Instanzen, Löschjob mit Zeilensperren, Alarme | `OperationsIntegrationTest`, `ops/k8s/base/backend.yaml`, `ops/monitoring/alerts.yml` | erfüllt |
| Protokollierung | Strukturierte Logs (ECS) mit technischen Kennungen; Nachweis jeder Bearbeitung in `audit.process_evidence` | `application.yaml`, `ProcessEvidenceRecorder` | erfüllt |

## Offene Punkte für einen echten Betrieb

- Penetrationstest und Prüfung durch eine anerkannte Stelle
- TLS- und Schlüsselverwaltung, Backup-Verschlüsselung und Wiederherstellungstests im Rechenzentrum des Hauses
- Abgleich der Prüfaspekte mit der aktuell gültigen Fassung der TR-03161 und dem Prüfschema des Hauses
