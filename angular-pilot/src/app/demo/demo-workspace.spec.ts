import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { App } from '../app';

const assessment = JSON.stringify({ formatVersion: 2, platformVersion: 'p', assessment: { knowledgeRelease: 'r', assessmentDate: '2026-10-10', outcome: 'HUMAN_REVIEW', missingRequiredFields: [],
  ruleTrace: { ruleId: 'R-1', ruleVersion: 1, sourceId: 's', conditionResult: 'UNKNOWN', outcome: 'HUMAN_REVIEW', source: { title: 'Fiktive Regelquelle', version: '1' },
    condition: { kind: 'requires_evidence', result: 'UNKNOWN', field: null, expected: null, actual: null, minimum: null, maximum: null, evidenceRequirementId: 'proof', evidenceStatus: 'MISSING', children: [] } } } });
const file = {
  caseId: 'demo-g-review', title: 'Synthetischer Arbeitslistenfall', scope: 'Fiktiv', context: null, validationLevel: 'SYNTHETIC',
  documents: [{ id: 'document-1', title: 'Prüfauftrag', mediaType: 'application/pdf', pages: 2, previews: [{ page: 1 }, { page: 2 }] },
              { id: 'transmission', title: 'Textfassung', mediaType: 'text/plain', pages: 1 }],
  observations: [{ id: 'document-1', page: 2, field: 'request_complete', value: 'YES', method: 'CONTROLLED_TEXT' }],
  findings: ['Ein Nachweis fehlt.'], fieldLabels: { request_complete: 'Auftragsangaben vollständig' }, evidenceLabels: { proof: 'Synthetischer Nachweis' }, outputLabels: {},
  source: { id: 's', title: 'Quelle', version: '1', url: 'https://example.org', scope: 'Teil' }, assessmentJson: assessment, inputJson: '{"evidence":{"proof":"MISSING"}}',
};

async function openDemo() {
  await TestBed.configureTestingModule({ imports: [App], providers: [provideHttpClient(), provideHttpClientTesting()] }).compileComponents();
  const fixture = TestBed.createComponent(App); fixture.detectChanges(); await fixture.whenStable();
  const element: HTMLElement = fixture.nativeElement;
  const http = TestBed.inject(HttpTestingController);
  [...element.querySelectorAll('button')].find(b => b.textContent?.includes('Demo öffnen'))!.click(); fixture.detectChanges();
  const queues = http.expectOne('/api/work-queues');
  expect(queues.request.headers.has('Authorization')).toBe(false);
  queues.flush(JSON.stringify({ totalCases: 7, queues: [{ queueId: 'review', items: Array.from({ length: 7 }, (_, i) => ({ caseId: i ? 'demo-volume-review-0' + i : 'demo-g-review', stateId: 'manual-review' })) }] }));
  http.expectOne('/api/document-cases').flush(JSON.stringify({ cases: [{ caseId: 'reference-a', title: 'NC-1 · Erika Muster', scope: 'Demo', context: { request: 'R', question: 'Frage?', background: 'B' } }] }));
  fixture.detectChanges(); await fixture.whenStable(); fixture.detectChanges();
  return { fixture, element, http };
}

describe('Synthetische Demo', () => {
  it('shows totals, a short queue preview, search and reference files', async () => {
    const { fixture, element, http } = await openDemo();
    expect(element.textContent).toContain('Fälle insgesamt');
    expect(element.textContent).toContain('Gegenprüfung erforderlich');
    expect(element.querySelectorAll('button[data-case-id^="demo-"]').length).toBe(6);
    [...element.querySelectorAll('button')].find(b => b.textContent?.includes('Alle anzeigen'))!.click(); fixture.detectChanges();
    expect(element.querySelectorAll('button[data-case-id^="demo-"]').length).toBe(7);
    const search = element.querySelector<HTMLInputElement>('input[type="search"]')!;
    search.value = 'erika'; search.dispatchEvent(new Event('input')); fixture.detectChanges();
    expect(element.querySelectorAll('button[data-case-id^="demo-"]').length).toBe(0);
    expect(element.querySelector('button[data-case-id="reference-a"]')).toBeTruthy();
    http.verify();
  });

  it('opens a case file with pages, provenance and the recorded reasoning', async () => {
    const { fixture, element, http } = await openDemo();
    element.querySelector<HTMLButtonElement>('button[data-case-id="demo-g-review"]')!.click(); fixture.detectChanges();
    const request = http.expectOne('/api/document-cases/demo-g-review');
    expect(request.request.responseType).toBe('text');
    request.flush(JSON.stringify(file)); fixture.detectChanges(); await fixture.whenStable(); fixture.detectChanges();
    expect(element.querySelector('h2')!.textContent).toContain('Synthetischer Arbeitslistenfall');
    expect(document.activeElement?.tagName).toBe('H2');
    expect(element.textContent).toContain('Manuelle Prüfung erforderlich');
    expect(element.textContent).toContain('Nachweis erforderlich: Synthetischer Nachweis – Fehlend');
    expect(element.textContent).toContain('Fiktive Regelquelle');
    const image = () => element.querySelector<HTMLImageElement>('.page-image')!;
    expect(image().getAttribute('src')).toBe('/api/document-cases/demo-g-review/documents/document-1/pages/1');
    element.querySelector<HTMLButtonElement>('.link-row')!.click(); fixture.detectChanges();
    expect(image().getAttribute('src')).toBe('/api/document-cases/demo-g-review/documents/document-1/pages/2');
    expect(element.textContent).toContain('Seite 2 von 2');
    [...element.querySelectorAll('button')].find(b => b.getAttribute('aria-label') === 'Vergrößern')!.click(); fixture.detectChanges();
    expect(image().style.width).toBe('125%');
    [...element.querySelectorAll<HTMLButtonElement>('.doc-tab')][1].click(); fixture.detectChanges();
    http.expectOne('/api/document-cases/demo-g-review/documents/transmission').flush('Kontrollierter Text'); fixture.detectChanges();
    expect(element.querySelector('.doc-text')!.textContent).toBe('Kontrollierter Text');
    element.querySelector('nc-case-file')!.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }));
    fixture.detectChanges(); await fixture.whenStable(); fixture.detectChanges();
    expect(document.activeElement?.getAttribute('data-case-id')).toBe('demo-g-review');
    http.verify();
  });

  it('rejects a file for another case and explains a missing demo host', async () => {
    const { fixture, element, http } = await openDemo();
    element.querySelector<HTMLButtonElement>('button[data-case-id="demo-g-review"]')!.click(); fixture.detectChanges();
    http.expectOne('/api/document-cases/demo-g-review').flush(JSON.stringify({ ...file, caseId: 'other' })); fixture.detectChanges();
    expect(element.querySelector('[role="alert"]')!.textContent).toContain('ungültig');
    expect(element.querySelector('.page-image')).toBeNull();
  });

  it('cancels requests when the demo is left', async () => {
    await TestBed.configureTestingModule({ imports: [App], providers: [provideHttpClient(), provideHttpClientTesting()] }).compileComponents();
    const fixture = TestBed.createComponent(App); fixture.detectChanges(); await fixture.whenStable();
    const element: HTMLElement = fixture.nativeElement; const http = TestBed.inject(HttpTestingController);
    [...element.querySelectorAll('button')].find(b => b.textContent?.includes('Demo öffnen'))!.click(); fixture.detectChanges();
    const queues = http.expectOne('/api/work-queues'); http.expectOne('/api/document-cases');
    fixture.destroy();
    expect(queues.cancelled).toBe(true);
  });
});
