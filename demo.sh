#!/bin/sh
# NormaCase-Demo für macOS und Linux. Voraussetzung: Docker Desktop (oder Docker Engine mit Compose).
# Aufruf: sh demo.sh            Start
#         sh demo.sh --reset    alles löschen und neu starten
#         sh demo.sh --stop     beenden
set -e
cd "$(dirname "$0")"
url=http://localhost:4200
fail() { echo "FEHLER: $1" >&2; exit 1; }

command -v docker >/dev/null 2>&1 || fail "Docker nicht gefunden. Bitte Docker Desktop installieren: https://www.docker.com/products/docker-desktop/"
if ! docker info >/dev/null 2>&1; then
  echo "Starte Docker Desktop …"
  [ "$(uname)" = Darwin ] && open -a Docker 2>/dev/null || true
  i=0; until docker info >/dev/null 2>&1; do i=$((i+1)); [ $i -gt 90 ] && fail "Docker läuft nicht. Docker Desktop öffnen und erneut versuchen."; sleep 2; done
fi
case "$1" in
  --stop) docker compose down; exit 0 ;;
  --reset) docker compose down -v ;;
esac

echo "Baue und starte NormaCase (beim ersten Mal 5–10 Minuten) …"
docker compose up --build -d || fail "Start fehlgeschlagen. Details: docker compose logs"
echo "Warte auf die Anwendung …"
i=0; until curl -fs "$url/api/v1/system" >/dev/null 2>&1; do
  i=$((i+1)); [ $i -gt 300 ] && fail "Nicht rechtzeitig bereit. Details: docker compose logs backend"; sleep 2; done
i=0; until docker compose ps -a --format '{{.State}}' demo-host 2>/dev/null | grep -q exited; do
  i=$((i+1)); [ $i -gt 90 ] && fail "Demo-Vorgänge nicht eingeliefert. Details: docker compose logs demo-host"; sleep 2; done
echo
echo "NormaCase läuft: $url"
echo "Anmelden: tobias.flöser / demo   (Freigabe: freigabe.c / demo)"
echo "Beenden:  sh demo.sh --stop"
[ "$(uname)" = Darwin ] && open "$url" || (command -v xdg-open >/dev/null && xdg-open "$url") || true
