# Getrennte lokale Testidentitäten

Der optionale Review-Host unterstützt mehrere lokal konfigurierte synthetische
Identitäten. Er bleibt Loopback-only und ist kein produktiver Identitätsdienst.
Der normale Vorschau-Modus ist weiterhin ohne Anmeldung und Datenbank verwendbar.

Aktiviere `SyntheticReview__Enabled=true` und für gespeicherte Fälle zusätzlich
die PostgreSQL-Konfiguration aus [Review-Anleitung](../development/SYNTHETIC_REVIEW.de.md).
Lasse `SyntheticReview__Credential` im Mehrbenutzermodus vollständig ungesetzt.
Verwende je Person unabhängig erzeugte kanonische Base64-Schlüssel aus 32 zufälligen
Bytes. Schlüssel gehören ausschließlich in geschützte externe Konfiguration.

Beispiel der Konfigurationsstruktur, ohne Schlüsselwerte:

| Schlüssel | Wert |
|---|---|
| `SyntheticReview__Users__alice__Credential` | separat erzeugter externer Schlüssel |
| `SyntheticReview__Users__alice__Actions__0` | `READ` |
| `SyntheticReview__Users__alice__Actions__1` | `ACCEPT` |
| `SyntheticReview__Users__alice__CaseIds__0` | `demo-g-supported` |
| `SyntheticReview__Users__bob__Credential` | anderer externer Schlüssel |
| `SyntheticReview__Users__bob__Actions__0` | `READ` |
| `SyntheticReview__Users__bob__CaseIds__0` | `demo-g-not-supported` |

Alice kann ausschließlich den zugewiesenen Fall sehen und das Systemergebnis
übernehmen. Bob kann ausschließlich seinen Fall lesen. Übersteuern (`OVERRIDE`),
Rückgabe (`EXPORT`) und Fallannahme (`INTAKE`) benötigen jeweils ausdrückliche
Aktionsrechte und eine genaue Fallzuweisung. Alle Aktionen mit Falldaten-Rückgabe
setzen zusätzlich `READ` voraus. Es gibt keine Wildcard-Zuweisung.

Namen enthalten ausschließlich Kleinbuchstaben, Ziffern und Bindestriche und
beginnen mit einem Buchstaben, maximal 64 Zeichen. Die serverseitige Identität
lautet `synthetic-local:user-<Name>`; Anfragen können sie nicht vorgeben.
Maximal 100 Identitäten und 500 Fallzuweisungen je Identität sind erlaubt.
Doppelte Schlüssel, unbekannte Aktionen, ungültige Fälle und gleichzeitige
Einzel-/Mehrbenutzerkonfiguration verhindern den Start.

Die Arbeitsliste zeigt nur lesbare Fälle. Nicht lesbare Fall-URLs liefern wie
unbekannte Fälle 404; fehlende Aktionsrechte liefern 403. Die Fallansicht zeigt
nur zulässige Freigabe-/Override-Aktionen. Der Server prüft unabhängig von diesen
UI-Hinweisen erneut, bevor die bestehende Review-Transaktion schreibt.
Die Historie enthält die individuelle serverseitige Identität.
Bei neuen Eingängen muss deren deterministisch abgeleitete interne Fall-ID
vorab zugewiesen sein; allgemeine Eingangspools sind nicht implizit freigegeben.

Die Fall- und Aktionszuweisungen werden weiterhin beim Start aus geschützter
Konfiguration übernommen. Eine optionale, separat konfigurierte synthetische
Verwaltungsidentität kann einzelne konfigurierte Testpersonen jedoch zur Laufzeit
sperren und reaktivieren. Setze dafür
`SyntheticReview__Administrator__Credential` auf einen dritten, unabhängigen
kanonischen 256-Bit-Schlüssel. Die Verwaltungs-API liegt unter
`/api/review/administration/identities`; Änderungen benötigen die erwartete Revision
und einen Grund. Ziel, Zustand, Verwaltungsidentität, UTC-Zeit und Grund werden
append-only in PostgreSQL gespeichert. Eine Sperre gilt ab der nächsten geschützten
Serveranfrage und bleibt nach einem Neustart erhalten.

Nach Anmeldung mit der getrennten Verwaltungsidentität zeigt die deutsche Workbench
nur die konfigurierten Testpersonen, ihren aktuellen Zugriffsstatus, die exakte
Berechtigungsrevision und die unveränderliche Änderungshistorie. Sperren oder
Reaktivieren verlangt eine ausdrückliche Begründung. Bei 409 lädt die Oberfläche den
gespeicherten Stand neu und wiederholt die Änderung nicht. Abmeldung, 401 und das
Verlassen der Ansicht entfernen Schlüssel, Auswahl, Formular und Historie aus dem
Browser-Arbeitsspeicher. Normale Testpersonen sehen diese Ansicht nicht und erhalten
auf den Verwaltungsendpunkten weiterhin 403.

Unbekannte Ziele liefern 404. Schlüsselwerte werden weder gespeichert noch
zurückgegeben. Das ist eine geprüfte Live-Sperre mit Verwaltungs-Audit und deutscher
Bedienoberfläche, aber noch keine vollständige Berechtigungsverwaltung: Änderungen
der Fall-/Aktionszuweisung und eine konfigurierbare Trennung administrativer Aufgaben
bleiben in [Issue #183](https://github.com/nfloser/normacase/issues/183) offen.
Auch konkrete institutionelle Rollen und ein produktiver Identity Provider sind
dadurch nicht festgelegt.

Ohne `Users` bleibt der bekannte einzelne synthetische Reviewer erhalten. Wähle
diesen Modus ausschließlich für die gemeinsame lokale Demo, nicht als Ersatz für
individuelle Benutzer und Berechtigungen.
