/** Deutsche Oberflächentexte. Technische Codes werden hier übersetzt, nie im Template. */
export const de = {
  product: 'NormaCase',
  workbench: 'Prüfwerkstatt',
  queuesHeading: 'Arbeitskörbe',
  queues: {
    open: 'Neu eingegangen',
    clarification: 'Rückfragen',
    approval: 'Zur Freigabe',
    done: 'Erledigt',
  },
  emptyQueueTitle: 'Keine Vorgänge in diesem Korb',
  emptyQueueHint: 'Vorgänge erscheinen hier, sobald das Fachsystem sie übergibt.',
  noCaseTitle: 'Kein Vorgang ausgewählt',
  noCaseHint: 'Wähle links einen Vorgang, um Kriterien, Belege und Dokumente zu sehen.',
  caseFile: 'Fallakte',
  system: {
    loading: 'Verbinde …',
    ok: 'Verbunden',
    offline: 'Backend nicht erreichbar',
    platform: 'Plattform',
    retention: {
      'zero-retention': 'Datenhaltung: ohne Vorhaltung',
    } as Record<string, string>,
  },
  syntheticNotice: 'Nur synthetische Daten',
  auth: {
    title: 'Anmeldung',
    intro: 'Melde dich mit deinem Konto des Hauses an, um Arbeitskörbe und Fallakten zu sehen.',
    login: 'Anmelden',
    logout: 'Abmelden',
    initializing: 'Anmeldung wird geprüft …',
    demoHint: 'Vorführung: z. B. gutachterin.a / demo',
    errors: {
      'idp-unreachable': 'Der Anmeldedienst ist nicht erreichbar.',
      'login-failed': 'Die Anmeldung konnte nicht abgeschlossen werden. Bitte erneut versuchen.',
    },
  },
  roles: {
    gutachter: 'Gutachter:in',
    freigabe: 'Freigabe',
    'regelwerk-redaktion': 'Regelwerk-Redaktion',
    'regelwerk-freigabe': 'Regelwerk-Freigabe',
    administration: 'Administration',
    integration: 'Fachsystem',
    revision: 'Revision',
  } as Record<string, string>,
} as const;

export type QueueId = keyof typeof de.queues;
