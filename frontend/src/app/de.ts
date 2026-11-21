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
} as const;

export type QueueId = keyof typeof de.queues;
