import { TestBed } from '@angular/core/testing';
import { Router, UrlTree, provideRouter } from '@angular/router';
import { vi } from 'vitest';
import { Role } from './api.types';
import { AuthService } from './auth.service';
import { homeRedirect, roleGuard } from './role.guard';

function run(guard: ReturnType<typeof roleGuard>, auth: Partial<AuthService>): boolean | UrlTree {
  TestBed.configureTestingModule({ providers: [provideRouter([]), { provide: AuthService, useValue: auth }] });
  return TestBed.runInInjectionContext(() => guard({} as never, {} as never)) as boolean | UrlTree;
}

const signedIn = (role: Role, home: string): Partial<AuthService> => ({
  isAuthenticated: () => true,
  hasRole: (...roles: Role[]) => roles.includes(role),
  homePath: () => home,
  logout: vi.fn(),
});

describe('roleGuard', () => {
  it('lets an allowed role in', () => {
    expect(run(roleGuard('Admin', 'GateClerk'), signedIn('GateClerk', '/gate'))).toBe(true);
  });

  it('sends a signed-out user to the login screen', () => {
    const result = run(roleGuard('Admin'), { isAuthenticated: () => false, logout: vi.fn() });
    expect(result).toBeInstanceOf(UrlTree);
    expect(TestBed.inject(Router).serializeUrl(result as UrlTree)).toBe('/login');
  });

  it('sends a user with the wrong role to their own home', () => {
    const result = run(roleGuard('Admin', 'BillingOfficer'), signedIn('GateClerk', '/gate'));
    expect(TestBed.inject(Router).serializeUrl(result as UrlTree)).toBe('/gate');
  });
});

describe('homeRedirect', () => {
  it('goes to the role home when signed in, to login otherwise', () => {
    TestBed.configureTestingModule({ providers: [provideRouter([]), { provide: AuthService, useValue: signedIn('YardPlanner', '/yard') }] });
    const router = TestBed.inject(Router);
    const url = TestBed.runInInjectionContext(() => homeRedirect({} as never, {} as never)) as UrlTree;
    expect(router.serializeUrl(url)).toBe('/yard');
  });
});
