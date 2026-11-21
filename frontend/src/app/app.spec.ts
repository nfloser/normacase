import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { App } from './app';
import { AuthService } from './auth/auth.service';

describe('App', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [App], providers: [provideHttpClient(), provideHttpClientTesting()] });
    http = TestBed.inject(HttpTestingController);
  });

  it('zeigt ohne Anmeldung nur die Anmeldung, keine Arbeitskörbe', async () => {
    TestBed.inject(AuthService).state.set({ status: 'anonymous' });
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();
    http.expectOne('/api/v1/system').flush({ product: 'NormaCase', platformVersion: '0.1.0', retentionMode: 'zero-retention' });
    await fixture.whenStable();
    const el = fixture.nativeElement as HTMLElement;

    expect(el.querySelector('nc-workbench')).toBeNull();
    expect(el.querySelector('#login-title')!.textContent).toContain('Anmeldung');
    expect(el.querySelector('[role=status]')!.textContent).toContain('Verbunden');
  });

  it('erklärt einen fehlgeschlagenen Login verständlich', async () => {
    TestBed.inject(AuthService).state.set({ status: 'anonymous', error: 'login-failed' });
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();
    http.expectOne('/api/v1/system').flush(null, { status: 503, statusText: 'x' });
    await fixture.whenStable();
    const el = fixture.nativeElement as HTMLElement;

    expect(el.querySelector('[role=alert]')!.textContent).toContain('nicht abgeschlossen');
    expect(el.querySelector('[role=status]')!.textContent).toContain('Backend nicht erreichbar');
  });

  it('öffnet nach der Anmeldung die Prüfwerkstatt', async () => {
    TestBed.inject(AuthService).state.set({ status: 'authenticated', user: { username: 'gutachterin.a', displayName: 'Gutachterin A', roles: ['gutachter'] } });
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).querySelector('nc-workbench')).not.toBeNull();
  });
});
