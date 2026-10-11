# Datensparsame technische Diagnose

Der synthetische Host lässt ausschließlich die Kategorie
`NormaCase.Api.SafeDiagnostics` auf Information-Level zu. Der Filter wird nach der
Konfigurationsbindung gesetzt; `Logging:LogLevel:Default=Trace` und Framework-
Trace-Einstellungen aktivieren keine ASP.NET-/Npgsql-Request- oder Exception-Logs.
Standardprovider bleiben abgeschaltet. Ein späterer lokaler Diagnoseprovider darf
nur unter Betreiberkontrolle ergänzt werden; externe Telemetrie ist nicht vorgesehen.

Das einzige aktuelle Ereignis ist `1000 / HttpCompleted` mit dem festen Text
`NC_HTTP_COMPLETED` und numerischem HTTP-Status. Es enthält keine URL, Query,
Header, IP, Akteur, Fall-ID, Body, Verbindung, Exception oder frei eingegebenen Text.
Auch bei einem abgebrochenen/fehlgeschlagenen Delegate wird nur ein technischer
500-Status ausgegeben; dies ist keine Aussage über einen bereits erfolgten Commit.
Die Diagnose ist keine fachliche Audit-Historie und darf diese nicht ersetzen.

`LogMinimizationTests` installiert einen Capture-Provider mit positiven
Ereignis-Assertions, damit eine leere Ausgabe nicht als Sicherheitsbeweis gilt:

- Authentifizierungsfehler mit synthetischem Token in Query und Header;
- fehlerhafter JSON-Body und fremder Dokumentpfad;
- tatsächlicher fehlgeschlagener PostgreSQL-Readiness-Verbindungsversuch;
- ausgeschlossene Framework-/Npgsql-Exceptiontexte und Scopes;
- ein fehlgeschlagener Outbound-Delegate an der gemeinsamen Diagnosegrenze.

Die Tests prüfen Logtext, Zustand, Scopes und Exceptions sowie öffentliche
HTTP-Fehler auf synthetische Canaries. Der Delegate-Test ist keine produktive
MD-Outbound-Integration; bestehende persistente Outbound-Integrationstests bleiben
zusätzlich erforderlich. Die Tests laufen in der normalen API-CI.

Offen für den Zielbetrieb: Auswahl/Zugriff/Aufbewahrung des Diagnose-Sinks,
Prozess-/Reverse-Proxy-/Betriebssystemlogs, APM und externe Datenbankdiagnose.
Ein Betreiber, der Filter im Code überschreibt, neue Kategorien/Scopes oder einen
Telemetrieprovider einführt, verändert diese Sicherheitsgrenze und muss sie erneut
prüfen. Keine produktive Datenschutzfreigabe aus diesen Tests ableiten.
