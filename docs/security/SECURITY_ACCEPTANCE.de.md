# Sicherheitsanforderungen und institutionelle Abnahme

Stand: 11.10.2026. NormaCase ist weiterhin ausschließlich für synthetische Entwicklung
und Demonstration nachgewiesen. Keine Freigabe für echte Patientendaten und keine
Compliance-Zertifizierung. Dieser Katalog ist eine erste prüfbare Baseline, keine
abschließende Liste aller auf eine konkrete Institution anwendbaren Anforderungen.

## Verbindlicher Nachweisstand

[requirements.v1.json](requirements.v1.json) hält 23 stabile Kontrollen mit Quelle,
Quellenversion, Bedrohung, betroffener Grenze, Zuständigkeit, Nachweisverweisen,
Restarbeit und Folge-Issue fest. Änderungen werden im normalen PR-Verfahren versioniert.
`reviewed_on` bezeichnet die Inventur, keinen erfolgreichen Sicherheitstest.

| Status | Aussage |
| --- | --- |
| `SYNTHETIC_TEST_COVERAGE` | Passender synthetischer Regressionstest ist verknüpft. Sein aktuelles Ergebnis muss aus CI für den konkreten Commit stammen. |
| `OPEN` | Eine technische oder Nachweislücke bleibt offen, auch wenn Teiltests vorhanden sind. |
| `INSTITUTION_REVIEW` | Betreiber-, Datenschutz-, Rechts- oder Fachprüfung im Zielbetrieb fehlt. |

Keiner dieser Status bedeutet produktiv erfüllt. `NORMACASE`, `OPERATOR` und `SHARED`
trennen Entwicklungsaufgaben, Betreiberpflichten und gemeinsame Abnahme. Ein vorhandener
Testname beweist weder fachliche Korrektheit noch die Wirksamkeit im Zielbetrieb.

## Quellen und ihre Grenzen

- DSGVO: Art. 6/9 (Rechtsgrundlage), 25 (Gestaltung), 32 (risikogerechte Maßnahmen),
  35/36 (Folgenabschätzung); Betreiberprüfung vor realer Verarbeitung.
- SGB X §§67b, 80, 84 und SGB V §276: im konkreten Auftrag juristisch prüfen.
  Insbesondere vermittelt §276 NormaCase als Software keine eigene pauschale
  Verarbeitungsbefugnis. Auftragsverarbeitung und zulässige Empfänger sind gesondert zu klären.
- DSK: Orientierungshilfe KI und Datenschutz, Version 1.0 vom 06.05.2024. Leitlinie
  für optionale Unterstützung; kein vollständiger Anforderungskatalog.
- BSI: CON.8, ORP.4, APP.3.1, Edition 2023 als benannte Referenz, **nicht als behauptete
  aktuellste Edition**. Der Textabruf war bei dieser Inventur blockiert. Deshalb
  `METADATA_ONLY`, Detailzuordnung zu Einzelanforderungen offen. Keine BSI-Erfüllungsbehauptung.
- OWASP ASVS 5.0.0: freiwillige Prüfbaseline, kein Gesetz. Projekt-/Versionsmetadaten
  geprüft; Auswahl von Level, Einzelanforderungen und vollständige Verifikation noch offen.
- NormaCase-Projektregeln: eigene Sicherheitsvorgaben, getrennt von Gesetzen und Standards.

Originalquellen und Abruf-/Versionsangaben stehen im JSON. Externe Quellen sind
öffentlich recherchierte Referenzen, keine institutionell oder juristisch freigegebenen
Anforderungen. Eine Quelle ohne archivierten Originaltext und geprüften Hash ist nicht
historisch authentifiziert. Rechtlich maßgebliche Fassungen, zusätzliche Landes-/Institutions-
regeln und Aktualisierungen sind vor dem Pilot erneut zu prüfen.

## Datenflüsse und Vertrauensgrenzen

| Kanal | Inhalt / Speicher | Aktuell nachgewiesene Grenze | Vor realem Einsatz offen |
| --- | --- | --- | --- |
| Upstream → Intake | Normalisierte JSON/XML-Fakten, Evidenz, externe Identität → PostgreSQL | Begrenzte synthetische Adapter, Validierung, Idempotenz | Autoritativer Schnittstellenvertrag, Transportauthentifizierung und Zulässigkeit |
| IdP → API → Application | Verifizierter Akteur, aktuelle Fall-/Aktionsrechte | Synthetische lokale Identitäten, transaktionaler Rechteentzug | Institutioneller IdP, Rollen/Mandanten, Session-/MFA-Politik |
| Application → Engine | Strukturierte Fakten und exakte Knowledge-Version | Deterministische Auswertung, explizites UNKNOWN | Fachfreigabe der produktiven Packs und Inputqualität |
| Browser ↔ API | Arbeitsliste, Fallansicht, Review; Zugangsdaten im Arbeitsspeicher | Loopback-/Origin-Grenze, kein Browser-Storage für Reviewdaten | Produktive Lese-/Dokumentrechte und Reverse-Proxy-/TLS-Abnahme |
| Dokumentablage → Viewer | Synthetische PDFs, PNG und Text; außerhalb Webroot | Fall-/Dokument-Allowlist, Hash-/Fundstellenprüfung | Authentifizierte produktive Dokumentablage, Upload-/Malwarekonzept |
| Review → Outbound | Exaktes freigegebenes Ergebnis → synthetische Inbox/Dateien | Review-/Revisionsbindung, wiederholbare Zustellung | Empfängerbefugnis, Zielauthentifizierung, Vertrag und Exportaufbewahrung |
| PostgreSQL/Dateien → Backup → Restore | Historie, Dokumente, Ausgabedateien, Konfiguration | Synthetische physische CI-Wiederherstellungsprobe | Verschlüsselung, Schlüsselverwahrung, RPO/RTO, Aufbewahrung und Restore-Nachbereinigung |
| Host → Logs/Support | Nur notwendige technische Diagnose | Begrenzte öffentliche Fehler; vollständiger Lognachweis offen | Capture-Tests #302, Zugriff und Aufbewahrung der Betriebslogs |
| Optionaler Assistent | Vorgesehene lokale, prüfpflichtige Hinweise | Experiment #299 separat; nicht Teil dieser Freigabe | Sandbox, Modell-/Lizenz-/Qualitätsprüfung, keine Entscheidungshoheit |

Der Headless-Betrieb benutzt dieselben Application-Grenzen. Ein vorgeschaltetes
Fachsystem oder eine eigene Oberfläche ersetzt keine serverseitige Autorisierung.
Offline-Betrieb reduziert mögliche Abflusswege, beweist aber keine Sicherheit.

## Ausführbare Inventur

Ohne Netzwerk, Modelle oder zusätzliche Pakete:

```bash
python scripts/test_security_readiness.py -q
python scripts/check_security_readiness.py validate
python scripts/check_security_readiness.py readiness
```

`validate` prüft Schema, Basiskontrollen und lokale Quell-/Nachweisverweise und liefert
bei gültigem Katalog Exit 0. Es führt die verknüpften .NET-/Browsertests nicht aus.
`readiness` liefert für einen gültigen synthetischen Katalog immer `BLOCKED`,
`patient_data_authorized: false`, alle noch produktiv offenen Kontroll-IDs und Exit 2.
Ungültige oder nicht lesbare Eingaben liefern Exit 1, ohne Inhalt oder Parserdetails
auszugeben. Es gibt kein Approval-Flag und keine Ausnahme durch Grünfärben der
synthetischen Teststatus. CI prüft die Inventur; erfolgreiches CI ist keine Betriebsfreigabe.

Dieses Werkzeug ist ein **Abnahmebericht**, kein Runtime-Zugriffsschutz. Es schaltet
weder den Server noch Datenimporte um. Die bestehende synthetische Hostgrenze bleibt
verbindlich; produktive Freigabemechanismen brauchen später eine gesonderte, vertrauenswürdige
Deployment-/Identitätsintegration. Eine lokal editierbare JSON-Datei darf keine Freigabe erteilen.

## Institutionelles Freigabetor

Vor einem Pilot mit echten Daten müssen die zuständigen Personen separat abnehmen:

1. Zweck, Rechtsgrundlage, Sozialdatenschutz, Verantwortliche, DSFA-Prüfung und
   Klassifikation des konkreten Einsatzes.
2. Geprüfter IdP, Fall-/Aktionsrechte, Rollen-/Mandantentrennung und Entzug.
3. Speicher-/Transport-/Backupverschlüsselung, Schlüsselzuständigkeit und Zugriffsschutz.
4. Lösch-/Aufbewahrungs-/Legal-Hold-Regeln für alle Kopien einschließlich Sicherungen.
5. Wiederherstellung, Updates/Lieferkette, Vorfallbehandlung, Schulung und Logminimierung.
6. Autoritative Integration, fachliche Knowledge-/Workflow-Freigabe und menschliche Verantwortung.
7. Freigegebene institutionelle Sicherheitsprüfung mit Nachweisen für exakte Software-,
   Knowledge- und Konfigurationsversion sowie schriftlicher Betreiberfreigabe.

Keine Freigabe aus dieser Inventur ableiten. #302 behandelt Log-Capture-Nachweise;
#303 behandelt Aufbewahrung/Löschung. #301 bleibt für Detailmapping, zusätzliche
institutionelle Anforderungen und Folgeslices offen. Historische Assessments werden
nicht durch pauschales Entfernen der Append-only-Garantien löschbar gemacht.
