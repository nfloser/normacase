import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting, TestRequest } from '@angular/common/http/testing';
import { Workbench } from './workbench';

const org = { folderId: null, color: null, bookmark: false, note: '', confirmed: false, dispatched: false };
const cases = [
  { caseId: 'reference-a', title: 'NC-2026-4101 · Marlene Berg · Krankenfahrt', queueId: 'approval', canConfirm: true, organization: { ...org } },
  { caseId: 'reference-b', title: 'NC-2026-4102 · Heinz Sommer · Pflichtangabe fehlt', queueId: 'clarification', canConfirm: false, organization: { ...org, note: 'Rückruf' } },
  { caseId: 'reference-c', title: 'NC-2026-4103 · Sabine Winter · Nachweise', queueId: 'review', canConfirm: false, organization: { ...org, bookmark: true } },
  { caseId: 'demo-g-review', title: 'Synthetischer Arbeitslistenfall', queueId: 'review', canConfirm: false, organization: { ...org } },
];
const snapshot = (revision = 0, patch: (c: typeof cases) => typeof cases = c => c) => ({ revision, localSimulation: true, folders: [], queueColors: {}, audit: [], cases: patch(structuredClone(cases)) });
const assessment = (outcome: string) => JSON.stringify({ formatVersion: 2, platformVersion: 'p', assessment: { outcome, knowledgeRelease: 'r', assessmentDate: '2026-10-10', missingRequiredFields: [], ruleTrace: null } });
const file = (caseId: string, outcome: string | null) => JSON.stringify({ caseId, title: 'T', scope: 'S', context: { request: 'Anlass ' + caseId, question: 'F', background: 'B' }, validationLevel: 'SYNTHETIC',
  documents: [{ id: 'document-1', title: 'Arztbrief', mediaType: 'application/pdf', pages: 1, previews: [{ page: 1 }] }], observations: [], findings: outcome === 'INCOMPLETE' ? ['Fehlt'] : [],
  fieldLabels: {}, evidenceLabels: {}, outputLabels: {}, source: null, assessmentJson: outcome ? assessment(outcome) : null, inputJson: '{}' });

async function setup() {
  await TestBed.configureTestingModule({ imports: [Workbench], providers: [provideHttpClient(), provideHttpClientTesting()] }).compileComponents();
  const fixture = TestBed.createComponent(Workbench);
  const http = TestBed.inject(HttpTestingController);
  fixture.detectChanges();
  http.expectOne('/api/demo-workspace').flush(JSON.stringify(snapshot()));
  const outcomes: Record<string, string | null> = { 'reference-a': 'SUPPORTED', 'reference-b': 'INCOMPLETE', 'reference-c': 'HUMAN_REVIEW', 'demo-g-review': 'HUMAN_REVIEW' };
  for (const id of Object.keys(outcomes)) http.match(`/api/document-cases/${id}`).forEach(r => r.flush(file(id, outcomes[id])));
  fixture.detectChanges(); await fixture.whenStable(); fixture.detectChanges();
  // the remaining files are requested as earlier ones complete
  for (let i = 0; i < 3; i++) { for (const id of Object.keys(outcomes)) http.match(`/api/document-cases/${id}`).forEach(r => r.flush(file(id, outcomes[id]))); fixture.detectChanges(); await fixture.whenStable(); }
  fixture.detectChanges();
  return { fixture, element: fixture.nativeElement as HTMLElement, http };
}
const rows = (e: HTMLElement) => [...e.querySelectorAll<HTMLTableRowElement>('tr[data-case-id]')].map(r => r.dataset['caseId']);
const button = (e: HTMLElement, text: string) => [...e.querySelectorAll<HTMLButtonElement>('button')].find(b => b.textContent?.trim().startsWith(text))!;
const nav = (e: HTMLElement, label: string) => [...e.querySelectorAll<HTMLButtonElement>('.nav-item')].find(b => b.querySelector('.nav-label')?.textContent?.trim() === label)!;
const row = (e: HTMLElement, id: string) => e.querySelector<HTMLTableRowElement>(`tr[data-case-id="${id}"]`)!;

describe('Prüfwerkstatt', () => {
  it('lists the reference files with result, status and request; platform cases on demand', async () => {
    const { fixture, element } = await setup();
    expect(rows(element)).toEqual(['reference-a', 'reference-b', 'reference-c']);
    expect(row(element, 'reference-a').textContent).toContain('Voraussetzungen erfüllt');
    expect(row(element, 'reference-b').textContent).toContain('Anlass reference-b');
    expect(row(element, 'reference-b').textContent).toContain('Rückruf');
    expect(row(element, 'reference-b').textContent).toContain('1 offene Punkte');
    const toggle = element.querySelector<HTMLInputElement>('.nav-toggle input')!;
    toggle.checked = true; toggle.dispatchEvent(new Event('change')); fixture.detectChanges();
    expect(rows(element)).toContain('demo-g-review');
  });

  it('filters by queue, bookmarks and search', async () => {
    const { fixture, element } = await setup();
    nav(element, 'Gegenprüfung erforderlich').click(); fixture.detectChanges();
    expect(rows(element)).toEqual(['reference-c']);
    nav(element, 'Lesezeichen').click(); fixture.detectChanges();
    expect(rows(element)).toEqual(['reference-c']);
    nav(element, 'Alle Fälle').click(); fixture.detectChanges();
    const search = element.querySelector<HTMLInputElement>('input[type="search"]')!;
    search.value = 'sommer'; search.dispatchEvent(new Event('input')); fixture.detectChanges();
    expect(rows(element)).toEqual(['reference-b']);
    expect(element.querySelector('.statusbar')!.textContent).toContain('Filter aktiv');
    button(element, 'Filter zurücksetzen').click(); fixture.detectChanges();
    expect(rows(element).length).toBe(3);
  });

  it('selects with click, ctrl and shift and opens the reading pane', async () => {
    const { fixture, element } = await setup();
    row(element, 'reference-a').dispatchEvent(new MouseEvent('click', { bubbles: true })); fixture.detectChanges();
    expect(element.querySelector('nc-case-view .case-title')!.textContent).toContain('Marlene Berg');
    row(element, 'reference-c').dispatchEvent(new MouseEvent('click', { bubbles: true, shiftKey: true })); fixture.detectChanges();
    expect(element.querySelectorAll('tr[aria-selected="true"]').length).toBe(3);
    row(element, 'reference-b').dispatchEvent(new MouseEvent('click', { bubbles: true, ctrlKey: true })); fixture.detectChanges();
    expect(element.querySelectorAll('tr[aria-selected="true"]').length).toBe(2);
    expect(element.querySelector('.statusbar')!.textContent).toContain('2 ausgewählt');
    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' })); fixture.detectChanges();
    expect(element.querySelectorAll('tr[aria-selected="true"]').length).toBe(0);
    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'F4' })); fixture.detectChanges();
    expect(element.querySelector('.reading-pane')).toBeNull();
  });

  it('sends organisation commands with the current revision and applies the new snapshot', async () => {
    const { fixture, element, http } = await setup();
    row(element, 'reference-a').dispatchEvent(new MouseEvent('contextmenu', { bubbles: true, cancelable: true, clientX: 100, clientY: 100 })); fixture.detectChanges();
    const menu = element.querySelector('.context-menu')!;
    [...menu.querySelectorAll<HTMLButtonElement>('button')].find(b => b.textContent?.includes('Lesezeichen setzen'))!.click(); fixture.detectChanges();
    const request: TestRequest = http.expectOne('/api/demo-workspace/commands');
    expect(request.request.body.action).toBe('BOOKMARK');
    expect(request.request.body.caseIds).toEqual(['reference-a']);
    expect(request.request.body.expectedRevision).toBe(0);
    expect(request.request.body.value).toBe(true);
    expect(typeof request.request.body.operationId).toBe('string');
    request.flush(JSON.stringify({ workspace: snapshot(1, c => { c[0].organization.bookmark = true; return c; }), packageJson: null }));
    await fixture.whenStable(); fixture.detectChanges();
    expect(row(element, 'reference-a').textContent).toContain('★');
    expect(element.querySelector('.toast')!.textContent).toContain('gespeichert');
  });

  it('confirms and dispatches only eligible cases and downloads the local package', async () => {
    const created: Blob[] = [];
    (URL as unknown as { createObjectURL: (b: Blob) => string }).createObjectURL = (b: Blob) => { created.push(b); return 'blob:x'; };
    (URL as unknown as { revokeObjectURL: () => void }).revokeObjectURL = () => undefined;
    const { fixture, element, http } = await setup();
    row(element, 'reference-b').dispatchEvent(new MouseEvent('click', { bubbles: true })); fixture.detectChanges();
    expect(button(element, '✓ Fall bestätigen').disabled).toBe(true);
    row(element, 'reference-a').dispatchEvent(new MouseEvent('click', { bubbles: true })); fixture.detectChanges();
    button(element, '✓ Fall bestätigen').click(); fixture.detectChanges(); await fixture.whenStable(); fixture.detectChanges();
    element.querySelector('dialog form')!.dispatchEvent(new Event('submit', { cancelable: true })); fixture.detectChanges();
    const confirm = http.expectOne('/api/demo-workspace/commands');
    expect(confirm.request.body.action).toBe('CONFIRM');
    confirm.flush(JSON.stringify({ workspace: snapshot(1, c => { c[0].organization.confirmed = true; return c; }) }));
    await fixture.whenStable(); fixture.detectChanges(); await fixture.whenStable(); fixture.detectChanges();
    expect(element.querySelector('dialog')).toBeNull();
    button(element, '✉ Versandpaket erstellen').click(); fixture.detectChanges(); await fixture.whenStable(); fixture.detectChanges();
    element.querySelector('dialog form')!.dispatchEvent(new Event('submit', { cancelable: true })); fixture.detectChanges();
    const dispatch = http.expectOne('/api/demo-workspace/commands');
    expect(dispatch.request.body).toEqual(expect.objectContaining({ action: 'DISPATCH', caseIds: ['reference-a'], expectedRevision: 1 }));
    dispatch.flush(JSON.stringify({ workspace: snapshot(2, c => { c[0].organization.confirmed = true; c[0].organization.dispatched = true; return c; }), packageJson: '{"formatVersion":1}' }));
    await fixture.whenStable(); fixture.detectChanges();
    expect(created.length).toBe(1);
    expect(element.textContent).toContain('Versand lokal simuliert');
    expect(element.querySelector('.statusbar')).toBeTruthy();
  });

  it('shows the server message and refreshes when a command is refused', async () => {
    const { fixture, element, http } = await setup();
    row(element, 'reference-a').dispatchEvent(new MouseEvent('click', { bubbles: true })); fixture.detectChanges();
    button(element, '☆ Lesezeichen').click(); fixture.detectChanges();
    http.expectOne('/api/demo-workspace/commands').flush(JSON.stringify({ code: 'workspace_conflict', message: 'Die Ablage wurde zwischenzeitlich geändert.' }), { status: 409, statusText: 'Conflict' });
    await Promise.resolve(); fixture.detectChanges();
    http.expectOne('/api/demo-workspace').flush(JSON.stringify(snapshot(5)));
    await fixture.whenStable(); fixture.detectChanges();
    expect(element.querySelector('[role="alert"]')!.textContent).toContain('zwischenzeitlich geändert');
  });

  it('offers the review mode when the host has no synthetic workspace', async () => {
    await TestBed.configureTestingModule({ imports: [Workbench], providers: [provideHttpClient(), provideHttpClientTesting()] }).compileComponents();
    const fixture = TestBed.createComponent(Workbench);
    let review = false;
    fixture.componentInstance.openReview.subscribe(() => review = true);
    fixture.detectChanges();
    TestBed.inject(HttpTestingController).expectOne('/api/demo-workspace').flush({}, { status: 404, statusText: 'Not Found' });
    fixture.detectChanges();
    const element: HTMLElement = fixture.nativeElement;
    expect(element.textContent).toContain('nicht verfügbar');
    [...element.querySelectorAll<HTMLButtonElement>('.empty-state button')][0].click();
    expect(review).toBe(true);
  });
});
