import { Routes } from '@angular/router';
import { authGuard } from './core/guards/auth.guard';
import { roleGuard } from './core/guards/role.guard';

export const routes: Routes = [
  { path: '', redirectTo: 'dashboard', pathMatch: 'full' },
  {
    path: 'login',
    loadComponent: () => import('./features/auth/login/login.component').then((m) => m.LoginComponent),
  },
  {
    path: 'register',
    loadComponent: () => import('./features/auth/register/register.component').then((m) => m.RegisterComponent),
  },
  {
    path: 'dashboard',
    loadComponent: () => import('./features/dashboard/dashboard.component').then((m) => m.DashboardComponent),
    canActivate: [authGuard],
  },
  {
    path: 'incidents',
    loadComponent: () =>
      import('./features/incidents/incident-list/incident-list.component').then((m) => m.IncidentListComponent),
    canActivate: [authGuard],
  },
  {
    path: 'incidents/:id',
    loadComponent: () =>
      import('./features/incidents/incident-detail/incident-detail.component').then((m) => m.IncidentDetailComponent),
    canActivate: [authGuard],
  },
  {
    path: 'users',
    loadComponent: () => import('./features/users/user-list/user-list.component').then((m) => m.UserListComponent),
    canActivate: [authGuard, roleGuard],
    data: { roles: ['Admin'] },
  },
  {
    path: 'alerts',
    loadComponent: () => import('./features/alerts/alert-rules/alert-rules.component').then((m) => m.AlertRulesComponent),
    canActivate: [authGuard],
  },
  { path: '**', redirectTo: 'dashboard' },
];
