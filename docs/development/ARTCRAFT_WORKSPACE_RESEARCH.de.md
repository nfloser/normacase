# Bedienmuster aus Artcraft

Stand: 10.10.2026. Geprüfter Commit: `8abde75` in
[storytold/artcraft](https://github.com/storytold/artcraft).
NormaCase übernimmt Bedienprinzipien; es wurde kein Artcraft-Code kopiert.

| Geprüfte Quelle | Muster | Umsetzung in NormaCase |
| --- | --- | --- |
| `frontend/libs/components/gallery-modal/src/lib/GalleryItemMenuItems.tsx` | Gemeinsame Aktionen für Rechtsklick und Ellipsis; Ordnerzuordnung separat | Ein gemeinsames Fallmenü, zusätzlich explizites Entfernen aus eigener Ablage |
| `frontend/libs/components/gallery-modal/src/lib/folderUtils.ts` | Gleiche Ordnersortierung in Sidebar und Zielauswahl | Deutsche alphabetische Sortierung in Baum und Verschiebedialog |
| `frontend/libs/components/gallery-modal/src/lib/FolderColorRow.tsx` | Vordefinierte Farben und freie Farbauswahl | Benannte, per Tastatur erreichbare Farbtasten plus freier Picker |
| `frontend/apps/artcraft/app/src/config/appMenu.tsx` | Beschriebene Navigation statt unabhängiger Zielimplementierungen | Kompakte Ablage-/Ansichtsleiste mit echten bestehenden Aktionen |
| `_docs/dev_setup.md` | React/Vite im Tauri-Host; Host-/Prozesslebenszyklus explizit | Gemeinsame React-Oberfläche bleibt unabhängig von nativen Fenstern |

Fachliche Ergebnisse, Arbeitslisten, persönliche Ablage und Demo-Versand bleiben
verschiedene Zustände. Ordner löschen entfernt keine Fälle. Farben sind Markierungen,
keine Freigaben; Namen und Status bleiben lesbar. Ein produktiver Versand wird nicht
simuliert als tatsächlicher Empfang ausgegeben.

## Desktop-Übernahme

Die bestehende React-Oberfläche und die versionierten API-/Anwendungsgrenzen bleiben
auch im Desktop nutzbar. Ein Wrapper muss keine eigene RuleEngine oder zweite
Fallverwaltung implementieren. Die aktuelle Demo verwendet gleiche Herkunft auf
Loopback; ein Host kann zunächst dieselbe lokale Anwendung in einem WebView öffnen.

`CaseExplorer` erhält die Dateiausgabe über `WorkspaceHost.saveJson`. Der Browser-Adapter
liefert die originale JSON-Zeichenfolge unverändert als Download aus. Ein späterer
nativer Adapter kann denselben Aufruf an einen lokalen Speicherdialog anbinden,
ohne Fachlogik oder Explorer-Aktionen zu verändern.

Die konkrete Hostwahl (z.B. Tauri oder Windows WebView2) erfolgt in einem eigenen ADR.
Noch erforderlich: Fenster-/Backend-Lebenszyklus, zulässige lokale Herkunft und
Authentifizierung, Download-/Dateidialoge, externe Links, Einzelinstanzverhalten,
signierte Installer/Updates sowie native Windows-Tests. Insbesondere darf ein
Tauri-Ursprung nicht durch pauschal offene CORS-Regeln zugelassen werden.

Die heutige Ablage ist ausdrücklich eine speicherinterne synthetische Demo und
endet beim Neustart des Hosts. Produktive persönliche Ablage benötigt actor-gebundene,
autorisierte Persistenz. Ein Desktop-Wrapper ersetzt diese Anforderungen nicht.
Der Umstieg erfordert dadurch keine Neuerstellung der UI, ist aber noch keine
fertige oder geprüfte Desktop-Auslieferung.
