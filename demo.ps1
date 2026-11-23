<#
.SYNOPSIS
  Startet die NormaCase-Demo mit einem Befehl.

.DESCRIPTION
  Prüft Docker, baut und startet alle Dienste (PostgreSQL, Keycloak, Backend, Oberfläche),
  wartet, bis die zehn fiktiven Demo-Vorgänge eingeliefert sind, und öffnet die Prüfwerkstatt im Browser.
  Läuft unter Windows PowerShell 5.1 und PowerShell 7 (Windows, macOS, Linux).

.EXAMPLE
  .\demo.ps1            # starten (Stand bleibt erhalten)
  .\demo.ps1 -Reset     # alles auf Anfang: zehn unbearbeitete Vorgänge
  .\demo.ps1 -Stop      # beenden
#>
[CmdletBinding()]
param(
  # Datenbank zurücksetzen, damit alle Demo-Vorgänge wieder unbearbeitet sind.
  [switch]$Reset,
  # Demo beenden (Daten bleiben erhalten).
  [switch]$Stop,
  # Zusätzlich Prometheus und Grafana starten.
  [switch]$Monitoring,
  # Browser nicht automatisch öffnen.
  [switch]$NoBrowser,
  [int]$TimeoutMinutes = 20
)

# Docker schreibt Fortschritt auf stderr; das ist kein Fehler. Fehler werden über Exit-Codes geprüft.
$ErrorActionPreference = 'Continue'
Set-Location -LiteralPath $PSScriptRoot -ErrorAction Stop
$url = 'http://localhost:4200'

function Write-Step([string]$Text) { Write-Host "==> $Text" -ForegroundColor Cyan }
function Stop-WithError([string]$Text) {
  Write-Host ''
  Write-Host "FEHLER: $Text" -ForegroundColor Red
  exit 1
}
function Test-DockerRunning {
  & docker info *> $null
  return $LASTEXITCODE -eq 0
}

# 1. Docker vorhanden und gestartet?
if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
  Stop-WithError 'Docker wurde nicht gefunden. Bitte Docker Desktop installieren: https://www.docker.com/products/docker-desktop/'
}
if (-not (Test-DockerRunning)) {
  $desktop = @(
    $(if ($env:ProgramFiles) { Join-Path $env:ProgramFiles 'Docker\Docker\Docker Desktop.exe' }),
    $(if ($env:LOCALAPPDATA) { Join-Path $env:LOCALAPPDATA 'Programs\Docker\Docker\Docker Desktop.exe' })
  ) | Where-Object { $_ -and (Test-Path -LiteralPath $_) } | Select-Object -First 1
  if ($desktop) {
    Write-Step 'Docker Desktop läuft nicht, wird gestartet'
    Start-Process -FilePath $desktop | Out-Null
    $until = (Get-Date).AddMinutes(3)
    while (-not (Test-DockerRunning)) {
      if ((Get-Date) -gt $until) { Stop-WithError 'Docker Desktop ist nach drei Minuten nicht bereit. Bitte Docker Desktop öffnen und das Skript erneut starten.' }
      Start-Sleep -Seconds 3
    }
  } else {
    Stop-WithError 'Docker läuft nicht. Bitte Docker Desktop starten und das Skript erneut ausführen.'
  }
}
& docker compose version *> $null
if ($LASTEXITCODE -ne 0) {
  Stop-WithError 'Docker Compose fehlt. Bitte Docker Desktop aktualisieren.'
}

$compose = @('compose')
if ($Monitoring) { $compose += @('--profile', 'monitoring') }

# 2. Beenden oder zurücksetzen
if ($Stop) {
  Write-Step 'Beende die Demo'
  & docker @compose down
  Write-Host 'Beendet. Starten mit .\demo.ps1' -ForegroundColor Green
  exit 0
}
if ($Reset) {
  Write-Step 'Setze die Demo zurück (alle Vorgänge wieder unbearbeitet)'
  & docker @compose down -v
}

# 3. Bauen und starten
Write-Step 'Baue und starte NormaCase (der erste Start dauert einige Minuten)'
# Docker direkt aufrufen, ohne Umleitung: Der Build braucht das Konsolenfenster (sonst unter Windows
# "failed to get console: Das Handle ist ungültig").
& docker @compose up --build -d
if ($LASTEXITCODE -ne 0) {
  Stop-WithError 'Der Start ist fehlgeschlagen. Details mit: docker compose logs'
}

# 4. Warten, bis die Oberfläche und das Backend antworten
$deadline = (Get-Date).AddMinutes($TimeoutMinutes)
Write-Step 'Warte, bis NormaCase bereit ist'
$ready = $false
while (-not $ready) {
  try {
    $response = Invoke-WebRequest -Uri "$url/api/v1/system" -UseBasicParsing -TimeoutSec 5 -ErrorAction Stop
    $ready = $response.StatusCode -eq 200
  } catch {
    $ready = $false
  }
  if (-not $ready) {
    if ((Get-Date) -gt $deadline) { Stop-WithError 'NormaCase ist nicht rechtzeitig bereit. Details mit: docker compose logs backend' }
    Start-Sleep -Seconds 3
  }
}

# 5. Warten, bis die Fachsystem-Attrappe die Demo-Vorgänge eingeliefert hat
Write-Step 'Warte auf die Demo-Vorgänge'
while ($true) {
  $state = (& docker compose ps -a --format '{{.State}} {{.ExitCode}}' demo-host 2>$null) -join ' '
  if ($state -match '^exited 0') { break }
  if ($state -match '^exited') {
    & docker compose logs demo-host
    Stop-WithError 'Die Demo-Vorgänge konnten nicht eingeliefert werden (Ausgabe oben).'
  }
  if ((Get-Date) -gt $deadline) { Stop-WithError 'Die Demo-Vorgänge wurden nicht rechtzeitig eingeliefert. Details mit: docker compose logs demo-host' }
  Start-Sleep -Seconds 3
}
$seeded = (& docker compose logs --no-log-prefix demo-host 2>$null | Select-Object -Last 1)
if ($seeded) { Write-Host "    $seeded" }

# 6. Fertig
Write-Host ''
Write-Host 'NormaCase läuft.' -ForegroundColor Green
Write-Host "  Prüfwerkstatt:  $url"
Write-Host '  Anmeldung:      gutachterin.a  (prüfen, Rückfragen, bestätigen)'
Write-Host '                  freigabe.c     (freigeben oder zurückweisen)'
Write-Host '  Passwort:       demo'
if ($Monitoring) { Write-Host '  Grafana:        http://localhost:3000' }
Write-Host ''
Write-Host '  Beenden:        .\demo.ps1 -Stop'
Write-Host '  Neu anfangen:   .\demo.ps1 -Reset'
Write-Host '  Alle Personen und Befunde sind frei erfunden.' -ForegroundColor DarkGray

if (-not $NoBrowser) {
  try { Start-Process $url } catch { Write-Host "Bitte im Browser öffnen: $url" }
}
