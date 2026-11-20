import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { App } from './app';

const detailJson = JSON.stringify({
  caseId: 'demo-g-review', caseRevision: '90071992547409931234', processRevision: '2', stateId: 'manual-review',
  assessmentId: 'assessment-1', packId: 'demo-g', auditRevision: '1', allowedActions: [],
  assessmentJson: JSON.stringify({ assessment: { outcome: 'HUMAN_REVIEW', knowledgeRelease: 'demo-g@1.0.0', missingRequiredFields: ['befund'] } }),
  evidence: { befund: 'MISSING' },
  audit: [{ sequence: '1', kind: 'ASSESSMENT_CREATED', occurredAtUtc: '2026-10-04T08:00:00Z', actorId: 'system', disposition: null, reason: null, overrideOutcome: null }],
});

async function openQueue() {
  window.location.hash = '#review';
  const fixture = TestBed.createComponent(App); fixture.detectChanges(); await fixture.whenStable(); fixture.detectChanges();
  const element: HTMLElement = fixture.nativeElement;
  const input = element.querySelector('input')!; input.value = 'test-key'; input.dispatchEvent(new Event('input')); await fixture.whenStable();
  element.querySelector('form')!.dispatchEvent(new Event('submit'));
  const http = TestBed.inject(HttpTestingController);
  http.expectOne('/api/review/work-queues').flush({ queues: [{ queueId: 'review', items: [{ caseId: 'demo-g-review' }] }], nextPageCursor: null });
  fixture.detectChanges(); await fixture.whenStable(); fixture.detectChanges();
  return { fixture, element, http };
}
function caseButton(element: HTMLElement) { return element.querySelector<HTMLButtonElement>('button[data-case-id="demo-g-review"]')!; }
function backButton(element: HTMLElement) { return [...element.querySelectorAll('button')].find(b => b.textContent?.includes('Zurück zu den Arbeitslisten'))!; }

describe('Arbeitslisten', () => {
  beforeEach(() => { window.location.hash = '#review'; });
  afterEach(() => { window.location.hash = ''; });
  beforeEach(async () => { await TestBed.configureTestingModule({ imports: [App], providers: [provideHttpClient(), provideHttpClientTesting()] }).compileComponents(); });
  it('switches the appearance and reports its state', async () => {
    const fixture = TestBed.createComponent(App); fixture.detectChanges(); await fixture.whenStable(); fixture.detectChanges();
    const element: HTMLElement = fixture.nativeElement;
    const toggle = element.querySelector<HTMLButtonElement>('.theme-toggle')!;
    expect(toggle).toBeTruthy();
    expect(element.querySelector('.app-shell')!.getAttribute('data-theme')).toBe('light');
    toggle.click(); fixture.detectChanges();
    expect(element.querySelector('.app-shell')!.getAttribute('data-theme')).toBe('dark');
    expect(toggle.getAttribute('aria-pressed')).toBe('true');
    expect(toggle.textContent).toContain('Dunkelmodus');
    toggle.click(); fixture.detectChanges();
    expect(element.querySelector('.app-shell')!.getAttribute('data-theme')).toBe('light');
    expect(toggle.getAttribute('aria-pressed')).toBe('false');
  });
  it('loads a page with a bearer token and follows the cursor', async () => {
    const fixture = TestBed.createComponent(App); fixture.detectChanges(); await fixture.whenStable(); fixture.detectChanges();
    const element: HTMLElement = fixture.nativeElement;
    const input = element.querySelector('input')!; input.value = 'test-key'; input.dispatchEvent(new Event('input')); await fixture.whenStable();
    element.querySelector('form')!.dispatchEvent(new Event('submit')); fixture.detectChanges();
    const http = TestBed.inject(HttpTestingController);
    const first = http.expectOne('/api/review/work-queues');
    expect(first.request.headers.get('Authorization')).toBe('Bearer test-key');
    expect(first.request.credentials).toBe('omit');
    first.flush({ queues: [{ queueId: 'review', items: [{caseId: 'synthetic-1'}] }], nextPageCursor: 'demo-1' });
    fixture.detectChanges(); expect(element.textContent).toContain('synthetic-1');
    [...element.querySelectorAll('button')].find(b => b.textContent?.includes('Weitere Fälle'))!.click();
    const next = http.expectOne('/api/review/work-queues?afterCaseId=demo-1');
    next.flush({ queues: [], nextPageCursor: null }); fixture.detectChanges();
    expect(element.textContent).not.toContain('synthetic-1'); http.verify();
  });
  it('clears credentials after authentication failure', async () => {
    const fixture = TestBed.createComponent(App); fixture.detectChanges(); await fixture.whenStable(); fixture.detectChanges();
    const element: HTMLElement = fixture.nativeElement;
    const input = element.querySelector('input')!; input.value = 'wrong'; input.dispatchEvent(new Event('input')); await fixture.whenStable();
    element.querySelector('form')!.dispatchEvent(new Event('submit'));
    const http = TestBed.inject(HttpTestingController); http.expectOne('/api/review/work-queues').flush({}, {status: 401, statusText: 'Unauthorized'});
    fixture.detectChanges(); await fixture.whenStable(); fixture.detectChanges(); expect(element.textContent).toContain('ungültig oder abgelaufen');
    expect(element.querySelector('input')!.value).toBe(''); http.verify();
  });
  it('rejects an invalid response', async () => {
    const fixture = TestBed.createComponent(App); fixture.detectChanges(); await fixture.whenStable(); fixture.detectChanges();
    const element: HTMLElement = fixture.nativeElement;
    const input = element.querySelector('input')!; input.value = 'test'; input.dispatchEvent(new Event('input')); await fixture.whenStable();
    element.querySelector('form')!.dispatchEvent(new Event('submit'));
    TestBed.inject(HttpTestingController).expectOne('/api/review/work-queues').flush({queues: 'invalid'});
    fixture.detectChanges(); expect(element.textContent).toContain('Antwort der Arbeitsliste ist ungültig');
  });
  it('handles unknown queue IDs without assigning them a known meaning', async () => {
    const fixture = TestBed.createComponent(App); fixture.detectChanges(); await fixture.whenStable(); fixture.detectChanges();
    const element: HTMLElement = fixture.nativeElement;
    const input = element.querySelector('input')!; input.value = 'test'; input.dispatchEvent(new Event('input')); await fixture.whenStable();
    element.querySelector('form')!.dispatchEvent(new Event('submit'));
    const http = TestBed.inject(HttpTestingController);
    http.expectOne('/api/review/work-queues').flush({ queues: [{queueId: '__proto__', items: []}], nextPageCursor: null });
    fixture.detectChanges(); expect(element.textContent).toContain('Unbekannte Arbeitsliste'); http.verify();
  });
  it('clears the pending request when the view is destroyed', async () => {
    const fixture = TestBed.createComponent(App); fixture.detectChanges(); await fixture.whenStable(); fixture.detectChanges();
    const element: HTMLElement = fixture.nativeElement;
    const input = element.querySelector('input')!; input.value = 'test'; input.dispatchEvent(new Event('input')); await fixture.whenStable();
    element.querySelector('form')!.dispatchEvent(new Event('submit'));
    const http = TestBed.inject(HttpTestingController);
    const request = http.expectOne('/api/review/work-queues');
    fixture.destroy(); expect(request.cancelled).toBe(true); http.verify();
  });
  it('opens an authorized case read-only and returns to the same queue page', async () => {
    const { fixture, element, http } = await openQueue();
    expect(caseButton(element).getAttribute('aria-label')).toBe('Fall öffnen: demo-g-review');
    caseButton(element).click(); fixture.detectChanges();
    const request = http.expectOne('/api/review/work-cases/demo-g-review');
    expect(request.request.headers.get('Authorization')).toBe('Bearer test-key');
    expect(request.request.credentials).toBe('omit');
    expect(request.request.responseType).toBe('text');
    expect(element.textContent).toContain('Fall wird geladen');
    request.flush(detailJson); fixture.detectChanges(); await fixture.whenStable(); fixture.detectChanges();
    expect(element.textContent).toContain('Manuelle Prüfung erforderlich');
    expect(element.textContent).toContain('Menschliche Gegenprüfung erforderlich');
    expect(element.textContent).toContain('90071992547409931234');
    expect(element.textContent).toContain('Fehlend');
    expect(element.textContent).toContain('Systemprüfung aufgezeichnet');
    expect(element.querySelector('.outcome')!.getAttribute('data-outcome')).toBe('HUMAN_REVIEW');
    expect(document.activeElement?.id).toBe('case-title');
    expect(caseButton(element)).toBeNull();
    backButton(element).click(); fixture.detectChanges(); await fixture.whenStable(); fixture.detectChanges();
    expect(caseButton(element)).toBeTruthy();
    expect(document.activeElement).toBe(caseButton(element));
    http.verify();
  });
  it('shows the same message for unknown and unauthorized cases and keeps the session', async () => {
    const { fixture, element, http } = await openQueue();
    caseButton(element).click(); fixture.detectChanges();
    http.expectOne('/api/review/work-cases/demo-g-review').flush({ message: 'x' }, { status: 404, statusText: 'Not Found' });
    fixture.detectChanges();
    expect(element.querySelector('[role="alert"]')!.textContent).toContain('nicht vorhanden oder für diese Anmeldung nicht freigegeben');
    expect(element.textContent).toContain('Abmelden');
    http.verify();
  });
  it('rejects a detail response that belongs to another case', async () => {
    const { fixture, element, http } = await openQueue();
    caseButton(element).click(); fixture.detectChanges();
    http.expectOne('/api/review/work-cases/demo-g-review').flush(detailJson.replace('"demo-g-review"', '"demo-g-supported"'));
    fixture.detectChanges();
    expect(element.textContent).toContain('Antwort zum Fall ist ungültig');
    expect(element.textContent).not.toContain('demo-g-supported');
  });
  it('signs out when the case request is no longer authenticated', async () => {
    const { fixture, element, http } = await openQueue();
    caseButton(element).click(); fixture.detectChanges();
    http.expectOne('/api/review/work-cases/demo-g-review').flush({}, { status: 401, statusText: 'Unauthorized' });
    fixture.detectChanges(); await fixture.whenStable(); fixture.detectChanges();
    expect(element.textContent).toContain('ungültig oder abgelaufen');
    expect(element.querySelector('#credential')).toBeTruthy();
    expect(element.textContent).not.toContain('demo-g-review');
  });
  it('cancels a pending case request on back, escape and logout', async () => {
    const { fixture, element, http } = await openQueue();
    caseButton(element).click(); fixture.detectChanges();
    const first = http.expectOne('/api/review/work-cases/demo-g-review');
    backButton(element).click(); fixture.detectChanges();
    expect(first.cancelled).toBe(true);
    caseButton(element).click(); fixture.detectChanges();
    const second = http.expectOne('/api/review/work-cases/demo-g-review');
    element.querySelector('.case-detail')!.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' })); fixture.detectChanges();
    expect(second.cancelled).toBe(true);
    caseButton(element).click(); fixture.detectChanges();
    const third = http.expectOne('/api/review/work-cases/demo-g-review');
    [...element.querySelectorAll('button')].find(b => b.textContent?.includes('Abmelden'))!.click(); fixture.detectChanges();
    expect(third.cancelled).toBe(true);
    http.verify();
  });
});
