import { Injectable, signal } from '@angular/core';
import { codeChallenge, decodeJwtPayload, randomToken } from './pkce';

export interface AuthConfig {
  readonly issuer: string;
  readonly clientId: string;
}

export interface AuthUser {
  readonly username: string;
  readonly displayName: string;
  readonly roles: readonly string[];
}

export type AuthState =
  | { readonly status: 'initializing' }
  | { readonly status: 'anonymous'; readonly error?: 'idp-unreachable' | 'login-failed' }
  | { readonly status: 'authenticated'; readonly user: AuthUser };

interface TokenResponse {
  readonly access_token: string;
  readonly id_token?: string;
  readonly refresh_token?: string;
  readonly expires_in: number;
}

const PENDING_KEY = 'normacase.pkce';

/**
 * Anmeldung per OpenID Connect (Authorization Code + PKCE).
 * Tokens liegen nur im Arbeitsspeicher; nach dem Neuladen meldet die SSO-Sitzung des IdP neu an.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  readonly state = signal<AuthState>({ status: 'initializing' });

  private config?: AuthConfig;
  private accessToken?: string;
  private idToken?: string;
  private refreshToken?: string;
  private refreshTimer?: ReturnType<typeof setTimeout>;

  async init(location: Location = window.location, storage: Storage = sessionStorage): Promise<void> {
    try {
      this.config = await this.loadConfig();
    } catch {
      this.state.set({ status: 'anonymous', error: 'idp-unreachable' });
      return;
    }
    const params = new URLSearchParams(location.search);
    const code = params.get('code');
    if (!code && !params.get('error')) {
      this.state.set({ status: 'anonymous' });
      return;
    }
    const pending = storage.getItem(PENDING_KEY);
    storage.removeItem(PENDING_KEY);
    history.replaceState(null, '', location.pathname);
    const expected = pending ? (JSON.parse(pending) as { state: string; verifier: string }) : undefined;
    if (!code || !expected || params.get('state') !== expected.state) {
      this.state.set({ status: 'anonymous', error: 'login-failed' });
      return;
    }
    try {
      const tokens = await this.tokenRequest({
        grant_type: 'authorization_code',
        code,
        code_verifier: expected.verifier,
        redirect_uri: this.redirectUri(location),
      });
      this.accept(tokens);
    } catch {
      this.state.set({ status: 'anonymous', error: 'login-failed' });
    }
  }

  async login(location: Location = window.location, storage: Storage = sessionStorage): Promise<void> {
    const config = this.requireConfig();
    const verifier = randomToken(48);
    const state = randomToken(16);
    storage.setItem(PENDING_KEY, JSON.stringify({ state, verifier }));
    const query = new URLSearchParams({
      client_id: config.clientId,
      response_type: 'code',
      scope: 'openid',
      redirect_uri: this.redirectUri(location),
      state,
      code_challenge: await codeChallenge(verifier),
      code_challenge_method: 'S256',
    });
    location.assign(`${config.issuer}/protocol/openid-connect/auth?${query}`);
  }

  logout(location: Location = window.location): void {
    const config = this.requireConfig();
    const query = new URLSearchParams({ client_id: config.clientId, post_logout_redirect_uri: this.redirectUri(location) });
    if (this.idToken) query.set('id_token_hint', this.idToken);
    this.clear();
    location.assign(`${config.issuer}/protocol/openid-connect/logout?${query}`);
  }

  token(): string | undefined {
    return this.accessToken;
  }

  /** Bei abgelaufener Sitzung zurück auf „abgemeldet“, ohne die Seite zu verlassen. */
  sessionExpired(): void {
    this.clear();
    this.state.set({ status: 'anonymous' });
  }

  private accept(tokens: TokenResponse): void {
    this.accessToken = tokens.access_token;
    this.idToken = tokens.id_token ?? this.idToken;
    this.refreshToken = tokens.refresh_token ?? this.refreshToken;
    const claims = decodeJwtPayload(tokens.access_token);
    const username = String(claims['preferred_username'] ?? claims['sub'] ?? '');
    this.state.set({
      status: 'authenticated',
      user: {
        username,
        displayName: String(claims['name'] ?? username),
        roles: Array.isArray(claims['roles']) ? (claims['roles'] as string[]) : [],
      },
    });
    clearTimeout(this.refreshTimer);
    if (this.refreshToken) {
      const delayMs = Math.max(10, tokens.expires_in - 30) * 1000;
      this.refreshTimer = setTimeout(() => void this.refresh(), delayMs);
    }
  }

  private async refresh(): Promise<void> {
    if (!this.refreshToken) return;
    try {
      this.accept(await this.tokenRequest({ grant_type: 'refresh_token', refresh_token: this.refreshToken }));
    } catch {
      this.sessionExpired();
    }
  }

  private async tokenRequest(body: Record<string, string>): Promise<TokenResponse> {
    const config = this.requireConfig();
    const response = await fetch(`${config.issuer}/protocol/openid-connect/token`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
      body: new URLSearchParams({ client_id: config.clientId, ...body }),
    });
    if (!response.ok) throw new Error(`token endpoint ${response.status}`);
    return (await response.json()) as TokenResponse;
  }

  private async loadConfig(): Promise<AuthConfig> {
    const response = await fetch('/config.json', { cache: 'no-store' });
    if (!response.ok) throw new Error('config');
    return (await response.json()) as AuthConfig;
  }

  private requireConfig(): AuthConfig {
    if (!this.config) throw new Error('Anmeldung nicht konfiguriert');
    return this.config;
  }

  private redirectUri(location: Location): string {
    return `${location.origin}/`;
  }

  private clear(): void {
    clearTimeout(this.refreshTimer);
    this.accessToken = this.idToken = this.refreshToken = undefined;
  }
}
