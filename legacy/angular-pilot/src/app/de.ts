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

/** Texts of the case workbench (synthetic local preview). Wording follows the existing workbench. */
export const wb = {
  app: 'NormaCase', product: 'Prüfwerkstatt', badge: 'Synthetisch',
  search: 'Fälle suchen', searchPlaceholder: 'Fall, Aktenzeichen oder Notiz suchen (Strg+K)',
  theme: 'Darstellung', themes: { system: 'System', light: 'Hell', dark: 'Dunkel' } as Record<string, string>,
  more: 'Weitere Optionen', classic: 'Bisherige Prüfwerkstatt öffnen', review: 'Review-Modus (Anmeldung)', shortcuts: 'Tastenkürzel',
  shortcutText: 'Strg+K Suche · ↑/↓ Fall wählen · Enter Fall öffnen · Strg/Umschalt+Klick Mehrfachauswahl · Strg+A sichtbare Fälle · Esc Auswahl aufheben · Umschalt+F10 Kontextmenü · F4 Lesebereich · F5 Aktualisieren · Alt+← zurück zur Liste',
  loading: 'Arbeitsablage wird geladen …', unavailable: 'Die synthetische Arbeitsablage ist auf diesem Host nicht verfügbar. Starte die lokale Vorschau über das Startskript oder nutze den Review-Modus.',
  invalid: 'Die Antwort des lokalen Hosts ist ungültig und wird nicht angezeigt.', completionUnknown: 'Der Abschluss ist unbekannt. Die Ansicht wurde aktualisiert – bitte prüfen, bevor die Aktion erneut ausgeführt wird.',
  saved: 'Änderung gespeichert.', dispatchedNotice: 'Versand lokal simuliert. Das Paket wurde heruntergeladen; es wurde nichts extern übermittelt.',
  selectionLimit: 'Die Auswahl darf höchstens 100 Fälle enthalten.',
  // command bar
  newFolder: 'Neuer Ordner', open: 'Öffnen', refresh: 'Aktualisieren (F5)', move: 'Verschieben', mark: 'Markieren', bookmark: 'Lesezeichen',
  confirm: 'Bestätigen', dispatch: 'Versand vorbereiten', readingPane: 'Lesebereich', paneRight: 'Rechts', paneBottom: 'Unten', paneOff: 'Aus',
  density: 'Zeilenhöhe', compact: 'Kompakt', comfortable: 'Zweizeilig', filters: 'Filter', noSelection: 'Keine Fälle ausgewählt',
  // navigation
  navigation: 'Ablage und Arbeitslisten', all: 'Alle Fälle', queues: 'Arbeitslisten', personal: 'Meine Ablage', bookmarks: 'Lesezeichen', confirmed: 'Bestätigt',
  dispatched: 'Versand simuliert', unfiled: 'Ohne eigenen Ordner', platformCases: 'Plattformfälle einbeziehen',
  listsHelp: '„Alle Fälle“ zeigt Vorgänge ohne eigenen Ordner. Bestätigt und Versand simuliert sind Statusansichten.',
  addSubfolder: 'Unterordner anlegen', editFolder: 'Ordner bearbeiten', queueColor: 'Farbe ändern',
  queueNames: { approval: 'Zur Freigabe vorbereitet', clarification: 'Informationen nachfordern', review: 'Gegenprüfung erforderlich', technical: 'Technische Klärung', documents: 'Unterlagensichtung' } as Record<string, string>,
  nextSteps: {
    approval: 'Ergebnis prüfen und Bestätigung vorbereiten', clarification: 'Fehlende Angaben und Nachweise anfordern', review: 'Widersprüche und Belege gegenprüfen',
    technical: 'Technische Auffälligkeit klären', documents: 'Unterlagen sichten; fachliche Prüfung offen',
  } as Record<string, string>,
  confirmedNext: 'Versandpaket erstellen', completeNext: 'Lokales Versandpaket erstellt',
  // list
  list: 'Fallliste', subject: 'Fall / Auftrag', number: 'Aktenzeichen', status: 'Stand', next: 'Nächster Schritt', result: 'Prüfergebnis', documents: 'Dok.',
  folder: 'Ordner', contains: 'enthält', allStates: 'Alle', selectVisible: 'Sichtbare auswählen (max. 100)', selectCase: 'Fall auswählen', bookmarked: 'Lesezeichen',
  personalColor: 'Eigene Markierung', noCases: 'Keine Fälle für diese Auswahl.', openPoints: 'offene Punkte', platformCheck: 'Plattformprüfung',
  noRuleResult: 'Unterlagensichtung', pending: '…', ofCases: 'von', cases: 'Fällen', selected: 'ausgewählt', filterActive: 'Filter aktiv', noFilter: 'Kein Filter',
  resetFilters: 'Filter zurücksetzen', footer: 'Synthetische Demonstration · Nicht fachlich freigegeben · Versand simuliert',
  emptyPane: 'Fall in der Liste auswählen, um Ergebnis und Dokumente hier zu sehen.', resize: 'Größe des Lesebereichs ändern',
  // context menu & dialogs
  caseActions: 'Fallaktionen', openCase: 'Fall öffnen', moveTo: 'Verschieben nach', removeFromFolder: 'Aus Ordner entfernen',
  addBookmark: 'Lesezeichen setzen', removeBookmark: 'Lesezeichen entfernen', note: 'Notiz', confirmCase: 'Fall bestätigen', dispatchCases: 'Versand vorbereiten',
  clearColor: 'Markierung entfernen', customColor: 'Eigene Farbe', newFolderEllipsis: 'Neuer Ordner …',
  colors: { blue: 'Blau', teal: 'Türkis', green: 'Grün', amber: 'Bernstein', purple: 'Violett', rose: 'Rosa' } as Record<string, string>,
  dialogTitles: {
    folder: 'Ordner anlegen', rename: 'Ordner bearbeiten', delete: 'Ordner löschen', move: 'Auswahl verschieben', color: 'Farbe markieren',
    queueColor: 'Arbeitslistenfarbe', note: 'Fallnotiz', confirm: 'Bestätigung prüfen', dispatch: 'Lokalen Versand simulieren',
  } as Record<string, string>,
  parentFolder: 'Übergeordneter Ordner', folderName: 'Ordnername', folderColor: 'Ordnerfarbe', markColor: 'Markierungsfarbe', targetFolder: 'Zielordner',
  save: 'Speichern', cancel: 'Abbrechen', delete: 'Löschen', deleteFolder: 'Ordner löschen', simulateDispatch: 'Versand simulieren',
  confirmHelp: 'Nach dem Bestätigen erscheint der Fall unter „Bestätigt“. Danach kann ein lokales Versandpaket erstellt werden. Die Bestätigung ist keine medizinische oder rechtliche Freigabe.',
  dispatchHelp: 'Erstellt und lädt ein lokales Versandpaket herunter. Anschließend erscheint der Fall unter „Versand simuliert“. Es wird nichts extern übermittelt.',
  deleteHelp: 'Der Ordner wird entfernt. Die Fälle bleiben erhalten und erscheinen wieder ohne eigenen Ordner.',
  // case file
  back: 'Zur Liste', fullView: 'Vollansicht', copyNumber: 'Aktenzeichen kopieren', copyResult: 'Ergebnistext kopieren', copied: 'In die Zwischenablage kopiert.',
  copyFailed: 'Kopieren nicht möglich. Bitte die Berechtigung für die Zwischenablage prüfen.',
  nextStep: 'Nächster Bearbeitungsschritt',
  steps: {
    confirm: 'Prüfergebnis und Belege prüfen, anschließend den Fall bestätigen.',
    blocked: 'Bestätigung gesperrt: Es liegt kein vollständiges, bestätigungsfähiges Prüfergebnis vor. Offene Angaben und Nachweise klären.',
    dispatch: 'Der Fall ist bestätigt. Jetzt das lokale Versandpaket erstellen.', complete: 'Versandpaket lokal erstellt. Es wurde nichts extern übermittelt.',
  } as Record<string, string>,
  confirmAction: 'Fall bestätigen', dispatchAction: 'Versandpaket erstellen', showOpenPoints: 'Offene Punkte ansehen',
  tabs: { overview: 'Überblick', documents: 'Dokumente', evidence: 'Angaben & Fundstellen', data: 'Falldaten', history: 'Verlauf & Notiz' } as Record<string, string>,
  fileLoading: 'Akte wird geladen …', fileMissing: 'Für diesen Fall liegt keine Dokumentakte vor.',
  publicReference: 'Öffentlich dokumentierte Referenzprüfung – nicht fachlich freigegeben', synthetic: 'Fiktive Plattformprüfung', documentOnly: 'Synthetische Unterlagen zur Sichtung',
  noRule: 'Für diesen Vorgang ist keine Regel hinterlegt. Die Unterlagen können gesichtet werden; eine Entscheidung wird nicht erzeugt.',
  findings: 'Was muss geklärt werden?', request: 'Anliegen', question: 'Fragestellung', background: 'Hintergrund', scope: 'Umfang der Prüfung',
  reasoning: 'Begründung', source: 'Grundlage', sourceVersion: 'Stand', openSource: 'Quelle öffnen',
  evidenceHelp: 'Aus dem kontrollierten Text der synthetischen PDFs übernommen – keine automatische Erkennung beliebiger Arztbriefe oder Scans.',
  criterion: 'Angabe', value: 'Verwendeter Wert', foundIn: 'Fundstelle', proofs: 'Nachweise', noObservations: 'Keine Fundstellen erfasst.',
  caseData: 'Falldaten', person: 'Person', queue: 'Arbeitsliste', owner: 'Zuständig', unassigned: 'Nicht zugewiesen', validation: 'Validierungsstufe',
  release: 'Wissensstand', assessedOn: 'Bewertungsdatum', platform: 'Plattformstand', personalFolder: 'Eigener Ordner', none: '–',
  history: 'Organisationsverlauf', noHistory: 'Noch keine Organisationsschritte für diesen Fall.', historyHelp: 'Nur lokale Ablageaktionen dieser Sitzung; die fachliche Bewertung bleibt unverändert.',
  noteHelp: 'Die Notiz ist nur in dieser lokalen Sitzung sichtbar.', saveNote: 'Notiz speichern',
  actions: { CREATE_FOLDER: 'Ordner angelegt', UPDATE_FOLDER: 'Ordner geändert', DELETE_FOLDER: 'Ordner gelöscht', QUEUE_COLOR: 'Listenfarbe geändert', MOVE: 'Verschoben', COLOR: 'Markiert', BOOKMARK: 'Lesezeichen geändert', NOTE: 'Notiz gespeichert', CONFIRM: 'Bestätigt', DISPATCH: 'Versand simuliert' } as Record<string, string>,
  // reader
  reader: 'Dokumentleser', files: 'Unterlagen', page: 'Seite', pages: 'Seiten', of: 'von', previousPage: 'Vorherige Seite', nextPage: 'Nächste Seite', pageNumber: 'Seitenzahl',
  zoom: 'Zoom', fitWidth: 'Seitenbreite', fitPage: 'Ganze Seite', zoomIn: 'Vergrößern', zoomOut: 'Verkleinern', rotate: 'Drehen (R)', newTab: 'In neuem Tab öffnen',
  download: 'Herunterladen', textFile: 'Textdatei', scan: 'Scan', pdf: 'PDF', noPreview: 'Für dieses Dokument gibt es keine Seitenvorschau. Bitte das Original öffnen.',
  previewFailed: 'Die Seitenvorschau konnte nicht geladen werden.', textLoading: 'Text wird geladen …', textFailed: 'Der Text konnte nicht geladen werden.',
  jumpedTo: 'Fundstelle', readerHint: 'Bild/Bild-Tasten oder ←/→ blättern · +/− zoomen · 0 Seitenbreite · R drehen',
  // decision trace
  trace: {
    result: 'Ergebnis', missingFields: 'Fehlende Pflichtangaben', reasoning: 'Begründung', rule: 'Regel', ruleVersion: 'Version', ruleSource: 'Regelquelle',
    release: 'Wissensstand', assessedOn: 'Bewertungsdatum', outputs: 'Teilbewertungen', expected: 'erwartet', actual: 'vorliegend', range: 'Bereich', minimum: 'mindestens', sourceVersion: 'Stand',
    conditionResults: { MATCHED: 'erfüllt', NOT_MATCHED: 'nicht erfüllt', UNKNOWN: 'offen' } as Record<string, string>,
    conditionKinds: {
      all: 'Alle folgenden Bedingungen', any: 'Mindestens eine Bedingung', not: 'Darf nicht erfüllt sein', requires_evidence: 'Nachweis erforderlich',
      field_equals: 'Angabe', number_gte: 'Zahlenwert', number_in_range: 'Zahlenwert im Bereich',
    } as Record<string, string>,
    numericKinds: { field: 'Wert', range_lookup: 'Bereichszuordnung', sum: 'Summe', max: 'Maximum' } as Record<string, string>,
    values: { yes: 'Ja', no: 'Nein', na: 'Nicht anwendbar', unknown: 'Unbekannt', unrecorded: 'nicht erfasst' },
  },
} as const;

/** Looks up a German label for a technical ID without inheriting prototype keys. */
export function label(labels: Record<string, string>, id: string | null | undefined, fallback: string): string {
  return id != null && Object.hasOwn(labels, id) ? labels[id] : fallback;
}
