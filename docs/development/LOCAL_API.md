# Lokaler synthetischer HTTP-Adapter

Start aus dem Repository-Verzeichnis mit .NET 10:

```bash
dotnet run --project src/NormaCase.Api
```

Der Dienst lauscht ausschließlich auf Loopback-Port 5080. Aufrufparameter oder
ASPNETCORE_URLS ändern diese Bindung nicht. Es gibt keine Runtime-Netzwerkabfragen.
Alle vier mitgelieferten synthetischen Packs werden lokal geladen und validiert.

- `GET http://localhost:5080/api/packs`: Pack-/Release-IDs, Felder und Evidenzreferenzen.
- `POST http://localhost:5080/api/assessments/synthetic.demo-c`: versionierter Fall als JSON.
- Antwort: verlustfreies Assessment-JSON mit Build-/Wissensstand und Decision Trace.

Beispiel (curl ist unter Windows als curl.exe verfügbar):

```bash
curl http://localhost:5080/api/packs
curl -H "Content-Type: application/json" --data-binary @examples/cases/demo-c-supported.json http://localhost:5080/api/assessments/synthetic.demo-c
```

Der Fall enthält das Prüfdatum explizit. Die Plattformversion stammt aus der
Assembly-InformationalVersion des Builds; es wird kein aktueller Zeitpunkt
hinzugefügt. Die Engine ist dieselbe wie im CLI-Adapter.

Fehlerstatus: 400 ungültige Eingabe, 403 nichtlokaler Host/fremder Origin,
404 unbekanntes Pack, 413 Eingabe zu groß, 415 falscher Inhaltstyp. Deutsche
Meldungen liegen in RESX-Ressourcen. Antworten sind nicht cachebar; Eingabeinhalte,
Dateipfade und Exception-Texte werden nicht ausgegeben oder protokolliert.

Requests sind auf 1 MiB begrenzt. Keine CORS-Freigabe, Forwarded-Header-Auswertung
oder externe Bindung. Der Dienst enthält keine Authentifizierung, Speicherung,
Patientendatenverwaltung oder fachliche Freigabe. Er dient ausschließlich lokalen
synthetischen Entwicklungstests. Eine produktive API benötigt eine gesonderte
Berechtigungs-/Datenschutzarchitektur und geprüfte Betriebsfreigabe.

```bash
dotnet test tests/NormaCase.Api.Tests --configuration Release
```
