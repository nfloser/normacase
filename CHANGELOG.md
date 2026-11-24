# Changelog

## 1.0.0 – 2026-11-24

Erste vollständige Fassung auf dem Pflicht-Stack (Angular, Spring Boot, PostgreSQL) mit dem Funktionsumfang des bisherigen NormaCase.

**Plattform**
- Deterministische Regel-Engine, 118 Paritätsfälle exakt wie die frühere .NET-Engine
- Versionierte Knowledge Packs mit Prüfsumme; öffentliche Referenzregelwerke als `PUBLIC_REFERENCE` gekennzeichnet
- Eingang über `POST /api/v1/intake` mit Idempotenz, nachsichtiger Modus (`Prefer: handling=lenient`) für technische Klärung
- Entscheidungsnachweis ohne Fallinhalt, Nachprüfung durch erneute Einreichung
- Zero Retention: Fallinhalte, Identifikationsdaten, Dokumente und Notizen werden bei der Quittung gelöscht
- Vier-Augen-Freigabe, Rollen über Keycloak (OIDC), Problem Details nach RFC 9457, OpenAPI-Vertrag mit Vertragstest

**Prüfwerkstatt (Oberfläche)**
- Arbeitslisten: Zur Freigabe vorbereitet, Informationen nachfordern, Gegenprüfung erforderlich, Technische Klärung, Unterlagensichtung, Bestätigt, Versendet
- Fallliste mit Zeilenfärbung, Spaltenoptionen, Filtern, Sortierung, gespeicherten Abfragen, Mehrfachauswahl und Sammelbestätigung
- Vorschau unten oder rechts, Vollansicht mit Dokumentenleser, Entscheidungsweg, Verlauf und Nachweis
- Meine Ablage: Ordner, Lesezeichen, Farbmarkierung, Notizen, Arbeitslistenfarben, Rechtsklick-Menü, Versandpaket
- Extras: Prüfwerkstatt, Referenzfälle, Massenfälle, Nachweise
- Hell, dunkel oder wie das System; Tastaturbedienung; Barrierefreiheit mit axe geprüft

**Betrieb**
- Start mit einem Befehl (`demo.ps1` / `demo.cmd`), Docker Compose, Kubernetes-Manifeste
- Prometheus und Grafana optional, k6-Lasttest in der CI
