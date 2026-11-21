@echo off
setlocal EnableExtensions
rem Waits until the local synthetic host answers, then opens the given loopback URL.
set "url=%~1"
if not defined url exit /b 1
if /i not "%url:~0,22%"=="http://localhost:5080/" exit /b 1
where curl.exe >nul 2>&1
if errorlevel 1 goto without_curl
for /l %%i in (1,1,90) do (
  curl.exe --silent --fail --max-time 1 --output nul "http://localhost:5080/health/live" >nul 2>&1 && goto open
  ping -n 2 127.0.0.1 >nul
)
exit /b 1

:without_curl
ping -n 8 127.0.0.1 >nul

:open
start "" "%url%"
exit /b 0
