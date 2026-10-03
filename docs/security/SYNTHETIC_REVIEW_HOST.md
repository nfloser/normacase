# Lokale Authentifizierung für synthetische Fallprüfung

Status: Authentifizierte, persistente lokale Fallprüfung für feste synthetische Fälle.
Ausschließlich synthetische Daten.

## Betriebsmodi

Ohne `SyntheticReview:Enabled=true` bleibt die lokale Vorschau anonym und ohne
Review-Session-Endpunkt. Die bestehenden Vorschau-Endpunkte bleiben unverändert.
Der opt-in Modus fügt `GET /api/review-session` und geschützte Fallwarteschlangen
sowie Prüfaktionen hinzu; er benötigt
`Authorization: Bearer <Schlüssel>` und liefert ausschließlich die bestätigte
technische Identität `synthetic-local:reviewer`. Falländerungen unterliegen zusätzlich einer expliziten fallbezogenen Prüfpolicy.

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

## Persistente Fallprüfung

`NORMACASE_REVIEW_DEMO_CONNECTION` konfiguriert PostgreSQL ausschließlich im
aktivierten Modus. Der Standardstart bleibt ohne Datenbank und ohne Falländerungen.
Die Oberfläche hält den maskiert eingegebenen Schlüssel nur im Arbeitsspeicher und
sendet ihn im Authorization-Header an dieselbe Origin; Abmelden löscht ihn und bricht
laufende Anfragen ab. Kein localStorage, keine URL-Schlüssel und keine Cookies.

Vier feste synthetische Bewertungen werden einmalig mit ihrer ursprünglichen
Plattform-/Wissensversion aufgezeichnet; Neustarts überschreiben sie nicht. Der fünfte,
technisch fehlerhafte Fall bleibt ausdrücklich ohne Bewertung. Der Client sendet nur
Bewertungsreferenz, erwartete Fall-/Prozess-/Auditrevision, Aktion, Begründung und ggf.
menschliches Ergebnis. Prüfer, UTC-Aufzeichnungszeit und Review-ID setzt der Server.

Die Fallberechtigung wird innerhalb derselben PostgreSQL-Aggregattransaktion erneut
geprüft. Vollständige positive und negative Ergebnisse dürfen angenommen oder
übersteuert werden; die synthetische Policy erlaubt für manuelle Prüfung nur ein
begründetes Übersteuern mit bekanntem Ergebnis. Unvollständige und technische Fälle
haben keine Prüfaktion. Veraltete Revisionen liefern 409 ohne automatischen Retry.
Originalbewertung und Auditpräfix bleiben unverändert; Prozessstand und neue
menschliche Entscheidung werden gemeinsam append-only gespeichert. Queue und
Historie werden aus dem bestätigten Fallstand gelesen.

## Grenzen und nächster Schritt

Der Schlüssel authentifiziert einen gemeinsamen synthetischen Bootstrap-Akteur,
keine individuelle reale Person. Jeder Besitzer kann denselben Akteur darstellen.
Loopback schützt nicht vor kompromittierten lokalen Prozessen oder Browsern.
Dieser HTTP-Demohost ist kein produktiver Identity Provider und darf nicht ins
Netzwerk veröffentlicht werden. Keine echten Patientendaten verwenden.

Die technische Prüfung dieser Demo ersetzt keine institutionelle Freigabe.
Produktiver Betrieb benötigt eine
institutionell geprüfte Identitäts-, Berechtigungs- und Datenschutzkonzeption (#119).

## Verifikation

HTTP-Integrationstests prüfen deaktivierten Standardmodus, gültige Anmeldung,
fehlende/falsche/ungültige Schlüssel, doppelte Header, ignorierte Query-Schlüssel,
serverseitige Identitätsbindung, Fremd-Origin-Abweisung, no-store und ungültige
Startkonfiguration. Zusätzliche Tests prüfen Fallberechtigung, unverändertes Original,
serverseitigen Audit-Akteur und konkurrierende Revisionen. Browser-Tests mit echtem
PostgreSQL prüfen Annehmen/Übersteuern, dauerhafte Historie, Ausschluss fehlender und
technischer Fälle, Abmelden und mobilen Aufbau. Die Vorschau-Tests laufen weiter.
