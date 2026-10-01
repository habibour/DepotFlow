import { Component, computed, inject } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { Role } from './core/api.types';
import { AuthService } from './core/auth.service';

interface NavLink {
  path: string;
  label: string;
  roles: Role[];
}

const NAV: NavLink[] = [
  { path: '/gate', label: 'Gate', roles: ['Admin', 'GateClerk'] },
  { path: '/yard', label: 'Yard', roles: ['Admin', 'GateClerk', 'YardPlanner'] },
  { path: '/invoices', label: 'Invoices', roles: ['Admin', 'BillingOfficer'] },
  { path: '/reports', label: 'Reports', roles: ['Admin', 'BillingOfficer', 'YardPlanner'] },
];

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  templateUrl: './app.html',
  styleUrl: './app.css',
})
export class App {
  protected readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  /** Only the screens the signed-in role may open. */
  protected readonly links = computed(() => {
    const role = this.auth.role();
    return role ? NAV.filter((link) => link.roles.includes(role)) : [];
  });

  protected signOut(): void {
    this.auth.logout();
    void this.router.navigate(['/login']);
  }
}
