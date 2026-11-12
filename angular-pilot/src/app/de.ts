/** German presentation resources; technical queue identifiers remain unchanged. */
export const de = {
  app: 'NormaCase', queuesTitle: 'Arbeitslisten', section: 'FALLVERWALTUNG',
  description: 'Fälle zur Prüfung und Bearbeitung im Überblick.', logout: 'Abmelden',
  login: 'Anmelden', loginHelp: 'Gib den lokalen Zugangsschlüssel für die Fallprüfung ein.',
  credential: 'Zugangsschlüssel', open: 'Arbeitslisten öffnen', loading: 'Arbeitslisten werden geladen …',
  empty: 'Keine Fälle auf dieser Seite.', case: 'Fall', more: 'Weitere Fälle',
  credentialRequired: 'Bitte einen Zugangsschlüssel eingeben.',
  invalidResponse: 'Die Antwort der Arbeitsliste ist ungültig.',
  expired: 'Die Anmeldung ist ungültig oder abgelaufen.',
  forbidden: 'Für diese Arbeitsliste fehlt die Berechtigung.',
  networkError: 'Die Arbeitsliste konnte nicht geladen werden. Bitte erneut anmelden.',
  unknownQueue: 'Unbekannte Arbeitsliste',
  queueLabels: { approval: 'Freigabe erforderlich', clarification: 'Klärung erforderlich', review: 'Prüfung erforderlich', completed: 'Abgeschlossen' } as Record<string, string>,
} as const;
