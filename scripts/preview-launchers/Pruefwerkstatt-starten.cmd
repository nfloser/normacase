@echo off
setlocal EnableExtensions
rem Synthetic local preview only. Starts the loopback host and opens the browser once it is ready.
title NormaCase - synthetische Pruefwerkstatt
set "NORMACASE_ROOT=%~dp0"
set "NORMACASE_URL=http://localhost:5080/angular-pilot/index.html"

if not exist "%NORMACASE_ROOT%api\NormaCase.Api.exe" goto not_extracted
if not exist "%NORMACASE_ROOT%scripts\browser-oeffnen.cmd" goto not_extracted

curl.exe --silent --fail --max-time 2 --output nul "http://localhost:5080/health/live" >nul 2>&1
if not errorlevel 1 goto already_running

echo NormaCase - synthetische Pruefwerkstatt
echo.
echo Der lokale Dienst startet. Der Browser oeffnet sich gleich automatisch:
echo   %NORMACASE_URL%
echo Bisherige Pruefwerkstatt: http://localhost:5080/
echo.
echo Dieses Fenster bitte geoeffnet lassen.
echo Beenden: Strg+C druecken oder dieses Fenster schliessen.
echo Nur synthetische Daten verwenden.
echo.
if not defined NORMACASE_NO_BROWSER start "" /min cmd /d /c ""%NORMACASE_ROOT%scripts\browser-oeffnen.cmd" "%NORMACASE_URL%""
cd /d "%NORMACASE_ROOT%api"
NormaCase.Api.exe
set "exitCode=%errorlevel%"
if "%exitCode%"=="0" exit /b 0
echo.
echo Der lokale Dienst wurde mit Fehlercode %exitCode% beendet.
echo Haeufige Ursache: Port 5080 wird bereits von einem anderen Programm verwendet.
echo Details stehen in START.de.md im Paketordner.
pause
exit /b %exitCode%

:already_running
echo NormaCase laeuft bereits. Der Browser wird geoeffnet:
echo   %NORMACASE_URL%
if not defined NORMACASE_NO_BROWSER start "" "%NORMACASE_URL%"
exit /b 0

:not_extracted
echo.
echo Die Programmdateien wurden nicht gefunden.
echo.
echo Bitte die heruntergeladene ZIP-Datei zuerst vollstaendig entpacken:
echo   Rechtsklick auf die ZIP-Datei ^> "Alle extrahieren..."
echo Liegt darin noch eine ZIP-Datei, diese ebenfalls entpacken.
echo Danach Pruefwerkstatt-starten.cmd im entpackten Ordner doppelklicken.
echo.
pause
exit /b 1
