import { Routes } from '@angular/router';
import { homeRedirect, roleGuard } from './core/role.guard';

// Each screen is loaded on demand, and each is closed to roles that should not see it. The API enforces the same
// rules on its side; the guards are for navigation, not for security.
export const routes: Routes = [
  { path: 'login', title: 'Sign in - DepotFlow', loadComponent: () => import('./features/login/login').then((m) => m.Login) },
  {
    path: 'gate',
    title: 'Gate - DepotFlow',
    canActivate: [roleGuard('Admin', 'GateClerk')],
    loadComponent: () => import('./features/gate/gate').then((m) => m.Gate),
  },
  {
    path: 'invoices',
    title: 'Invoices - DepotFlow',
    canActivate: [roleGuard('Admin', 'BillingOfficer')],
    loadComponent: () => import('./features/invoices/invoices').then((m) => m.Invoices),
  },
  {
    path: 'yard',
    title: 'Yard - DepotFlow',
    canActivate: [roleGuard('Admin', 'GateClerk', 'YardPlanner')],
    loadComponent: () => import('./features/yard/yard').then((m) => m.Yard),
  },
  {
    path: 'reports',
    title: 'Reports - DepotFlow',
    canActivate: [roleGuard('Admin', 'BillingOfficer', 'YardPlanner')],
    loadComponent: () => import('./features/reports/reports').then((m) => m.Reports),
  },
  { path: '', pathMatch: 'full', canActivate: [homeRedirect], children: [] },
  { path: '**', redirectTo: '' },
];
