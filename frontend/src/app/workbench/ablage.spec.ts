import { folderTree, isPersonalView } from './ablage';

describe('Meine Ablage', () => {
  const folders = [
    { id: 'b', parentId: null, label: 'Rückfragen', color: '#497ab7' },
    { id: 'c', parentId: 'b', label: 'Diese Woche', color: '#24708f' },
    { id: 'a', parentId: null, label: 'Ärztliche Rückmeldung', color: '#287554' },
    { id: 'x', parentId: 'fehlt', label: 'Verwaist', color: '#a54866' },
  ];

  it('ordnet Ordner als Baum, alphabetisch je Ebene; verwaiste Ordner stehen oben', () => {
    expect(folderTree(folders).map((f) => `${f.depth}:${f.label}`))
      .toEqual(['0:Ärztliche Rückmeldung', '0:Rückfragen', '1:Diese Woche', '0:Verwaist']);
  });

  it('erkennt persönliche Ansichten für gespeicherte Abfragen', () => {
    expect(isPersonalView('bookmarks', folders)).toBe(true);
    expect(isPersonalView('folder:c', folders)).toBe(true);
    expect(isPersonalView('folder:gelöscht', folders)).toBe(false);
    expect(isPersonalView('review', folders)).toBe(false);
  });
});
