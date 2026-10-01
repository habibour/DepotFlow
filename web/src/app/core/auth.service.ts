import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { LoginResponse, Role } from './api.types';

interface Session {
  accessToken: string;
  expiresAtUtc: string;
  role: Role;
  email: string;
}

const STORAGE_KEY = 'depotflow.session';

/** Where each role lands after signing in, and what it may open. */
export const HOME_BY_ROLE: Record<Role, string> = {
  Admin: '/gate',
  GateClerk: '/gate',
  YardPlanner: '/yard',
  BillingOfficer: '/invoices',
};

/**
 * Holds the signed-in user. The token lives in sessionStorage (gone when the tab closes). A production system would
 * rather keep it in an httpOnly cookie, which scripts on the page cannot read; sessionStorage keeps this demo simple.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly session = signal<Session | null>(this.restore());

  readonly role = computed(() => this.session()?.role ?? null);
  readonly email = computed(() => this.session()?.email ?? null);

  get token(): string | null {
    return this.session()?.accessToken ?? null;
  }

  /** True while a token exists and has not expired (checked against the clock each time it is asked). */
  isAuthenticated(): boolean {
    const session = this.session();
    return session !== null && Date.parse(session.expiresAtUtc) > Date.now();
  }

  hasRole(...roles: Role[]): boolean {
    const role = this.session()?.role;
    return role !== undefined && roles.includes(role);
  }

  homePath(): string {
    const role = this.session()?.role;
    return role ? HOME_BY_ROLE[role] : '/login';
  }

  async login(email: string, password: string): Promise<void> {
    const response = await firstValueFrom(this.http.post<LoginResponse>('/api/v1/auth/login', { email, password }));
    const session: Session = { ...response, email: emailFromToken(response.accessToken) ?? email };
    sessionStorage.setItem(STORAGE_KEY, JSON.stringify(session));
    this.session.set(session);
  }

  logout(): void {
    sessionStorage.removeItem(STORAGE_KEY);
    this.session.set(null);
  }

  private restore(): Session | null {
    try {
      const raw = sessionStorage.getItem(STORAGE_KEY);
      return raw ? (JSON.parse(raw) as Session) : null;
    } catch {
      return null;   // unreadable storage: start signed out
    }
  }
}

/** Reads the "email" claim from a JWT for display only; the server validates the token, the client never trusts it. */
export function emailFromToken(token: string): string | null {
  try {
    const payload = token.split('.')[1].replace(/-/g, '+').replace(/_/g, '/');
    return (JSON.parse(atob(payload)) as { email?: string }).email ?? null;
  } catch {
    return null;
  }
}
