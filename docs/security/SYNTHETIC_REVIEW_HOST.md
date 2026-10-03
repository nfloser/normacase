# Lokale Authentifizierung für synthetische Fallprüfung

Status: Authentifizierungsgrenze implementiert; persistente HTTP-Fallprüfung und
Oberflächenanbindung folgen in #138. Ausschließlich synthetische Daten.

## Betriebsmodi

Ohne `SyntheticReview:Enabled=true` bleibt die lokale Vorschau anonym und ohne
Review-Session-Endpunkt. Die bestehenden Vorschau-Endpunkte bleiben unverändert.
Der opt-in Modus fügt `GET /api/review-session` hinzu; er benötigt
`Authorization: Bearer <Schlüssel>` und liefert ausschließlich die bestätigte
technische Identität `synthetic-local:reviewer`. Er erlaubt noch keine Falländerung.

Konfiguration über die Betreiberumgebung:

- `SyntheticReview__Enabled=true`
- `SyntheticReview__Credential`: kanonische Base64-Kodierung von 32 kryptografisch
  zufälligen Bytes (44 Zeichen einschließlich Padding).

Schlüssel außerhalb des Repositorys erzeugen, beispielsweise mit einem lokalen
Passwort-/Secret-Werkzeug. Nicht in Kommandozeilenargumente, Git, URLs, Screenshots,
Issue-Texte oder CI-Ausgaben übernehmen. Keine Vorgabe und kein eingebauter Schlüssel.
Ungültige aktive Konfiguration verhindert den Hoststart ohne den Wert auszugeben.
Zum Sperren/Rotieren den Schlüssel ersetzen bzw. den Modus deaktivieren und neu starten.

## Vertrauensgrenze

ASP.NET Core authentifiziert ausschließlich einen einzelnen Authorization-Header.
Query-Parameter, Formulardaten und eigene Actor-Header sind keine Identitätsquelle.
Der Schlüssel muss exakt der kanonischen Kodierung entsprechen; zusätzliche
Leerzeichen, mehrere Werte und alternative Kodierungen werden zurückgewiesen.
Vergleich dekodierter Schlüssel erfolgt in konstanter Zeit. Die feste Identität wird
nur aus dem vom eigenen Scheme ausgestellten Principal in `AuthenticatedReviewActor`
überführt. Die Authority bleibt für die spätere fallbezogene Autorisierung erhalten.
Authentifizierung allein ist keine Fallberechtigung oder fachliche Freigabe.

Loopback-/Host-/Origin-Prüfung bleibt vor der Authentifizierung wirksam. Keine CORS-
Freigabe, Cookies, Query-Schlüssel, externe Identitätsdienste oder zusätzliche Logs.
Session-Antworten und Fehler unterliegen denselben no-store-, CSP- und weiteren
Sicherheitsheadern. Fehlende/ungültige Anmeldung liefert 401 mit deutschem Fehlertext
und Bearer-Challenge; verbotene Aktionen erhalten einen deutschen 403-Fehler.

## Grenzen und nächster Schritt

Der Schlüssel authentifiziert einen gemeinsamen synthetischen Bootstrap-Akteur,
keine individuelle reale Person. Jeder Besitzer kann denselben Akteur darstellen.
Loopback schützt nicht vor kompromittierten lokalen Prozessen oder Browsern.
Dieser HTTP-Demohost ist kein produktiver Identity Provider und darf nicht ins
Netzwerk veröffentlicht werden. Keine echten Patientendaten verwenden.

#138 verbindet danach PostgreSQL-Aggregate, explizite Fall-/Prozessberechtigungen,
Revisionsprüfung und append-only Review-Audit mit der deutschen Oberfläche.
Dafür bleibt ein gesonderter Review erforderlich. Produktiver Betrieb benötigt eine
institutionell geprüfte Identitäts-, Berechtigungs- und Datenschutzkonzeption (#119).

## Verifikation

HTTP-Integrationstests prüfen deaktivierten Standardmodus, gültige Anmeldung,
fehlende/falsche/ungültige Schlüssel, doppelte Header, ignorierte Query-Schlüssel,
serverseitige Identitätsbindung, Fremd-Origin-Abweisung, no-store und ungültige
Startkonfiguration. Die unveränderten Vorschau-Tests laufen ebenfalls weiter.
