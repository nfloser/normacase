import { parseDocumentCase, safeExternalUrl } from './document-case';

const file = {
  caseId: 'reference-x', title: 'NC-1 · Akte', scope: 'Nur Demo', context: { request: 'A', question: 'F', background: 'H' }, validationLevel: 'SYNTHETIC',
  documents: [{ id: 'document-1', title: 'Arztbrief', filename: 'document-1.pdf', mediaType: 'application/pdf', sha256: 'x', pages: 2, sourceId: 's', previews: [{ page: 1, filename: 'a', sha256: 'b' }, { page: 2, filename: 'c', sha256: 'd' }] }],
  observations: [{ id: 'document-1', page: 2, field: 'care_grade', value: '4', method: 'CONTROLLED_TEXT' }],
  findings: ['Hinweis'], fieldLabels: { care_grade: 'Pflegegrad' }, evidenceLabels: { proof: 'Bescheid' }, outputLabels: { o: { label: 'Ergebnis', choices: { A: 'Wahl A' } } },
  source: { id: 's', title: 'Quelle', version: '1', url: 'https://example.org/q.pdf', scope: 'Teil' },
  assessmentJson: null, inputJson: '{"evidence":{"proof":"MISSING"}}',
};

describe('case file contract', () => {
  it('reads a case file with provenance and recorded evidence', () => {
    const parsed = parseDocumentCase(JSON.stringify(file), 'reference-x')!;
    expect(parsed.documents[0].previewPages).toEqual([1, 2]);
    expect(parsed.observations[0]).toEqual({ documentId: 'document-1', page: 2, field: 'care_grade', value: '4', method: 'CONTROLLED_TEXT' });
    expect(parsed.evidence).toEqual([['proof', 'MISSING']]);
    expect(parsed.outputLabels['o'].choices['A']).toBe('Wahl A');
    expect(parsed.assessmentJson).toBeNull();
  });
  it('rejects a file for another case or with broken parts', () => {
    expect(parseDocumentCase(JSON.stringify(file), 'reference-y')).toBeNull();
    expect(parseDocumentCase(JSON.stringify({ ...file, documents: [{ ...file.documents[0], id: '../etc' }] }), 'reference-x')).toBeNull();
    expect(parseDocumentCase(JSON.stringify({ ...file, fieldLabels: { a: 1 } }), 'reference-x')).toBeNull();
    expect(parseDocumentCase(JSON.stringify({ ...file, findings: [1] }), 'reference-x')).toBeNull();
    expect(parseDocumentCase('nope', 'reference-x')).toBeNull();
  });
  it('only links https sources', () => {
    expect(safeExternalUrl('https://example.org/x')).toBe('https://example.org/x');
    expect(safeExternalUrl('javascript:alert(1)')).toBeNull();
    expect(safeExternalUrl('http://example.org')).toBeNull();
  });
});
