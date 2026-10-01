import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { AuthService, emailFromToken } from './auth.service';

// A token whose payload is {"email":"gate@depotflow.local"} (header and signature are irrelevant to the client).
const TOKEN = 'h.' + btoa(JSON.stringify({ email: 'gate@depotflow.local', role: 'GateClerk' })) + '.s';

function setup(): { auth: AuthService; http: HttpTestingController } {
  TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
  return { auth: TestBed.inject(AuthService), http: TestBed.inject(HttpTestingController) };
}

describe('AuthService', () => {
  beforeEach(() => sessionStorage.clear());

  it('starts signed out', () => {
    const { auth } = setup();
    expect(auth.isAuthenticated()).toBe(false);
    expect(auth.role()).toBeNull();
    expect(auth.homePath()).toBe('/login');
  });

  it('logs in, keeps the session in sessionStorage and knows the role', async () => {
    const { auth, http } = setup();
    const done = auth.login('gate@depotflow.local', 'pw');

    const request = http.expectOne('/api/v1/auth/login');
    expect(request.request.body).toEqual({ email: 'gate@depotflow.local', password: 'pw' });
    request.flush({ accessToken: TOKEN, expiresAtUtc: new Date(Date.now() + 3_600_000).toISOString(), role: 'GateClerk' });
    await done;

    expect(auth.isAuthenticated()).toBe(true);
    expect(auth.role()).toBe('GateClerk');
    expect(auth.email()).toBe('gate@depotflow.local');
    expect(auth.token).toBe(TOKEN);
    expect(auth.homePath()).toBe('/gate');
    expect(auth.hasRole('Admin', 'GateClerk')).toBe(true);
    expect(auth.hasRole('BillingOfficer')).toBe(false);
    expect(sessionStorage.getItem('depotflow.session')).toContain('GateClerk');
  });

  it('treats an expired token as signed out', async () => {
    sessionStorage.setItem(
      'depotflow.session',
      JSON.stringify({ accessToken: TOKEN, expiresAtUtc: new Date(Date.now() - 1000).toISOString(), role: 'Admin', email: 'a@b.c' }),
    );
    const { auth } = setup();
    expect(auth.isAuthenticated()).toBe(false);
  });

  it('restores a still-valid session after a reload', () => {
    sessionStorage.setItem(
      'depotflow.session',
      JSON.stringify({ accessToken: TOKEN, expiresAtUtc: new Date(Date.now() + 60_000).toISOString(), role: 'BillingOfficer', email: 'billing@depotflow.local' }),
    );
    const { auth } = setup();
    expect(auth.isAuthenticated()).toBe(true);
    expect(auth.homePath()).toBe('/invoices');
  });

  it('ignores a corrupt stored session', () => {
    sessionStorage.setItem('depotflow.session', '{not json');
    expect(setup().auth.isAuthenticated()).toBe(false);
  });

  it('logout clears the session', async () => {
    const { auth, http } = setup();
    const done = auth.login('x@y.z', 'pw');
    http.expectOne('/api/v1/auth/login').flush({ accessToken: TOKEN, expiresAtUtc: new Date(Date.now() + 60_000).toISOString(), role: 'Admin' });
    await done;

    auth.logout();

    expect(auth.isAuthenticated()).toBe(false);
    expect(auth.token).toBeNull();
    expect(sessionStorage.getItem('depotflow.session')).toBeNull();
  });

  it('reads the email claim from a token, and returns null for garbage', () => {
    expect(emailFromToken(TOKEN)).toBe('gate@depotflow.local');
    expect(emailFromToken('not-a-token')).toBeNull();
  });
});
