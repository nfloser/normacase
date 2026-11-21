# 0008 – Teststrategie

Status: angenommen · 21.11.2026

## Entscheidung
Tests folgen dem Risiko: Die Regel-Engine bekommt Unit-, Eigenschafts- und Paritätstests; Modulgrenzen werden mit Spring Modulith und ArchUnit geprüft; Persistenz läuft gegen echtes PostgreSQL per Testcontainers; die Oberfläche wird mit Playwright und axe geprüft; Lasttests mit k6 ab Phase 5. Datenschutzzusagen bekommen eigene Tests (Löschung, inhaltsfreies Audit, Logs).

## Folgen
- Keine Mocks für die Datenbank in Integrationstests.
- Grüne Tests sind notwendig, aber kein Beweis fachlicher Richtigkeit.
