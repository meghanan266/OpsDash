import { Component, effect, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { filter } from 'rxjs';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatSidenavModule } from '@angular/material/sidenav';
import { MatListModule } from '@angular/material/list';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { AuthService } from './core/services/auth.service';
import { NotificationOrchestratorService } from './core/services/notification-orchestrator.service';
import { ToastContainerComponent } from './shared/components/toast-container/toast-container.component';

@Component({
  selector: 'app-root',
  imports: [
    RouterOutlet,
    RouterLink,
    RouterLinkActive,
    MatToolbarModule,
    MatSidenavModule,
    MatListModule,
    MatIconModule,
    MatButtonModule,
    ToastContainerComponent,
  ],
  templateUrl: './app.component.html',
  styleUrl: './app.component.scss',
})
export class AppComponent {
  protected readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly notifications = inject(NotificationOrchestratorService);
  /** Full-page auth layout without shell (toolbar / sidenav). */
  protected readonly isAuthPage = signal(this.isAuthRoute(this.router.url));

  constructor() {
    effect(() => {
      if (this.auth.isAuthenticated()) {
        void this.notifications.start();
      } else {
        void this.notifications.stop();
      }
    });

    this.router.events
      .pipe(
        filter((e): e is NavigationEnd => e instanceof NavigationEnd),
        takeUntilDestroyed(),
      )
      .subscribe(() => this.isAuthPage.set(this.isAuthRoute(this.router.url)));
  }

  logout(): void {
    this.auth.logout();
  }

  private isAuthRoute(url: string): boolean {
    const path = url.split(/[?#]/)[0];
    return path === '/login' || path === '/register';
  }
}
