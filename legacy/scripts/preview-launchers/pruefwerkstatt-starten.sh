#!/bin/sh
# Synthetic local preview only. Starts the loopback host and opens the browser once it is ready.
set -eu
root=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
url="http://localhost:5080/angular-pilot/index.html"
health="http://localhost:5080/health/live"

if [ ! -f "$root/api/NormaCase.Api" ]; then
  printf '%s\n' "Die Programmdateien wurden nicht gefunden." \
    "Bitte das ZIP vollständig entpacken und dieses Skript im entpackten Ordner starten."
  exit 1
fi
if [ ! -x "$root/api/NormaCase.Api" ]; then
  printf '%s\n' "Das Programm ist noch nicht ausführbar. Einmalig im Paketordner ausführen:" \
    "  chmod +x pruefwerkstatt-starten.sh api/NormaCase.Api cli/NormaCase.Cli"
  exit 1
fi

open_browser() {
  [ -n "${NORMACASE_NO_BROWSER:-}" ] && return 0
  if command -v open >/dev/null 2>&1; then open "$url" >/dev/null 2>&1 || true
  elif command -v xdg-open >/dev/null 2>&1; then xdg-open "$url" >/dev/null 2>&1 || true
  else printf '%s\n' "Bitte im Browser öffnen: $url"
  fi
}
ready() { command -v curl >/dev/null 2>&1 && curl -fs --max-time 1 -o /dev/null "$health"; }

if ready; then
  printf '%s\n' "NormaCase läuft bereits: $url"
  open_browser
  exit 0
fi

printf '%s\n' "NormaCase - synthetische Prüfwerkstatt" "" \
  "Der lokale Dienst startet. Der Browser öffnet sich gleich automatisch:" "  $url" \
  "Bisherige Prüfwerkstatt: http://localhost:5080/" "" \
  "Dieses Fenster bitte geöffnet lassen. Beenden: Strg+C." "Nur synthetische Daten verwenden." ""
if command -v curl >/dev/null 2>&1; then
  (
    attempt=0
    while [ "$attempt" -lt 90 ]; do
      if ready; then open_browser; exit 0; fi
      attempt=$((attempt + 1)); sleep 1
    done
  ) &
else
  printf '%s\n' "Nach dem Start im Browser öffnen: $url"
fi
cd "$root/api"
exec ./NormaCase.Api
