import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { App } from './app';

describe('Arbeitslisten', () => {
  beforeEach(async () => { await TestBed.configureTestingModule({ imports: [App], providers: [provideHttpClient(), provideHttpClientTesting()] }).compileComponents(); });
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
});
