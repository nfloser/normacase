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

Die Konfiguration wird beim Start übernommen. Für eine Änderung oder Sperrung
stoppe den Host, ändere die geschützte Konfiguration und starte ihn neu.
Entfernte Schlüssel werden danach mit 401 abgelehnt. Das ist keine Live-Sperrung,
keine administrative Berechtigungsoberfläche und kein revisionssicherer
Verwaltungs-Audit. Diese technischen Aufgaben bleiben in
[Issue #183](https://github.com/nfloser/normacase/issues/183) offen.
Auch konkrete institutionelle Rollen und ein produktiver Identity Provider sind
dadurch nicht festgelegt.

Ohne `Users` bleibt der bekannte einzelne synthetische Reviewer erhalten. Wähle
diesen Modus ausschließlich für die gemeinsame lokale Demo, nicht als Ersatz für
individuelle Benutzer und Berechtigungen.
