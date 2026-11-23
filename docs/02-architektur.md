# Architektur

Stand: 21.11.2026 · Entscheidungen im Einzelnen: [ADRs](adr/)

## 1. Überblick

Ein modularer Monolith in Spring Boot, eine Angular-Oberfläche und eine PostgreSQL-Datenbank. Alles läuft in Containern, ohne Verbindung nach außen.

```
┌──────────────── Fachsystem (MDconnect, MEDIKOS, …) ────────────────┐
└───────┬──────────────────────────────────────────────▲────────────┘
        │ Fall (REST/JSON, später Nachricht)            │ Ergebnis + Entscheidungspfad
┌───────▼──────────────────────────────────────────────┴────────────┐
│ NormaCase Backend (Spring Boot, ein Deployable)                    │
│                                                                    │
│  intake ──▶ assessment ──▶ workflow ──▶ outbound                   │
│    │            │             │             │                      │
│    │        knowledge      documents      audit ◀── alle Module    │
│    └──────── identity (Querschnitt: Rollen, fallbezogene Rechte) ──┘
│                                                                    │
└───────┬──────────────────────────────┬────────────────────────────┘
        │ JDBC                          │ OIDC
┌───────▼────────┐             ┌────────▼────────┐      ┌───────────────────┐
│ PostgreSQL     │             │ Keycloak (lokal)│      │ Angular-SPA       │
│ (Schemas je    │             │ oder Haus-IdP   │◀─────│ Prüfwerkstatt     │
│  Modul)        │             └─────────────────┘      └───────────────────┘
└────────────────┘
```

## 2. Module

Jedes Modul ist ein Java-Paket unter `de.normacase.<modul>` mit öffentlicher API im Wurzelpaket und Interna in `internal`. Spring Modulith prüft im Test, dass kein Modul in die Interna eines anderen greift und keine Zyklen entstehen. Module sprechen über ihre API oder über Anwendungsereignisse miteinander.

| Modul | Verantwortung | Eigene Daten |
|---|---|---|
| `intake` | Annahme, Schemaprüfung, Idempotenz, Normalisierung in das kanonische Fallmodell; Adapter je Eingangsweg | Eingangsquittung (Nachrichten-ID, Hash, Zeitpunkt) |
| `knowledge` | Laden, Prüfen und Aktivieren von Knowledge Releases; Abfrage „welche Regel galt am Stichtag“ | Releases, Packs, Regeln, Quellen (keine Fallbezüge) |
| `assessment` | Deterministische Regel-Engine; reine Funktion `(Fall, Release, Stichtag) → Ergebnis` | keine |
| `workflow` | Bearbeitungszustand, Arbeitskörbe, Bestätigung, Vier-Augen-Freigabe | Vorgänge in Bearbeitung (befristet), Review-Ereignisse |
| `documents` | Dokumente eines Vorgangs für die Dauer der Bearbeitung bereitstellen (PDF-Leser) | befristeter Zwischenspeicher oder Durchreichen vom Fachsystem |
| `outbound` | Rückgabe an das Fachsystem, Wiederholung, Quittung | Ausgangsquittung |
| `audit` | Nachweis jeder fachlich relevanten Handlung, nur anfügend | Entscheidungsnachweise (ohne Fallinhalt) |
| `identity` | Abbildung der IdP-Rollen, Akteur, Sicherheitskonfiguration | keine |
| `system` | Plattformversion, Systemauskunft | keine |
| `common` | Striktes JSON, Problem Details, Uhr, Rate Limiting (offenes Modul ohne Fachbezug) | keine |

Die Regel-Engine (`assessment`) hängt weder von Spring noch von der Datenbank ab. Sie ist eine Bibliothek aus reinen Funktionen mit eigenen Unit- und Paritätstests.

## 3. Ablauf einer Bewertung

1. `intake` nimmt den Fall an, prüft Schema und Idempotenzschlüssel und erzeugt den kanonischen Fall. Der SHA-256-Hash der kanonischen Form wird festgehalten.
2. `knowledge` liefert das zum Stichtag gültige Release. Der Stichtag kommt aus dem Fall, nie aus der Systemuhr.
3. `assessment` bewertet und erzeugt Ergebnis und Entscheidungspfad.
4. `audit` schreibt den Entscheidungsnachweis: Vorgangs-ID, Eingabe-Hash, Plattform- und Release-Version, Regel-IDs, Ergebnis.
5. Eindeutig und nach Hausregel ohne Prüfung zulässig: direkt zu `outbound`. Sonst legt `workflow` einen befristeten Vorgang im passenden Korb an.

   Arbeitslisten (wie im bisherigen Fallexplorer): *Zur Freigabe vorbereitet* (eindeutig bewertet), *Informationen nachfordern* (`INCOMPLETE`), *Gegenprüfung erforderlich* (`HUMAN_REVIEW`), *Technische Klärung* (Fehler vor der Bewertung, nur mit `Prefer: handling=lenient` nach RFC 7240; sonst lehnt der Eingang ab) und *Unterlagensichtung* (Einlieferung ohne Regelwerk: nur Dokumente, keine Bewertung). Nach der Bestätigung folgen *Bestätigt* (wartet auf Freigabe) und *Versendet*.
6. Nach Freigabe gibt `outbound` das Ergebnis zurück. Mit der Quittung des Fachsystems löscht `workflow` den Fallinhalt; der Nachweis bleibt.

## 4. Schnittstellen

- **REST, versioniert** unter `/api/v1`. Vertrag als OpenAPI-Datei im Repository (`backend/src/main/resources/openapi/`), Abgleich zwischen Vertrag und Implementierung im Test.
- **Fehler** als RFC 9457 Problem Details mit stabilem `type` und Korrelations-ID; nie mit Fallinhalten.
- **Idempotenz** über `Idempotency-Key` beim Einliefern; gleiche Nachricht ergibt gleiche Antwort.
- **Headless und Prüfwerkstatt** nutzen dieselben Anwendungsfälle. Die Oberfläche bekommt keine eigene Fachlogik.
- **Asynchron** (Nachrichten) erst, wenn ein Fachsystem es verlangt; der Adapter-Rand ist dafür vorbereitet.

## 5. Frontend

Angular mit Standalone-Komponenten, Signals für Oberflächenzustand und RxJS für HTTP und Ereignisströme. Gestaltung nach dem bewährten Konzept der Prüfwerkstatt: links Körbe wie Ordner, in der Mitte die Fallliste wie ein Postfach, rechts die Fallakte mit Kriterien, Belegen und PDF-Leser. Das Astryx-Theme (MIT) wird lokal eingebunden, nicht per CDN. Texte liegen in Übersetzungsressourcen (de-DE).

Aus `legacy/angular-pilot` übernommen sind das exakte Dezimal-Parsing, die Darstellung des Entscheidungspfads und das Stylesheet. Die Anmeldung läuft über einen eigenen PKCE-Ablauf ohne Fremdbibliothek; Tokens liegen nur im Arbeitsspeicher.

## 6. Datenhaltung

PostgreSQL mit einem Schema je Modul. Migrationen laufen mit dem Eigentümerkonto, die Anwendung mit dem Laufzeitkonto `normacase_app`: nur Datenzugriff, keine Schemaänderung, Nachweise, Eingangsquittungen und Release-Register nur lesen und anfügen (`V4__runtime_role.sql`, `RuntimeRoleIntegrationTest`). Getrennte Konten je Modul würden getrennte Verbindungspools verlangen; das lohnt erst bei einer Aufteilung in mehrere Dienste. Migrationen mit Flyway, nur vorwärts. Fallinhalte liegen ausschließlich im Schema `workflow`, haben eine Ablaufzeit und werden nach Rückgabe gelöscht. Was dauerhaft bleibt, steht in [Datenschutz und Datenhaltung](03-datenschutz-und-datenhaltung.md).

## 7. Technik und Versionen

| Bereich | Wahl |
|---|---|
| Backend | Java 25 (LTS), Spring Boot 4.1, Spring Modulith 2.1, Spring Security (Resource Server), Jackson 3 |
| Persistenz | PostgreSQL 17, Flyway, `JdbcClient` (kein ORM: jede Spalte ist bewusst gewählt) |
| API | OpenAPI 3.1 als eingecheckte Datei, `OpenApiContractTest` gleicht sie mit den Endpunkten ab |
| Frontend | Angular 22, TypeScript strict, RxJS, Signals, Vitest, angular-eslint |
| Tests | JUnit 5, AssertJ, ArchUnit, Testcontainers, Playwright, axe |
| Betrieb | Docker Compose lokal, Kubernetes-Manifeste, Actuator, Micrometer/Prometheus |

Konkrete Versionsnummern stehen in `pom.xml` und `package.json` und werden dort gepflegt, nicht hier.
