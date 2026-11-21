import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { authInterceptor } from './auth.interceptor';
import { AuthService } from './auth.service';

describe('authInterceptor', () => {
  let http: HttpClient;
  let ctrl: HttpTestingController;
  let auth: AuthService;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(withInterceptors([authInterceptor])), provideHttpClientTesting()],
    });
    http = TestBed.inject(HttpClient);
    ctrl = TestBed.inject(HttpTestingController);
    auth = TestBed.inject(AuthService);
    vi.spyOn(auth, 'token').mockReturnValue('tok');
  });

  it('sendet das Token nur an die eigene API', () => {
    http.get('/api/v1/me').subscribe();
    http.get('http://idp.test/x').subscribe();

    expect(ctrl.expectOne('/api/v1/me').request.headers.get('Authorization')).toBe('Bearer tok');
    expect(ctrl.expectOne('http://idp.test/x').request.headers.has('Authorization')).toBe(false);
  });

  it('meldet bei 401 ab', () => {
    const expired = vi.spyOn(auth, 'sessionExpired');
    http.get('/api/v1/me').subscribe({ error: () => undefined });
    ctrl.expectOne('/api/v1/me').flush(null, { status: 401, statusText: 'Unauthorized' });
    expect(expired).toHaveBeenCalled();
  });
});
