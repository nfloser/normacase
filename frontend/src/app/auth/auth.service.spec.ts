import { TestBed } from '@angular/core/testing';
import { AuthService } from './auth.service';
import { base64Url, codeChallenge, decodeJwtPayload } from './pkce';

function jwt(payload: object): string {
  const enc = (o: object) => base64Url(new TextEncoder().encode(JSON.stringify(o)));
  return `${enc({ alg: 'none' })}.${enc(payload)}.sig`;
}

function fakeLocation(search = ''): Location & { assigned?: string } {
  const loc = {
    search,
    origin: 'http://localhost:4200',
    pathname: '/',
    assign(url: string) {
      loc.assigned = url;
    },
  } as unknown as Location & { assigned?: string };
  return loc;
}

describe('PKCE', () => {
  it('berechnet die Code Challenge wie RFC 7636 Anhang B', async () => {
    expect(await codeChallenge('dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk')).toBe('E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM');
  });

  it('liest Umlaute im Token korrekt', () => {
    expect(decodeJwtPayload(jwt({ name: 'Jürgen Größe' }))['name']).toBe('Jürgen Größe');
  });
});

describe('AuthService', () => {
  const config = { issuer: 'http://idp.test/realms/normacase', clientId: 'normacase-workbench' };
  let fetchMock: ReturnType<typeof vi.fn>;
  let auth: AuthService;

  beforeEach(() => {
    fetchMock = vi.fn();
    vi.stubGlobal('fetch', fetchMock);
    sessionStorage.clear();
    auth = TestBed.inject(AuthService);
  });

  afterEach(() => vi.unstubAllGlobals());

  function respond(...bodies: object[]) {
    for (const body of bodies) fetchMock.mockResolvedValueOnce({ ok: true, json: async () => body });
  }

  it('ist ohne Rückruf abgemeldet', async () => {
    respond(config);
    await auth.init(fakeLocation());
    expect(auth.state()).toEqual({ status: 'anonymous' });
  });

  it('meldet einen nicht erreichbaren Anmeldedienst', async () => {
    fetchMock.mockRejectedValueOnce(new Error('offline'));
    await auth.init(fakeLocation());
    expect(auth.state()).toEqual({ status: 'anonymous', error: 'idp-unreachable' });
  });

  it('leitet mit PKCE zum IdP und tauscht den Code nach der Rückkehr', async () => {
    respond(config);
    const start = fakeLocation();
    await auth.init(start);
    await auth.login(start);

    const authorize = new URL(start.assigned!);
    expect(authorize.pathname).toBe('/realms/normacase/protocol/openid-connect/auth');
    expect(authorize.searchParams.get('code_challenge_method')).toBe('S256');
    expect(authorize.searchParams.get('redirect_uri')).toBe('http://localhost:4200/');

    const state = authorize.searchParams.get('state')!;
    const token = jwt({ preferred_username: 'gutachterin.a', name: 'Gutachterin A', roles: ['gutachter'] });
    respond(config, { access_token: token, id_token: 'id', expires_in: 300 });
    await auth.init(fakeLocation(`?code=abc&state=${state}`));

    const exchange = fetchMock.mock.calls.at(-1)!;
    const body = new URLSearchParams(exchange[1].body as URLSearchParams);
    expect(body.get('grant_type')).toBe('authorization_code');
    expect(await codeChallenge(body.get('code_verifier')!)).toBe(authorize.searchParams.get('code_challenge'));
    expect(auth.state()).toEqual({
      status: 'authenticated',
      user: { username: 'gutachterin.a', displayName: 'Gutachterin A', roles: ['gutachter'] },
    });
    expect(auth.token()).toBe(token);
  });

  it('verwirft einen Rückruf mit fremdem state', async () => {
    respond(config);
    const start = fakeLocation();
    await auth.init(start);
    await auth.login(start);
    respond(config);

    await auth.init(fakeLocation('?code=abc&state=manipuliert'));

    expect(auth.state()).toEqual({ status: 'anonymous', error: 'login-failed' });
    expect(fetchMock).toHaveBeenCalledTimes(2);
  });
});
