# NormaCase Assist – Sicherheits- und Datenschutzgrenzen

**Status:** ausschließlich synthetischer Entwicklungsprototyp, keine Freigabe für Patientendaten und keine behauptete DSGVO-/BSI-Zertifizierung. Issue #298.

## Vertrauensgrenzen

NormaCase Assist darf weder die deterministische Rule Engine noch Approval, Outbound oder Knowledge Releases ersetzen oder ändern. Das führende MD-Fachsystem verwaltet reale Akten und Berechtigungen. Für reale MD-Systeme existiert in diesem Slice **kein** Anschluss. Der lokale Assistent verarbeitet nur die fest mitgelieferte synthetische Testdatei und liefert unverbindliche, menschlich zu prüfende Quellenhinweise.

## Sicherheitsmatrix (nachprüfbarer Stand)

| Anforderung | Nachweis im aktuellen Prototyp | Status |
| --- | --- | --- |
| Default OFF und keine Laufzeit-Downloads | CLI-Schalter `--enable-synthetic-assist`; keine Gewichte oder Downloads in CI | Umgesetzt/testbar |
| Kein Cloud-Ziel | Festes `127.0.0.1:18081`, System-Proxy ignoriert, Redirects abgelehnt | Umgesetzt im CLI; Netzwerksperre als Betreiberkontrolle offen |
| Minimierung sensibler Daten | Ausschließlich feste synthetische Fixture, keine beliebigen Dateipfade oder offene HTTP-Endpunkte | Umgesetzt für den Prototyp |
| Beschränkte Ressourcennutzung | Dokument max. 16 KiB, Modellantwort max. 64 KiB, max. 8 Hinweise, 20 Sekunden Timeout | Umgesetzt; Regressionen teilweise |
| Unvertrauenswürdige Dokumente/Prompt Injection | Dokument wird als untrusted markiert; Ausgaben brauchen exaktes Zitat, Wert im Zitat, feste Typen und JSON-Schema | Technische Eindämmung, kein vollständiger Schutz |
| Keine automatischen fachlichen Entscheidungen | Nur Quellenhinweise mit `NEEDS_HUMAN_REVIEW`; kein Schreibzugriff auf Fälle/Regeln/Versand | Umgesetzt/testbar |
| Authentifizierung lokaler Modellanfragen | Bearer-Secret von mindestens 32 Zeichen, HTTP nur Loopback | Umgesetzt für CLI; produktive Identitäten offen |
| Sparsame Protokolle | Keine Prompt-/Dokumentprotokollierung durch den CLI; allgemeine Fehlermeldung | CLI umgesetzt; Modellserver-Logs gesondert konfigurieren |
| Lieferkettensicherheit | Einmalige manuelle Bereitstellung, Empfehlung für Versions-/Hash-Pinning | Dokumentiert, kein attestierter Modellimport |
| Fallbezogene Berechtigungen/SSO | Institutionelle Rechte müssen aus vertrauenswürdiger Quelle geprüft werden | Nicht Teil des CLI-Prototyps; vor Patientenbezug offen |
| Aufbewahrung, Audit, DSFA und getrennte Datenhaltung | Maßgeblich sind Betriebsmodell, konkretes MD-Verfahren und Rechtsgrundlage | Offen, fachkundig/institutionell zu entscheiden |
| Medizinische Qualität, Fehlerquoten, Bias und Aufsicht | Eigener Evaluationskorpus und fachliche Freigabe notwendig | Offen, keine behauptete Eignung |

Ein exaktes Zitat belegt **nur**, dass ein Textfragment im Dokument vorkommt; es beweist keine medizinische Wahrheit und keine zulässige Rechtsfolge. Insbesondere dürfen OCR und LLM keinen unbekannten oder fehlenden Befund automatisch als vorhanden markieren.

## Öffentliche Quellen für die Maßnahmenplanung

- [DSGVO Art. 9, 25, 32, 35 (EUR-Lex)](https://eur-lex.europa.eu/eli/reg/2016/679/oj): Gesundheitsdaten, Datenschutz durch Technikgestaltung, TOMs und ggf. DSFA.
- [SGB V § 276](https://www.gesetze-im-internet.de/sgb_5/__276.html) und [SGB X § 80](https://www.gesetze-im-internet.de/sgb_10/__80.html): institutionell anhand der konkreten Verarbeitung zu prüfen.
- [DSK Orientierungshilfen KI / technische und organisatorische Maßnahmen (Juni 2025)](https://datenschutzkonferenz-online.de/orientierungshilfen.html): Auswahl, Entwicklung, Einführung, Betrieb und Monitoring.
- [BSI IT-Grundschutz](https://www.bsi.bund.de/DE/Themen/Unternehmen-und-Organisationen/Standards-und-Zertifizierung/IT-Grundschutz/it-grundschutz_node.html): insbesondere CON.8, ORP.4 und APP.3.1.
- [OWASP ASVS](https://github.com/OWASP/ASVS) und [OWASP LLM Top 10 (2025)](https://genai.owasp.org/llm-top-10/): Autorisierung, Prompt Injection, Datenabfluss, Supply Chain, Output-Validierung.

Die Referenzen sind **keine** Checkliste mit automatisch erfülltem Compliance-Status. Die MDR-/EU-KI-Verordnung-Einstufung ergibt sich erst aus konkreter Zweckbestimmung und Einsatz; sie bleibt offen.

## Produktiv-Gates

Vor Zugriff auf reale Akten sind verbindlich zu klären: MD-Verarbeitungsverzeichnis/Rechtsgrundlage und Verantwortlichkeiten; DSFA soweit erforderlich; Rollen-/Fallrechte und technische Konten; sichere Dokumentenübernahme; getrennte Datenhaltung und Löschkonzept; Audit; Schlüsselverwaltung; Netzwerkausgangssperre; Modellgewichte-Provenienz; OCR-Qualität, medizinische Qualität und fachliche Abnahme; dokumentierte Security-Tests und Freigabe der Zielumgebung.

**Bis dahin:** Modell nur lokal und opt-in mit synthetischer Fixture, kein produktiver Upload, keine Änderung an Gutachten, Akten oder Freigaben.
