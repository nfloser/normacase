/** German presentation resources; technical queue identifiers remain unchanged. */
export const de = {
  app: 'NormaCase', queuesTitle: 'Arbeitslisten', section: 'FALLVERWALTUNG',
  description: 'Fälle zur Prüfung und Bearbeitung im Überblick.', logout: 'Abmelden',
  login: 'Anmelden', loginHelp: 'Gib den lokalen Zugangsschlüssel für die Fallprüfung ein.',
  credential: 'Zugangsschlüssel', open: 'Arbeitslisten öffnen', loading: 'Arbeitslisten werden geladen …',
  darkTheme: 'Dunkelmodus', pageCount: 'Fälle auf dieser Seite',
  empty: 'Keine Fälle auf dieser Seite.', case: 'Fall', more: 'Weitere Fälle',
  credentialRequired: 'Bitte einen Zugangsschlüssel eingeben.',
  invalidResponse: 'Die Antwort der Arbeitsliste ist ungültig.',
  expired: 'Die Anmeldung ist ungültig oder abgelaufen.',
  forbidden: 'Für diese Arbeitsliste fehlt die Berechtigung.',
  networkError: 'Die Arbeitsliste konnte nicht geladen werden. Bitte erneut anmelden.',
  unknownQueue: 'Unbekannte Arbeitsliste',
  queueLabels: { approval: 'Freigabe erforderlich', clarification: 'Klärung erforderlich', review: 'Prüfung erforderlich', completed: 'Abgeschlossen' } as Record<string, string>,

  openCase: 'Fall öffnen', back: 'Zurück zu den Arbeitslisten', caseDetail: 'Falldetail',
  loadingCase: 'Fall wird geladen …',
  caseNotFound: 'Der Fall ist nicht vorhanden oder für diese Anmeldung nicht freigegeben.',
  caseForbidden: 'Für diesen Fall fehlt die Berechtigung.',
  caseInvalid: 'Die Antwort zum Fall ist ungültig und wird nicht angezeigt.',
  caseNetworkError: 'Der Fall konnte nicht geladen werden. Bitte später erneut versuchen.',
  readOnly: 'Nur Ansicht. Freigaben, Übersteuerungen und Korrekturen bleiben in der bestehenden Prüfwerkstatt.',
  outcome: 'Systemergebnis', state: 'Bearbeitungsstand',
  noAssessment: 'Keine Bewertung vorhanden', unreadableAssessment: 'Bewertung nicht lesbar',
  facts: 'Versionen und Bezüge', packId: 'Knowledge Pack', knowledgeRelease: 'Knowledge Release',
  caseRevision: 'Fallrevision', processRevision: 'Prozessrevision', assessmentId: 'Bewertungs-ID', auditRevision: 'Auditrevision',
  missingFields: 'Fehlende Pflichtangaben',
  evidence: 'Evidenzen', evidenceField: 'Angabe', evidenceStatus: 'Status', noEvidence: 'Keine Evidenzen hinterlegt.',
  audit: 'Unveränderliche Prüfhistorie', noAudit: 'Keine Einträge.',
  actor: 'Durch', reason: 'Begründung', overrideOutcome: 'Abweichendes Ergebnis',
  unknown: 'Unbekannt', unknownState: 'Unbekannter Bearbeitungsstand',
  outcomes: {
    SUPPORTED: 'Voraussetzungen erfüllt', NOT_SUPPORTED: 'Voraussetzungen nicht erfüllt', INCOMPLETE: 'Angaben unvollständig',
    HUMAN_REVIEW: 'Manuelle Prüfung erforderlich', NOT_APPLICABLE: 'Nicht anwendbar',
  } as Record<string, string>,
  states: {
    'awaiting-approval': 'Wartet auf menschliche Freigabe', 'waiting-information': 'Informationen fehlen',
    'manual-review': 'Menschliche Gegenprüfung erforderlich', accepted: 'Freigegeben', overridden: 'Übersteuert',
  } as Record<string, string>,
  evidenceStatuses: { PRESENT: 'Vorhanden', MISSING: 'Fehlend', CONFLICTING: 'Widersprüchlich' } as Record<string, string>,
  auditKinds: { ASSESSMENT_CREATED: 'Systemprüfung aufgezeichnet', HUMAN_REVIEW_RECORDED: 'Menschliche Review-Entscheidung aufgezeichnet' } as Record<string, string>,
  dispositions: { ACCEPT_SYSTEM_RESULT: 'Systemergebnis übernommen', OVERRIDE: 'Systemergebnis übersteuert' } as Record<string, string>,
} as const;

/** Texts of the synthetic demo workspace (anonymous local preview). */
export const demo = {
  startTitle: 'Synthetische Demo', startHelp: 'Arbeitslisten, Fallakten und Begründungen mit erfundenen Daten – ohne Anmeldung und ohne Datenbank, nur lesend.',
  start: 'Demo öffnen', reviewTitle: 'Review-Modus', reviewHelp: 'Für den lokalen Review-Host mit PostgreSQL und Zugangsschlüssel.',
  classic: 'Bisherige Prüfwerkstatt', badge: 'Synthetisch · nur Lesen', leave: 'Demo verlassen',
  loading: 'Arbeitslisten werden geladen …', unavailable: 'Die Demo-Daten sind auf diesem Host nicht verfügbar. Starte die lokale Vorschau über das Startskript.',
  invalid: 'Die Antwort des lokalen Hosts ist ungültig und wird nicht angezeigt.',
  overviewTitle: 'Arbeitslisten', overviewHelp: 'Die Fälle wurden beim Start deterministisch bewertet und automatisch einer Arbeitsliste zugeordnet.',
  totalCases: 'Fälle insgesamt', search: 'Fälle filtern', searchPlaceholder: 'Fall-ID oder Titel', noMatch: 'Keine passenden Fälle.',
  showAll: 'Alle anzeigen', showLess: 'Weniger anzeigen', referenceTitle: 'Referenzakten', referenceHelp: 'Ausführlichere synthetische Akten mit Fallkontext, Arztbriefen und Bescheiden.',
  queues: {
    approval: 'Zur Freigabe vorbereitet', clarification: 'Informationen nachfordern', review: 'Gegenprüfung erforderlich', technical: 'Technische Klärung',
  } as Record<string, string>,
  queueHelp: {
    approval: 'Vollständig und eindeutig bewertet – wartet auf menschliche Freigabe.',
    clarification: 'Pflichtangaben fehlen; das Ergebnis bleibt bewusst unvollständig.',
    review: 'Unbekannte oder widersprüchliche Evidenz – eine Fachkraft muss prüfen.',
    technical: 'Technischer Fehler bei der Übernahme; keine fachliche Bewertung.',
  } as Record<string, string>,
  states: {
    'awaiting-approval': 'Wartet auf Freigabe', 'waiting-information': 'Informationen fehlen', 'manual-review': 'Gegenprüfung', 'integration-error': 'Technischer Fehler',
  } as Record<string, string>,
  referenceState: 'Referenzakte',
  // case workspace
  back: 'Zurück zur Übersicht', caseLoading: 'Akte wird geladen …', caseMissing: 'Für diesen Fall liegt keine Akte vor.',
  caseError: 'Die Akte konnte nicht geladen werden.', file: 'Akte', documents: 'Dokumente', page: 'Seite', of: 'von',
  previousPage: 'Vorherige Seite', nextPage: 'Nächste Seite', zoomIn: 'Vergrößern', zoomOut: 'Verkleinern', zoomReset: 'Zoom zurücksetzen',
  openOriginal: 'Original öffnen', noPreview: 'Für dieses Dokument gibt es keine Seitenvorschau.', textLoading: 'Text wird geladen …', textError: 'Der Text konnte nicht geladen werden.',
  pageImage: 'Seitenvorschau', result: 'Ergebnis', reasoning: 'Begründung', findings: 'Hinweise', provenance: 'Fundstellen',
  provenanceHelp: 'Angaben, die aus kontrolliertem PDF-Text übernommen wurden. Ein Klick öffnet die Seite.',
  noProvenance: 'Keine Fundstellen erfasst.', evidence: 'Nachweise', noEvidence: 'Keine Nachweise erfasst.', context: 'Fallkontext',
  request: 'Anliegen', question: 'Fragestellung', background: 'Hintergrund', scope: 'Geltungsbereich', source: 'Quelle', sourceVersion: 'Stand',
  noAssessment: 'Keine fachliche Bewertung – Aktenvorbereitung ohne Regelwerk.', unreadable: 'Die Bewertung ist nicht lesbar.',
  missingFields: 'Fehlende Pflichtangaben', rule: 'Regel', ruleVersion: 'Version', ruleSource: 'Regelquelle', release: 'Wissensstand', assessedOn: 'Bewertungsdatum',
  outputs: 'Weitere Ergebnisse', expected: 'erwartet', actual: 'vorliegend', range: 'Bereich', minimum: 'mindestens',
  disclaimer: 'Synthetische Demonstration. Keine fachliche Freigabe, keine echte Begutachtung.',
  conditionResults: { MATCHED: 'erfüllt', NOT_MATCHED: 'nicht erfüllt', UNKNOWN: 'offen' } as Record<string, string>,
  conditionKinds: {
    all: 'Alle folgenden Bedingungen', any: 'Mindestens eine Bedingung', not: 'Nicht erfüllt sein darf',
    requires_evidence: 'Nachweis erforderlich', field_equals: 'Angabe', number_gte: 'Zahlenwert', number_in_range: 'Zahlenwert im Bereich',
  } as Record<string, string>,
  numericKinds: { field: 'Wert', range_lookup: 'Bereichszuordnung', sum: 'Summe', max: 'Maximum' } as Record<string, string>,
  values: { yes: 'Ja', no: 'Nein', na: 'Nicht anwendbar', unknown: 'Unbekannt', unrecorded: 'nicht erfasst' },
} as const;

/** Looks up a German label for a technical ID without inheriting prototype keys. */
export function label(labels: Record<string, string>, id: string | null | undefined, fallback: string): string {
  return id != null && Object.hasOwn(labels, id) ? labels[id] : fallback;
}
