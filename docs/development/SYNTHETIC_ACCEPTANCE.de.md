# Synthetische Abnahme und Übergabe

Diese Anleitung ist der zentrale Einstieg für den aktuell vollständig ausführbaren
synthetischen NormaCase-Stand. Sie bündelt Vorschaupaket, Pitch, persistente
Fallprüfung und Wiederherstellungsprobe.

Der Stand ist **keine produktive MD-Integration und keine fachliche Freigabe**.
Ausschließlich synthetische Daten verwenden.

## 1. Schnellste Produktprüfung: fertiges Vorschaupaket

Öffne in GitHub den erfolgreichen Workflow **Synthetic preview bundles** des
gewünschten Commits und lade für das Zielsystem eines der Artefakte:

- `synthetic-preview-win-x64`
- `synthetic-preview-linux-x64`

Entpacke das darin enthaltene ZIP vollständig. Prüfe die mitgelieferte
`.zip.sha256`-Datei und den Commit in `preview.json`. Danach den Anweisungen in
`START.de.md` folgen.

Die Pakete enthalten die deutsche Prüfwerkstatt, CLI, synthetische Knowledge Packs
und feste Pitch-Beispiele. Sie benötigen keine installierte .NET-Laufzeit und keine
Runtime-Internetverbindung. Der normale Preview-Modus verwendet keine Datenbank.

Details: [Lokaler Preview-Start](PREVIEW_START.de.md).

## 2. Reproduzierbaren Pitch durchführen

Nutze [Synthetische Pitch-Demo](PITCH_DEMO.de.md). Die vier vorgesehenen Fälle zeigen:

1. fehlende Pflichtangabe → **Angaben unvollständig**,
2. fehlende Evidenz → **Manuelle Prüfung erforderlich**,
3. vollständiger positiver Fall → **Voraussetzungen erfüllt**,
4. ausdrücklich negatives Kriterium → **Voraussetzungen nicht erfüllt**.

Für jeden Fall müssen Ergebnis, Knowledge Release und technische Prüfspur mit der
Anleitung übereinstimmen. UNKNOWN darf nie still zu Ja oder Nein werden.

## 3. Persistente menschliche Fallprüfung zeigen

Für Review, Arbeitslisten und dauerhafte Historie PostgreSQL und die opt-in
Authentifizierung gemäß
[Persistente synthetische Fallprüfung](SYNTHETIC_REVIEW.de.md) starten.

Manuell prüfen:

- anonymer Zugriff auf Review-Endpunkte wird abgewiesen,
- vollständige Fälle können nur mit Server-erlaubter Aktion abgeschlossen werden,
- Override benötigt eine ausdrückliche Begründung und lässt das Originalergebnis
  unverändert sichtbar,
- unvollständige/manuell zu prüfende Fälle bieten keine erfundene Freigabe,
- veraltete Revisionen werden mit Konflikt abgewiesen,
- nach Neustart bleiben Fall- und Review-Historie erhalten.

## 4. Gesamtablauf Eingang → Rückgabe prüfen

[Synthetischer Gesamtablauf](SYNTHETIC_ROUNDTRIP.de.md) beschreibt den persistenten
End-to-End-Pfad:

```text
synthetischer JSON/XML-Eingang
→ normalisierter unveränderlicher Eingang
→ deterministische Bewertung
→ persistente Arbeitsliste
→ authentifizierte menschliche Prüfung
→ synthetischer Outbound
→ dauerhafter Zustellbeleg
```

Die vorgesehenen Ziele sind ausschließlich `synthetic-inbox` und
`synthetic-file`. Sie beweisen die generische Integrationsgrenze, nicht die
Kompatibilität zu einem realen MD-, SAP- oder Vendor-Protokoll.

## 5. Recovery und historische Reproduzierbarkeit

Die CI führt den echten Host mit PostgreSQL auf einer leeren Datenbank aus, startet
ihn neu, erstellt einen `pg_dump`, stellt diesen in einer zweiten frischen Datenbank
wieder her und verifiziert Eingang, Assessment, Review-Historie und Outbound-Belege
erneut. Der synthetische Dateiausgang wird separat wiederhergestellt und verglichen.

Für eine lokale technische Probe gelten die Befehle aus
[Synthetischer Gesamtablauf](SYNTHETIC_ROUNDTRIP.de.md).

## Manuelle Abnahmekriterien dieses Repository-Stands

Der access-unabhängige synthetische Stand gilt als nachvollziehbar abgenommen, wenn:

- die CI des exakt vorgeführten Commits grün ist,
- Windows- oder Linux-Preview für denselben Commit erfolgreich gebaut wurde,
- die vier Pitch-Fälle die dokumentierten Ergebnisse liefern,
- die technische Prüfspur Knowledge-/Source-Versionen sichtbar macht,
- Review-Authentifizierung, Override und Concurrency wie dokumentiert funktionieren,
- derselbe persistente Fall nach Neustart unverändert geladen wird,
- der vollständige synthetische Roundtrip inklusive Zustellbeleg funktioniert,
- die Recovery-Probe denselben historischen Zustand wiederherstellt,
- ausschließlich synthetische Daten verwendet wurden.

Grüne Tests sind dabei technische Akzeptanzevidenz, keine fachliche oder rechtliche
Freigabe.

## Was bewusst noch nicht abgenommen werden kann

Diese Punkte brauchen externe bzw. institutionelle Autorität und dürfen nicht aus
synthetischen Annahmen erfunden werden:

- reale MD-/Institution-Schnittstellen und Transportverträge,
- produktive Benutzer-, Rollen-, Mandanten- und Berechtigungskonzepte,
- Datenschutz-, Informationssicherheits- und Betriebsfreigabe der Zielumgebung,
- reale Korrektur-, Nachforderungs- und Eskalationsprozesse,
- fachlich verbindliche Knowledge Releases,
- kontrollierter Pilot mit dafür genehmigten sensiblen Daten,
- produktive Backup-/Retention-/RPO-/RTO-Vorgaben.

Der aktuelle Repository-Stand ist damit eine vollständige synthetische
Integrations- und Produktdemo, nicht ein produktiv freigegebenes Begutachtungssystem.
