# Geprüfte Installation, Aktualisierung und Wiederherstellung

Die Pakete für Windows x64 und Linux x64 enthalten die vollständige lokale Anwendung,
Browserdateien, .NET-Laufzeit, synthetisches Wissen, PostgreSQL-Provisionierung und
diese Anleitung. Die Anwendung braucht zur Laufzeit weder SDK, Node.js, Internet,
CDN noch KI-Dienst. Der optionale Installationshelfer benötigt Python 3.11 oder neuer
und ausschließlich dessen Standardbibliothek. PostgreSQL wird separat betrieben.
Die geprüfte Grenze bleibt ein lokaler, ausschließlich synthetischer Betriebsmodus.

## Paket ausdrücklich auswählen und prüfen

Wähle einen vollständigen Quellcommit mit erfolgreicher CI, Sicherheitsanalyse und
Paketprüfung. Lade das zugehörige native ZIP und seine `.zip.sha256`-Datei.
Prüfe Herkunft und erwarteten Commit separat; eine Prüfsumme allein bestätigt keine
Herkunft. Verwende keinen automatisch wechselnden „latest“-Stand.

Der Helfer liegt im Repository und in neuen Paketen unter `scripts`. Beispiele
verwenden den bereits geprüften Helfer und ausdrücklich eingesetzte Commitwerte:

```text
python scripts/manage_installation.py stage --root INSTALLATIONSORDNER --archive PAKET.zip --rid linux-x64 --commit VOLLSTAENDIGER_COMMIT
python scripts/manage_installation.py activate --root INSTALLATIONSORDNER --commit VOLLSTAENDIGER_COMMIT
python scripts/manage_installation.py status --root INSTALLATIONSORDNER
python scripts/manage_installation.py run --root INSTALLATIONSORDNER
```

Unter Windows verwende `--rid win-x64`. Die Version liegt unveränderlich unter
`releases/COMMIT`; `current.json` nennt den aktivierten und vorherigen Stand.
Staging überschreibt keine vorhandene Version. Vor Aktivierung werden alle
Paketdateien erneut geprüft. Ein laufender verwalteter Dienst oder ein belegter
lokaler Dienstport verhindert den Wechsel. Nach einem harten Prozessabbruch prüfe
zuerst, dass kein Dienst mehr läuft, bevor du eine verbliebene lokale Sperrdatei
manuell entfernst. Die Startumgebung und Konfiguration bleiben außerhalb der Pakete.

## PostgreSQL und persistente Fallprüfung

Für den synthetischen Containerbetrieb sind Docker und das vorab bereitgestellte
Image `postgres:18.6` erforderlich. In einer Offlineumgebung übertrage das geprüfte
Image vorab und lade es lokal. Verwende bei jedem Versionswechsel denselben
Compose-Projektnamen, damit das Datenvolume nicht mit dem Versionsordner wechselt:

```text
docker compose -p normacase-synthetic -f compose.synthetic-review.yml up -d
```

Übernimm die erforderlichen Kennwörter aus dem Betreiber-Secretwerkzeug in die
Prozessumgebung. Speichere sie weder in Paketen, Versionskontrolle, Argumenten noch
Abnahmeprotokollen. Der Container veröffentlicht PostgreSQL ausschließlich lokal.
Ein vorhandener PostgreSQL-Dienst kann stattdessen über seine genaue Verbindung
verwendet werden.

Richte genaue lokale Testidentitäten, Fall- und Aktionsgrants nach
[Synthetische Fallprüfung](SYNTHETIC_REVIEW.de.md) und
[Testidentitäten](../security/LOCAL_SYNTHETIC_IDENTITIES.de.md) ein. Ergänze nach
Bedarf ausdrücklich `CORRECT` und `CLARIFY`; beide setzen `READ` voraus.
Verwende getrennte Verbindungen für Migration und Laufzeit:
`ConnectionStrings__SyntheticReviewMigrations` und
`ConnectionStrings__SyntheticReview`. Der Laufzeitaccount erhält ausschließlich
SELECT/INSERT; die Provisionierung liegt unter `ops/postgresql`.

Der aktive Dienst wird mit der vom Betreiber bereitgestellten Umgebung gestartet.
Schlüssel werden nicht durch den Installationshelfer gespeichert. Prüfe
`/health/live` und `/health/ready` sowie die angezeigte Paketidentität.
Bereits gespeicherte Prüfungen werden beim Neustart nicht mit neuer Plattform oder
neuem Wissen überschrieben.

## Aktualisierung mit nachvollziehbarer Rückkehr

1. Neuen genauen Commit prüfen und neben der bisherigen Version bereitstellen.
2. Eingänge und Bearbeitung anhalten, laufenden Dienst geordnet beenden.
3. Konsistente PostgreSQL-Sicherung mit `pg_dump --format=custom` sowie eine
   Sicherung des externen Rückgabeordners erstellen. Auch Betreiberkonfiguration,
   genaue Paketidentitäten und die gewählte Wissensaktivierung separat sichern.
   Keine Sicherungsinhalte in Git oder CI-Protokolle übernehmen.
4. Prüfe Wiederherstellung und Integrität der Sicherung in einer frischen,
   getrennten Datenbank. Mit separaten Rollen deren Provisionierung erneut anwenden.
5. Genaues neues Paket aktivieren und starten. Migrationen sind versioniert,
   checksum-geprüft und transaktional. Fehler verhindern Bereitschaft; der Dienst
   meldet keine erfolgreiche Installation allein aufgrund eines Paketwechsels.
6. Bereitschaft, originalgetreue Fallhistorie, neue Fallrevision, neue menschliche
   Freigabe und genaue Zustellbelege prüfen. Erst danach Bearbeitung wieder aufnehmen.

Bei Fehlern keine automatische Neuübertragung und keine geratenen Schema-Reparaturen.
Beende den Kandidaten. Stelle die Sicherung in einer frischen Datenbank und den
gesicherten Rückgabeordner wieder her, verwende deren genaue Betreiberverbindungen
und aktiviere das ausdrücklich gewählte vorige Paket. Eine reine Umschaltung der
Programmdateien macht Schemaänderungen nicht rückgängig. Vor Rückkehr sind
Bereitschaft und die gesicherten Historien erneut zu prüfen.

Wenn nach dem Versionswechsel bereits neue Bearbeitung erfolgte, muss der Betreiber
entscheiden, wie diese Daten erhalten werden. Ein älterer Sicherungsstand kann spätere
Änderungen nicht enthalten. Aufbewahrung, RPO/RTO, TLS, produktive Identitäten und
Datenschutz-/Betriebsfreigaben sind tatsächliche Zielbetriebsentscheidungen.

## Ausgeführte technische Nachweise

Die CI baut das genau benannte vorherige und neue native Paket, prüft Integrität
und unveränderliches Staging, installiert auf einer frischen synthetischen Datenbank
und wechselt anschließend auf den Kandidaten. Originale Prüfungen behalten ihren
alten Plattformstand; neue Korrekturen erfassen den neuen Commit und benötigen
erneute Freigaben. Klärungsanfragen, ursprüngliche und neue Zustellbelege werden nach
Neustart und physischer Sicherung/Wiederherstellung exakt verglichen.

Zusätzlich werden ein Versionswechsel bei laufendem nativen Dienst und manipulierte
Pakete abgelehnt. Ein Start mit nicht erreichbarer Testdatenbank scheitert begrenzt;
die gültige Datenbank bleibt unverändert. Die Wiederherstellung des alten Pakets
gegen dessen eigenen Sicherungsstand und des Kandidaten gegen den aktuellen Stand
werden getrennt geprüft. Diese Nachweise sind technische synthetische Abnahme und
ersetzen keine institutionelle Betriebsfreigabe.
