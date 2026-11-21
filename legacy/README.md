# Referenzstand (eingefroren)

Dieser Ordner enthält den bisherigen NormaCase-Stand unverändert: .NET-Kern mit Regel-Engine, React-Prüfwerkstatt, Angular-Pilot, Spring-Boot-Vertragsprototyp, Knowledge Packs, synthetische Fallakten und die zugehörige Dokumentation.

Referenz-Commit: `dd1024b` (Stand nach #357). Alles in diesem Ordner ist der Zustand dieses Commits, nur in `legacy/` verschoben; die Git-Historie jeder Datei bleibt über die Umbenennung erhalten (`git log --follow legacy/<pfad>`).

## Wofür der Ordner noch dient

- **Vergleich.** Der Neuaufbau im Wurzelverzeichnis wird fachlich an diesem Stand gemessen.
- **Paritäts-Orakel.** Die Java-Regel-Engine des Neuaufbaus muss für die Knowledge Packs und Fallakten hier dieselben Ergebnisse liefern wie der .NET-Kern (siehe `docs/adr/0005-regel-engine-port-mit-paritaet.md`).
- **Quelle für Fachwissen.** Knowledge Packs (`knowledge/`), Fallakten (`examples/`) und die Spezifikationen unter `docs/` werden übernommen, nicht neu erfunden.

## Was nicht mehr passiert

- Keine Weiterentwicklung. Fehler werden im Neuaufbau behoben.
- Die alten Workflows unter `legacy/.github/` laufen nicht mehr; GitHub liest Workflows nur aus `.github/` im Wurzelverzeichnis.
- Die Dateien zählen nicht zur GitHub-Sprachstatistik (`.gitattributes`).

## Alten Stand ausführen

Am einfachsten den Referenz-Commit auschecken, dann gelten Pfade und Workflows wie damals:

```bash
git worktree add ../normacase-legacy dd1024b
cd ../normacase-legacy
dotnet test NormaCase.sln          # .NET-Kern und Tests
```

Die Windows-Vorschau (Prüfwerkstatt) liegt als CI-Artefakt `synthetic-preview-win-x64` am Lauf von #357; Start über `Pruefwerkstatt-starten.cmd`.
