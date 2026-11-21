# System Design

Stand: 21.11.2026 · gilt für den Neuaufbau (Angular, Spring Boot, PostgreSQL)

## 1. Zweck

NormaCase bewertet strukturierte Begutachtungsfälle gegen versionierte, quellgebundene Regelwerke und liefert ein reproduzierbares Ergebnis mit vollständiger Begründung. Es ersetzt keine Gutachterin und keinen Gutachter. Es nimmt ihnen die mechanische Prüfung ab, macht fehlende Angaben sichtbar und lenkt unklare Fälle gezielt in menschliche Bearbeitung.

Die fachliche Grundlage ist die Master-Spezifikation des Referenzstands (`legacy/docs/project/MASTER_SPECIFICATION.md`). Dieses Dokument übernimmt deren Invarianten und beschreibt, wie der Neuaufbau sie umsetzt.

## 2. Systemgrenze

NormaCase ist **kein Fallverwaltungssystem**. Führend für Fälle, Personen, Dokumente und Fristen ist das Fachsystem des Hauses (etwa MDconnect oder MEDIKOS). NormaCase arbeitet als Baustein daneben:

```
Fachsystem ──(Fall, strukturiert)──▶ NormaCase ──(Ergebnis + Entscheidungspfad)──▶ Fachsystem
                                         │
                                         └─ Prüfwerkstatt (optional, für Rückfragen und Freigabe)
```

| Gehört zu NormaCase | Gehört nicht zu NormaCase |
|---|---|
| Normalisierung und Vollständigkeitsprüfung eingehender Fälle | Führende Fallakte, Stammdaten, Fristenverwaltung |
| Deterministische Bewertung gegen Knowledge Packs | Medizinische Entscheidung, rechtliche Freigabe |
| Entscheidungspfad, Nachweis, Audit | Dauerhafte Archivierung von Gesundheitsdaten |
| Arbeitskörbe für Rückfragen und Freigaben | Kommunikation mit Versicherten |
| Pflege und Freigabe von Regelwerken | Abrechnung |

## 3. Rollen

| Rolle | Aufgabe | Typische Rechte |
|---|---|---|
| Gutachter:in | Fälle prüfen, Rückfragen klären, Ergebnis bestätigen | Fälle des eigenen Korbs lesen, prüfen, bestätigen |
| Freigabe (Vier-Augen) | Bestätigte Ergebnisse freigeben, wo die Hausregel das verlangt | Freigeben, zurückweisen; nie eigene Prüfung freigeben |
| Fachredaktion Regelwerk | Knowledge Packs pflegen, Quellen und Tests hinterlegen | Entwürfe anlegen, Review anfordern |
| Fachliche Freigabe Regelwerk | Knowledge Release aktivieren | Aktivieren; nie eigene Änderung |
| Administration | Benutzer, Körbe, Adapter konfigurieren | Keine Einsicht in Fallinhalte |
| Integrationskonto (technisch) | Fälle einliefern, Ergebnisse abholen | Nur eigene Schnittstelle, keine Oberfläche |
| Revision/Datenschutz | Nachweise einsehen | Audit lesen, keine Fallinhalte |

Trennung der Pflichten ist eine Regel, keine Empfehlung: Wer prüft, gibt nicht frei; wer ein Regelwerk ändert, aktiviert es nicht.

## 4. Anwendungsfälle

1. **Fall einliefern (headless).** Das Fachsystem übergibt einen Fall über die API. NormaCase prüft Schema und Vollständigkeit, bewertet und antwortet mit Ergebnis und Entscheidungspfad. Der Fallinhalt wird danach verworfen.
2. **Fall mit Klärungsbedarf.** Ergebnis `INCOMPLETE` oder `HUMAN_REVIEW`: Der Fall landet für die Dauer der Bearbeitung im Arbeitskorb. Gutachter:in sieht in der Prüfwerkstatt genau die offenen Kriterien und die zugehörigen Belege.
3. **Bestätigen und freigeben.** Eindeutige Fälle können gesammelt bestätigt werden, soweit die Hausregel das erlaubt. Freigabe erfolgt durch eine zweite Person.
4. **Ergebnis zurückgeben.** Das freigegebene Ergebnis geht über den Ausgangsadapter zurück. Danach bleibt nur der Nachweis (siehe Datenhaltung).
5. **Bewertung nachvollziehen.** Revision fragt: Warum kam bei Vorgang X am Tag Y dieses Ergebnis heraus? NormaCase belegt es über Eingabe-Hash, Plattformversion, Regelwerksversion und Entscheidungspfad, ohne den Fallinhalt zu speichern.
6. **Regelwerk ändern.** Quelle → Wirkungsanalyse → Änderung → Review → Tests → Freigabe → Release → Aktivierung. Alte Releases bleiben abrufbar.
7. **Datei-Import (Ausnahme).** Für Demo, Test und Störfall. Gleicher Ablauf wie 1, nur mit anderem Eingang.

## 5. Domänenmodell

```
KnowledgeRelease 1─* KnowledgePack 1─* Rule ─* Criterion
       │                                 │
       │                                 └─ SourceReference (Quelle, Fundstelle, Gültigkeit)
       │
Assessment ── referenziert ── KnowledgeRelease, PlatformVersion, Stichtag
   │
   ├─ Input (normalisiert; nur während der Verarbeitung)
   ├─ CriterionResult * ─ (Kriterium, Wert oder UNKNOWN, Regel, Quelle, Ergebnis)
   └─ Outcome: SUPPORTED | NOT_SUPPORTED | INCOMPLETE | HUMAN_REVIEW | NOT_APPLICABLE

CaseProcess (Workflow) ── Zustand: RECEIVED → ASSESSED → IN_REVIEW → CONFIRMED → APPROVED → DISPATCHED → CLOSED
   └─ ReviewEvent * (nur anfügend)

DecisionEvidence (dauerhaft) ── Vorgangs-ID des Fachsystems, Eingabe-Hash, Versionen, Regel-IDs, Ergebnis, Zeitpunkte, handelnde Rolle
```

Bewertungsergebnis und Bearbeitungszustand sind getrennt: `SUPPORTED` ist ein Ergebnis, `IN_REVIEW` ein Zustand. Ein `UNKNOWN` wird nie zu ja oder nein.

## 6. Qualitätsziele

| Ziel | Messbar als |
|---|---|
| Determinismus | Gleiche Eingabe, Plattformversion, Release und Stichtag ergeben bitgleiches Ergebnis-JSON. Paritätstest gegen den Referenzstand. |
| Nachvollziehbarkeit | Jedes Ergebnis lässt sich bis zur Quelle zurückverfolgen; Entscheidungspfad ist Teil der Antwort. |
| Datensparsamkeit | Nach Abschluss sind keine Fallinhalte mehr in der Datenbank (automatischer Test). |
| Betrieb ohne Internet | Start und Betrieb ohne externe Dienste, CDN, Webfonts, Telemetrie. |
| Sicherheit | Fallbezogene Rechte, nicht unterscheidbare 404 bei fehlendem Recht, Prüfliste nach BSI TR-03161. |
| Antwortzeit | Bewertung eines Falls p95 < 200 ms ohne Netzlatenz (Lasttest ab Phase 5). |
| Barrierefreiheit | Prüfwerkstatt ohne axe-Verstöße der Stufe „serious“, vollständig per Tastatur bedienbar. |
| Sprache | Gesamte Oberfläche de-DE; technische Codes nie ungeübersetzt sichtbar. |

## 7. Annahmen und offene Fragen

- Schnittstellen von MDconnect und MEDIKOS sind nicht öffentlich. Bis eine verbindliche Spezifikation vorliegt, gibt es nur einen generischen Vertrag mit synthetischen Beispielen.
- Ob das Haus Ergebnisse vor Rückgabe immer freigeben lässt, ist eine Hausregel und daher Konfiguration.
- Welche Nachweise eine Revision tatsächlich verlangt, ist mit Datenschutz und Revision abzustimmen; der Entwurf steht in `03-datenschutz-und-datenhaltung.md`.
- Fachliche Regelinhalte bleiben `PUBLIC_REFERENCE`, bis eine fachliche Freigabe vorliegt.
