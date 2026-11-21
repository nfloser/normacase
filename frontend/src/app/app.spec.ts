import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { App } from './app';
import { AuthService } from './auth/auth.service';

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

  async function render(authenticated = true) {
    const auth = TestBed.inject(AuthService);
    auth.state.set(
      authenticated
        ? { status: 'authenticated', user: { username: 'gutachterin.a', displayName: 'Gutachterin A', roles: ['gutachter'] } }
        : { status: 'anonymous' },
    );
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

  it('zeigt Name und Rolle der angemeldeten Person', async () => {
    const fixture = await render();
    http.expectOne('/api/v1/system').flush(null, { status: 503, statusText: 'x' });
    await fixture.whenStable();
    const header = (fixture.nativeElement as HTMLElement).querySelector('.topbar')!.textContent!;

    expect(header).toContain('Gutachterin A');
    expect(header).toContain('Gutachter:in');
    expect(header).toContain('Abmelden');
  });

  it('zeigt ohne Anmeldung nur die Anmeldung, keine Arbeitskörbe', async () => {
    const fixture = await render(false);
    http.expectOne('/api/v1/system').flush(null, { status: 503, statusText: 'x' });
    await fixture.whenStable();
    const el = fixture.nativeElement as HTMLElement;

    expect(el.querySelector('.queues')).toBeNull();
    expect(el.querySelector('#login-title')!.textContent).toContain('Anmeldung');
  });

  it('erklärt einen fehlgeschlagenen Login verständlich', async () => {
    const fixture = await render(false);
    TestBed.inject(AuthService).state.set({ status: 'anonymous', error: 'login-failed' });
    http.expectOne('/api/v1/system').flush(null, { status: 503, statusText: 'x' });
    await fixture.whenStable();

    expect((fixture.nativeElement as HTMLElement).querySelector('[role=alert]')!.textContent).toContain('nicht abgeschlossen');
  });
});
