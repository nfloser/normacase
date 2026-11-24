# NormaCase vorführen – Schritt für Schritt

Die Demo läuft vollständig auf dem Rechner, ohne Internet nach dem Start. Einzige Voraussetzung ist **Docker Desktop** (kostenlos, einmal installieren). Ohne Docker geht es nicht, weil NormaCase aus Datenbank, Anmeldedienst, Backend und Oberfläche besteht.

## Mac

1. Docker Desktop installieren: https://www.docker.com/products/docker-desktop/ → „Download for Mac“ (Apple-Chip oder Intel, steht unter  → Über diesen Mac). Öffnen, Bedingungen bestätigen, Anmeldung überspringen.
2. **Terminal** öffnen (Cmd + Leertaste, „Terminal“ tippen, Enter).
3. Diese drei Zeilen einfügen, jeweils mit Enter:

```sh
curl -L https://github.com/nfloser/normacase/archive/refs/heads/main.zip -o normacase.zip
unzip -q normacase.zip && cd normacase-main
sh demo.sh
```

Beim ersten Start baut Docker alles selbst (5–10 Minuten). Danach öffnet sich der Browser mit http://localhost:4200.

## Windows

1. Docker Desktop installieren (wie oben, „Download for Windows“), einmal starten.
2. **PowerShell** öffnen und eingeben:

```powershell
Invoke-WebRequest https://github.com/nfloser/normacase/archive/refs/heads/main.zip -OutFile normacase.zip
Expand-Archive normacase.zip -DestinationPath . ; cd normacase-main
.\demo.cmd
```

## Anmelden und zeigen

- Gutachterin: `gutachterin.a` / `demo` · Freigabe (zweite Person): `freigabe.c` / `demo`
- Guter Ablauf: Arbeitsliste „Zur Freigabe vorbereitet“ → Gerda Lehmann doppelklicken → in der Begründung auf „Erhebungsbogen, S. 1“ klicken → Dokumente durchklicken (Fax, Foto, Scan) → Bestätigen → abmelden, als `freigabe.c` freigeben.
- Widerspruch zeigen: „Gegenprüfung erforderlich“ → Ulrich Hartmann.

## Beenden und aufräumen

- Beenden: `sh demo.sh --stop` (Mac) bzw. `.\demo.cmd -Stop` (Windows)
- Neu mit frischen Fällen: `sh demo.sh --reset` bzw. `.\demo.cmd -Reset`
- Alles entfernen: Ordner `normacase-main` löschen, in Docker Desktop unter „Images“ die NormaCase-Images löschen.

## Wenn etwas hakt

| Problem | Lösung |
|---|---|
| „Docker nicht gefunden“ | Docker Desktop installieren und einmal öffnen |
| Port belegt | Anderes Programm auf 4200, 8080 oder 8081 beenden |
| Hängt beim ersten Start | Internet nötig für den Download; Details mit `docker compose logs` |
| Anmeldung lädt nicht | 1 Minute warten (Anmeldedienst startet zuletzt), Seite neu laden |

In der Prüfwerkstatt klappt **Menü** die Menü- und Aktionsleiste ein oder aus. Am rechten Rand der Spaltenköpfe lässt sich die Breite ziehen, mit den Pfeiltasten ändern oder per Doppelklick zurücksetzen. Breiten gehören zur aktuellen Ansicht und zu gespeicherten Abfragen. Ein Klick auf eine Vorgangsnummer kopiert sie.

### Arbeitsplatz anordnen

Explorer, Fallvorschau, Dokumentbereich und Unterlagenliste lassen sich jeweils links, rechts, oben oder unten anordnen. Ziehe den jeweiligen Trenner oder verändere die Größe mit den Pfeiltasten. Die Einstellungen bleiben pro Konto im Browser gespeichert. Über die Schaltflächen in der Aktionsleiste und der Fallakte kannst du die Bereiche ausblenden und zurückholen.

Im Tab **Notizen** bearbeitest du dieselbe persönliche Notiz, die im Kontextmenü der Liste verfügbar ist. Unter **Hilfe → Bedienung** findest du Antworten auf häufige Fragen. **Hilfe → Tastenkürzel** zeigt die verfügbaren Kürzel.
