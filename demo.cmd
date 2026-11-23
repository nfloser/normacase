@echo off
rem Doppelklick startet die NormaCase-Demo. Parameter werden weitergereicht, z. B. demo.cmd -Reset
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0demo.ps1" %*
if errorlevel 1 pause
