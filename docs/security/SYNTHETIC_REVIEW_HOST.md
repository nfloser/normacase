# Lokale Authentifizierung für synthetische Fallprüfung

Status: Authentifizierungsgrenze implementiert; die persistente synthetische
HTTP-Fallprüfung ist separat opt-in. Die deutsche Oberfläche ist über den optionalen Review-Modus angebunden.
Ausschließlich synthetische Daten.

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

Der getrennte Mehrbenutzermodus verwendet stattdessen mehrere unabhängige Schlüssel
und genaue Fall-/Aktionszuweisungen. Im persistenten Modus kann zusätzlich eine
separate synthetische Verwaltungsidentität konfiguriert werden, die einzelne
Testpersonen live sperrt oder reaktiviert. Der Zustand und jede Änderung werden
revisionsgeprüft und append-only gespeichert. Konfiguration und Grenzen stehen unter
[Getrennte lokale Testidentitäten](LOCAL_SYNTHETIC_IDENTITIES.de.md); die technische
Grenze ist in [Identity access administration](../architecture/IDENTITY_ACCESS_ADMINISTRATION.md)
dokumentiert.

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
sein. Der Host migriert sein Schema und initialisiert die vier synthetischen `demo-g`-Fixtures,
falls sie noch nicht vorhanden sind. Authentifizierte JSON-/XML-Testeingänge können
zusätzlich frische synthetische Fälle anlegen.

Die geschützten Endpunkte `/api/review/work-queues`,
`/api/review/work-cases/{caseId}` und
`/api/review/work-cases/{caseId}/reviews` lesen den committed PostgreSQL-Zustand.
Accept/Override ist nur aus `awaiting-approval` erlaubt. Commands enthalten explizite
Case-/Process-/Audit-Revisionen und einen Grund; Actor, Review-ID und UTC-Zeitpunkt
stammen ausschließlich von der vertrauenswürdigen Servergrenze. Veraltete Revisionen
werden mit 409 abgewiesen, auch wenn der Fall inzwischen abgeschlossen wurde.
Die Fallberechtigung wird vor der Revisionsprüfung kontrolliert; die explizite
Prozesspolicy erlaubt weiterhin ausschließlich Übergänge aus `awaiting-approval`.
Ein aktueller Command gegen einen abgeschlossenen Fall bleibt mit 403 gesperrt.

Die Oberfläche hält den Schlüssel ausschließlich im Arbeitsspeicher, entfernt das
maskierte Eingabefeld nach erfolgreicher Anmeldung und speichert keine Cookies,
localStorage oder sessionStorage. Abmeldung/Unmount brechen laufende Anfragen ab;
verspätete Antworten dürfen keinen Fallstand oder Akteur wieder einsetzen.
Review-Commands verwenden die angezeigten exakten Revisionen als Strings, eine
Begründung und beim Override ein ausdrücklich gewähltes Ergebnis. Keine
optimistische Falländerung; 409 lädt den committed Stand neu, 401 beendet die Sitzung.
403 zeigt einen begrenzten deutschen Hinweis ohne serverseitigen Inhalt zu spiegeln.
Die Anleitung steht in [synthetischer Review-Demo](../development/SYNTHETIC_REVIEW.de.md).
Produktiver Betrieb benötigt weiterhin eine institutionell geprüfte Identitäts-,
Berechtigungs- und Datenschutzkonzeption (#119).

## Verifikation

HTTP-Integrationstests prüfen deaktivierten Standardmodus, gültige Anmeldung,
fehlende/falsche/ungültige Schlüssel, doppelte Header, ignorierte Query-Schlüssel,
serverseitige Identitätsbindung, Fremd-Origin-Abweisung, no-store und ungültige
Startkonfiguration. PostgreSQL-/HTTP-Tests prüfen außerdem konkurrierende
Revisionsänderungen, append-only Audit, Live-Sperrung, Neustart und Reaktivierung.
Die unveränderten Vorschau-Tests laufen ebenfalls weiter.


## Frische Testeingänge und Rückgabe

Die neuen geschützten `/api/review/intake/{json|xml}`-Endpunkte sind auf 64 KiB,
die expliziten synthetischen Formate und initiale Revision 1 begrenzt. Unbekannte
Eigenschaften, doppelte JSON-Schlüssel, DTDs und gerundete Zahlen werden abgewiesen.
Die Quellidentität ist an den gewählten Testadapter gebunden; eine reale externe
Quelle oder echte Dokumentauthentizität wird damit nicht bestätigt. Fall-IDs sind
serverseitig aus Testquelle/Auftrags-ID abgeleitet. Fallberechtigung gilt für gespeicherte
Testfälle unter diesem Host und seiner synthetischen Workflow-Policy.

`/api/review/work-cases/{caseId}/outbound` benötigt denselben verifizierten Akteur,
einen unveränderlichen Eingangsbeleg, terminalen menschlichen Review und exakte
Revisionen. Nur der explizite PostgreSQL-Testposteingang und das optional konfigurierte
lokale Dateiverzeichnis sind Ziele. Dateipfade werden nicht aus Benutzereingaben
übernommen; die Nachricht wird durch atomare Veröffentlichung ohne Überschreiben
bereitgestellt. Der Betreiber muss das Verzeichnis vor anderen lokalen Nutzern
schützen. Wiederholung nach Abbruch vergleicht den ursprünglichen Inhalt; sie ändert
keinen Review. Details und die Backup-/Restore-Probe stehen in
[synthetischer Gesamtablauf](../development/SYNTHETIC_ROUNDTRIP.de.md).
