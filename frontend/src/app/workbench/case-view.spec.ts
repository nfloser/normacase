import { DEFAULT_VIEW, rowTone, sanitize, storableFilters } from './case-view';

describe('Ansicht der Fallliste', () => {
  it('färbt Zeilen nach Bearbeitungsstand', () => {
    expect(rowTone({ state: 'IN_REVIEW', outcome: 'NOT_SUPPORTED' })).toBe('ready');
    expect(rowTone({ state: 'AWAITING_APPROVAL', outcome: 'SUPPORTED' })).toBe('approval');
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

  it('liest Spaltenbreiten aus der Ansicht und aus gespeicherten Abfragen', () => {
    const view = sanitize({ columnSizes: { number: 20, status: 210.5, subject: 1000, request: '220', bogus: 777 },
      queries: [{ name: 'Alle', queue: 'all', columnSizes: { number: 190 } }] });
    expect(view.columnSizes).toEqual({ number: 80, status: 211, subject: 900 });
    expect(view.queries[0].columnSizes).toEqual({ number: 190 });
  });

  it('speichert Filter auf Namen nicht', () => {
    expect(storableFilters({ subject: 'Lehmann', result: 'Angaben unvollständig', number: '  ', fremd: 'x' }))
      .toEqual({ result: 'Angaben unvollständig' });
    const view = sanitize({ queries: [{ name: 'Offen', queue: 'clarification', filters: { subject: 'Lehmann', status: 'Rückfrage offen' } }] });
    expect(view.queries[0].filters).toEqual({ status: 'Rückfrage offen' });
  });
});
