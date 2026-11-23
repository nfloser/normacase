import { DEFAULT_VIEW, rowTone, sanitize, storableFilters } from './case-view';

describe('Ansicht der Fallliste', () => {
  it('färbt Zeilen nach Bearbeitungsstand', () => {
    expect(rowTone({ state: 'IN_REVIEW', outcome: 'NOT_SUPPORTED' })).toBe('ready');
    expect(rowTone({ state: 'AWAITING_APPROVAL', outcome: 'SUPPORTED' })).toBe('ready');
    expect(rowTone({ state: 'IN_CLARIFICATION', outcome: 'INCOMPLETE' })).toBe('missing');
    expect(rowTone({ state: 'IN_CLARIFICATION', outcome: 'HUMAN_REVIEW' })).toBe('conflict');
    expect(rowTone({ state: 'CLOSED', outcome: 'SUPPORTED' })).toBe('done');
  });

  it('fällt bei kaputten oder fremden Daten auf den Standard zurück', () => {
    expect(sanitize(null)).toEqual(DEFAULT_VIEW);
    expect(sanitize({ columns: ['gibtsnicht'], sort: { key: 'x' } })).toEqual(DEFAULT_VIEW);
    const view = sanitize({ columns: ['number', 'number', 'release'], rowColors: false, sort: { key: 'number', asc: false } });
    expect(view.columns).toEqual(['number', 'release']);
    expect(view.rowColors).toBe(false);
    expect(view.sort).toEqual({ key: 'number', asc: false });
  });

  it('speichert Filter auf Namen nicht', () => {
    expect(storableFilters({ subject: 'Lehmann', result: 'Angaben unvollständig', number: '  ', fremd: 'x' }))
      .toEqual({ result: 'Angaben unvollständig' });
    const view = sanitize({ queries: [{ name: 'Offen', queue: 'clarification', filters: { subject: 'Lehmann', status: 'Rückfrage offen' } }] });
    expect(view.queries[0].filters).toEqual({ status: 'Rückfrage offen' });
  });
});
