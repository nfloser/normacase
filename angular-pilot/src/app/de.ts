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

/** Looks up a German label for a technical ID without inheriting prototype keys. */
export function label(labels: Record<string, string>, id: string | null | undefined, fallback: string): string {
  return id != null && Object.hasOwn(labels, id) ? labels[id] : fallback;
}
