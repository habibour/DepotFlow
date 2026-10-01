import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { vi } from 'vitest';
import { authInterceptor } from './auth.interceptor';
import { AuthService } from './auth.service';

function setup(token: string | null) {
  const auth = { token, logout: vi.fn() };
  const router = { navigate: vi.fn().mockResolvedValue(true), url: '/invoices' };
  TestBed.configureTestingModule({
    providers: [
      provideHttpClient(withInterceptors([authInterceptor])),
      provideHttpClientTesting(),
      { provide: AuthService, useValue: auth },
      { provide: Router, useValue: router },
    ],
  });
  return { auth, router, http: TestBed.inject(HttpClient), controller: TestBed.inject(HttpTestingController) };
}

describe('authInterceptor', () => {
  it('adds the bearer token to API calls', () => {
    const { http, controller } = setup('abc');
    http.get('/api/v1/visits').subscribe();
    expect(controller.expectOne('/api/v1/visits').request.headers.get('Authorization')).toBe('Bearer abc');
  });

  it('does not add a token to the login call or to non-API requests', () => {
    const { http, controller } = setup('abc');
    http.post('/api/v1/auth/login', {}).subscribe();
    http.get('/assets/logo.svg').subscribe();
    expect(controller.expectOne('/api/v1/auth/login').request.headers.has('Authorization')).toBe(false);
    expect(controller.expectOne('/assets/logo.svg').request.headers.has('Authorization')).toBe(false);
  });

  it('sends no header when signed out', () => {
    const { http, controller } = setup(null);
    http.get('/api/v1/visits').subscribe({ error: () => undefined });
    expect(controller.expectOne('/api/v1/visits').request.headers.has('Authorization')).toBe(false);
  });

  it('signs out and goes to the login screen when the API answers 401', () => {
    const { http, controller, auth, router } = setup('abc');
    http.get('/api/v1/invoices').subscribe({ error: () => undefined });

    controller.expectOne('/api/v1/invoices').flush({}, { status: 401, statusText: 'Unauthorized' });

    expect(auth.logout).toHaveBeenCalled();
    expect(router.navigate).toHaveBeenCalledWith(['/login'], { queryParams: { returnUrl: '/invoices' } });
  });

  it('leaves a 401 from the login call to the login screen (wrong password)', () => {
    const { http, controller, auth, router } = setup(null);
    let failed = false;
    http.post('/api/v1/auth/login', {}).subscribe({ error: () => (failed = true) });

    controller.expectOne('/api/v1/auth/login').flush({}, { status: 401, statusText: 'Unauthorized' });

    expect(failed).toBe(true);
    expect(auth.logout).not.toHaveBeenCalled();
    expect(router.navigate).not.toHaveBeenCalled();
  });

  it('does not sign out on other errors such as 403 or 409', () => {
    const { http, controller, auth } = setup('abc');
    http.get('/api/v1/audit-logs').subscribe({ error: () => undefined });
    controller.expectOne('/api/v1/audit-logs').flush({}, { status: 403, statusText: 'Forbidden' });
    expect(auth.logout).not.toHaveBeenCalled();
  });
});
