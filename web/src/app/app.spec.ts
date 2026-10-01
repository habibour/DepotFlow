import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { vi } from 'vitest';
import { App } from './app';
import { Role } from './core/api.types';
import { AuthService } from './core/auth.service';

async function navFor(role: Role | null): Promise<string[]> {
  TestBed.configureTestingModule({
    imports: [App],
    providers: [provideRouter([]), { provide: AuthService, useValue: { role: signal(role), email: signal('someone@depotflow.local'), logout: vi.fn() } }],
  });
  const fixture = TestBed.createComponent(App);
  await fixture.whenStable();
  return [...(fixture.nativeElement as HTMLElement).querySelectorAll('nav a')].map((a) => a.textContent!.trim());
}

describe('navigation', () => {
  it.each([
    ['GateClerk', ['Gate', 'Yard']],
    ['BillingOfficer', ['Invoices', 'Reports']],
    ['YardPlanner', ['Yard', 'Reports']],
    ['Admin', ['Gate', 'Yard', 'Invoices', 'Reports']],
  ] as [Role, string[]][])('shows %s only the screens it may open', async (role, expected) => {
    expect(await navFor(role)).toEqual(expected);
  });

  it('shows no navigation when signed out', async () => {
    expect(await navFor(null)).toEqual([]);
  });
});
