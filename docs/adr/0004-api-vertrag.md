# 0004 – OpenAPI-Vertrag und Problem Details

Status: angenommen · 21.11.2026

## Entscheidung
Die REST-API ist unter `/api/v1` versioniert und als OpenAPI-Datei im Repository beschrieben. Fehler folgen RFC 9457 mit stabilen `type`-URIs. Einlieferungen sind über `Idempotency-Key` idempotent. Headless-Zugriff und Prüfwerkstatt nutzen dieselben Endpunkte.

## Folgen
- Fachsysteme können gegen den Vertrag entwickeln, bevor die Oberfläche fertig ist.
- Ein Test gleicht Vertrag und Implementierung ab.
