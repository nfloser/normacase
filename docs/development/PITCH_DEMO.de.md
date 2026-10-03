# Synthetische Pitch-Demo

Diese Demo ist ein reproduzierbares Produkt-Showcase für NormaCase. Sämtliche
Falldaten, Regeln, Bezeichnungen und Quellen im Prüfbereich
`synthetic.demo-g` sind frei erfunden. Sie haben keine medizinische,
sozialmedizinische oder rechtliche Aussagekraft und sind keine fachlich
freigegebene Begutachtung.

## Ziel

Die Demo zeigt in wenigen Minuten vier Eigenschaften der Plattform:

1. fehlende Pflichtangaben bleiben `UNKNOWN` und führen zu `INCOMPLETE`,
2. fehlende Evidenz wird nicht geraten, sondern zu `HUMAN_REVIEW` geroutet,
3. vollständige Eingaben ergeben mit demselben Wissensstand deterministisch
   `SUPPORTED`,
4. ein ausdrücklich negatives Kriterium ergibt nachvollziehbar
   `NOT_SUPPORTED`.

Der fachliche Inhalt ist absichtlich bedeutungslos. Gezeigt werden Plattform,
Nachvollziehbarkeit, Versionierung und sichere Unsicherheitsbehandlung.

## Vorführung in der Prüfwerkstatt

Starte die lokale Vorschau und öffne <http://localhost:5080>. Wähle als
Prüfbereich **„Pitch-Demo – Synthetische Fallprüfung“**.

### 1. Unvollständiger Eingang

Wähle **„1 · Pflichtangabe fehlt“**, lade das Beispiel und starte die Prüfung.

Erwartet:

- Ergebnis: **Angaben unvollständig**,
- fehlende Pflichtangabe: **Pflichtangaben vollständig**,
- Prüfdatum: **03.10.2026**,
- Knowledge Release: `demo-g-2026.1`.

Der zentrale Punkt für die Präsentation: NormaCase ergänzt die fehlende Angabe
nicht stillschweigend.

### 2. Fehlende Evidenz

Wähle **„2 · Nachweis fehlt – manuelle Prüfung“** und prüfe erneut.

Erwartet:

- Ergebnis: **Manuelle Prüfung erforderlich**,
- keine fehlende Pflichtangabe,
- Regel: `DEMO-G-DECISION`,
- Quelle: **Fiktive Regelquelle für die NormaCase Pitch-Demo**.

Damit lässt sich erklären, dass ein formal vollständiger Fall trotzdem gezielt
in menschliche Arbeit gehen kann, wenn die notwendige Evidenz fehlt.

### 3. Vollständiger Fall

Wähle **„3 · Vollständig – Voraussetzungen erfüllt“** und prüfe erneut.

Erwartet:

- Ergebnis: **Voraussetzungen erfüllt**,
- Knowledge Release: `demo-g-2026.1`,
- Regel: `DEMO-G-DECISION`,
- Quelle: `SYNTH-DEMO-G-001`,
- Fundstelle: `repository:knowledge/demo-g/pack.json`.

Öffne anschließend **„Technische Prüfspur anzeigen“**. Dort sind der feste
Prüfzeitpunkt, der Wissensstand und die quellengebundene Regel nachvollziehbar.

Optional kannst du **„Prüfsnapshot herunterladen“** verwenden und denselben
Snapshot über **„Gespeicherte Prüfung offline überprüfen“** wieder einlesen.
Damit lässt sich die historische Reproduzierbarkeit zeigen.

### 4. Explizit negatives Kriterium

Wähle **„4 · Kriterium ausdrücklich nicht erfüllt“**.

Erwartet:

- Ergebnis: **Voraussetzungen nicht erfüllt**,
- kein UNKNOWN und keine manuelle Ersatzentscheidung.

Dieser Schritt grenzt „bekanntes Nein“ sichtbar von „fehlender Information“ ab.

## Workflow separat zeigen

Der generische Vorgangsablauf befindet sich derzeit im Prüfbereich
**„Demo F – Vorgang und Prüfung“**. Er zeigt manuelle Übergänge und eine
nachvollziehbare Historie.

Die Pitch-Demo darf Assessment und Vorgangsfreigabe nicht als bereits automatisch
gekoppelt darstellen. Routing, persistente Arbeitswarteschlange, authentifizierte
Freigabe und Outbound-Integration werden entlang des Produkt-Critical-Paths
separat vervollständigt.

## CLI-Fallback

Aus dem entpackten Preview-Paket:

```powershell
$stand = (Get-Content -Raw preview.json | ConvertFrom-Json).platformVersion
.\cli\NormaCase.Cli.exe evaluate --pack knowledge/demo-g/pack.json --case examples/cases/demo-g-supported.json --platform-version $stand
```

Linux:

```sh
./cli/NormaCase.Cli evaluate --pack knowledge/demo-g/pack.json --case examples/cases/demo-g-supported.json --platform-version 'PLATTFORMSTAND-AUS-preview.json'
```

Die vollständige maschinenlesbare Reihenfolge liegt in
`examples/scenarios/pitch-demo-v1.json`.

## Sicherheitsgrenze

- Keine echten Patientendaten, Gutachten, Namen, Aktenzeichen oder internen
  Dokumente in die Demo übernehmen.
- Für Screenshots und Videos ausschließlich die mitgelieferten synthetischen
  Beispiele verwenden.
- Das synthetische Knowledge Pack nicht als MD-Regelwerk oder fachlich
  validierte Logik bezeichnen.
- Public-Reference-Packs sind von dieser Pitch-Demo getrennt und ebenfalls
  nicht automatisch fachlich freigegeben.
