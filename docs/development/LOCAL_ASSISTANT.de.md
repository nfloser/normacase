# NormaCase Assist – lokaler Experimentieradapter

**Ausschließlich synthetischer Proof of Concept.** Noch nicht mit NormaCase API, Fallverwaltung, PDF/OCR oder Arbeitsoberfläche verbunden. Die deterministische Prüfung funktioniert immer auch ohne KI.

## Modell und Größe

Erster Kandidat: [Qwen3-0.6B (Apache-2.0)](https://huggingface.co/Qwen/Qwen3-0.6B) als Q4_K_M GGUF, beispielsweise die konkret ausgewählte [Unsloth-Datei, Revision ec72b9975](https://huggingface.co/unsloth/Qwen3-0.6B-GGUF/blob/ec72b9975ebc7408610202b246332a25675431e7/Qwen3-0.6B-Q4_K_M.gguf), ungefähr **397 MB**. Auf deutschsprachigen Arztbriefen ist die Qualität nicht nachgewiesen; spätere Vergleiche entscheiden über geeignete Modelle. Keine Gewichtedatei gehört in Git, CI, das reguläre NormaCase-Paket oder Build-Artefakte.

## Einmalige Offline-fähige Einrichtung

1. Eine freigegebene [llama.cpp](https://github.com/ggml-org/llama.cpp)-Version (`llama-server`) installieren. Ihre Version und Lizenz dokumentieren.
2. Die festgelegte Modellrevision **einmalig** beziehen und lokal speichern. Vom Distributor unabhängig Provenienz, Lizenz und SHA-256 prüfen; den SHA-256 und die konkrete Modellrevision im Betreiberinventar festhalten. Nicht bei jedem Demo- oder CI-Test herunterladen; keine `-hf`-Option und keine dynamische Modellauflösung verwenden.
3. Lokal ein zufälliges Secret (mindestens 32 Zeichen) generieren, etwa per `python -c "import secrets; print(secrets.token_urlsafe(32))"`, und außerhalb von Git/Tickets als Umgebungsvariable `NORMACASE_ASSIST_API_KEY` setzen.
4. Beispiel für Linux/macOS (Modellpfad anpassen):

```bash
llama-server -m /lokaler/pfad/Qwen3-0.6B-Q4_K_M.gguf \
  --host 127.0.0.1 --port 18081 --alias normacase-assist \
  --api-key "$NORMACASE_ASSIST_API_KEY" --no-webui -c 2048
python scripts/local_document_assist.py --enable-synthetic-assist
```

Unter Windows in PowerShell `llama-server.exe` starten, die Argumente auf eine Zeile schreiben und das Secret mit `$env:NORMACASE_ASSIST_API_KEY` übergeben. Beachten: Secrets in Prozessargumenten können lokal sichtbar werden; produktiv benötigen wir eine freigegebene Secret- und Prozessisolation. Der Server darf ausschließlich an Loopback gebunden werden.

## Tests ohne Modell und Internet

```bash
python scripts/test_local_document_assist.py
```

Die Tests simulieren Antworten. Zulässig sind maximal acht JSON-Hinweise mit `kind` = `document_mentioned`, `date_mentioned` oder `measurement_mentioned` und wortgetreuem `quote` aus dem Quelldokument. Der Wert muss Teil des Zitats sein; alle Hinweise tragen `NEEDS_HUMAN_REVIEW`. Ungültige/zu große Antworten werden vollständig verworfen.

Start ohne explizites `--enable-synthetic-assist` wird abgelehnt. Ohne laufenden lokalen Inferenzserver bzw. Secret wird lediglich eine allgemeine Fehlermeldung ausgegeben. Die einzige verwendete Datei ist `examples/scenarios/local-assist-synthetic.de.txt`. Ein fester synthetischer Text kann **keine** klinische oder OCR-Genauigkeit belegen.

Der vollständige Sicherheits- und Freigaberahmen steht in [DOCUMENT_ASSISTANCE_SECURITY.de.md](../security/DOCUMENT_ASSISTANCE_SECURITY.de.md). Eine echte PDF-OCR-Pipeline und die UI-Anbindung sind eigene Folgeaufgaben nach einer Qualitätsmessung.
