# Lokale Authentifizierung für synthetische Fallprüfung

Status: Authentifizierungsgrenze, persistente synthetische HTTP-Fallprüfung und
optionale Workbench-Anbindung sind implementiert. Ausschließlich synthetische Daten.

## Betriebsmodi

Ohne `SyntheticReview:Enabled=true` bleibt die lokale Vorschau anonym und ohne
Review-Session-Endpunkt. Die bestehenden Vorschau-Endpunkte bleiben unverändert.
Der opt-in Modus fügt `GET /api/review-session` hinzu; er benötigt
`Authorization: Bearer <Schlüssel>` und liefert ausschließlich die bestätigte
technische Identität `synthetic-local:reviewer`. Falländerungen sind nur zusätzlich
bei aktivierter Persistenz über die geschützten `/api/review/*`-Endpunkte möglich.

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

Mit `SyntheticReview:PersistenceEnabled=true` wird zusätzlich die persistente
Review-API aktiviert. Dafür müssen die oben beschriebene Authentifizierung aktiv und
`ConnectionStrings:SyntheticReview` auf eine lokale PostgreSQL-Datenbank gesetzt
sein. Der Host migriert sein Schema und initialisiert ausschließlich die vier
synthetischen `demo-g`-Fixtures, falls sie noch nicht vorhanden sind.

Die geschützten Endpunkte `/api/review/work-queues`,
`/api/review/work-cases/{caseId}` und
`/api/review/work-cases/{caseId}/reviews` lesen den committed PostgreSQL-Zustand.
Accept/Override ist nur aus `awaiting-approval` erlaubt. Commands enthalten explizite
Case-/Process-/Audit-Revisionen und einen Grund; Actor, Review-ID und UTC-Zeitpunkt
stammen ausschließlich von der vertrauenswürdigen Servergrenze. Veraltete Revisionen
werden mit 409 abgewiesen.

Die deutsche Workbench verwendet denselben Vertrag. Der Schlüssel bleibt nur im
React-Arbeitsspeicher und wird ausschließlich als Authorization-Header gesendet;
kein Browser-Speicher, Cookie, URL-Parameter oder lokaler Actor-Wert dient als
Identitätsquelle. 401 löscht den Review-Browserzustand; 403 und 409 verwerfen einen
möglicherweise veralteten Falldetailstand und laden den aktuellen committed Zustand
neu. Erfolgreiche Aktionen werden ebenfalls vollständig vom Server neu geladen.

Produktiver Betrieb benötigt weiterhin eine institutionell geprüfte Identitäts-,
Berechtigungs- und Datenschutzkonzeption (#119).

## Verifikation

HTTP-Integrationstests prüfen deaktivierten Standardmodus, gültige Anmeldung,
fehlende/falsche/ungültige Schlüssel, doppelte Header, ignorierte Query-Schlüssel,
serverseitige Identitätsbindung, Fremd-Origin-Abweisung, no-store und ungültige
Startkonfiguration. Die unveränderten Vorschau-Tests laufen ebenfalls weiter.
Zusätzlich prüft Playwright gegen einen echten PostgreSQL-Dienst Anmeldung,
Accept, Override, konkurrierende veraltete Revisionen, Logout und den Verzicht auf
Browser-Speicher. Der CI-Schlüssel wird pro Lauf flüchtig erzeugt und nicht im
Repository hinterlegt.
