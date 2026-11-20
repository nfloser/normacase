import { canConfirm, canDispatch, caseIdentity, folderTree, parseWorkspace, processingStep, selectRange, WorkCase } from './workspace';

const org = { folderId: null, color: null, bookmark: false, note: '', confirmed: false, dispatched: false };
const item = (id: string, extra: Partial<WorkCase> = {}): WorkCase => ({ caseId: id, title: 'T', queueId: 'approval', canConfirm: true, organization: { ...org }, ...extra });

describe('workspace model', () => {
  it('accepts the local workspace snapshot and drops invalid colours', () => {
    const parsed = parseWorkspace({ revision: 3, localSimulation: true, folders: [{ id: 'folder-1', label: 'A', color: '#112233', parentId: null }],
      queueColors: { approval: '#abcdef', review: 'red' }, audit: [{ sequence: 1, action: 'MOVE', caseIds: ['a'], folderId: null }],
      cases: [{ caseId: 'a', title: 'NC-2026-1 · P · S', queueId: 'approval', canConfirm: true, organization: { ...org, color: 'javascript:x' } }] })!;
    expect(parsed.revision).toBe(3);
    expect(parsed.cases[0].organization.color).toBeNull();
    expect(parsed.queueColors).toEqual({ approval: '#abcdef' });
    expect(parsed.audit[0]).toEqual({ sequence: 1, action: 'MOVE', caseIds: ['a'] });
  });
  it('rejects incomplete snapshots', () => {
    expect(parseWorkspace({ revision: 1, folders: [], cases: [{ caseId: 'a' }] })).toBeNull();
    expect(parseWorkspace({ revision: 1, folders: [{ id: 'f', label: 'x', color: 'blue' }], cases: [] })).toBeNull();
    expect(parseWorkspace(null)).toBeNull();
  });
  it('splits synthetic titles for display only', () => {
    expect(caseIdentity('reference-x', 'NC-2026-4103 · Sabine Winter · Krankenfahrt · Nachweise', 'P')).toEqual({ number: 'NC-2026-4103', person: 'Sabine Winter', subject: 'Krankenfahrt · Nachweise' });
    expect(caseIdentity('demo-g-review', 'Synthetischer Arbeitslistenfall', 'Plattformprüfung')).toEqual({ number: 'demo-g-review', person: '', subject: 'Plattformprüfung' });
  });
  it('mirrors server eligibility for confirmation and dispatch', () => {
    expect(processingStep(item('a'))).toBe('confirm');
    expect(processingStep(item('a', { canConfirm: false }))).toBe('blocked');
    expect(processingStep(item('a', { organization: { ...org, confirmed: true } }))).toBe('dispatch');
    expect(processingStep(item('a', { organization: { ...org, confirmed: true, dispatched: true } }))).toBe('complete');
    expect(canConfirm([item('a'), item('b', { canConfirm: false })])).toBe(false);
    expect(canDispatch([item('a', { organization: { ...org, confirmed: true } })])).toBe(true);
    expect(canConfirm([])).toBe(false);
  });
  it('selects ranges within the visible order and refuses more than 100', () => {
    const ids = Array.from({ length: 150 }, (_, i) => 'c' + i);
    expect(selectRange(ids, 'c2', 'c4')).toEqual(['c2', 'c3', 'c4']);
    expect(selectRange(ids, 'c4', 'c2')).toEqual(['c2', 'c3', 'c4']);
    expect(selectRange(ids, 'hidden', 'c3')).toEqual(['c3']);
    expect(selectRange(ids, 'c0', 'c120')).toBeNull();
  });
  it('orders folders as a tree and survives cycles', () => {
    const tree = folderTree([
      { id: 'b', label: 'Beta', color: '#000000', parentId: null }, { id: 'a', label: 'alpha', color: '#000000', parentId: null },
      { id: 'c', label: 'Kind', color: '#000000', parentId: 'a' }, { id: 'x', label: 'X', color: '#000000', parentId: 'y' }, { id: 'y', label: 'Y', color: '#000000', parentId: 'x' },
    ]);
    expect(tree.map(f => `${f.id}:${f.depth}`)).toEqual(['a:0', 'c:1', 'b:0']);
  });
});
