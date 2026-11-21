import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { App } from './app';

describe('App', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [App],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  async function render() {
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();
    return fixture;
  }

  it('zeigt die drei Bereiche der Prüfwerkstatt auf Deutsch', async () => {
    const fixture = await render();
    http.expectOne('/api/v1/system').flush({ product: 'NormaCase', platformVersion: '0.1.0', retentionMode: 'zero-retention' });
    await fixture.whenStable();
    const el = fixture.nativeElement as HTMLElement;

    expect(el.querySelectorAll('.queue').length).toBe(4);
    expect(el.textContent).toContain('Arbeitskörbe');
    expect(el.textContent).toContain('Keine Vorgänge in diesem Korb');
    expect(el.textContent).toContain('Kein Vorgang ausgewählt');
    expect(el.textContent).not.toContain('zero-retention');
  });

  it('meldet Plattformversion und Datenhaltung, wenn das Backend antwortet', async () => {
    const fixture = await render();
    expect(fixture.nativeElement.querySelector('[role=status]').textContent).toContain('Verbinde');

    http.expectOne('/api/v1/system').flush({ product: 'NormaCase', platformVersion: '0.1.0', retentionMode: 'zero-retention' });
    await fixture.whenStable();
    const el = fixture.nativeElement as HTMLElement;

    expect(el.querySelector('[role=status]')!.textContent).toContain('Verbunden');
    expect(el.querySelector('.footer')!.textContent).toContain('Plattform 0.1.0');
    expect(el.querySelector('.footer')!.textContent).toContain('ohne Vorhaltung');
  });

  it('bleibt bedienbar und sagt es klar, wenn das Backend fehlt', async () => {
    const fixture = await render();
    http.expectOne('/api/v1/system').flush(null, { status: 503, statusText: 'Service Unavailable' });
    await fixture.whenStable();
    const el = fixture.nativeElement as HTMLElement;

    expect(el.querySelector('[role=status]')!.textContent).toContain('Backend nicht erreichbar');
    expect(el.querySelector('.status')!.classList).toContain('offline');
  });

  it('wechselt den Korb per Klick', async () => {
    const fixture = await render();
    http.expectOne('/api/v1/system').flush(null, { status: 503, statusText: 'x' });
    const el = fixture.nativeElement as HTMLElement;
    const approval = Array.from(el.querySelectorAll<HTMLButtonElement>('.queue')).find((b) => b.textContent!.includes('Zur Freigabe'))!;

    approval.click();
    await fixture.whenStable();

    expect(el.querySelector('#queue-title')!.textContent).toContain('Zur Freigabe');
    expect(approval.getAttribute('aria-current')).toBe('true');
  });
});
