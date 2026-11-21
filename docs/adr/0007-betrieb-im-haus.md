# 0007 – Betrieb im Haus, ohne CDN und externe Dienste

Status: angenommen · 21.11.2026

## Entscheidung
NormaCase läuft vollständig offline: keine CDNs, Webfonts, Telemetrie oder Cloud-Dienste zur Laufzeit. Auslieferung als Container-Images, lokal mit Docker Compose, produktiv mit Kubernetes-Manifesten. Caching nur in der Anwendung (unveränderliche Releases) und für gehashte statische Dateien.

## Folgen
- Assets wie das Astryx-Theme werden lokal eingebunden.
- Beobachtbarkeit über Prometheus/Grafana im Haus.
